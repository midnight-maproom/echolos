// タイトル画面（起動直後・Phase=Title 時のみ描画）。
//
// 【画面構成（PC 16:9 中央寄せ）】
// - 背景：title_background（BackgroundRegistry 経由・ScaleAndCrop で全画面・未配置時は黒系単色塗り）
// - 中央上：ゲームタイトル「Echoes of the Lost Kingdom」（背景画像未配置時のみ）
// - 中央下：「ゲームスタート」ボタン（メイン）／直下に一回り小さく「ゲーム終了」ボタン
// - 右下隅：「DEMO: 救出戦から始める(R4)」ショートカット（控えめ配色・補助役）
//
// 【挙動】
// - ゲームスタート押下 → Bootstrap.StartFromTitle()
//   - セーブあり → Phase=Hub（メタ拠点）
//   - セーブなし → StartNewRun（A-a プロローグ → 1 周目固定構成）
// - ゲーム終了押下 → Application.Quit（Editor では再生停止）
// - DEMO ショートカット押下 → Bootstrap.StartDemoMode(DemoSaveCatalog.Save2Id)
//   メタ進行は触らず、既存進行を温存したまま R4 から一時的にラン開始。
//   エンディング後は通常通り Hub に戻り、メタ進行も通常通り保存される。
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
        private static readonly Color ColorDemoButtonBg = new Color(0.18f, 0.18f, 0.22f, 0.75f);

        // タイトル文字（フルネーム）
        private const string GameTitle = "Echoes of the Lost Kingdom";

        private VSPrototypeBootstrap _bootstrap;
        private GUIStyle _titleStyle;
        private GUIStyle _startButtonStyle;
        private GUIStyle _quitButtonStyle;
        private GUIStyle _demoButtonStyle;
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

            // ゲームスタートボタン（中央下 2/3・メイン）
            const float StartBtnW = 260f;
            const float StartBtnH = 60f;
            float startBtnTop = Screen.height * 0.62f;
            var startRect = new Rect(
                (Screen.width - StartBtnW) * 0.5f,
                startBtnTop,
                StartBtnW, StartBtnH);
            FillRect(startRect, ColorButtonBg);
            if (GUI.Button(startRect, "ゲームスタート", _startButtonStyle))
            {
                _bootstrap.StartFromTitle();
            }

            // ゲーム終了ボタン（メイン直下・一回り小さく）
            const float QuitBtnW = 220f;
            const float QuitBtnH = 44f;
            var quitRect = new Rect(
                (Screen.width - QuitBtnW) * 0.5f,
                startBtnTop + StartBtnH + 20f,
                QuitBtnW, QuitBtnH);
            FillRect(quitRect, ColorButtonBg);
            if (GUI.Button(quitRect, "ゲーム終了", _quitButtonStyle))
            {
                QuitApplication();
            }

            DrawDemoButton();
        }

        private void DrawDemoButton()
        {
            const float DemoBtnW = 300f;
            const float DemoBtnH = 38f;
            const float Margin = 20f;
            float left = Screen.width - DemoBtnW - Margin;
            float top = Screen.height - DemoBtnH - Margin;

            var rect = new Rect(left, top, DemoBtnW, DemoBtnH);
            FillRect(rect, ColorDemoButtonBg);
            if (GUI.Button(rect, "DEMO: 救出戦から始める(R4)", _demoButtonStyle))
            {
                _bootstrap.StartDemoMode(DemoSaveCatalog.Save2Id);
            }
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

            _quitButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
            };

            _demoButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
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
