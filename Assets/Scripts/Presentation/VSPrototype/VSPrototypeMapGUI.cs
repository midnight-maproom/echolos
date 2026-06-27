// 戦略マップ GUI。
//
// 【画面レイアウト（PC 16:9 横長前提・3列分割）】
//   左 20%：王国軍パネル（縦並びリスト）
//   中央 55%：マップ本体（9マス＋本拠地）
//   右 25%：ヘッダー＋戦闘結果サマリ＋ガイド＋進行ボタン
//
// 【描画責務】
// - マップ描画（色分け＋ラベル＋バルドゥイン金枠）
// - 王国軍選択 → マスクリックで配置（CanAssign 経由）
// - 配置済の王国軍クリックで解除
// - リセットボタンで初期状態に戻す
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Echolos.Domain.Models;
using Echolos.UseCase.VSPrototype;
using Echolos.Presentation;  // GuiTheme / IconRegistry を参照

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.VSPrototype
{
    // [DefaultExecutionOrder] で OnGUI 描画順を確定させる。
    // MapGUI を先（-100）に描画し、InteriorGUI（0 = 後）が上にサブモーダル（半透明オーバーレイ＋
    // 中央モーダル）を被せる構造。同じ GameObject に複数 GUI をアタッチした場合、Unity の OnGUI
    // 呼び出し順は Script Execution Order に従う（指定なしだとアタッチ順依存で不安定）。
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class VSPrototypeMapGUI : MonoBehaviour
    {
        // 色定義（レイアウト用パレット）

        private static readonly Color ColorBg                = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color ColorPanelBg           = new Color(0.14f, 0.15f, 0.20f);
        // マス背景色塗りは撤廃。マップ背景画像＋拠点アイコンで種別を表現する。
        private static readonly Color ColorBalduinBorder     = new Color(1.00f, 0.85f, 0.20f);
        private static readonly Color ColorSelectedBorder    = new Color(0.20f, 0.90f, 0.95f);
        private static readonly Color ColorAssignableBorder  = new Color(0.55f, 0.95f, 1.00f);

        // ノード配置：マップ背景画像（map_background.png 1254×1254）内の円中心位置に合わせる
        // Inspector で Play 中も含めて目視調整可能（[SerializeField]）。
        // 配列 index：[0-2]=敵拠点左中右 / [3-5]=敵領左中右 / [6-8]=自領左中右 / [9]=本拠地。
        // 編集後、ユーザーが「OK」と言った時点の Inspector 値を Claude がコードのデフォルトに反映する運用。

        [Header("マップノード配置（画像内正規化座標 0-1）")]
        [Tooltip("[0-2]=敵拠点左中右 / [3-5]=敵領左中右 / [6-8]=自領左中右 / [9]=本拠地（10 要素必須）")]
        [SerializeField] private Vector2[] _nodeImageCenters = new Vector2[]
        {
            new Vector2(0.245f, 0.13f), new Vector2(0.510f, 0.13f), new Vector2(0.780f, 0.13f),
            new Vector2(0.235f, 0.35f), new Vector2(0.505f, 0.35f), new Vector2(0.775f, 0.35f),
            new Vector2(0.230f, 0.57f), new Vector2(0.505f, 0.57f), new Vector2(0.775f, 0.57f),
            new Vector2(0.505f, 0.79f),
        };

        [Tooltip("城型拠点（home / enemy_stronghold / balduin）の直径。円を覆い隠すくらいが目安。")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float _cityDiameter = 0.22f;

        [Tooltip("野営地（friendly / enemy_territory）の直径。円内に収まるサイズが目安。")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float _campDiameter = 0.18f;
        private static readonly Color ColorRosterBg          = new Color(0.20f, 0.22f, 0.28f);
        private static readonly Color ColorRosterSelected    = new Color(0.10f, 0.50f, 0.65f);
        private static readonly Color ColorRosterAssigned    = new Color(0.30f, 0.30f, 0.36f);
        private static readonly Color ColorText              = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ColorTextMuted         = new Color(0.65f, 0.65f, 0.72f);

        // 人数バッジ用配色：BattleMode × 人数比で枠色が変わる省スペース表示。
        // 防衛系（自陣最前線・本拠地ボス）は赤/黄/水で強調、攻め込み系は灰/橙/青で控えめ、戦線外は灰固定。
        private static readonly Color ColorBadgeBg                  = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color ColorBadgeFrameDefenseDanger  = new Color(0.95f, 0.30f, 0.30f);
        private static readonly Color ColorBadgeFrameDefenseWarn    = new Color(0.95f, 0.85f, 0.25f);
        private static readonly Color ColorBadgeFrameDefenseSafe    = new Color(0.40f, 0.85f, 0.95f);
        private static readonly Color ColorBadgeFrameAttackWarn     = new Color(0.95f, 0.55f, 0.20f);
        private static readonly Color ColorBadgeFrameAttackSafe     = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color ColorBadgeFrameNeutral        = new Color(0.55f, 0.55f, 0.60f);

        // 内部状態

        private VSPrototypeBootstrap _bootstrap;
        private string _flashMessage = "";

        // 配置モーダル内で表示するメッセージ（右パネル _flashMessage と分離）。
        // モーダルが暗幕で右パネルを隠すため、モーダル内操作のフィードバックはこちらに表示する。
        private string _modalMessage = "";
        private Vector2 _rosterScroll;
        private string _resultSummary = ""; // 戦闘解決後の結果一覧表示用

        // 配置統合モーダル。マスクリックで開き、内部でユニット選択＋スロット配置の2クリック動線。
        private MapNode _placementModalNode;
        // 配置モーダル内で現在選択中のユニット（モーダルを跨いで保持しない・閉じると消える）
        private Unit _placementModalSelectedUnit;
        // モーダル内ユニット一覧スクロール
        private Vector2 _placementModalUnitScroll;
        // ホバー中のマス（敵編成ツールチップ用・常時偵察）
        private MapNode _hoveredNode;
        // ホバー中の王国軍リスト行（ユニット説明ツールチップ用）
        private Unit _hoveredRosterUnit;

        // 汎用確認ダイアログ。null=非表示。表示中は他のクリック入力（マス／ボタン／ホバー）を抑止して
        // モーダル外 MouseDown でキャンセル扱い。OnGUI モーダル 3 大落とし穴対応：
        //   ・ホット争奪 → マス／他ボタンの GUI.Button を _confirmDialog 非 null 時に非描画
        //   ・MouseDown+Use → モーダル外 MouseDown はキャンセル＋ Event.Use で背面 Button へ伝播抑制
        //   ・遅延状態消去 → コールバック直後に _confirmDialog=null
        private struct ConfirmDialog
        {
            public string Title;
            public string Message;
            public string ConfirmLabel;
            public Action OnConfirm;
        }
        private ConfirmDialog? _confirmDialog;

        // GUIStyle はランタイム生成（OnGUI 初回で構築）
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _nodeSubStyle;
        private GUIStyle _rosterNameStyle;
        private GUIStyle _rosterBadgeStyle;
        private GUIStyle _rosterLvBoostStyle;
        private GUIStyle _flashStyle;
        private GUIStyle _badgeStyle;
        // 右パネル戦線状況・属性シナジー Tips 用。
        // textColor を呼び出し都度書き換える mutable な汎用ラベル（行ごとに色を変えるため）。
        private GUIStyle _dynamicLabelStyle;
        private bool _stylesBuilt;

        // ライフサイクル

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void OnGUI()
        {
            // Phase=Run / InteriorAction の両方で描画する。
            // InteriorAction 中も配置を有効化し、行動力消費の召集/強化を右パネルから呼べるようにする。
            var phase = _bootstrap.CurrentPhase;
            if (phase != VSPrototypePhase.Run && phase != VSPrototypePhase.InteriorAction) return;
            if (_bootstrap.MapState == null) return; // 念のためのガード

            // 自動加入の Flash 表示（内政フェーズ開始時に Bootstrap が LastAutoConscriptUnit をセットする）。
            // これがラウンド開始通知を兼ねるため R 番号も含める（R1-R6 で発火）。
            if (_bootstrap.LastAutoConscriptUnit != null)
            {
                _flashMessage = $"R{_bootstrap.CurrentRound} 開始 {_bootstrap.LastAutoConscriptUnit.Name} が王国軍に加わりました（ランダム加入）";
                _bootstrap.ConsumeAutoConscriptNotice();
            }

            // R7 ボス戦突入時の専用通知（内政なし＝自動加入なしのため別経路）
            if (_bootstrap.LastRoundStartedAsBoss)
            {
                _flashMessage = "王都に帝国軍が現れました。本拠地にユニットを配置して最終決戦に挑みましょう!";
                _bootstrap.ConsumeBossRoundStartNotice();
            }

            // 召集モーダルから戻った瞬間のフィードバック（モーダル中は右パネルが見えないので
            // モーダル終了直後にここで反映する）
            if (_bootstrap.LastConscriptedUnit != null)
            {
                _flashMessage = $"{_bootstrap.LastConscriptedUnit.Name} を召集";
                _bootstrap.ConsumeConscriptNotice();
            }

            // 強化モーダルから戻った瞬間のフィードバック（同上）
            if (_bootstrap.LastUpgradedUnit != null)
            {
                _flashMessage = $"{_bootstrap.LastUpgradedUnit.Name} に「{_bootstrap.LastUpgradedUpgrade.Name}」を適用";
                _bootstrap.ConsumeUpgradeNotice();
            }

            // Phase=Battle から Run 復帰した直後（BattleGUI が FinishBattleReplay した）に
            // 結果サマリを自動構築する。戦闘実行ボタン側では「Run のまま戻ってきた縮退ケース」のみ
            // 即時構築するため、Battle 経由の通常ケースは LastRoundResult ベースの自動同期で拾う。
            if (_bootstrap.LastRoundResult != null && string.IsNullOrEmpty(_resultSummary))
                _resultSummary = BuildResultSummary(_bootstrap.LastRoundResult);

            // InteriorAction で内政サブモードが立っている時は InteriorGUI が全画面で描画する。
            // ここで MapGUI が背景描画すると上書きしてしまう（OnGUI 呼び出し順は同 GameObject 内では
            // 不安定で、DefaultExecutionOrder も保証されない実機ケースあり）ので、明示的にスキップ。
            if (phase == VSPrototypePhase.InteriorAction
                && _bootstrap.CurrentInteriorSubMode != VSPrototypeInteriorSubMode.None) return;

            BuildStylesIfNeeded();

            // 背景
            FillRect(new Rect(0, 0, Screen.width, Screen.height), ColorBg);

            // 3列分割：左 20% / 中央 55% / 右 25%
            float leftW   = Screen.width * 0.20f;
            float centerW = Screen.width * 0.55f;
            float rightW  = Screen.width - leftW - centerW;

            // ホバー検出は描画前に毎フレームリセット（マウスが何処にも無ければ null）
            _hoveredNode = null;
            _hoveredRosterUnit = null;

            DrawRosterPanel(new Rect(0, 0, leftW, Screen.height));
            DrawMap(new Rect(leftW, 0, centerW, Screen.height));
            DrawInfoPanel(new Rect(leftW + centerW, 0, rightW, Screen.height));

            // モーダル系は最後に描画して上に重ねる。
            // 確認ダイアログは最優先＝表示中は他モーダル／ホバーを抑制する。
            if (_confirmDialog.HasValue) DrawConfirmDialog();
            else if (_placementModalNode != null) DrawPlacementModal();
            else if (_hoveredNode != null) DrawNodeTooltip(_hoveredNode);
            else if (_hoveredRosterUnit != null) DrawRosterUnitTooltip(_hoveredRosterUnit);
        }

        // 左カラム：王国軍パネル

        private void DrawRosterPanel(Rect area)
        {
            FillRect(area, ColorPanelBg);
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 12, area.width - 16, area.height - 24));

            // ヘッダ：王国軍数＋配置／未配置サマリ
            int total = _bootstrap.Roster.Count;
            int assigned = 0;
            foreach (var n in _bootstrap.MapState.AllNodes())
                assigned += n.AssignedAllies.Count;
            int unassigned = total - assigned;

            GUILayout.Label("王国軍", _titleStyle);
            GUILayout.Label($"全{total} 体  配置 {assigned} / 未配置 {unassigned}", _mutedStyle);
            GUILayout.Space(6);

            _rosterScroll = GUILayout.BeginScrollView(_rosterScroll);
            float rowWidth = area.width - 24;
            foreach (var group in BuildRosterGroups(_bootstrap.Roster))
            {
                GUILayout.Label(group.Header, _mutedStyle);
                if (group.Units.Count == 0)
                    GUILayout.Label("（なし）", _mutedStyle);
                else
                    foreach (var u in group.Units)
                        DrawRosterUnit(u, rowWidth);
                GUILayout.Space(6);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        /// <summary>左ペイン王国軍リストのグループ単位（見出し＋該当ユニット）。</summary>
        private struct RosterGroup
        {
            public string Header;
            public List<Unit> Units;
        }

        /// <summary>
        /// 王国軍を「未配置 → 本拠地 → 自領 左/中/右 → 敵領 左/中/右 → 敵拠点 左/中/右」の
        /// 11 グループに振り分ける。順序は固定で空グループも見出しを残し、配置不在のマスが
        /// 一目でわかる一覧性を優先（フォロー⑤主軸）。各見出しに「N/6」配置済人数を表示。
        /// グループ内の二次ソートは SortRosterForDisplay（固有→Normal→Rare）を継承。
        /// </summary>
        private List<RosterGroup> BuildRosterGroups(IEnumerable<Unit> roster)
        {
            var assignmentMap = new Dictionary<Unit, MapNode>();
            foreach (var n in _bootstrap.MapState.AllNodes())
                foreach (var a in n.AssignedAllies)
                    assignmentMap[a.BaseUnit] = n;

            var byNode = new Dictionary<MapNode, List<Unit>>();
            var unassigned = new List<Unit>();
            foreach (var u in UnitRosterSorter.SortByPoolOrder(roster, _bootstrap.DraftPoolCatalog))
            {
                if (assignmentMap.TryGetValue(u, out var n))
                {
                    if (!byNode.TryGetValue(n, out var list))
                        byNode[n] = list = new List<Unit>();
                    list.Add(u);
                }
                else
                {
                    unassigned.Add(u);
                }
            }

            var groups = new List<RosterGroup>
            {
                new RosterGroup { Header = $"── 未配置（{unassigned.Count} 体）──", Units = unassigned },
            };

            var state = _bootstrap.MapState;
            AddNodeGroup(groups, byNode, state.Home, "本拠地");
            for (int col = 0; col < 3; col++)
                AddNodeGroup(groups, byNode,
                    state.GetNode(col, VSPrototypeMapState.LayerFriendly),
                    $"自領 {ColLabel(col)}");
            for (int col = 0; col < 3; col++)
                AddNodeGroup(groups, byNode,
                    state.GetNode(col, VSPrototypeMapState.LayerEnemyTerritory),
                    $"敵領 {ColLabel(col)}");
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);
                AddNodeGroup(groups, byNode, node, $"敵拠点 {ColLabel(col)}");
            }

            return groups;
        }

        private static void AddNodeGroup(
            List<RosterGroup> groups,
            Dictionary<MapNode, List<Unit>> byNode,
            MapNode node,
            string headerBase)
        {
            var units = byNode.TryGetValue(node, out var list) ? list : new List<Unit>();
            groups.Add(new RosterGroup
            {
                Header = $"── {headerBase}（{node.AssignedAllies.Count}/{MapNode.MaxAlliedSlots}）──",
                Units = units,
            });
        }

        private static string ColLabel(int col) => col == 0 ? "左" : col == 1 ? "中" : "右";

        // 王国軍リスト表示用ソートは UnitRosterSorter.SortByPoolOrder に集約（強化リストと共通）。

        private void DrawRosterUnit(Unit unit, float panelW)
        {
            MapNode assignedNode = FindAssignedNode(unit);
            bool isAssigned = assignedNode != null;

            const float rowH = 80f;
            const float iconSize = 60f;
            var rect = GUILayoutUtility.GetRect(panelW, rowH);

            // 配置済はグレー、未配置は通常色（配置先はグループ見出しで識別済のため
            // 色だけは行単位で残し、未配置 = 操作対象という視覚誘導を維持）。
            Color bg = isAssigned ? ColorRosterAssigned : ColorRosterBg;
            FillRect(rect, bg);

            var iconRect = new Rect(rect.x + 6, rect.y + (rowH - iconSize) / 2f, iconSize, iconSize);
            IconRegistry.TryDrawIcon(iconRect, unit.Id);

            var nameRect = new Rect(rect.x + iconSize + 14, rect.y + 14, rect.width - iconSize - 18, 20);
            GUI.Label(nameRect, unit.Name, _rosterNameStyle);

            // ステ行：Lv 表示（Lv2 以上は金色強調）＋ HP/ATK ＋ 属性（色付き）＋ 攻撃種別＋役割タグ。
            const float lvWidth = 44f;
            const float elemWidth = 18f;
            var lvRect    = new Rect(rect.x + iconSize + 14, rect.y + 38, lvWidth, 18);
            var statRect  = new Rect(lvRect.xMax + 4,        rect.y + 38, 130, 18);
            var elemRect  = new Rect(statRect.xMax,          rect.y + 38, elemWidth, 18);
            var tailRect  = new Rect(elemRect.xMax + 2,      rect.y + 38, rect.width - iconSize - lvWidth - 130 - elemWidth - 30, 18);
            string roleTag = UnitDisplayLabels.RoleTagsLabel(unit.CombatRoles);
            string atkLabel = UnitDisplayLabels.AttackKindLabel(unit.AttackKind);
            string elemLabel = UnitDisplayLabels.ElementLabel(unit.UnitElement);
            GUI.Label(lvRect, $"Lv {unit.Level}",
                unit.Level >= 2 ? _rosterLvBoostStyle : _rosterBadgeStyle);
            GUI.Label(statRect, $"HP {unit.EffectiveMaxHP} ATK {unit.EffectiveATK}", _rosterBadgeStyle);
            _dynamicLabelStyle.normal.textColor = UnitDisplayLabels.ElementColor(unit.UnitElement);
            _dynamicLabelStyle.fontSize = 13;
            _dynamicLabelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(elemRect, elemLabel, _dynamicLabelStyle);
            _dynamicLabelStyle.fontStyle = FontStyle.Normal;
            _dynamicLabelStyle.fontSize = 14;
            GUI.Label(tailRect, $"{atkLabel} {roleTag}", _rosterBadgeStyle);

            // ホバー検出（モーダル中はツールチップを出さないので非モーダル時のみ拾う）
            if (_placementModalNode == null && rect.Contains(Event.current.mousePosition))
                _hoveredRosterUnit = unit;

            GUILayout.Space(4);
        }

        /// <summary>指定ユニットが配置されているマスを返す。未配置なら null。</summary>
        private MapNode FindAssignedNode(Unit unit)
        {
            foreach (var node in _bootstrap.MapState.AllNodes())
                if (node.AssignedAllies.Any(a => a.BaseUnit == unit))
                    return node;
            return null;
        }

        /// <summary>マスの位置を「自領 中央」のような短いラベルで返す（左パネルバッジ用）。</summary>
        private static string NodeLocationLabel(MapNode node)
        {
            if (node.Kind == MapNodeKind.Home) return "本拠地";
            string layer = node.Kind == MapNodeKind.Friendly ? "自領"
                : node.Kind == MapNodeKind.EnemyTerritory ? "敵領"
                : "敵拠点";
            string col = node.Col == 0 ? "左" : node.Col == 1 ? "中" : "右";
            return $"{layer} {col}";
        }

        // 中央カラム：マップ本体

        private void DrawMap(Rect area)
        {
            // マップ背景画像（Resources/Images/VSPrototype/map_background.png）が
            // あれば中央領域に ScaleAndCrop で描画。無ければ何もしない＝透けて ColorBg が見える。
            BackgroundRegistry.TryDrawCover(area, "map_background");

            // マス描画：画像内の円位置に合わせて配置（_nodeImageCenters[0-9] 参照・Inspector 編集可）。
            // 進攻ルートはマップ背景画像内の道で表現する。
            var state = _bootstrap.MapState;

            // Row 0 = 敵拠点（index 0,1,2）
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);
                DrawMapNode(ResolveNodeRectByImage(area, _nodeImageCenters[col], DiameterOf(node)), node);
            }
            // Row 1 = 敵領（index 3,4,5）
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerEnemyTerritory);
                DrawMapNode(ResolveNodeRectByImage(area, _nodeImageCenters[3 + col], DiameterOf(node)), node);
            }
            // Row 2 = 自領（index 6,7,8）
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerFriendly);
                DrawMapNode(ResolveNodeRectByImage(area, _nodeImageCenters[6 + col], DiameterOf(node)), node);
            }
            // Row 3 = 本拠地（index 9・中央列のみ）
            DrawMapNode(ResolveNodeRectByImage(area, _nodeImageCenters[9], DiameterOf(state.Home)), state.Home);
        }

        /// <summary>
        /// マス種別に応じた直径（正規化サイズ）を返す。城型は大きく、野営地は小さく。
        /// </summary>
        private float DiameterOf(MapNode node)
        {
            bool isCamp = node.Kind == MapNodeKind.Friendly
                       || node.Kind == MapNodeKind.EnemyTerritory;
            return isCamp ? _campDiameter : _cityDiameter;
        }

        /// <summary>
        /// 画像内正規化座標（0-1）→ 描画 Rect 内の絶対座標へ変換。
        /// map_background.png は正方形（1254×1254）を ScaleAndCrop で area に縦合わせ表示する前提
        /// （area.height &gt; area.width で横方向に左右はみ出てクロップ）。
        /// </summary>
        private static Rect ResolveNodeRectByImage(Rect area, Vector2 imagePos, float diameter)
        {
            float scale = area.height;
            float imgLeftX = area.x - (scale - area.width) * 0.5f;
            float centerX = imgLeftX + imagePos.x * scale;
            float centerY = area.y + imagePos.y * scale;
            float size = diameter * scale;
            return new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size);
        }

        private void DrawMapNode(Rect rect, MapNode node)
        {
            // 拠点アイコン＋人数バッジを描画。マス背景色／配置可能ハイライト枠／配置スロット小
            // アイコン等は撤廃済＝マップ背景画像と拠点アイコンの絵で種別・状態を伝え、戦況は
            // 人数バッジ（味方-敵）と枠色（BattleMode × 人数比）で省スペース表現する方針。
            TryDrawNodeIcon(rect, node);
            DrawPersonnelBadge(rect, node);

            // 透明ボタンでクリック検出
            // ※ モーダル表示中は GUI.Button 自体を描画しない。これがないと、MouseDown 時に
            //   マスのボタンがホットコントロールを横取りして、上に重なるモーダル内ボタンが
            //   MouseUp で発火しなくなる（GUI.Button のホット争奪・実機試遊で観測）。
            //   配置モーダル／確認ダイアログの両方で抑止。
            if (_placementModalNode == null && !_confirmDialog.HasValue)
            {
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    HandleMapNodeClick(node);
            }

            // ホバー検出（敵編成ツールチップ用・常時偵察）
            // Layout/Repaint 双方で同じ判定になるよう Event.type は見ない
            // （見ると DrawEnemyTooltip が Layout で呼ばれない時に GUILayout カウントが食い違う）
            if (rect.Contains(Event.current.mousePosition))
                _hoveredNode = node;
        }

        /// <summary>
        /// マス種別 → 拠点アイコンキーのマッピング。バルドゥイン拠点だけ独自キー、
        /// 所属（どちら陣営が現在保持しているか）で画像が決まるシンプルな対応：
        ///   自領健在 / 占領済み敵領 / 占領済み敵拠点 → 自軍画像
        ///   自領陥落 / 未占領敵領 / 未占領敵拠点     → 敵軍画像
        /// 未対応種別（Home 以外で想定外）は null＝アイコン描画スキップ。
        /// </summary>
        private static string NodeIconKey(MapNode node)
        {
            switch (node.Kind)
            {
                case MapNodeKind.Home:
                    return "node_home";
                case MapNodeKind.Friendly:
                    return node.IsFallen ? "node_enemy_territory" : "node_friendly";
                case MapNodeKind.EnemyTerritory:
                    return node.IsCaptured ? "node_friendly" : "node_enemy_territory";
                case MapNodeKind.EnemyStronghold:
                    if (node.IsCaptured) return "node_friendly_stronghold";
                    return node.IsBalduinStronghold ? "node_balduin" : "node_enemy_stronghold";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 拠点アイコンをマス内に内接描画する。マス Rect は画像内円位置で計算済（全マス同サイズ）
        /// ＝拠点（city）と野営地（camp）の視覚階層差は絵側のデザインで表現（コード側でサイズ分岐しない）。
        /// 所属の変化は NodeIconKey で画像自体が切り替わるためここでは色加工しない。
        /// </summary>
        private static void TryDrawNodeIcon(Rect rect, MapNode node)
        {
            string key = NodeIconKey(node);
            if (key == null) return;
            BackgroundRegistry.TryDrawFit(rect, key);
        }

        /// <summary>
        /// マス下端中央に「味方-敵」人数バッジを描画する。枠色は BattleMode × 人数比で決まり、
        /// プレイヤーは数字と色だけで「ここに攻めてくる／攻め込み先／戦線外」と「劣勢か安全か」を
        /// 一目で判断できる。具体的な敵編成内容はホバーツールチップ（DrawNodeTooltip）に一任。
        /// 戦線外（BattleMode=None）の敵領／敵拠点は何も起こらないため非表示。自領・本拠地は
        /// 0-0 でも表示してレイアウト一定性と配置可視化を両立。
        /// </summary>
        private void DrawPersonnelBadge(Rect nodeRect, MapNode node)
        {
            if (node.BattleMode == MapNodeBattleMode.None
                && (node.Kind == MapNodeKind.EnemyTerritory || node.Kind == MapNodeKind.EnemyStronghold))
                return;

            int ally = node.AssignedAllies.Count;
            int enemy = node.EnemyComposition.Count;
            var frame = ResolveBadgeFrameColor(node, ally, enemy);

            const float w = 50f;
            const float h = 22f;
            var badgeRect = new Rect(
                nodeRect.x + (nodeRect.width - w) * 0.5f,
                nodeRect.yMax - h * 0.5f,
                w, h);

            FillRect(badgeRect, ColorBadgeBg);
            DrawBorder(badgeRect, 2, frame);
            GUI.Label(badgeRect, $"{ally}-{enemy}", _badgeStyle);
        }

        /// <summary>
        /// 人数バッジの枠色を BattleMode × 人数比から決定する。
        /// 防衛系（Defense / Boss）：未配置=赤・劣勢=黄・安全=水色（強調系）。
        /// 攻め込み（Attack）：未配置=灰・劣勢=橙・安全=青（中庸系で防衛と区別）。
        /// 戦線外（None）：常に灰（味方は置けるが警告しない）。
        /// 「安全」判定はユーザー指示通り頭数比較（ally ≧ enemy）で割り切る。
        /// </summary>
        private static Color ResolveBadgeFrameColor(MapNode node, int ally, int enemy)
        {
            switch (node.BattleMode)
            {
                case MapNodeBattleMode.Defense:
                case MapNodeBattleMode.Boss:
                    if (ally == 0) return ColorBadgeFrameDefenseDanger;
                    if (ally < enemy) return ColorBadgeFrameDefenseWarn;
                    return ColorBadgeFrameDefenseSafe;
                case MapNodeBattleMode.Attack:
                    if (ally == 0) return ColorBadgeFrameNeutral;
                    if (ally < enemy) return ColorBadgeFrameAttackWarn;
                    return ColorBadgeFrameAttackSafe;
                case MapNodeBattleMode.None:
                default:
                    return ColorBadgeFrameNeutral;
            }
        }

        private static string NodeLabel(MapNode node)
        {
            switch (node.Kind)
            {
                case MapNodeKind.Home:
                    return "★ 本拠地（王宮）";
                case MapNodeKind.Friendly:
                    return node.IsFallen ? "自領（陥落）" : "自領";
                case MapNodeKind.EnemyTerritory:
                    return node.IsCaptured ? "敵領（制圧）" : "敵領";
                case MapNodeKind.EnemyStronghold:
                    string col = node.Col == 0 ? "左" : node.Col == 1 ? "中" : "右";
                    return node.IsCaptured ? $"敵拠点 {col}（制圧）" : $"敵拠点 {col}";
                default:
                    return node.Kind.ToString();
            }
        }

        private void HandleMapNodeClick(MapNode node)
        {
            // マスクリックで配置統合モーダルを開く（ユニット選択と配置スロット選択を
            // モーダル内で完結させる方式）。
            if (!_bootstrap.RoundManager.CanAssign(_bootstrap.MapState, node, _bootstrap.CurrentRound))
            {
                _flashMessage = $"そのマスには配置できません（{NodeLabel(node)}）";
                return;
            }
            _placementModalNode = node;
            _placementModalSelectedUnit = null;
            _modalMessage = "ユニットとスロットを選択";
        }

        // 配置統合モーダル（ユニット一覧＋6スロット 2クリック動線）

        /// <summary>
        /// マスクリックで開く配置モーダル。左半分に未配置ユニット一覧、右半分に 6 スロット（前列3＋後列3）。
        /// 動線：(1) 左でユニットアイコン選択 → (2) 右の空きスロットクリックで配置確定。
        /// 配置済スロットクリックで解除。モーダル外 MouseDown / 閉じるボタンでキャンセル。
        /// 連続配置可能（配置後もモーダル維持・selectedUnit のみクリア）。
        /// </summary>
        private void DrawPlacementModal()
        {
            if (_placementModalNode == null) return;

            // 半透明オーバーレイ
            var fullscreen = new Rect(0, 0, Screen.width, Screen.height);
            FillRect(fullscreen, new Color(0f, 0f, 0f, 0.55f));

            // モーダル本体の矩形（横長：左=ユニット一覧／右=6スロット）
            float modalW = Mathf.Min(Screen.width * 0.65f, 920f);
            float modalH = Mathf.Min(Screen.height * 0.70f, 540f);
            var modal = new Rect(
                (Screen.width - modalW) * 0.5f,
                (Screen.height - modalH) * 0.5f,
                modalW, modalH);

            // モーダル外 MouseDown でキャンセル（既存スロットモーダルと同じホット争奪回避方式）
            if (Event.current.type == EventType.MouseDown
                && !modal.Contains(Event.current.mousePosition))
            {
                ClosePlacementModal("");
                Event.current.Use();
                return;
            }

            FillRect(modal, ColorPanelBg);
            DrawBorder(modal, 2, ColorSelectedBorder);

            // ヘッダ
            var headerRect = new Rect(modal.x + 20, modal.y + 14, modal.width - 140, 30);
            GUI.Label(headerRect, $"配置先：{NodeLabel(_placementModalNode)}", _titleStyle);

            // 閉じるボタン（右上）
            var closeBtnRect = new Rect(modal.xMax - 110, modal.y + 14, 90, 28);
            if (GUI.Button(closeBtnRect, "閉じる"))
            {
                ClosePlacementModal("");
                return;
            }

            // ヘッダ下のメッセージ行（モーダル内操作フィードバック専用）
            if (!string.IsNullOrEmpty(_modalMessage))
            {
                var msgRect = new Rect(modal.x + 20, modal.y + 50, modal.width - 40, 24);
                GUI.Label(msgRect, _modalMessage, _flashStyle);
            }

            // 本体：左=未配置ユニット一覧 ／ 右=6スロット
            const float padX = 20f;
            const float padY = 80f;
            const float gap = 18f;
            float bodyY = modal.y + padY;
            float bodyH = modal.height - padY - 18f;
            float halfW = (modal.width - padX * 2 - gap) * 0.5f;

            var unitsRect = new Rect(modal.x + padX, bodyY, halfW, bodyH);
            var slotsRect = new Rect(unitsRect.xMax + gap, bodyY, halfW, bodyH);

            DrawPlacementModalUnitList(unitsRect);
            DrawPlacementModalSlots(slotsRect, _placementModalNode);
        }

        private void DrawPlacementModalUnitList(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 20),
                "ユニット一覧（クリックで選択）", _mutedStyle);

            var scrollArea = new Rect(area.x, area.y + 22, area.width, area.height - 22);

            // 配置場所マップ作成（誰がどのマスに配置中か）
            var assignmentMap = new Dictionary<Unit, MapNode>();
            foreach (var n in _bootstrap.MapState.AllNodes())
                foreach (var a in n.AssignedAllies)
                    assignmentMap[a.BaseUnit] = n;

            // 「未配置」と「他マス配置済（移動可）」の 2 リストに分ける
            // 同マスに配置中のユニットは右側の 6 スロット枠で表示済のため除外
            var sortedAll = UnitRosterSorter.SortByPoolOrder(_bootstrap.Roster, _bootstrap.DraftPoolCatalog);
            var unassigned = sortedAll.Where(u => !assignmentMap.ContainsKey(u)).ToList();
            var assignedElsewhere = sortedAll
                .Where(u => assignmentMap.ContainsKey(u) && assignmentMap[u] != _placementModalNode)
                .ToList();

            GUILayout.BeginArea(scrollArea);
            _placementModalUnitScroll = GUILayout.BeginScrollView(_placementModalUnitScroll);

            // 未配置セクション
            GUILayout.Label("── 未配置 ──", _mutedStyle);
            if (unassigned.Count == 0)
                GUILayout.Label("未配置ユニットなし", _mutedStyle);
            else
                foreach (var u in unassigned)
                    DrawPlacementModalUnitRow(u, locationLabel: null);

            // 配置済セクション（クリックで旧マスから自動解除＋新マスに移動）
            GUILayout.Space(8);
            GUILayout.Label("── 他マスに配置済み（クリックで移動）──", _mutedStyle);
            if (assignedElsewhere.Count == 0)
                GUILayout.Label("他マスに配置済のユニットなし", _mutedStyle);
            else
                foreach (var u in assignedElsewhere)
                    DrawPlacementModalUnitRow(u, locationLabel: NodeLocationLabel(assignmentMap[u]));

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPlacementModalUnitRow(Unit unit, string locationLabel)
        {
            bool isSelected = _placementModalSelectedUnit == unit;
            const float rowH = 56f;
            const float iconSize = 44f;
            var rect = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
            FillRect(rect, isSelected ? ColorRosterSelected : ColorRosterBg);

            var iconRect = new Rect(rect.x + 6, rect.y + (rowH - iconSize) / 2f, iconSize, iconSize);
            IconRegistry.TryDrawIcon(iconRect, unit.Id);

            var nameRect = new Rect(rect.x + iconSize + 12, rect.y + 8, rect.width - iconSize - 16, 20);
            string nameLabel = locationLabel != null
                ? $"{unit.Name}  [{locationLabel}]"
                : unit.Name;
            GUI.Label(nameRect, nameLabel, _rosterNameStyle);

            // ステ行：Lv 表示（Lv2 以上は金色強調）＋ HP/ATK ＋ 属性（色付き）＋ 攻撃種別＋役割タグ。
            const float lvWidth = 44f;
            const float elemWidth = 18f;
            var lvRect   = new Rect(rect.x + iconSize + 12, rect.y + 30, lvWidth, 18);
            var statRect = new Rect(lvRect.xMax + 4,        rect.y + 30, 130, 18);
            var elemRect = new Rect(statRect.xMax,          rect.y + 30, elemWidth, 18);
            var tailRect = new Rect(elemRect.xMax + 2,      rect.y + 30, rect.width - iconSize - lvWidth - 130 - elemWidth - 28, 18);
            string roleTag = UnitDisplayLabels.RoleTagsLabel(unit.CombatRoles);
            string atkLabel = UnitDisplayLabels.AttackKindLabel(unit.AttackKind);
            string elemLabel = UnitDisplayLabels.ElementLabel(unit.UnitElement);
            GUI.Label(lvRect, $"Lv {unit.Level}",
                unit.Level >= 2 ? _rosterLvBoostStyle : _rosterBadgeStyle);
            GUI.Label(statRect, $"HP {unit.EffectiveMaxHP} ATK {unit.EffectiveATK}", _rosterBadgeStyle);
            _dynamicLabelStyle.normal.textColor = UnitDisplayLabels.ElementColor(unit.UnitElement);
            _dynamicLabelStyle.fontSize = 13;
            _dynamicLabelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(elemRect, elemLabel, _dynamicLabelStyle);
            _dynamicLabelStyle.fontStyle = FontStyle.Normal;
            _dynamicLabelStyle.fontSize = 14;
            GUI.Label(tailRect, $"{atkLabel} {roleTag}", _rosterBadgeStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                _placementModalSelectedUnit = isSelected ? null : unit;

            GUILayout.Space(4);
        }

        private void DrawPlacementModalSlots(Rect area, MapNode node)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 20),
                "配置スロット（空=配置／配置済=解除）", _mutedStyle);

            // 2 行 3 列：上=前列 0/1/2、下=後列 3/4/5
            const float rowGap = 14f;
            float gridY = area.y + 22f;
            float gridH = area.height - 22f;
            float rowH = (gridH - rowGap) * 0.5f;

            DrawPlacementModalSlotRow(
                new Rect(area.x, gridY, area.width, rowH),
                node, baseIndex: 0, rowLabel: "前列（近接攻撃ターゲット）");

            DrawPlacementModalSlotRow(
                new Rect(area.x, gridY + rowH + rowGap, area.width, rowH),
                node, baseIndex: 3, rowLabel: "後列（遠距離・後列攻撃のみ被弾）");
        }

        private void DrawPlacementModalSlotRow(Rect area, MapNode node, int baseIndex, string rowLabel)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 18), rowLabel, _mutedStyle);
            float cellY = area.y + 20f;
            float cellH = area.height - 20f;
            const float gap = 8f;
            float cellW = (area.width - gap * 2) / 3f;

            for (int offset = 0; offset < 3; offset++)
            {
                int slotIndex = baseIndex + offset;
                var cellRect = new Rect(area.x + offset * (cellW + gap), cellY, cellW, cellH);
                DrawPlacementModalSlotCell(cellRect, node, slotIndex);
            }
        }

        private void DrawPlacementModalSlotCell(Rect rect, MapNode node, int slotIndex)
        {
            var occupant = FindSlotOccupant(node, slotIndex);
            bool occupied = occupant != null;

            FillRect(rect, occupied ? ColorRosterAssigned : ColorRosterBg);
            DrawBorder(rect, 1, occupied ? ColorBalduinBorder : ColorAssignableBorder);

            // アイコン
            float iconSize = Mathf.Min(rect.width - 12f, rect.height * 0.55f);
            var iconRect = new Rect(
                rect.x + (rect.width - iconSize) * 0.5f,
                rect.y + 4,
                iconSize, iconSize);
            if (occupied) IconRegistry.TryDrawIcon(iconRect, occupant.BaseUnit.Id);

            // ラベル
            var labelRect = new Rect(rect.x, rect.yMax - 22, rect.width, 18);
            GUI.Label(labelRect,
                occupied ? occupant.BaseUnit.Name : $"空き #{slotIndex}",
                _nodeSubStyle);

            // クリック
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                HandlePlacementModalSlotClick(node, slotIndex, occupant);
        }

        private void HandlePlacementModalSlotClick(MapNode node, int slotIndex, RuntimeUnit occupant)
        {
            if (occupant != null)
            {
                // 配置済 → 解除（モーダル維持）
                node.UnassignAlly(occupant);
                _modalMessage = $"{occupant.BaseUnit.Name} を解除";
                return;
            }
            if (_placementModalSelectedUnit == null)
            {
                _modalMessage = "左のユニット一覧からユニットを選んでください";
                return;
            }

            // 他マスに配置済のユニットは旧マスから自動解除（移動扱い・確認なし）
            string movedFromLabel = null;
            foreach (var n in _bootstrap.MapState.AllNodes())
            {
                if (n == node) continue;
                var existing = n.AssignedAllies.FirstOrDefault(a => a.BaseUnit == _placementModalSelectedUnit);
                if (existing != null)
                {
                    movedFromLabel = NodeLocationLabel(n);
                    n.UnassignAlly(existing);
                    break;
                }
            }

            // 配置確定（連続配置のためモーダル維持・selectedUnit のみクリア）
            node.AssignAlly(new RuntimeUnit(_placementModalSelectedUnit, slotIndex));
            _modalMessage = movedFromLabel != null
                ? $"{_placementModalSelectedUnit.Name} を {movedFromLabel} から移動してスロット {slotIndex} に配置"
                : $"{_placementModalSelectedUnit.Name} をスロット {slotIndex} に配置";
            _placementModalSelectedUnit = null;
        }

        private void ClosePlacementModal(string flash)
        {
            _placementModalNode = null;
            _placementModalSelectedUnit = null;
            _modalMessage = "";
            if (!string.IsNullOrEmpty(flash)) _flashMessage = flash;
        }

        private static RuntimeUnit FindSlotOccupant(MapNode node, int slotIndex)
        {
            foreach (var a in node.AssignedAllies)
                if (a.SlotIndex == slotIndex) return a;
            return null;
        }

        // 拠点ホバーツールチップ（敵編成 + 味方配置・常時偵察＋配置可視化）

        /// <summary>
        /// マスホバー時に表示するツールチップ。マス種別に応じてセクションを自動切り替え：
        ///   - 敵編成あり（敵領／敵拠点）：「── 敵編成 ──」＋合計HP
        ///   - 味方配置あり or 自領／本拠地：「── 味方配置 ──」＋前列/後列件数
        /// 敵編成も味方配置もないマス（敵不在の自領等）は表示自体しない。
        /// </summary>
        private void DrawNodeTooltip(MapNode node)
        {
            int enemyCount = node.EnemyComposition.Count;
            int allyCount = node.AssignedAllies.Count;

            // 自領・本拠地は配置可視化のため味方配置 0 でも表示する（「未配置」を出す）
            bool showAllySection = allyCount > 0
                || node.Kind == MapNodeKind.Friendly
                || node.Kind == MapNodeKind.Home;
            bool showEnemySection = enemyCount > 0;

            if (!showEnemySection && !showAllySection) return;

            var enemyGroups = showEnemySection
                ? node.EnemyComposition.GroupBy(r => r.BaseUnit.Name).ToList()
                : null;
            int enemyTotalHp = showEnemySection ? node.EnemyComposition.Sum(r => r.MaxHP) : 0;

            var allyGroups = showAllySection && allyCount > 0
                ? node.AssignedAllies.GroupBy(r => r.BaseUnit.Name).ToList()
                : null;
            int frontCount = 0, backCount = 0;
            if (showAllySection)
            {
                foreach (var a in node.AssignedAllies)
                {
                    if (a.SlotIndex < 3) frontCount++; else backCount++;
                }
            }

            // 高さ計算：ヘッダ20 + 各セクション（見出し22 + 行22×N + サマリ22）+ セクション間ギャップ6 + 下余白8
            const float lineH = 22f;
            const float sectionGap = 8f;
            float h = 8f;
            if (showEnemySection)
            {
                h += lineH + lineH * enemyGroups.Count + lineH; // 見出し + 行 + 合計HP
            }
            if (showAllySection)
            {
                if (showEnemySection) h += sectionGap;
                h += lineH; // 見出し
                if (allyCount > 0) h += lineH * allyGroups.Count + lineH; // 行 + 前列/後列サマリ
                else h += lineH; // 「（未配置）」
            }
            h += 8f;

            var mouse = Event.current.mousePosition;
            const float w = 240f;
            var rect = new Rect(mouse.x + 16f, mouse.y + 16f, w, h);
            if (rect.xMax > Screen.width)  rect.x = mouse.x - w - 16f;
            if (rect.yMax > Screen.height) rect.y = Screen.height - h - 8f;

            FillRect(rect, new Color(0.10f, 0.10f, 0.14f, 0.95f));
            DrawBorder(rect, 1, ColorAssignableBorder);

            float yOff = rect.y + 8f;
            if (showEnemySection)
            {
                GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                    "── 敵編成 ──", _mutedStyle);
                yOff += lineH;
                foreach (var g in enemyGroups)
                {
                    GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                        $"{g.Key} ×{g.Count()}", _bodyStyle);
                    yOff += lineH;
                }
                GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                    $"合計HP {enemyTotalHp}", _mutedStyle);
                yOff += lineH;
            }
            if (showAllySection)
            {
                if (showEnemySection) yOff += sectionGap;
                GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                    "── 味方配置 ──", _mutedStyle);
                yOff += lineH;
                if (allyCount > 0)
                {
                    foreach (var g in allyGroups)
                    {
                        GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                            $"{g.Key} ×{g.Count()}", _bodyStyle);
                        yOff += lineH;
                    }
                    GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                        $"前列 {frontCount} / 後列 {backCount}", _mutedStyle);
                }
                else
                {
                    GUI.Label(new Rect(rect.x + 8, yOff, rect.width - 16, 20),
                        "（未配置）", _mutedStyle);
                }
            }
        }

        // 汎用確認ダイアログ（行動力残し内政終了・ラン放棄等から共用）

        private void ShowConfirm(string title, string message, string confirmLabel, Action onConfirm)
        {
            _confirmDialog = new ConfirmDialog
            {
                Title = title,
                Message = message,
                ConfirmLabel = confirmLabel,
                OnConfirm = onConfirm,
            };
        }

        private void DrawConfirmDialog()
        {
            if (!_confirmDialog.HasValue) return;
            var dlg = _confirmDialog.Value;

            // 半透明オーバーレイ
            var fullscreen = new Rect(0, 0, Screen.width, Screen.height);
            FillRect(fullscreen, new Color(0f, 0f, 0f, 0.55f));

            // モーダル本体
            float modalW = Mathf.Min(Screen.width * 0.40f, 520f);
            float modalH = 220f;
            var modal = new Rect(
                (Screen.width - modalW) * 0.5f,
                (Screen.height - modalH) * 0.5f,
                modalW, modalH);

            // モーダル外 MouseDown でキャンセル（既存 PlacementModal と同パターン）
            if (Event.current.type == EventType.MouseDown
                && !modal.Contains(Event.current.mousePosition))
            {
                _confirmDialog = null;
                Event.current.Use();
                return;
            }

            FillRect(modal, ColorPanelBg);
            DrawBorder(modal, 2, ColorSelectedBorder);

            // タイトル
            var titleRect = new Rect(modal.x + 20, modal.y + 16, modal.width - 40, 30);
            GUI.Label(titleRect, dlg.Title, _titleStyle);

            // メッセージ（wordWrap で複数行対応）
            _dynamicLabelStyle.wordWrap = true;
            _dynamicLabelStyle.normal.textColor = ColorText;
            var msgRect = new Rect(modal.x + 20, modal.y + 56, modal.width - 40, 80);
            GUI.Label(msgRect, dlg.Message, _dynamicLabelStyle);
            _dynamicLabelStyle.wordWrap = false;

            // ボタン：[キャンセル]　[実行]
            const float btnW = 160f;
            const float btnH = 40f;
            const float gap = 16f;
            float btnY = modal.yMax - btnH - 16f;
            var cancelRect  = new Rect(modal.x + (modal.width - btnW * 2 - gap) * 0.5f, btnY, btnW, btnH);
            var confirmRect = new Rect(cancelRect.xMax + gap, btnY, btnW, btnH);

            if (GUI.Button(cancelRect, "キャンセル"))
            {
                _confirmDialog = null;
                return;
            }
            if (GUI.Button(confirmRect, dlg.ConfirmLabel))
            {
                var cb = dlg.OnConfirm;
                _confirmDialog = null; // 先に閉じる（コールバック中に再度 Show される可能性に備えて順序固定）
                cb?.Invoke();
            }
        }

        // 王国軍リスト行ホバーツールチップ（ユニット個別の説明＋フルステ）

        /// <summary>
        /// 王国軍リスト行ホバー時に表示するツールチップ。マス用 DrawNodeTooltip と同じく
        /// マウス追従＋画面端で自動反転で配置する。表示内容は属性（色付き 1 文字）／攻撃種別＋
        /// 役割タグ／フルステ（HP/ATK/DEF/SPD）／Description（wordWrap）。
        /// </summary>
        private void DrawRosterUnitTooltip(Unit unit)
        {
            string elemLabel = UnitDisplayLabels.ElementLabel(unit.UnitElement);
            string atkLabel = UnitDisplayLabels.AttackKindLabel(unit.AttackKind);
            string roleTag = UnitDisplayLabels.RoleTagsLabel(unit.CombatRoles);
            string desc = unit.Description ?? "";

            const float w = 320f;
            const float lineH = 22f;
            // 高さ：見出し + メタ行 + ステ行 + 区切り + 説明行（行数概算 desc 長 / 28 文字、最小 1 行）
            int descLines = string.IsNullOrEmpty(desc) ? 0 : Mathf.Max(1, Mathf.CeilToInt(desc.Length / 28f));
            float h = 12f + lineH /*name*/ + lineH /*meta*/ + lineH /*stats*/
                    + (descLines > 0 ? 8f + lineH * descLines : 0f) + 10f;

            var mouse = Event.current.mousePosition;
            var rect = new Rect(mouse.x + 16f, mouse.y + 16f, w, h);
            if (rect.xMax > Screen.width)  rect.x = mouse.x - w - 16f;
            if (rect.yMax > Screen.height) rect.y = Screen.height - h - 8f;

            FillRect(rect, new Color(0.10f, 0.10f, 0.14f, 0.95f));
            DrawBorder(rect, 1, ColorAssignableBorder);

            float yOff = rect.y + 8f;
            GUI.Label(new Rect(rect.x + 10, yOff, rect.width - 20, 20),
                $"── {unit.Name} ──", _bodyStyle);
            yOff += lineH;

            // メタ行：属性（色付き 1 文字） / 近接 [盾/攻]
            var elemRect = new Rect(rect.x + 10, yOff, 16, 20);
            _dynamicLabelStyle.normal.textColor = UnitDisplayLabels.ElementColor(unit.UnitElement);
            _dynamicLabelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(elemRect, elemLabel, _dynamicLabelStyle);
            _dynamicLabelStyle.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(elemRect.xMax + 4, yOff, rect.width - 40, 20),
                $"/ {atkLabel} {roleTag}", _mutedStyle);
            yOff += lineH;

            GUI.Label(new Rect(rect.x + 10, yOff, rect.width - 20, 20),
                $"HP {unit.EffectiveMaxHP}  ATK {unit.EffectiveATK}  DEF {unit.EffectiveDEF}  SPD {unit.BaseSPD}",
                _bodyStyle);
            yOff += lineH;

            if (descLines > 0)
            {
                yOff += 8f;
                GUI.Label(new Rect(rect.x + 10, yOff, rect.width - 20, lineH * descLines),
                    desc, _flashStyle);
            }
        }

        // 右カラム：ヘッダー＋戦闘結果サマリ＋ガイド＋進行ボタン

        private void DrawInfoPanel(Rect area)
        {
            FillRect(area, ColorPanelBg);
            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 12, area.width - 24, area.height - 24));

            // ヘッダー
            int round = _bootstrap.CurrentRound;
            string roundLabel = round == VSPrototypeRoundManager.BossRound
                ? $"R{round}/7（本拠地ボス戦）"
                : $"R{round}/7";
            GUILayout.Label(roundLabel, _titleStyle);

            GUILayout.Space(20);

            // 戦闘結果サマリ（解決後に表示）
            if (!string.IsNullOrEmpty(_resultSummary))
            {
                GUILayout.Label("── 結果 ──", _mutedStyle);
                GUILayout.Label(_resultSummary, _flashStyle);
                GUILayout.Space(8);
            }

            // Flash message
            GUILayout.Label("── ガイド ──", _mutedStyle);
            GUILayout.Label(_flashMessage, _flashStyle);

            GUILayout.Space(10);
            DrawFrontlineStatus();

            GUILayout.Space(10);
            DrawSynergyTips(area.width - 24);

            GUILayout.FlexibleSpace();

            // 進行ボタン：Phase 別に分岐。
            //   InteriorAction：行動力＋召集／強化／内政終了
            //   Run           ：戦闘実行→ / 次ラウンドへ→ / メタ拠点へ→
            if (_bootstrap.CurrentPhase == VSPrototypePhase.InteriorAction)
                DrawInteriorActionButtons();
            else
                DrawProgressButtons();

            GUILayout.Space(8);
            // ラン放棄：誤クリック防止のため確認ダイアログを挟む
            if (GUILayout.Button("もとの世界へ戻る（ラン放棄）", GUILayout.Height(28)))
            {
                ShowConfirm(
                    title: "ラン放棄",
                    message: "ラン放棄するともとの世界（メタ拠点）に戻ります。本ランの進行は失われます。よろしいですか？",
                    confirmLabel: "ラン放棄する",
                    onConfirm: () =>
                    {
                        _bootstrap.AbandonRunAndReturnToHub();
                        ClosePlacementModal("");
                        _resultSummary = "";
                    });
            }

            GUILayout.Space(4);
            var noteStyle = new GUIStyle(_mutedStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("Vertical Sliceプロト", noteStyle);

            GUILayout.EndArea();
        }

        /// <summary>
        /// Phase=InteriorAction 中の右パネル進行 UI。
        /// 行動力表示＋「召集」「ユニット強化」「内政終了 → 配置・戦闘へ」の3ボタンを描画。
        /// 召集/強化は Bootstrap のサブモード API を呼び、InteriorGUI 側のサブモーダル描画に委譲する。
        /// </summary>
        private void DrawInteriorActionButtons()
        {
            var state = _bootstrap.InteriorState;
            int ap = state.ActionPoints;
            int apMax = state.ActionPointsPerRound;

            GUILayout.Label("── 内政コマンド ──", _mutedStyle);
            GUILayout.Label($"行動力 {ap}/{apMax}", _bodyStyle);
            GUILayout.Space(6);

            bool canConscript = state.CanExecuteAction(VSPrototypeInteriorAction.Conscript);

            GUI.enabled = canConscript;
            if (GUILayout.Button("召集（行動力 1／ラウンド中 1 回のみ）", GUILayout.Height(36)))
            {
                _bootstrap.BeginConscript();
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            // ユニット強化は同一ラウンド複数回可。一覧画面までは行動力なしでも進めるため常時 enabled。
            if (GUILayout.Button("ユニット強化（行動力 1）", GUILayout.Height(36)))
            {
                _bootstrap.BeginUpgradeSubMode();
            }

            // 不可理由の案内（控えめに）
            if (!canConscript && state.HasExecutedThisRound(VSPrototypeInteriorAction.Conscript))
                GUILayout.Label("※ 召集は同一ラウンド1回まで", _mutedStyle);

            GUILayout.Space(10);
            if (GUILayout.Button("内政終了 → 配置・戦闘へ", GUILayout.Height(48)))
            {
                // 行動力を残したまま終了しようとした場合は確認ダイアログ。0 なら即時遷移。
                if (ap > 0)
                {
                    ShowConfirm(
                        title: "内政終了",
                        message: $"行動力が {ap} 残っています。このラウンドでは使えなくなりますが、内政を終了しますか？",
                        confirmLabel: "終了する",
                        onConfirm: () =>
                        {
                            _bootstrap.FinishInteriorPhase();
                            _flashMessage = $"R{_bootstrap.CurrentRound} 配置フェーズ。マスをクリックして配置";
                        });
                }
                else
                {
                    _bootstrap.FinishInteriorPhase();
                    _flashMessage = $"R{_bootstrap.CurrentRound} 配置フェーズ。マスをクリックして配置";
                }
            }
        }

        /// <summary>
        /// ラン進行ボタンの描画。状態に応じて：
        ///   - 配置中（LastRoundResult==null）→ 「戦闘実行 →」
        ///   - 解決済＆エンディング未確定 → 「次のラウンドへ →」
        ///       内部で Bootstrap.AdvanceAfterRound() を呼ぶ：
        ///         R5 終了は BridgetEvent 演出を挟んでから R6 へ進む。それ以外は即次ラウンドへ。
        ///   - エンディング確定 → 「メタ拠点へ →」
        ///       Bootstrap.EnterEndingEvent() で Defeat/True 演出を挟んでから FinishCurrentRun。
        /// 演出 Phase 中は本 GUI 自体が描画されないので、ボタン押下→演出突入→Run 復帰時に再描画される。
        /// </summary>
        private void DrawProgressButtons()
        {
            var last = _bootstrap.LastRoundResult;
            if (last == null)
            {
                if (GUILayout.Button("戦闘実行 →", GUILayout.Height(48)))
                {
                    ClosePlacementModal("");
                    var result = _bootstrap.ResolveAndBeginBattleReplay();
                    // 戦闘がある場合は Phase=Battle へ遷移し BattleGUI が描画する。
                    // 戦闘ゼロ（敵不在ラウンド等の縮退ケース）のみここで結果サマリを即表示する。
                    if (_bootstrap.CurrentPhase == VSPrototypePhase.Run)
                    {
                        _resultSummary = BuildResultSummary(result);
                        _flashMessage = result.EndingKind != VSPrototypeEndingKind.None
                            ? EndingFlash(result.EndingKind)
                            : "戦闘終了。次のラウンドへ進んでください";
                    }
                    else
                    {
                        _resultSummary = "";
                    }
                }
                return;
            }

            if (last.EndingKind == VSPrototypeEndingKind.None)
            {
                if (GUILayout.Button("次のラウンドへ →", GUILayout.Height(48)))
                {
                    if (_bootstrap.AdvanceAfterRound())
                    {
                        // ラウンド開始通知は内政フェーズ突入時の自動加入フラッシュ（行 133）が兼ねる。
                        _resultSummary = "";
                    }
                }
                return;
            }

            // エンディング確定
            string label = $"メタ拠点へ →（{EndingLabel(last.EndingKind)}）";
            if (GUILayout.Button(label, GUILayout.Height(48)))
            {
                _bootstrap.EnterEndingEvent();
                ClosePlacementModal("");
                _resultSummary = "";
            }
        }

        // 戦線状況：戦線が立っているマス（BattleMode != None）だけを
        // 本拠地→自領→敵領→敵拠点 の順に列挙し、配置充足を文字＋枠色と同配色で示す。
        // バッジ枠色判定は ResolveBadgeFrameColor を流用＝マップ上のバッジ色と完全一致。
        private void DrawFrontlineStatus()
        {
            GUILayout.Label("── 戦線状況 ──", _mutedStyle);

            var state = _bootstrap.MapState;
            bool anyFrontline = false;

            if (state.Home.BattleMode != MapNodeBattleMode.None)
            {
                DrawFrontlineRow(state.Home, "本拠地");
                anyFrontline = true;
            }
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerFriendly);
                if (node.BattleMode != MapNodeBattleMode.None)
                {
                    DrawFrontlineRow(node, $"自領 {ColLabel(col)}");
                    anyFrontline = true;
                }
            }
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerEnemyTerritory);
                if (node.BattleMode != MapNodeBattleMode.None)
                {
                    DrawFrontlineRow(node, $"敵領 {ColLabel(col)}");
                    anyFrontline = true;
                }
            }
            for (int col = 0; col < 3; col++)
            {
                var node = state.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);
                if (node.BattleMode != MapNodeBattleMode.None)
                {
                    DrawFrontlineRow(node, $"敵拠点 {ColLabel(col)}");
                    anyFrontline = true;
                }
            }

            if (!anyFrontline)
                GUILayout.Label("戦線なし", _mutedStyle);
        }

        private void DrawFrontlineRow(MapNode node, string label)
        {
            int ally = node.AssignedAllies.Count;
            int enemy = node.EnemyComposition.Count;

            string symbol = ally == 0 ? "[!]" : (ally < enemy ? "[△]" : "[○]");
            string mode = node.BattleMode == MapNodeBattleMode.Defense ? "防衛"
                        : node.BattleMode == MapNodeBattleMode.Boss ? "ボス"
                        : "攻撃";

            _dynamicLabelStyle.normal.textColor = ResolveBadgeFrameColor(node, ally, enemy);
            GUILayout.Label($"{symbol} {label} {mode}  {ally}/{enemy}", _dynamicLabelStyle);
        }

        // 属性シナジー Tips：3 属性の特性を 1 行ずつ。
        // 数値は出さず、「2 体から発動」「4 体節目で追加効果（水のシールド）」「人数で強化」が
        // 伝わる粒度に抑える。数値暗記は試遊者の負荷になるため意図的に省略。
        // contentWidth は折り返し計算用の実描画幅（CalcHeight で必要高さを明示確保するため）。
        private void DrawSynergyTips(float contentWidth)
        {
            GUILayout.Label("── 属性シナジー（同属性 2 体から発動・人数で強化）──", _mutedStyle);

            // 戦線状況行と共用スタイルなので末尾で false に戻す。
            _dynamicLabelStyle.wordWrap = true;

            DrawSynergyLine("火：最も ATK の高いユニット1体の与ダメージを割合で上昇（火4↑で対象が2体へ増加）", Element.Fire,  contentWidth);
            DrawSynergyLine("水：味方全員の DEF 強化（水4-6で攻撃を1-3回無効にするシールド付与）",          Element.Water, contentWidth);
            DrawSynergyLine("光：ターンの最後に味方全員の HP を継続回復",                                    Element.Light, contentWidth);

            _dynamicLabelStyle.wordWrap = false;
        }

        private void DrawSynergyLine(string text, Element element, float contentWidth)
        {
            _dynamicLabelStyle.normal.textColor = UnitDisplayLabels.ElementColor(element);
            float h = _dynamicLabelStyle.CalcHeight(new GUIContent(text), contentWidth);
            GUILayout.Label(text, _dynamicLabelStyle, GUILayout.Height(h));
        }

        private static string BuildResultSummary(VSPrototypeRoundResult result)
        {
            if (result == null) return "";
            int captured = 0, fallen = 0;
            foreach (var n in result.NodeResults)
            {
                if (n.MarkedCaptured) captured++;
                if (n.MarkedFallen) fallen++;
            }
            int homeBattles = result.HomeBattleReports.Count;
            string s = $"制圧 +{captured} / 陥落 +{fallen}";
            if (homeBattles > 0)
                s += $"\n本拠地戦 {homeBattles} 回{(result.HomeCollapsed ? "（敗北）" : "（防衛成功）")}";
            if (result.Round == VSPrototypeRoundManager.BossRound)
                s += $"\nR7 ボス：{(result.BossDefeated ? "勝利" : "敗北")}";
            return s;
        }

        private static string EndingFlash(VSPrototypeEndingKind kind)
        {
            switch (kind)
            {
                case VSPrototypeEndingKind.Defeat: return "ラン敗北。メタ拠点へ戻ってください";
                case VSPrototypeEndingKind.True:   return "★ トゥルーエンド到達 ★";
                default: return "";
            }
        }

        private static string EndingLabel(VSPrototypeEndingKind kind)
        {
            switch (kind)
            {
                case VSPrototypeEndingKind.Defeat: return "敗北エンド";
                case VSPrototypeEndingKind.True:   return "トゥルーエンド";
                default: return "";
            }
        }

        // 描画ユーティリティ

        private static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, thickness), color);                          // 上
            FillRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);           // 下
            FillRect(new Rect(rect.x, rect.y, thickness, rect.height), color);                         // 左
            FillRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);          // 右
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;

            // 日本語フォント適用（戦闘プロトと同様、GuiTheme 経由）
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
            };
            _mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _nodeSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 0.85f) },
                alignment = TextAnchor.LowerCenter,
            };
            _rosterNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _rosterBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _rosterLvBoostStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorBalduinBorder }, // 金色＝強化済みアクセント
                alignment = TextAnchor.MiddleLeft,
            };
            _flashStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = ColorText },
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter,
            };
            _dynamicLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
            };

            _stylesBuilt = true;
        }
    }
}
