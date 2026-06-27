// 初期ドラフト＋内政サブモード（Conscript/Upgrade）の描画担当。
//
// 【描画責務】
// - Phase=InitialDraft 中：3択カードを連続表示、選択で次オファー or R1 内政フェーズへ
// - Phase=InteriorAction 中：Bootstrap.CurrentInteriorSubMode が None 以外の時のみサブモード描画
//   （メインメニューと内政終了ボタンは MapGUI 右パネルに統合済み）
// - その他 Phase では描画しない（MapGUI／StoryGUI／MetaHubGUI が担当）
//
// 【サブモード（InteriorAction 中）】
// - None       ：MapGUI 右パネル表示中。本 GUI は描画しない
// - Conscript  ：召集ドラフト3択カード（行動力消費は選択時）
// - Upgrade    ：兵種強化対象の選択リスト
//
// 【サブモード状態の所在】
// SubMode は Bootstrap.CurrentInteriorSubMode に集約。InteriorGUI 内に
// `_subMode` を持たない（GUI 間で状態が分散すると同期漏れが起きるため）。状態遷移は
// Bootstrap の BeginConscript / BeginUpgradeSubMode / CancelInteriorSubMode / 各 Execute API
// が一元管理し、本 GUI は表示と入力中継のみを担う。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Echolos.Domain.Models;
using Echolos.UseCase.VSPrototype;
using Echolos.Presentation; // GuiTheme / IconRegistry を参照

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.VSPrototype
{
    // [DefaultExecutionOrder] で OnGUI 描画順を確定。MapGUI（-100）の後に描画されることで
    // InteriorAction サブモーダル（半透明オーバーレイ＋中央モーダル）が MapGUI の前面に重なる。
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class VSPrototypeInteriorGUI : MonoBehaviour
    {
        // 色定義（MetaHubGUI と同じトーン）

        private static readonly Color ColorBg          = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color ColorPanelBg     = new Color(0.14f, 0.15f, 0.20f);
        private static readonly Color ColorCardBg      = new Color(0.20f, 0.22f, 0.28f);
        private static readonly Color ColorCardRareBg  = new Color(0.30f, 0.24f, 0.40f);
        private static readonly Color ColorCardBorder  = new Color(0.55f, 0.95f, 1.00f);
        private static readonly Color ColorCardRareBdr = new Color(1.00f, 0.85f, 0.20f);
        private static readonly Color ColorRowBg       = new Color(0.18f, 0.20f, 0.26f);
        private static readonly Color ColorText        = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ColorTextMuted   = new Color(0.65f, 0.65f, 0.72f);
        private static readonly Color ColorAccent      = new Color(1.00f, 0.85f, 0.20f);

        // 内部状態

        // SubMode は Bootstrap.CurrentInteriorSubMode に集約。本 GUI は描画と入力中継のみを担う。

        private VSPrototypeBootstrap _bootstrap;
        private string _flashMessage = "";
        private Vector2 _upgradeScroll;

        // サブモード切替検知用（前回 OnGUI 時の SubMode）。切替時に _flashMessage を自動クリアして
        // 前モードの残骸メッセージを次モーダルに持ち越さない。
        private VSPrototypeInteriorSubMode _lastSeenSubMode = VSPrototypeInteriorSubMode.None;

        // 強化選択中のユニット（null＝ユニット一覧表示、非 null＝3 択カード表示）。
        private Unit _selectedUnitForUpgrade;

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _accentStyle;
        private GUIStyle _cardNameStyle;
        private GUIStyle _cardStatStyle;
        private GUIStyle _cardDescStyle;
        private GUIStyle _flashStyle;
        private GUIStyle _primaryButtonStyle;
        // ドラフトカードの属性ラベル用（textColor を都度書き換える）
        private GUIStyle _dynamicElementStyle;
        private bool _stylesBuilt;

        // ライフサイクル

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void OnGUI()
        {
            var phase = _bootstrap.CurrentPhase;
            bool isInitial = phase == VSPrototypePhase.InitialDraft;
            bool isInterior = phase == VSPrototypePhase.InteriorAction;
            if (!isInitial && !isInterior) return;

            // InteriorAction 中は SubMode=None のときメインメニューが MapGUI 側に出るので、
            // 本 GUI は何も描画しない。Conscript/Upgrade 起動時のみサブモーダル描画する。
            if (isInterior && _bootstrap.CurrentInteriorSubMode == VSPrototypeInteriorSubMode.None) return;

            // サブモード切替時に前モードの flash メッセージをクリア
            // （例：強化失敗メッセージが残ったまま次のサブモードを開くのを防ぐ）。
            var currentSubMode = _bootstrap.CurrentInteriorSubMode;
            if (currentSubMode != _lastSeenSubMode)
            {
                _flashMessage = "";
                _lastSeenSubMode = currentSubMode;
            }

            BuildStylesIfNeeded();

            if (isInitial) DrawInitialDraftLayout();
            else           DrawInteriorSubModalLayout();
        }

        /// <summary>
        /// Phase=InitialDraft：全画面背景＋中央寄せパネル。
        /// MapGUI 未描画フェーズなので全画面を占有してよい。
        /// </summary>
        private void DrawInitialDraftLayout()
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), ColorBg);

            float panelW = Mathf.Min(Screen.width * 0.78f, 1300f);
            float panelH = Mathf.Min(Screen.height * 0.88f, 760f);
            var panel = new Rect(
                (Screen.width  - panelW) * 0.5f,
                (Screen.height - panelH) * 0.5f,
                panelW, panelH);
            FillRect(panel, ColorPanelBg);

            GUILayout.BeginArea(new Rect(panel.x + 24, panel.y + 20, panel.width - 48, panel.height - 40));
            DrawInitialDraftUI();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Phase=InteriorAction かつ SubMode!=None：全画面背景＋中央モーダルパネル。
        /// MapGUI 側で同条件のときは描画スキップする取り決めに合わせて、InteriorGUI 側で
        /// 全画面背景を描画する（OnGUI 呼び出し順が不安定でも上書きされず確実に表示される）。
        /// モーダル外 MouseDown でサブモードキャンセル（MapGUI 既存スロットモーダルと同一仕様）。
        /// </summary>
        private void DrawInteriorSubModalLayout()
        {
            // 全画面背景（InitialDraft と同じ ColorBg で塗りつぶし）
            FillRect(new Rect(0, 0, Screen.width, Screen.height), ColorBg);

            // モーダル本体の矩形（中央寄せパネル）
            float modalW = Mathf.Min(Screen.width * 0.70f, 1100f);
            float modalH = Mathf.Min(Screen.height * 0.78f, 680f);
            var modal = new Rect(
                (Screen.width  - modalW) * 0.5f,
                (Screen.height - modalH) * 0.5f,
                modalW, modalH);

            // モーダル外 MouseDown でキャンセル（GUI.Button 方式だとボタン同時押下で配置無効化に
            // なるため、MouseDown を直接見て Event.Use する。MapGUI のスロットモーダルと同様）。
            // Conscript はキャンセル不可（ガチャ的やり直し封じ）。Upgrade は維持。
            bool canCancelByOutsideClick = _bootstrap.CurrentInteriorSubMode != VSPrototypeInteriorSubMode.Conscript;
            if (canCancelByOutsideClick
                && Event.current.type == EventType.MouseDown
                && !modal.Contains(Event.current.mousePosition))
            {
                _bootstrap.CancelInteriorSubMode();
                _flashMessage = "";
                Event.current.Use();
                return;
            }

            FillRect(modal, ColorPanelBg);
            DrawBorder(modal, 2, ColorCardBorder);

            GUILayout.BeginArea(new Rect(modal.x + 24, modal.y + 20, modal.width - 48, modal.height - 40));
            DrawInteriorActionUI();
            GUILayout.EndArea();
        }

        // 初期ドラフト UI

        private void DrawInitialDraftUI()
        {
            int remaining = _bootstrap.InteriorState.InitialDraftRemaining;
            int picked = _bootstrap.Roster.Count;
            GUILayout.Label("ユニット選抜（初期編成）", _titleStyle);
            GUILayout.Label($"3択ドラフト  残 {remaining}  ／  王国軍 {picked} 体", _mutedStyle);
            GUILayout.Space(16);

            var offer = _bootstrap.CurrentDraftOffer;
            if (offer != null && offer.Candidates.Count > 0)
            {
                DrawDraftCards(offer, isInitial: true);
            }
            else
            {
                GUILayout.Label("ドラフト準備中…", _bodyStyle);
            }

            GUILayout.FlexibleSpace();
            DrawCurrentRosterRow();
        }

        // 内政フェーズ UI（サブモード起動時のみ描画）

        private void DrawInteriorActionUI()
        {
            int round = _bootstrap.CurrentRound;
            var state = _bootstrap.InteriorState;

            // 軽量ヘッダ（コンテキスト保持・サブモード中も R 番号と行動力を見せる）
            GUILayout.BeginHorizontal();
            GUILayout.Label($"R{round}/7  内政サブメニュー", _subtitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"行動力 {state.ActionPoints}/{state.ActionPointsPerRound}", _accentStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            switch (_bootstrap.CurrentInteriorSubMode)
            {
                case VSPrototypeInteriorSubMode.Conscript: DrawConscriptCards(); break;
                case VSPrototypeInteriorSubMode.Upgrade:   DrawUpgradeList();    break;
                default: return; // OnGUI 側で除外済（ガード）
            }

            if (!string.IsNullOrEmpty(_flashMessage))
            {
                GUILayout.Space(4);
                GUILayout.Label(_flashMessage, _flashStyle);
            }
        }

        // 召集ドラフト（InteriorAction サブモード）

        private void DrawConscriptCards()
        {
            GUILayout.Label("── 召集ドラフト ──", _subtitleStyle);
            // 3 択から必ず 1 体選択（キャンセル不可・ガチャ的やり直し封じ）
            GUILayout.Label("3 体から必ず 1 体を選択してください（キャンセル不可）", _mutedStyle);
            GUILayout.Space(6);

            var offer = _bootstrap.CurrentDraftOffer;
            if (offer == null) { _bootstrap.CancelInteriorSubMode(); return; }
            DrawDraftCards(offer, isInitial: false);
        }

        // ユニット個別 Lv 強化（InteriorAction サブモード）
        //
        // 2 段構成：
        //   段階 1：強化可能ユニット一覧（個別行ごと・固有ユニットは除外）→ 1 体選択
        //   段階 2：そのユニットの AvailableUpgrades 3 件をカード表示 → 1 件選択
        // _selectedUnitForUpgrade が null かどうかで段階を切替える。

        private void DrawUpgradeList()
        {
            if (_selectedUnitForUpgrade != null)
            {
                DrawUpgradeChoiceCards();
                return;
            }

            GUILayout.Label("── ユニット強化 ──", _subtitleStyle);
            GUILayout.Label("対象のユニットを選んでください（Lv 上限 3）", _mutedStyle);
            GUILayout.Space(6);

            // 固有ユニット（王女・ブリジット）は内政画面の強化対象外（メタ強化画面で扱う）。
            // 並び順は王国軍リストと共通：DraftPool 順 + 同兵種は Lv 降順。
            var candidates = UnitRosterSorter
                .SortByPoolOrder(_bootstrap.Roster, _bootstrap.DraftPoolCatalog)
                .Where(u => !VSPrototypeInteriorService.IsUniqueUnit(u.Id))
                .ToList();

            _upgradeScroll = GUILayout.BeginScrollView(_upgradeScroll, GUILayout.Height(360));
            foreach (var unit in candidates) DrawUpgradeUnitRow(unit);
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("キャンセル", GUILayout.Width(220), GUILayout.Height(34)))
            {
                _bootstrap.CancelInteriorSubMode();
                _selectedUnitForUpgrade = null;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawUpgradeUnitRow(Unit unit)
        {
            bool atCap = unit.Level >= VSPrototypeInteriorState.MaxUnitLevel;
            bool noUpgrades = unit.AvailableUpgrades == null || unit.AvailableUpgrades.Count == 0;
            bool disabled = atCap || noUpgrades;

            var rowRect = GUILayoutUtility.GetRect(0, 56, GUILayout.ExpandWidth(true));
            FillRect(rowRect, ColorRowBg);

            float padX = 12f;
            var iconRect = new Rect(rowRect.x + padX, rowRect.y + 8, 40, 40);
            IconRegistry.TryDrawIcon(iconRect, unit.Id);

            var nameRect = new Rect(iconRect.xMax + 12, rowRect.y + 8, 360, 20);
            string roleTag = UnitDisplayLabels.RoleTagsLabel(unit.CombatRoles);
            GUI.Label(nameRect, $"{unit.Name}  {roleTag}", _cardNameStyle);

            var statRect = new Rect(iconRect.xMax + 12, rowRect.y + 30, 320, 20);
            GUI.Label(statRect, $"HP {unit.EffectiveMaxHP}  ATK {unit.EffectiveATK}  DEF {unit.EffectiveDEF}",
                _cardStatStyle);

            var lvRect = new Rect(rowRect.xMax - 320, rowRect.y + 16, 160, 24);
            GUI.Label(lvRect, $"Lv {unit.Level}/{VSPrototypeInteriorState.MaxUnitLevel}", _bodyStyle);

            var btnRect = new Rect(rowRect.xMax - 140, rowRect.y + 10, 120, 36);
            string label = atCap ? "Lv MAX" : (noUpgrades ? "選択肢なし" : "強化選択 →");
            GUI.enabled = !disabled;
            if (GUI.Button(btnRect, label))
            {
                _selectedUnitForUpgrade = unit;
            }
            GUI.enabled = true;

            GUILayout.Space(4);
        }

        private void DrawUpgradeChoiceCards()
        {
            var unit = _selectedUnitForUpgrade;
            GUILayout.Label($"── {unit.Name} の強化選択 ──", _subtitleStyle);
            GUILayout.Label($"現在 Lv {unit.Level} → Lv {unit.Level + 1}　残り {unit.AvailableUpgrades.Count} 択", _mutedStyle);
            GUILayout.Space(8);

            const float cardW = 280f;
            const float cardH = 280f;
            const float gap = 24f;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var options = unit.AvailableUpgrades.ToList();
            for (int i = 0; i < options.Count; i++)
            {
                var rect = GUILayoutUtility.GetRect(cardW, cardH, GUILayout.Width(cardW), GUILayout.Height(cardH));
                DrawUpgradeChoiceCard(rect, unit, options[i]);
                if (i < options.Count - 1) GUILayout.Space(gap);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("← ユニット選択に戻る", GUILayout.Width(220), GUILayout.Height(34)))
            {
                _selectedUnitForUpgrade = null;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawUpgradeChoiceCard(Rect rect, Unit unit, UnitUpgrade upgrade)
        {
            FillRect(rect, ColorCardBg);
            DrawBorder(rect, 2, ColorCardBorder);

            var nameRect = new Rect(rect.x + 8, rect.y + 18, rect.width - 16, 30);
            GUI.Label(nameRect, upgrade.Name, _cardNameStyle);

            var descRect = new Rect(rect.x + 16, rect.y + 64, rect.width - 32, 100);
            GUI.Label(descRect, upgrade.Description, _bodyStyle);

            // 行動力 0 のときはボタン disabled＋ラベル変更（強化選択画面までは進めるが採用は不可）。
            bool noActionPoints = _bootstrap.InteriorState.ActionPoints < VSPrototypeInteriorState.ActionCost;
            var btnRect = new Rect(rect.x + 16, rect.yMax - 44, rect.width - 32, 32);
            string btnLabel = noActionPoints ? "行動力不足" : "この強化を採用";
            GUI.enabled = !noActionPoints;
            if (GUI.Button(btnRect, btnLabel))
            {
                if (_bootstrap.ExecuteUpgradeUnit(unit, upgrade))
                {
                    // 成功時は SubMode=None＝モーダル閉じる。完了通知は MapGUI 側で
                    // Bootstrap.LastUpgradedUnit 検知して右パネルに表示する。
                    _selectedUnitForUpgrade = null;
                }
                else
                {
                    _flashMessage = $"{unit.Name} の強化に失敗";
                }
            }
            GUI.enabled = true;
        }

        // ドラフトカード（初期・召集 共通）

        private void DrawDraftCards(VSPrototypeDraftOffer offer, bool isInitial)
        {
            // 全枠 Rare 状態は★スペシャル演出（明示の AllRareSpecial 当選／独立抽選の偶発全 Rare のどちらも統一）。
            if (offer.IsRare)
                GUILayout.Label("★ レアプール抽選", _accentStyle);

            const float cardW = 280f;
            const float cardH = 460f;
            const float gap = 24f;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int i = 0; i < offer.Candidates.Count; i++)
            {
                var rect = GUILayoutUtility.GetRect(cardW, cardH, GUILayout.Width(cardW), GUILayout.Height(cardH));
                bool slotIsRare = i < offer.CandidateRarities.Count && offer.CandidateRarities[i];
                DrawCard(rect, offer.Candidates[i], slotIsRare, i, isInitial);
                if (i < offer.Candidates.Count - 1) GUILayout.Space(gap);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawCard(Rect rect, Unit unit, bool isRare, int index, bool isInitial)
        {
            FillRect(rect, isRare ? ColorCardRareBg : ColorCardBg);
            DrawBorder(rect, 2, isRare ? ColorCardRareBdr : ColorCardBorder);

            // アイコン
            var iconRect = new Rect(rect.x + 8, rect.y + 14, rect.width - 16, 130);
            IconRegistry.TryDrawIcon(iconRect, unit.Id);

            // 名前
            var nameRect = new Rect(rect.x + 8, rect.y + 156, rect.width - 16, 30);
            GUI.Label(nameRect, unit.Name, _cardNameStyle);

            // 兵種属性＋役割タグ（属性 1 文字のみ色付き）
            string roleTag = UnitDisplayLabels.RoleTagsLabel(unit.CombatRoles);
            string atkLabel = UnitDisplayLabels.AttackKindLabel(unit.AttackKind);
            string elemLabel = UnitDisplayLabels.ElementLabel(unit.UnitElement);
            // 3 ラベルを Rect 並列描画。中央寄せのため概算幅で配置（カード幅 280 想定）。
            float metaY = rect.y + 184;
            float metaCenter = rect.x + rect.width * 0.5f;
            var elemRect = new Rect(metaCenter - 80, metaY, 18, 20);
            var sepRect  = new Rect(elemRect.xMax + 2, metaY, 12, 20);
            var atkRect  = new Rect(sepRect.xMax + 2, metaY, 56, 20);
            var roleRect = new Rect(atkRect.xMax + 4, metaY, 80, 20);
            _dynamicElementStyle.normal.textColor = UnitDisplayLabels.ElementColor(unit.UnitElement);
            GUI.Label(elemRect, elemLabel, _dynamicElementStyle);
            GUI.Label(sepRect,  "/", _mutedStyle);
            GUI.Label(atkRect,  atkLabel, _mutedStyle);
            GUI.Label(roleRect, roleTag, _mutedStyle);

            // ステータス
            int y = 210;
            GUI.Label(new Rect(rect.x + 16, rect.y + y,      rect.width - 32, 20), $"HP   {unit.MaxHP}", _cardStatStyle); y += 22;
            GUI.Label(new Rect(rect.x + 16, rect.y + y,      rect.width - 32, 20), $"ATK  {unit.BaseATK}", _cardStatStyle); y += 22;
            GUI.Label(new Rect(rect.x + 16, rect.y + y,      rect.width - 32, 20), $"DEF  {unit.DEF}", _cardStatStyle); y += 22;
            GUI.Label(new Rect(rect.x + 16, rect.y + y,      rect.width - 32, 20), $"SPD  {unit.BaseSPD}", _cardStatStyle);

            // 説明文（5 行・自動改行）。空文字なら描画スキップ。
            if (!string.IsNullOrEmpty(unit.Description))
            {
                var descRect = new Rect(rect.x + 12, rect.y + 308, rect.width - 24, 100);
                GUI.Label(descRect, unit.Description, _cardDescStyle);
            }

            // 選択ボタン
            var btnRect = new Rect(rect.x + 16, rect.yMax - 44, rect.width - 32, 32);
            if (GUI.Button(btnRect, "この兵種を採用"))
            {
                if (isInitial)
                {
                    // InitialDraft 中は次オファーがすぐ出る＋ロスター数が増える＝フィードバック自明。
                    _bootstrap.AcceptInitialDraftPick(index);
                }
                else
                {
                    // 召集成功時は SubMode=None＝モーダル閉じる。完了通知は MapGUI 側で
                    // Bootstrap.LastConscriptedUnit 検知して右パネルに表示する。
                    _bootstrap.AcceptConscriptPick(index);
                }
            }
        }

        // 現在の王国軍サマリ（パネル下部の細い帯）

        private void DrawCurrentRosterRow()
        {
            GUILayout.Label("── 現在の王国軍 ──", _mutedStyle);
            GUILayout.BeginHorizontal();
            foreach (var unit in _bootstrap.Roster)
            {
                var rect = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48), GUILayout.Height(48));
                IconRegistry.TryDrawIcon(rect, unit.Id);
                GUILayout.Space(6);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // 描画ユーティリティ＋スタイル構築

        private static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            FillRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            FillRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            FillRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 28, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 16, normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _bodyStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 16, normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _mutedStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14, normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _accentStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorAccent },
                alignment = TextAnchor.MiddleLeft,
            };
            _cardNameStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter, wordWrap = true,
            };
            _cardStatStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _cardDescStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 13, normal = { textColor = ColorText },
                alignment = TextAnchor.UpperLeft, wordWrap = true,
            };
            _flashStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, normal = { textColor = ColorAccent },
                alignment = TextAnchor.MiddleCenter,
            };
            _primaryButtonStyle = new GUIStyle(GUI.skin.button) {
                fontSize = 16, fontStyle = FontStyle.Bold,
            };
            _dynamicElementStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _stylesBuilt = true;
        }
    }
}
