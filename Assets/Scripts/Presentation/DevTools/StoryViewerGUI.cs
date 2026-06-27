// Debug_StoryViewer シーン用：ストーリーシーンを ID から直接再生して確認するための開発 GUI。
//
// 【使い方】
// 1. EcholosProto_VS.unity を Save As で複製して Debug_StoryViewer.unity を作成
//    （Build Settings 登録対象外＝ Debug_* プレフィックスで自動除外）
// 2. VSPrototypeManager GameObject に本コンポーネントを Add
// 3. Play すると Phase=Title でシーン一覧が右側に出る
// 4. 「初見再生」or「既見再生」ボタンでシーンを再生 → 完了で Title に戻る
//
// 【挙動】
// - Phase=Title の時のみ描画。StoryEvent 中（再生中）は何も描かない（StoryGUI に任せる）
// - 「初見再生」：Meta フラグに関係なく本文ページを再生
// - 「既見再生」：Meta フラグに関係なく RepeatNarration 1 ページを再生
// - どちらも Meta.HasSeenStoryScene を変更しない（連続確認のため Meta を汚さない）
// - 既見マーカー●は Meta フラグの現状表示（過去ラン経由の真の既見か）
// - 上部「セーブ＋既見フラグリセット」で過去ラン分含む Meta を完全初期化
//
// 【設計】
// - Debug_StoryViewer シーンはセーブを汚さないよう、起動時に ResetAllProgress を呼ぶことを推奨
//   （本 GUI 内でリセットボタンを提供）
using UnityEngine;
using Echolos.UseCase.VSPrototype;
using Echolos.Presentation.Common;
using Echolos.Presentation.VSPrototype;

namespace Echolos.Presentation.DevTools
{
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class StoryViewerGUI : MonoBehaviour
    {
        // 短縮対象 9 シーン＋エンディング系 4 シーンの並び（330 §5 演出発火順）
        private static readonly (string Id, string Label)[] SceneList = {
            (VSPrototypeStorySceneIds.Opening,                  "A-a プロローグ"),
            (VSPrototypeStorySceneIds.BalduinIntro,             "B-a バルドゥインの背景"),
            (VSPrototypeStorySceneIds.BalduinLetter,            "B-b1 戦況悪化＋宰相握りつぶし"),
            (VSPrototypeStorySceneIds.BalduinSurrender,         "B-b2 バルドゥイン降伏"),
            (VSPrototypeStorySceneIds.BalduinRescue,            "B-d 救援成功（ブリジット託す）"),
            (VSPrototypeStorySceneIds.MysteriousGirl,           "B-c 謎の少女"),
            (VSPrototypeStorySceneIds.SwordEmpowered,           "B-e 聖剣の真の力（気づき＋強化）"),
            (VSPrototypeStorySceneIds.BossAttack,               "A-c1 闇のゲート（必敗版）"),
            (VSPrototypeStorySceneIds.BossPurify,               "A-c2 オーラ祓い（戦える版）"),
            (VSPrototypeStorySceneIds.EndingDefeatFirst,        "Defeat 初回"),
            (VSPrototypeStorySceneIds.EndingDefeatNormalClear,  "Defeat 6R 達成版"),
            (VSPrototypeStorySceneIds.EndingDefeatRepeated,     "Defeat 2 周目以降"),
            (VSPrototypeStorySceneIds.EndingTrue,               "True エンド"),
        };

        private static readonly Color ColorBg     = new Color(0.05f, 0.05f, 0.10f, 0.92f);
        private static readonly Color ColorRowBg  = new Color(0.18f, 0.20f, 0.26f);
        private static readonly Color ColorTitle  = new Color(1.00f, 0.92f, 0.70f);

        private VSPrototypeBootstrap _bootstrap;
        private Vector2 _scroll;

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallButtonStyle;
        private GUIStyle _resetButtonStyle;
        private GUIStyle _statusStyle;
        private bool _stylesBuilt;

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void OnGUI()
        {
            // Title フェーズの時だけ確認 UI を出す。StoryEvent 中は StoryGUI 側に任せる。
            if (_bootstrap.CurrentPhase != VSPrototypePhase.Title) return;

            BuildStylesIfNeeded();

            // 右側に縦長パネル（タイトル画面の上に重ねる形）
            float panelW = 460f;
            var panel = new Rect(Screen.width - panelW - 20f, 20f, panelW, Screen.height - 40f);
            FillRect(panel, ColorBg);

            GUILayout.BeginArea(new Rect(panel.x + 16, panel.y + 14, panel.width - 32, panel.height - 28));

            GUILayout.Label("── ストーリーシーン ビューア（Dev）──", _titleStyle);
            GUILayout.Space(4);
            GUILayout.Label("初見＝本文再生／既見＝短縮ナレ再生（どちらも Meta フラグ不変）", _statusStyle);
            GUILayout.Space(8);

            if (GUILayout.Button("既見フラグ全クリア＋セーブもリセット", _resetButtonStyle, GUILayout.Height(32)))
            {
                _bootstrap.DevResetProgressForStoryViewer();
            }

            GUILayout.Space(8);
            _scroll = GUILayout.BeginScrollView(_scroll);

            foreach (var (id, label) in SceneList)
            {
                DrawRow(id, label);
                GUILayout.Space(4);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRow(string sceneId, string label)
        {
            var rowRect = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true));
            FillRect(rowRect, ColorRowBg);

            bool registered = _bootstrap.StorySceneCatalog != null
                && _bootstrap.StorySceneCatalog.IsRegistered(sceneId);
            bool seen = _bootstrap.Meta.HasSeenStoryScene(sceneId);

            // 左：シーン名＋ ID＋既見マーカー
            string seenMark = seen ? " ●" : "";
            var nameRect = new Rect(rowRect.x + 10, rowRect.y + 4, rowRect.width - 240, 20);
            GUI.Label(nameRect, $"{label}{seenMark}", _labelStyle);
            var idRect = new Rect(rowRect.x + 10, rowRect.y + 24, rowRect.width - 240, 18);
            GUI.Label(idRect, $"id: {sceneId}{(registered ? "" : "（SO 未登録）")}", _statusStyle);

            // 右：再生ボタン 2 種
            float btnW = 100f, btnH = 30f;
            float btnY = rowRect.y + 9;
            float rightX = rowRect.xMax - btnW - 8;
            GUI.enabled = registered;
            if (GUI.Button(new Rect(rightX, btnY, btnW, btnH), "既見再生", _smallButtonStyle))
                _bootstrap.DevPlayStoryScene(sceneId, treatAsSeen: true);
            if (GUI.Button(new Rect(rightX - btnW - 6, btnY, btnW, btnH), "初見再生", _smallButtonStyle))
                _bootstrap.DevPlayStoryScene(sceneId, treatAsSeen: false);
            GUI.enabled = true;
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _titleStyle.normal.textColor = ColorTitle;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
            };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
            };
            _statusStyle.normal.textColor = new Color(0.75f, 0.75f, 0.80f);

            _smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
            };
            _resetButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
            };

            _stylesBuilt = true;
        }

        private static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
