// メタ拠点 UI（亡国の遺跡）。
//
// 【画面構成（PC 16:9 中央寄せ）】
// - 上部：タイトル＋メタ通貨残高／周回数／トゥルーエンド達成フラグ
// - 中央：永続強化リスト（3項目：王女 ATK+3 / 行動力+1 / 初期ユニット+1）
// - 下部：解禁ユニット表示＋「次のランへ →」ボタン
//
// 【描画ガード】
// - `Bootstrap.CurrentPhase == Hub` の時のみ描画。
// - VSPrototypeMapGUI（Phase=Run）と同じ GameObject に並べてアタッチする。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Echolos.Domain.Meta;
using Echolos.Domain.Models;
using Echolos.UseCase.VSPrototype;
using Echolos.Presentation;  // GuiTheme を参照

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.VSPrototype
{
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class MetaHubGUI : MonoBehaviour
    {
        // 色定義

        private static readonly Color ColorBg          = new Color(0.08f, 0.08f, 0.12f);
        // 背景画像（meta_hub_background）が見えるよう中央パネルは半透明（仮 α=0.88・うっすら見える程度）。
        // 画像未配置時のフォールバックでも下地が ColorBg なのでほぼ気にならない。
        private static readonly Color ColorPanelBg     = new Color(0.14f, 0.15f, 0.20f, 0.88f);
        private static readonly Color ColorItemBg      = new Color(0.20f, 0.22f, 0.28f);
        private static readonly Color ColorText        = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ColorTextMuted   = new Color(0.65f, 0.65f, 0.72f);
        private static readonly Color ColorAccent      = new Color(1.00f, 0.85f, 0.20f); // 金（メタ通貨）
        private static readonly Color ColorTrueEnd     = new Color(0.95f, 0.55f, 0.85f); // 桜・トゥルーエンド達成

        private static readonly Color ColorCardBg     = new Color(0.20f, 0.22f, 0.28f);
        private static readonly Color ColorCardBorder = new Color(0.55f, 0.95f, 1.00f);

        // 内部状態

        private VSPrototypeBootstrap _bootstrap;
        private string _flashMessage = "";

        // 固有ユニット Lv 強化の 3 択選択モーダル状態
        //   _pendingChoiceUpgrade : 購入対象（princess_level / bridget_level）
        //   _pendingChoiceUnitId  : 対象 Unit ID（princess / bridget）
        // 両方非 null＝モーダル表示中、null＝メイン画面。
        private MetaUpgrade _pendingChoiceUpgrade;
        private string _pendingChoiceUnitId;

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _accentStyle;
        private GUIStyle _trueEndStyle;
        private GUIStyle _itemNameStyle;
        private GUIStyle _itemEffectStyle;
        private GUIStyle _flashStyle;
        private GUIStyle _primaryButtonStyle;
        private bool _stylesBuilt;

        // ライフサイクル

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void OnGUI()
        {
            // メタ拠点（Phase=Hub）の時だけ描画。Run 中は MapGUI が描画する。
            if (_bootstrap.CurrentPhase != VSPrototypePhase.Hub) return;

            BuildStylesIfNeeded();

            // 背景：meta_hub_background があれば ScaleAndCrop で全画面、なければ単色塗り。
            var screenRect = new Rect(0, 0, Screen.width, Screen.height);
            if (!BackgroundRegistry.TryDrawCover(screenRect, "meta_hub_background"))
                FillRect(screenRect, ColorBg);

            // 中央寄せの主パネル（横画面でも幅 60% 程度に絞る）
            float panelW = Mathf.Min(Screen.width * 0.6f, 800f);
            float panelH = Mathf.Min(Screen.height * 0.85f, 720f);
            var panel = new Rect(
                (Screen.width  - panelW) * 0.5f,
                (Screen.height - panelH) * 0.5f,
                panelW, panelH);

            FillRect(panel, ColorPanelBg);

            // 内部パディング 24px
            GUILayout.BeginArea(new Rect(panel.x + 24, panel.y + 24, panel.width - 48, panel.height - 48));

            DrawHeader();
            GUILayout.Space(12);
            DrawLastRunSection();
            GUILayout.Space(12);
            DrawUpgradeList();
            GUILayout.FlexibleSpace();
            DrawNextRunButton();

            GUILayout.EndArea();

            // 3 択選択モーダルは中身を最後に重ねる（メイン UI の上に被せる）。
            if (_pendingChoiceUpgrade != null)
                DrawUpgradeChoiceModal();
        }

        // ヘッダー：タイトル＋通貨残高

        private void DrawHeader()
        {
            GUILayout.Label("亡国の遺跡", _titleStyle);
            GUILayout.Space(4);

            var meta = _bootstrap.Meta;

            GUILayout.BeginHorizontal();
            GUILayout.Label("王国の記憶 ", _bodyStyle, GUILayout.Width(96));
            GUILayout.Label(meta.Memories.ToString(), _accentStyle, GUILayout.Width(80));
            GUILayout.Space(16);
            GUILayout.Label($"周回 {meta.RunCount}", _bodyStyle, GUILayout.Width(80));
            GUILayout.Space(16);
            GUILayout.Label(
                meta.HasReachedTrueEnd ? "★ トゥルーエンド達成" : "トゥルーエンド 未達成",
                meta.HasReachedTrueEnd ? _trueEndStyle : _mutedStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // 直近ラン結果（演出後の Hub 戻り時に表示）

        private void DrawLastRunSection()
        {
            // 初回起動直後 or 演出を経ていない場合は非表示
            if (_bootstrap.LastEnding == VSPrototypeEndingKind.None) return;

            var endingLabel = EndingDisplayLabel(_bootstrap.LastEnding);
            var endingStyle = _bootstrap.LastEnding == VSPrototypeEndingKind.True
                ? _trueEndStyle : _bodyStyle;

            GUILayout.Label("── 直近のラン ──", _subtitleStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("結果", _mutedStyle, GUILayout.Width(64));
            GUILayout.Label(endingLabel, endingStyle, GUILayout.Width(160));
            GUILayout.Space(16);
            GUILayout.Label("獲得", _mutedStyle, GUILayout.Width(64));
            GUILayout.Label($"+{_bootstrap.LastRunReward}", _accentStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static string EndingDisplayLabel(VSPrototypeEndingKind kind)
        {
            switch (kind)
            {
                case VSPrototypeEndingKind.Defeat: return "敗北エンド";
                case VSPrototypeEndingKind.True:   return "★ トゥルーエンド";
                default: return "";
            }
        }

        // 永続強化リスト

        // メタ強化の表示順（固定）。Resources.LoadAll の順序保証は無いので GUI 側で明示制御。
        // 汎用強化（行動力／初期ユニット）→ 個別ユニット Lv 強化（王女／ブリジット）の順。
        // ブリジットは救援後のみ意味があるため最下段。
        private static readonly string[] UpgradeDisplayOrder = new[]
        {
            MetaUpgradeIds.ActionPoints,
            MetaUpgradeIds.InitialUnit,
            MetaUpgradeIds.PrincessLevel,
            MetaUpgradeIds.BridgetLevel,
        };

        private void DrawUpgradeList()
        {
            GUILayout.Label("── 永続強化 ──", _subtitleStyle);
            GUILayout.Space(8);

            // ブリジット未解禁時は Lv 強化行を「ブリジット : 未解禁」表示に差し替える
            //（Cap 解禁前にコスト・購入ボタンを並べると誤解を招くため）。
            bool bridgetUnlocked = _bootstrap.Meta.IsUnitUnlocked(MetaUnitIds.Bridget);
            foreach (var id in UpgradeDisplayOrder)
            {
                if (id == MetaUpgradeIds.BridgetLevel && !bridgetUnlocked)
                {
                    DrawBridgetLockedRow();
                    continue;
                }
                DrawUpgradeRow(_bootstrap.MetaUpgradeCatalog.Get(id));
            }
        }

        // ブリジット未解禁の差替行：表示は「ブリジット : 未解禁」のみ。
        // 解禁条件（バルドゥイン拠点を R5 までに救援）はゲーム内チュートリアル／ゲーム外ガイドで
        // 説明する想定なので、ここのメニュー本文には書かない。
        private void DrawBridgetLockedRow()
        {
            var rowRect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            FillRect(rowRect, ColorItemBg);
            var labelRect = new Rect(rowRect.x + 12, rowRect.y + 6, rowRect.width - 24, 20);
            GUI.Label(labelRect, "ブリジット : 未解禁", _mutedStyle);
            GUILayout.Space(6);
        }

        private void DrawUpgradeRow(MetaUpgrade upgrade)
        {
            var meta = _bootstrap.Meta;
            int curLv = meta.GetUpgradeLevel(upgrade.Id);
            bool atCap = curLv >= upgrade.Cap;
            int nextCost = upgrade.GetCostForNextLevel(curLv);
            bool canAfford = meta.Memories >= nextCost;
            bool purchasable = !atCap && canAfford;

            // 行背景
            var rowRect = GUILayoutUtility.GetRect(0, 64, GUILayout.ExpandWidth(true));
            FillRect(rowRect, ColorItemBg);

            // 左：名前＋効果
            float padX = 12f;
            float btnW = 110f;
            float costW = 80f;
            float lvW = 80f;
            var nameRect = new Rect(rowRect.x + padX, rowRect.y + 8,
                rowRect.width - btnW - costW - lvW - padX * 2 - 24, 20);
            GUI.Label(nameRect, upgrade.DisplayName, _itemNameStyle);
            var effectRect = new Rect(nameRect.x, rowRect.y + 32, nameRect.width, 18);
            GUI.Label(effectRect, upgrade.EffectText, _itemEffectStyle);

            // 右：コスト・Lv・購入ボタン
            float rightX = rowRect.xMax - padX - btnW;
            var lvRect = new Rect(rightX - costW - lvW - 16, rowRect.y + 22, lvW, 20);
            GUI.Label(lvRect, $"Lv {curLv}/{upgrade.Cap}", _bodyStyle);

            var costRect = new Rect(rightX - costW - 8, rowRect.y + 22, costW, 20);
            GUI.Label(costRect, atCap ? "コスト -" : $"コスト {nextCost}",
                (atCap || canAfford) ? _bodyStyle : _mutedStyle);

            var btnRect = new Rect(rightX, rowRect.y + 14, btnW, 36);
            string btnLabel = atCap ? "上限到達"
                : !canAfford ? "残高不足"
                : "購入";
            GUI.enabled = purchasable;
            if (GUI.Button(btnRect, btnLabel))
                TryPurchase(upgrade);
            GUI.enabled = true;

            GUILayout.Space(6);
        }

        private void TryPurchase(MetaUpgrade upgrade)
        {
            // 王女・ブリジット Lv 強化は購入前に 3 択モーダルを出す（残高消費は採用時）。
            string unitId = ResolveLevelUpgradeUnitId(upgrade.Id);
            if (unitId != null)
            {
                _pendingChoiceUpgrade = upgrade;
                _pendingChoiceUnitId = unitId;
                _flashMessage = "";
                return;
            }

            // 行動力 / 初期所持ユニット枠 等は即購入。次段階コストは現在 Lv から決まる。
            var meta = _bootstrap.Meta;
            int cost = upgrade.GetCostForNextLevel(meta.GetUpgradeLevel(upgrade.Id));
            if (!meta.SpendMemories(cost))
            {
                _flashMessage = $"残高不足：{upgrade.DisplayName} を購入できません";
                return;
            }
            bool applied = meta.ApplyUpgrade(upgrade.Id, upgrade.Cap);
            if (!applied)
            {
                meta.EarnMemories(cost);
                _flashMessage = $"{upgrade.DisplayName} は上限に達しています";
                return;
            }
            _flashMessage = $"{upgrade.DisplayName} を購入（Lv {meta.GetUpgradeLevel(upgrade.Id)}）";
        }

        // 固有ユニット Lv 強化 ID → 対象 Unit ID。それ以外は null。
        private static string ResolveLevelUpgradeUnitId(string upgradeId)
        {
            if (upgradeId == MetaUpgradeIds.PrincessLevel) return UniqueUnitIds.Princess;
            if (upgradeId == MetaUpgradeIds.BridgetLevel)  return UniqueUnitIds.Bridget;
            return null;
        }

        // 3 択選択モーダル

        private void DrawUpgradeChoiceModal()
        {
            // 全画面オーバーレイ（背景を暗くする）
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.7f));

            float modalW = Mathf.Min(Screen.width * 0.78f, 1100f);
            float modalH = Mathf.Min(Screen.height * 0.70f, 540f);
            var modal = new Rect(
                (Screen.width  - modalW) * 0.5f,
                (Screen.height - modalH) * 0.5f,
                modalW, modalH);

            // モーダル外 MouseDown でキャンセル（InteriorGUI 既存の Upgrade サブモードと同じ仕様）。
            if (Event.current.type == EventType.MouseDown
                && !modal.Contains(Event.current.mousePosition))
            {
                CancelChoiceModal();
                Event.current.Use();
                return;
            }

            FillRect(modal, ColorPanelBg);

            GUILayout.BeginArea(new Rect(modal.x + 24, modal.y + 20, modal.width - 48, modal.height - 40));

            var unit = _bootstrap.UnitCatalog.Get(_pendingChoiceUnitId);
            var chosenIds = _bootstrap.Meta.GetUpgradeChoices(_pendingChoiceUnitId);
            var remaining = unit.AvailableUpgrades
                .Where(u => !chosenIds.Contains(u.UpgradeId))
                .ToList();

            GUILayout.Label($"── {unit.Name} の Lv 強化選択 ──", _subtitleStyle);
            int currentLv = 1 + chosenIds.Count;
            int curUpgradeLv = _bootstrap.Meta.GetUpgradeLevel(_pendingChoiceUpgrade.Id);
            int modalCost = _pendingChoiceUpgrade.GetCostForNextLevel(curUpgradeLv);
            GUILayout.Label(
                $"次回ラン開始時 Lv {currentLv} → Lv {currentLv + 1}　残り {remaining.Count} 択　コスト {modalCost}",
                _mutedStyle);
            GUILayout.Space(8);

            const float cardW = 280f;
            const float cardH = 280f;
            const float gap = 24f;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var up in remaining)
            {
                var rect = GUILayoutUtility.GetRect(cardW, cardH,
                    GUILayout.Width(cardW), GUILayout.Height(cardH));
                DrawUpgradeChoiceCard(rect, up);
                GUILayout.Space(gap);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("← キャンセル", GUILayout.Width(200), GUILayout.Height(34)))
                CancelChoiceModal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawUpgradeChoiceCard(Rect rect, UnitUpgrade upgrade)
        {
            // ConfirmChoice → CancelChoiceModal で _pendingChoiceUpgrade=null になった後も
            // 同フレームのループで残カードが描画される。早期 return で NRE を防ぐ。
            if (_pendingChoiceUpgrade == null) return;

            FillRect(rect, ColorCardBg);
            DrawBorder(rect, 2, ColorCardBorder);

            var nameRect = new Rect(rect.x + 8, rect.y + 18, rect.width - 16, 30);
            GUI.Label(nameRect, upgrade.Name, _itemNameStyle);

            var descRect = new Rect(rect.x + 16, rect.y + 64, rect.width - 32, 100);
            GUI.Label(descRect, upgrade.Description, _bodyStyle);

            // 残高チェック（モーダル中も購入時に再判定）。次段階コストは現在 Lv から決まる。
            int curUpgradeLv = _bootstrap.Meta.GetUpgradeLevel(_pendingChoiceUpgrade.Id);
            int cardCost = _pendingChoiceUpgrade.GetCostForNextLevel(curUpgradeLv);
            bool canAfford = _bootstrap.Meta.Memories >= cardCost;
            var btnRect = new Rect(rect.x + 16, rect.yMax - 44, rect.width - 32, 32);
            string btnLabel = canAfford ? "この強化を採用" : "残高不足";
            GUI.enabled = canAfford;
            if (GUI.Button(btnRect, btnLabel))
                ConfirmChoice(upgrade);
            GUI.enabled = true;
        }

        private void ConfirmChoice(UnitUpgrade selectedUpgrade)
        {
            var meta = _bootstrap.Meta;
            // 段階別コスト：現在 Lv（ApplyUpgrade 前）の値で消費＋失敗時の返戻。
            int cost = _pendingChoiceUpgrade.GetCostForNextLevel(meta.GetUpgradeLevel(_pendingChoiceUpgrade.Id));
            if (!meta.SpendMemories(cost))
            {
                _flashMessage = $"残高不足：{_pendingChoiceUpgrade.DisplayName} を購入できません";
                return;
            }
            if (!meta.ApplyUpgrade(_pendingChoiceUpgrade.Id, _pendingChoiceUpgrade.Cap))
            {
                meta.EarnMemories(cost);
                _flashMessage = $"{_pendingChoiceUpgrade.DisplayName} は上限に達しています";
                CancelChoiceModal();
                return;
            }
            meta.ApplyUpgradeChoice(_pendingChoiceUnitId, selectedUpgrade.UpgradeId);
            // 表示 Lv = Lv1（初期）+ 購入済み Lv 数。ApplyUpgrade 後なので新 Lv を含む。
            int nextRunStartLevel = 1 + meta.GetUpgradeLevel(_pendingChoiceUpgrade.Id);
            _flashMessage =
                $"{_pendingChoiceUpgrade.DisplayName} を購入（次ラン Lv {nextRunStartLevel}）：{selectedUpgrade.Name}";
            CancelChoiceModal();
        }

        private void CancelChoiceModal()
        {
            _pendingChoiceUpgrade = null;
            _pendingChoiceUnitId = null;
        }

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            FillRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            FillRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            FillRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

// 「次のランへ」ボタン＋Flash メッセージ

        private void DrawNextRunButton()
        {
            // Flash メッセージ
            if (!string.IsNullOrEmpty(_flashMessage))
            {
                GUILayout.Label(_flashMessage, _flashStyle);
                GUILayout.Space(8);
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("← タイトルへ", _primaryButtonStyle,
                GUILayout.Width(180), GUILayout.Height(48)))
            {
                _bootstrap.ReturnToTitleFromHub();
                _flashMessage = "";
            }
            GUILayout.Space(24);
            if (GUILayout.Button("次のランへ →", _primaryButtonStyle,
                GUILayout.Width(260), GUILayout.Height(48)))
            {
                _bootstrap.StartNewRun();
                _flashMessage = "";
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // ユーティリティ

        private static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _accentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorAccent },
                alignment = TextAnchor.MiddleLeft,
            };
            _trueEndStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorTrueEnd },
                alignment = TextAnchor.MiddleLeft,
            };
            _itemNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _itemEffectStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _flashStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = ColorAccent },
                alignment = TextAnchor.MiddleCenter,
            };
            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };

            _stylesBuilt = true;
        }
    }
}
