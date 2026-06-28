// タイトル画面（起動直後・Phase=Title 時のみ描画）。
//
// 【画面構成（PC 16:9 中央寄せ）】
// - 背景：title_background（BackgroundRegistry 経由・ScaleAndCrop で全画面・未配置時は黒系単色塗り）
// - 中央上：ゲームタイトル「Echoes of the Lost Kingdom」（背景画像未配置時のみ）
// - 中央下：「ゲームスタート」ボタン
// - 試遊シーンでは「ゲームスタート」直下に「試遊：救出戦を体験」ボタンを 1 個だけ追加（_showDemoModeButtons=true）
//
// 【挙動】
// - ボタン押下 → Bootstrap.StartFromTitle()
//   - セーブあり → Phase=Hub（メタ拠点）
//   - セーブなし → StartNewRun（A-a プロローグ → 1 周目固定構成）
// - 試遊ボタン押下 → Bootstrap.StartDemoMode(DemoSaveCatalog.Save2Id)
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

        // タイトル文字（フルネーム）
        private const string GameTitle = "Echoes of the Lost Kingdom";

        [SerializeField, Tooltip("試遊モードボタンを表示する（試遊シーン側で ON／通常シーン側で OFF）")]
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

            // 試遊ボタン（_showDemoModeButtons=true なら表示）。
            // 試遊シーン（EcholosProto_Demo.unity）でフラグ ON ／通常シーンでは OFF。
            if (_showDemoModeButtons)
            {
                DrawDemoButton(btnW, btnH);
            }
        }

        private void DrawDemoButton(float btnW, float btnH)
        {
            const float DemoBtnW = 420f;
            const float DemoBtnH = 50f;
            float y = Screen.height * 0.62f + btnH + 30f;
            float left = Screen.width * 0.5f - DemoBtnW * 0.5f;

            var rect = new Rect(left, y, DemoBtnW, DemoBtnH);
            FillRect(rect, ColorDemoButtonBg);
            if (GUI.Button(rect, "試遊：救出戦を体験", _startButtonStyle))
            {
                _bootstrap.StartDemoMode(DemoSaveCatalog.Save2Id);
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
