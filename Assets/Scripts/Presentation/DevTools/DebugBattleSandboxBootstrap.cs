// Debug_BattleSandbox シーン用 Bootstrap：戦闘単体検証画面。
//
// 【使い方】
// 1. Debug_BattleSandbox.unity を開く
// 2. Bootstrap GameObject に本コンポーネント＋ VSPrototypeBattleGUI が Add 済
// 3. Play すると Placement フェーズで起動。配置→ Run ▶ で戦闘再生。
// 4. 戦闘終了 or 「全戦闘をスキップ」で配置画面に戻る（編成は保持）。
//
// 【設計】
// - IBattleReplayHost を実装し、VSPrototypeBattleGUI が透過的に共有される。
// - 配置情報は SlotIndex → UnitId の Dictionary で持つ（IUnitCatalog で都度 Unit 解決）。
// - VSプロト本体（VSPrototypeBootstrap・MapState・SaveStore）には一切依存しない。
// - 戦闘条件（攻め/守り・地形強度）を GUI で可変にし、戦線位置の再現が可能。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Echolos.Data;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;
using Echolos.Presentation.VSPrototype;
using Echolos.Presentation.Common;

namespace Echolos.Presentation.DevTools
{
    /// <summary>Debug_BattleSandbox シーンの Bootstrap。配置画面＋戦闘実行＋ IBattleReplayHost 実装。</summary>
    public sealed class DebugBattleSandboxBootstrap : MonoBehaviour, IBattleReplayHost
    {
        // 敵 Id プレフィックス。Roster 命名規約（味方=属性名 / 敵=imperial_*）に依存する判定。
        private const string EnemyIdPrefix = "imperial_";

        //
        // 色定義（VSプロト本体と統一感）
        //

        private static readonly Color ColorBg          = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color ColorPanelBg     = new Color(0.14f, 0.15f, 0.20f);
        private static readonly Color ColorRosterBg    = new Color(0.18f, 0.20f, 0.28f);
        private static readonly Color ColorRosterSel   = new Color(0.32f, 0.45f, 0.70f);
        private static readonly Color ColorSlotBg      = new Color(0.20f, 0.22f, 0.30f);
        private static readonly Color ColorSlotEmpty   = new Color(0.14f, 0.14f, 0.18f);
        private static readonly Color ColorBorder      = new Color(0.55f, 0.65f, 0.85f);

        //
        // Catalog（IUnitCatalog 経由で Unit を解決・SO アセット由来）
        //

        private IWazaCatalog _wazaCatalog;
        private IUnitCatalog _unitCatalog;
        private List<string> _allyIds;
        private List<string> _enemyIds;

        //
        // 配置状態
        //

        // SlotIndex (0-5) → UnitId。null/未登録 = 空スロット。
        private readonly Dictionary<int, string> _allySlots  = new();
        private readonly Dictionary<int, string> _enemySlots = new();

        // 直前にクリック選択されたユニット（陣営別に独立保持）
        private string _selectedAllyId;
        private string _selectedEnemyId;

        // プリセット選択 index（-1 = 未選択）
        private int _allyPresetIndex  = -1;
        private int _enemyPresetIndex = -1;

        // 乱数シード（戦闘実行時に new System.Random(_seed) して BattleRunner に渡す）
        private string _seedText = "3475";

        // 戦闘条件（GUI で切替可・Phase R-6 バランス調整の戦線位置再現用）
        private bool _isAttackingSide = false;                          // 守り（自領防衛）デフォルト
        private TerrainStrength _terrainStrength = TerrainStrength.Light;
        private TerrainKind _terrainKind = TerrainKind.Neutral;         // 地形種別は当面 Neutral 固定

        //
        // 戦闘再生キュー（IBattleReplayHost 用）
        //

        private List<VSPrototypeBattleSegment> _segments;
        private int _currentIndex;
        private bool _isActive;

        //
        // OnGUI スタイル
        //

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private bool _stylesBuilt;

        private Vector2 _allyPoolScroll;
        private Vector2 _enemyPoolScroll;

        //
        // ライフサイクル
        //

        private void Awake()
        {
            _wazaCatalog = new WazaCatalog();
            _unitCatalog = new UnitCatalog(_wazaCatalog, new UnitUpgradeCatalog());

            // Roster 命名規約（味方=属性名 / 敵=imperial_*）で陣営分離
            var allIds = _unitCatalog.GetAllIds().ToList();
            _allyIds  = allIds.Where(id => !id.StartsWith(EnemyIdPrefix)).ToList();
            _enemyIds = allIds.Where(id =>  id.StartsWith(EnemyIdPrefix)).ToList();
        }

        private void OnGUI()
        {
            // 戦闘再生中は VSPrototypeBattleGUI 側が全画面描画するのでこちらは描かない
            if (_isActive) return;

            BuildStylesIfNeeded();
            DrawPlacementPhase();
        }

        //
        // IBattleReplayHost 実装
        //

        bool IBattleReplayHost.IsActive => _isActive;
        IReadOnlyList<VSPrototypeBattleSegment> IBattleReplayHost.Segments => _segments;
        int IBattleReplayHost.CurrentIndex => _currentIndex;
        VSPrototypeBattleSegment IBattleReplayHost.CurrentSegment =>
            _segments != null && _currentIndex >= 0 && _currentIndex < _segments.Count
                ? _segments[_currentIndex] : null;
        string IBattleReplayHost.HeaderProgressLabel => "Debug Sandbox";

        bool IBattleReplayHost.AdvanceToNext()
        {
            if (_segments == null) return false;
            _currentIndex++;
            if (_currentIndex >= _segments.Count)
            {
                ((IBattleReplayHost)this).FinishAll();
                return false;
            }
            return true;
        }

        void IBattleReplayHost.FinishAll()
        {
            _segments = null;
            _currentIndex = 0;
            _isActive = false;
            // 編成は保持。配置画面に戻ったらそのまま再戦・微調整できる。
        }

        //
        // 配置フェーズ描画
        //

        private void DrawPlacementPhase()
        {
            var fullscreen = new Rect(0, 0, Screen.width, Screen.height);
            GuiTheme.FillRect(fullscreen, ColorBg);

            const float headerH = 90f;
            DrawHeader(new Rect(0, 0, Screen.width, headerH));

            float bodyY = headerH + 6f;
            float bodyH = Screen.height - bodyY - 10f;

            float poolW = Mathf.Min(Screen.width * 0.22f, 320f);
            float centerW = Screen.width - poolW * 2 - 24f;

            var allyPoolRect  = new Rect(8, bodyY, poolW, bodyH);
            var centerRect    = new Rect(allyPoolRect.xMax + 8, bodyY, centerW, bodyH);
            var enemyPoolRect = new Rect(centerRect.xMax + 8, bodyY, poolW, bodyH);

            DrawPool(allyPoolRect,  "味方プール（クリックで選択）", isEnemy: false);
            DrawSlotsArea(centerRect);
            DrawPool(enemyPoolRect, "敵プール（クリックで選択）", isEnemy: true);
        }

        //
        // ヘッダ（プリセット選択＋戦闘条件＋シード＋ Run）
        //

        private void DrawHeader(Rect area)
        {
            GuiTheme.FillRect(area, ColorPanelBg);

            GUI.Label(new Rect(area.x + 16, area.y + 8, 240, 26),
                "Debug Battle Sandbox", _titleStyle);

            // 行 1：プリセット選択
            float row1Y = area.y + 10f;
            float x = area.x + 240f;

            GUI.Label(new Rect(x, row1Y, 50, 26), "味方:", _mutedStyle);
            x += 46f;
            int newAllyIdx = DrawPresetDropdown(
                new Rect(x, row1Y, 200, 26),
                DebugBattlePresets.Allies, _allyPresetIndex);
            if (newAllyIdx != _allyPresetIndex)
            {
                _allyPresetIndex = newAllyIdx;
                if (newAllyIdx >= 0) ApplyPreset(DebugBattlePresets.Allies[newAllyIdx], isEnemy: false);
            }
            x += 208f;

            GUI.Label(new Rect(x, row1Y, 40, 26), "敵:", _mutedStyle);
            x += 36f;
            int newEnemyIdx = DrawPresetDropdown(
                new Rect(x, row1Y, 200, 26),
                DebugBattlePresets.Enemies, _enemyPresetIndex);
            if (newEnemyIdx != _enemyPresetIndex)
            {
                _enemyPresetIndex = newEnemyIdx;
                if (newEnemyIdx >= 0) ApplyPreset(DebugBattlePresets.Enemies[newEnemyIdx], isEnemy: true);
            }

            // 行 2：戦闘条件（攻め/守り・地形強度）＋ Seed ＋ Run / Clear
            float row2Y = area.y + 44f;
            x = area.x + 16f;

            // 攻め/守り
            GUI.Label(new Rect(x, row2Y, 60, 26), "戦闘:", _mutedStyle);
            x += 56f;
            if (GUI.Button(new Rect(x, row2Y, 80, 26), _isAttackingSide ? "攻め" : "守り"))
                _isAttackingSide = !_isAttackingSide;
            x += 90f;

            // 地形強度
            GUI.Label(new Rect(x, row2Y, 60, 26), "地形:", _mutedStyle);
            x += 56f;
            x += DrawTerrainStrengthSelector(new Rect(x, row2Y, 200, 26));

            // Seed
            x += 16f;
            GUI.Label(new Rect(x, row2Y, 48, 26), "Seed:", _mutedStyle);
            x += 46f;
            _seedText = GUI.TextField(new Rect(x, row2Y, 80, 26), _seedText);
            x += 86f;
            if (GUI.Button(new Rect(x, row2Y, 36, 26), "🎲"))
                _seedText = UnityEngine.Random.Range(1, 99999).ToString();

            // Run / Clear ボタン（右寄せ）
            float btnW = 110f;
            bool canRun = _allySlots.Values.Any(v => !string.IsNullOrEmpty(v))
                       && _enemySlots.Values.Any(v => !string.IsNullOrEmpty(v));
            float runX = area.xMax - btnW - 10f;

            bool prev = GUI.enabled;
            GUI.enabled = canRun;
            if (GUI.Button(new Rect(runX, row2Y, btnW, 28), "Run ▶"))
                StartBattle();
            GUI.enabled = prev;

            if (GUI.Button(new Rect(runX - btnW - 8, row2Y, btnW, 28), "全クリア"))
                ClearAllSlots();
        }

        /// <summary>プリセット選択を「◀ / Name (idx/N) / ▶」の左右ボタンで表現。</summary>
        private int DrawPresetDropdown(Rect rect, List<DebugBattlePreset> presets, int currentIndex)
        {
            if (presets.Count == 0) return currentIndex;
            int idx = currentIndex < 0 ? -1 : Mathf.Clamp(currentIndex, 0, presets.Count - 1);
            const float arrowW = 24f;

            if (GUI.Button(new Rect(rect.x, rect.y, arrowW, rect.height), "◀"))
                idx = (idx <= 0) ? presets.Count - 1 : idx - 1;

            string label = idx < 0
                ? "（未選択）"
                : $"{presets[idx].Name}  ({idx + 1}/{presets.Count})";
            GuiTheme.FillRect(new Rect(rect.x + arrowW, rect.y, rect.width - arrowW * 2, rect.height), ColorRosterBg);
            GUI.Label(new Rect(rect.x + arrowW + 6, rect.y + 4, rect.width - arrowW * 2 - 12, rect.height - 8),
                label, _bodyStyle);

            if (GUI.Button(new Rect(rect.xMax - arrowW, rect.y, arrowW, rect.height), "▶"))
                idx = (idx >= presets.Count - 1) ? 0 : idx + 1;

            return idx;
        }

        /// <summary>Light / Medium / Heavy の 3 択を横並びトグルで選ぶ。返り値は描画した幅。</summary>
        private float DrawTerrainStrengthSelector(Rect rect)
        {
            const float cellW = 62f;
            var values = new[] { TerrainStrength.Light, TerrainStrength.Medium, TerrainStrength.Heavy };
            var labels = new[] { "軽微", "中", "重" };

            for (int i = 0; i < values.Length; i++)
            {
                var cell = new Rect(rect.x + i * (cellW + 2f), rect.y, cellW, rect.height);
                bool isSelected = _terrainStrength == values[i];
                GuiTheme.FillRect(cell, isSelected ? ColorRosterSel : ColorRosterBg);
                GUI.Label(new Rect(cell.x, cell.y + 4, cell.width, cell.height - 8), labels[i], _bodyStyle);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    _terrainStrength = values[i];
            }
            return cellW * 3 + 4f;
        }

        //
        // ユニットプール（左：味方／右：敵）
        //

        private void DrawPool(Rect area, string title, bool isEnemy)
        {
            GuiTheme.FillRect(area, ColorPanelBg);
            GUI.Label(new Rect(area.x + 8, area.y + 6, area.width - 16, 22), title, _mutedStyle);

            var listArea = new Rect(area.x + 4, area.y + 30, area.width - 8, area.height - 36);
            var ids = isEnemy ? _enemyIds : _allyIds;
            string selected = isEnemy ? _selectedEnemyId : _selectedAllyId;

            GUILayout.BeginArea(listArea);
            if (isEnemy) _enemyPoolScroll = GUILayout.BeginScrollView(_enemyPoolScroll);
            else         _allyPoolScroll  = GUILayout.BeginScrollView(_allyPoolScroll);

            foreach (var id in ids)
            {
                Unit u = _unitCatalog.Get(id);
                if (DrawPoolRow(u, isSelected: selected == id))
                {
                    // クリックで選択トグル
                    if (isEnemy) _selectedEnemyId = (selected == id) ? null : id;
                    else         _selectedAllyId  = (selected == id) ? null : id;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool DrawPoolRow(Unit unit, bool isSelected)
        {
            const float rowH = 56f;
            const float iconSize = 44f;
            var rect = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
            GuiTheme.FillRect(rect, isSelected ? ColorRosterSel : ColorRosterBg);

            var iconRect = new Rect(rect.x + 6, rect.y + (rowH - iconSize) / 2f, iconSize, iconSize);
            IconRegistry.TryDrawIcon(iconRect, unit.Id);

            GUI.Label(new Rect(rect.x + iconSize + 12, rect.y + 6, rect.width - iconSize - 16, 22),
                unit.Name, _bodyStyle);
            string roleTag = UnitDisplayLabels.RoleTagsLabel(unit.CombatRoles);
            GUI.Label(new Rect(rect.x + iconSize + 12, rect.y + 30, rect.width - iconSize - 16, 18),
                $"HP {unit.MaxHP}  ATK {unit.BaseATK}  {unit.AttackKind} {roleTag}",
                _mutedStyle);

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUILayout.Space(4);
            return clicked;
        }

        //
        // 中央：味方 6＋敵 6 スロット
        //

        private void DrawSlotsArea(Rect area)
        {
            GuiTheme.FillRect(area, ColorPanelBg);

            float halfH = (area.height - 40f) * 0.5f;
            var allySection  = new Rect(area.x, area.y,      area.width, halfH);
            var enemySection = new Rect(area.x, area.y + halfH + 40f, area.width, halfH);

            DrawSlotSection(allySection,  "味方 6 スロット", _allySlots,  isEnemy: false);
            DrawSlotSection(enemySection, "敵 6 スロット",   _enemySlots, isEnemy: true);
        }

        private void DrawSlotSection(Rect area, string title, Dictionary<int, string> slots, bool isEnemy)
        {
            GUI.Label(new Rect(area.x + 8, area.y + 4, area.width - 16, 20), title, _mutedStyle);

            float gridY = area.y + 26f;
            float gridH = area.height - 30f;
            float rowH = (gridH - 8f) * 0.5f;
            DrawSlotRow(new Rect(area.x, gridY,                area.width, rowH), slots, baseIndex: 0, isEnemy, rowLabel: "前列");
            DrawSlotRow(new Rect(area.x, gridY + rowH + 8f,    area.width, rowH), slots, baseIndex: 3, isEnemy, rowLabel: "後列");
        }

        private void DrawSlotRow(Rect area, Dictionary<int, string> slots, int baseIndex, bool isEnemy, string rowLabel)
        {
            GUI.Label(new Rect(area.x + 8, area.y, 80, 18), rowLabel, _mutedStyle);
            float cellY = area.y + 18f;
            float cellH = area.height - 18f;
            const float gap = 6f;
            float cellW = (area.width - 90f - gap * 2) * (1f / 3f);
            float startX = area.x + 80f;

            for (int i = 0; i < 3; i++)
            {
                int slotIndex = baseIndex + i;
                var cell = new Rect(startX + i * (cellW + gap), cellY, cellW, cellH);
                DrawSlot(cell, slotIndex, slots, isEnemy);
            }
        }

        private void DrawSlot(Rect cell, int slotIndex, Dictionary<int, string> slots, bool isEnemy)
        {
            slots.TryGetValue(slotIndex, out string occupantId);
            bool occupied = !string.IsNullOrEmpty(occupantId);

            GuiTheme.FillRect(cell, occupied ? ColorSlotBg : ColorSlotEmpty);

            if (occupied)
            {
                var iconSize = Mathf.Min(cell.height - 24f, cell.width - 8f);
                var iconRect = new Rect(cell.x + (cell.width - iconSize) * 0.5f, cell.y + 4f, iconSize, iconSize);
                IconRegistry.TryDrawIcon(iconRect, occupantId);

                var unit = _unitCatalog.Get(occupantId);
                GUI.Label(new Rect(cell.x + 2, cell.yMax - 22, cell.width - 4, 20),
                    unit.Name, _bodyStyle);
            }
            else
            {
                GUI.Label(new Rect(cell.x, cell.y + cell.height * 0.4f, cell.width, 20),
                    $"slot {slotIndex}", _mutedStyle);
            }

            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
            {
                string selected = isEnemy ? _selectedEnemyId : _selectedAllyId;
                if (occupied)
                {
                    slots.Remove(slotIndex);
                }
                else if (!string.IsNullOrEmpty(selected))
                {
                    // 空＋選択あり → 配置（他スロットに同 Id があっても複数体配置を許可）
                    slots[slotIndex] = selected;
                }
            }

            if (occupied) GuiTheme.FillRect(new Rect(cell.x, cell.y, cell.width, 1), ColorBorder);
        }

        //
        // プリセット適用 / クリア
        //

        private void ApplyPreset(DebugBattlePreset preset, bool isEnemy)
        {
            var slots = isEnemy ? _enemySlots : _allySlots;
            slots.Clear();
            int n = Mathf.Min(preset.UnitIds.Count, 6);
            for (int i = 0; i < n; i++)
            {
                string id = preset.UnitIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!_unitCatalog.IsRegistered(id)) continue;
                if (isEnemy != id.StartsWith(EnemyIdPrefix)) continue;
                slots[i] = id;
            }
        }

        private void ClearAllSlots()
        {
            _allySlots.Clear();
            _enemySlots.Clear();
            _selectedAllyId = null;
            _selectedEnemyId = null;
            _allyPresetIndex = -1;
            _enemyPresetIndex = -1;
        }

        //
        // 戦闘実行
        //

        private void StartBattle()
        {
            var allies  = BuildRuntimeUnits(_allySlots);
            var enemies = BuildRuntimeUnits(_enemySlots);
            if (allies.Count == 0 || enemies.Count == 0) return;

            int seed = ParseSeedOrZero(_seedText);
            var rng = new System.Random(seed);
            Func<int> random0to99 = () => rng.Next(0, 100);

            // VSプロト本体と同じ戦闘前準備（BattleWazas 再構築＋ ClearAllEffects＋ PersistentEffects Clone 付与）
            PrepareForBattle(allies);
            PrepareForBattle(enemies);

            var battleWazasByUnit = new Dictionary<RuntimeUnit, IList<RuntimeWaza>>();
            foreach (var u in allies)  battleWazasByUnit[u] = u.BattleWazas;
            foreach (var u in enemies) battleWazasByUnit[u] = u.BattleWazas;

            var report = BattleRunner.Run(
                allies, enemies, maxTurns: 15,
                battleWazasByUnit,
                terrain: _terrainKind,
                terrainStrength: _terrainStrength,
                isAttackingSide: _isAttackingSide,
                random0to99: random0to99,
                synergyDefinitions: SynergyDefinitions.All);

            string title = $"Debug 戦闘  seed={seed}  {(_isAttackingSide ? "攻め" : "守り")}  地形{_terrainStrength}";

            _segments = new List<VSPrototypeBattleSegment>
            {
                new VSPrototypeBattleSegment(title, report),
            };
            _currentIndex = 0;
            _isActive = true;
        }

        private List<RuntimeUnit> BuildRuntimeUnits(Dictionary<int, string> slots)
        {
            var list = new List<RuntimeUnit>();
            for (int i = 0; i < 6; i++)
            {
                if (!slots.TryGetValue(i, out string id) || string.IsNullOrEmpty(id)) continue;
                Unit u = _unitCatalog.Get(id);
                list.Add(new RuntimeUnit(u, i));
            }
            return list;
        }

        // VSPrototypeBootstrap.PrepareForBattle と同等の処理（戦闘前の RuntimeUnit 初期化）
        private static void PrepareForBattle(IList<RuntimeUnit> units)
        {
            foreach (var u in units)
            {
                if (u == null) continue;

                u.BattleWazas = new List<RuntimeWaza>();
                if (u.BaseUnit.BaseWazas != null)
                    foreach (var w in u.BaseUnit.BaseWazas)
                        if (w != null) u.BattleWazas.Add(new RuntimeWaza(w));

                u.ClearAllEffects();

                if (u.BaseUnit.PersistentEffects != null)
                    foreach (var e in u.BaseUnit.PersistentEffects)
                        if (e != null) u.AddEffect(e.ToEffect());
            }
        }

        private static int ParseSeedOrZero(string text) =>
            int.TryParse(text, out int seed) ? seed : 0;

        //
        // GUI スタイル初期化
        //

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = GuiTheme.TextPrimary } };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 14, normal = { textColor = GuiTheme.TextPrimary }, alignment = TextAnchor.MiddleCenter };
            _mutedStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 12, normal = { textColor = GuiTheme.TextMuted } };

            if (GuiTheme.JapaneseFont != null)
            {
                _titleStyle.font = GuiTheme.JapaneseFont;
                _bodyStyle.font = GuiTheme.JapaneseFont;
                _mutedStyle.font = GuiTheme.JapaneseFont;
            }
        }
    }
}
