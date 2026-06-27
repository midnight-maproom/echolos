// タイトル画面（起動直後・Phase=Title 時のみ描画）。
//
// 【画面構成（PC 16:9 中央寄せ）】
// - 背景：title_background（BackgroundRegistry 経由・ScaleAndCrop で全画面・未配置時は黒系単色塗り）
// - 中央上：ゲームタイトル「Echoes of the Lost Kingdom」
// - 中央下：「ゲームスタート」ボタン
//
// 【挙動】
// - ボタン押下 → Bootstrap.StartFromTitle()
//   - セーブあり → Phase=Hub（メタ拠点）
//   - セーブなし → StartNewRun（A-a プロローグ → 1 周目固定構成）
//
// 【セットアップ】
// VSPrototypeBootstrap と同じ GameObject に AddComponent する。Phase=Title の時だけ OnGUI で描画。
using UnityEngine;
using Echolos.Presentation.Common;
using Echolos.UseCase.Demo;

namespace Echolos.Presentation.VSPrototype
{
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class VSPrototypeTitleGUI : MonoBehaviour
    {
        // 色定義（背景画像未配置時のフォールバック）
        private static readonly Color ColorBgFallback = new Color(0.05f, 0.05f, 0.08f);
        private static readonly Color ColorTitle      = new Color(1.00f, 0.95f, 0.80f);
        private static readonly Color ColorButtonBg   = new Color(0.20f, 0.22f, 0.28f, 0.92f);
        private static readonly Color ColorDemoButtonBg = new Color(0.28f, 0.22f, 0.18f, 0.92f);
        private static readonly Color ColorRecButtonBg  = new Color(0.18f, 0.28f, 0.22f, 0.92f);

        // タイトル文字（フルネーム）
        private const string GameTitle = "Echoes of the Lost Kingdom";

        [SerializeField, Tooltip("試遊モードボタン群を表示する（試遊シーン側で ON／通常シーン側で OFF）")]
        private bool _showDemoModeButtons;

        private VSPrototypeBootstrap _bootstrap;
        private GUIStyle _titleStyle;
        private GUIStyle _startButtonStyle;
        private bool _stylesBuilt;

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void OnGUI()
        {
            if (_bootstrap.CurrentPhase != VSPrototypePhase.Title) return;

            BuildStylesIfNeeded();

            // 背景：title_background があれば ScaleAndCrop で全画面、なければ単色塗り
            var screenRect = new Rect(0, 0, Screen.width, Screen.height);
            bool hasBackground = BackgroundRegistry.TryDrawCover(screenRect, "title_background");
            if (!hasBackground)
                FillRect(screenRect, ColorBgFallback);

            // タイトル文字：背景画像にロゴが含まれる前提のため、画像なし時のフォールバックとしてのみ表示
            if (!hasBackground)
            {
                var titleRect = new Rect(0, Screen.height * 0.30f, Screen.width, 80);
                GUI.Label(titleRect, GameTitle, _titleStyle);
            }

            // ゲームスタートボタン（中央下 2/3）
            float btnW = 260f;
            float btnH = 60f;
            var btnRect = new Rect(
                (Screen.width - btnW) * 0.5f,
                Screen.height * 0.62f,
                btnW, btnH);
            FillRect(btnRect, ColorButtonBg);
            if (GUI.Button(btnRect, "ゲームスタート", _startButtonStyle))
            {
                _bootstrap.StartFromTitle();
            }

            // 試遊モードボタン群（_showDemoModeButtons=true なら表示）。
            // 試遊シーン（EcholosProto_Demo.unity）でフラグ ON ／通常シーンでは OFF。
            if (_showDemoModeButtons)
            {
                DrawDemoModeButtons(btnW, btnH);
            }
        }

        private void DrawDemoModeButtons(float btnW, float btnH)
        {
            float demoBtnW = btnW * 0.7f;
            float demoBtnH = btnH * 0.7f;
            float gap = 8f;
            float topY = Screen.height * 0.62f + btnH + 30f;
            float centerX = Screen.width * 0.5f;
            float left = centerX - demoBtnW * 0.5f;
            float y = topY;

            // 試遊（茶色）
            DrawScenarioButton(
                new Rect(left, y, demoBtnW, demoBtnH),
                "試遊：救出戦を体験",
                DemoScenarioCatalog.Scenario2Id,
                ColorDemoButtonBg);
            y += demoBtnH + gap * 2;

            // 録画用（緑系・動画撮影専用・020 §3）
            DrawScenarioButton(
                new Rect(left, y, demoBtnW, demoBtnH),
                "録画：R5 B-b2 から",
                DemoScenarioCatalog.RecR5BB2Id,
                ColorRecButtonBg);
            y += demoBtnH + gap;
            DrawScenarioButton(
                new Rect(left, y, demoBtnW, demoBtnH),
                "録画：R7 A-c1 必敗から",
                DemoScenarioCatalog.RecR7AC1Id,
                ColorRecButtonBg);
            y += demoBtnH + gap;
            DrawScenarioButton(
                new Rect(left, y, demoBtnW, demoBtnH),
                "録画：R6 救出戦から",
                DemoScenarioCatalog.RecR6RescueId,
                ColorRecButtonBg);
            y += demoBtnH + gap;
            DrawScenarioButton(
                new Rect(left, y, demoBtnW, demoBtnH),
                "録画：R7 トゥルー直前から",
                DemoScenarioCatalog.RecR7TrueId,
                ColorRecButtonBg);
        }

        private void DrawScenarioButton(Rect rect, string label, string scenarioId, Color bg)
        {
            FillRect(rect, bg);
            if (GUI.Button(rect, label, _startButtonStyle))
            {
                _bootstrap.StartDemoMode(scenarioId);
            }
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            // 日本語フォントを GUI.skin.font に適用（MetaHubGUI 等と同じパターン）。
            // カスタムスタイルは font を明示せず GUI.skin.font の継承に任せる。
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _titleStyle.normal.textColor = ColorTitle;

            _startButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
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
