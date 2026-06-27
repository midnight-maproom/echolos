// 試遊モード専用：画面下部の目的バーを常時表示する。
//
// 現在ラウンドの ObjectiveText を Bootstrap.Demo 経由で取得して表示。
// Demo.IsActive=false / ObjectiveText 空なら描画スキップ＝通常版に影響なし。
//
// セットアップ：試遊シーン EcholosProto_Demo.unity の VSPrototypeBootstrap と同じ GameObject に
// AddComponent する。通常シーン側には付けない（通常版に影響を残さないため）。
using UnityEngine;
using Echolos.Presentation.Common;

namespace Echolos.Presentation.VSPrototype
{
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class VSPrototypeDemoObjectiveGUI : MonoBehaviour
    {
        private static readonly Color ColorBarBg    = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        private static readonly Color ColorBarFg    = new Color(1.00f, 0.92f, 0.70f);
        private static readonly Color ColorBarFrame = new Color(0.95f, 0.80f, 0.40f, 0.9f);

        private const float BarHeight = 48f;

        private VSPrototypeBootstrap _bootstrap;
        private GUIStyle _textStyle;
        private bool _styleBuilt;

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void OnGUI()
        {
            if (_bootstrap == null) return;
            if (_bootstrap.Demo == null || !_bootstrap.Demo.IsActive) return;
            var text = _bootstrap.Demo.GetObjectiveText(_bootstrap.CurrentRound);
            if (string.IsNullOrEmpty(text)) return;

            BuildStyleIfNeeded();

            var bgRect = new Rect(0, Screen.height - BarHeight, Screen.width, BarHeight);
            FillRect(bgRect, ColorBarBg);
            // 上辺フレーム（2 px）
            FillRect(new Rect(0, Screen.height - BarHeight, Screen.width, 2), ColorBarFrame);

            GUI.Label(bgRect, text, _textStyle);
        }

        private void BuildStyleIfNeeded()
        {
            if (_styleBuilt) return;
            GuiTheme.EnsureJapaneseFont();
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _textStyle.normal.textColor = ColorBarFg;
            _styleBuilt = true;
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
