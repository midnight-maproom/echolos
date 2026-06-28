// BridgetEvent / EndingEvent の演出描画。
//
// 【描画責務】
// - Phase=BridgetEvent / EndingEvent の間だけ全画面描画する
// - 暗幕＋スチル＋ナレは StoryOverlay の静的描画を流用
// - 「次へ →」「Skip」ボタンは本ファイルで独自描画
// - StoryProgress 本体（ロジック）は VSPrototypeBootstrap が保持・Tick する
//
// 【Bootstrap との分担】
// - 進捗・遷移ロジック：Bootstrap.StoryProgress と Bootstrap.OnStoryComplete（Phase 遷移）
// - 描画と入力：本ファイル
using UnityEngine;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.VSPrototype
{
    [RequireComponent(typeof(VSPrototypeBootstrap))]
    public sealed class VSPrototypeStoryGUI : MonoBehaviour
    {
        private static readonly Color ColorText = new Color(0.97f, 0.97f, 0.95f);

        // 進行ボタン文言（UI 文言なので Presentation 層が持つ）。
        private const string ButtonNext = "次へ →";
        private const string ButtonSkip = "Skip";

        // 「次へ」ボタン領域（幅 220 + 右マージン 56）＋緩衝 16 = ナレ帯右に確保する余白幅。
        private const float AdvanceButtonReservedWidth = 292f;

        private VSPrototypeBootstrap _bootstrap;
        private GUIStyle _narrationStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesBuilt;

        private void Awake()
        {
            _bootstrap = GetComponent<VSPrototypeBootstrap>();
        }

        private void Update()
        {
            // StoryProgress の時間進行は MonoBehaviour 側で Tick する
            //（Core 側は時間を持たない設計に合わせる）
            var phase = _bootstrap.CurrentPhase;
            if (phase != VSPrototypePhase.StoryEvent) return;
            var progress = _bootstrap.StoryProgress;
            if (progress == null || progress.IsFinished) return;
            progress.Tick(Time.deltaTime);
            // 自然完了したらコールバックが Bootstrap 側で発火している
        }

        private void OnGUI()
        {
            var phase = _bootstrap.CurrentPhase;
            if (phase != VSPrototypePhase.StoryEvent) return;

            var progress = _bootstrap.StoryProgress;
            if (progress == null) return;

            BuildStylesIfNeeded();

            var area = new Rect(0, 0, Screen.width, Screen.height);

            // 1) 暗幕＋スチル
            StoryOverlay.DrawBackground(area, progress);

            // 2) ナレ帯（「次へ」ボタン表示中はボタン領域分の右余白を確保して重なりを回避）
            float narrationReservedRight = progress.IsWaitingForManualAdvance ? AdvanceButtonReservedWidth : 0f;
            StoryOverlay.DrawNarration(area, progress, _narrationStyle, narrationReservedRight);

            // 3) 進行ボタン（手動送り＝Display 中で DisplaySeconds==0 の時）
            DrawAdvanceButton(area, progress);

            // 4) Skip ボタン（右上に常駐）
            DrawSkipButton(area, progress);
        }

        private void DrawAdvanceButton(Rect area, StoryProgress progress)
        {
            if (!progress.IsWaitingForManualAdvance) return;
            // ナレ帯の右端から少し内側に配置
            float w = 220f, h = 56f;
            float marginR = 56f, marginB = 56f;
            var rect = new Rect(area.xMax - w - marginR, area.yMax - h - marginB, w, h);
            if (GUI.Button(rect, ButtonNext, _buttonStyle))
                progress.NextPage();
        }

        private void DrawSkipButton(Rect area, StoryProgress progress)
        {
            float w = 110f, h = 40f;
            float marginR = 24f, marginT = 24f;
            var rect = new Rect(area.xMax - w - marginR, area.y + marginT, w, h);
            if (GUI.Button(rect, ButtonSkip, _buttonStyle))
                progress.Skip();
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;

            GuiTheme.EnsureJapaneseFont();

            _narrationStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            _stylesBuilt = true;
        }
    }
}
