// Assets/Scripts/UnityView/Stage3CampaignGUI.cs
// 段階3 ローグライト・ミニループ（H3）の OnGUI シェル。
//
// 【目的】
// - 段階3のCore実装（Stage3CampaignState / Stage3RoundManager / Stage3DraftService /
//   Stage3InteriorService / Stage3EnemyPatterns）を1画面で遊んで H3 を検証するハーネス。
// - Sandbox（バトル単体）とは別に、ラウンド進行・戦線3つ・ドラフト・内政・点数を一気通貫で体感する。
//
// 【方針】
// - Canvas・Prefabは使わず、空GameObjectにこのコンポーネントを付けてPlayするだけで動く。
// - フェーズ遷移：InitialDraft → RoundDraft → Interior → Assignment → BattleResult →（次R）
//                R7はドラフトなしで Interior → Assignment → BattleResult → GameEnd。
// - 戦闘はバックエンドの Stage3RoundManager.ResolveAllBattles → BattleRunner を呼ぶ。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Echolos.Domain.Models;
using Echolos.Domain.Prototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>段階3 キャンペーンの OnGUI シェル（H3検証）。</summary>
    public sealed class Stage3CampaignGUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        // 内部状態
        // ══════════════════════════════════════════════

        private Stage3CampaignState _state;
        private Stage3DraftService _draftService;
        private Stage3InteriorService _interiorService;
        private System.Random _rng;

        private Phase _phase;
        private DraftOffer _currentOffer;
        private int _initialDraftIndex;
        private List<Stage3FrontResolution> _lastResolutions;
        private string _flashMessage = "";
        private string _battleLogPreview = "";

        // 内政の対象選択用の保留状態
        private InteriorActionKind? _pendingAction;

        // 配置UIの選択状態
        private Unit _selectedUnit;

        private Vector2 _rosterScroll;
        private Vector2 _logScroll;

        // §11.3 戦闘可視化：戦線ごとの観戦再生
        private readonly Stage3BattleSpectatorView _spectator = new Stage3BattleSpectatorView();
        private Queue<Stage3FrontResolution> _spectateQueue;
        private Stage3FrontResolution _currentSpectating;

        // §11.3 テンポ調整：観戦中の倍速設定・自動進行設定（複数戦闘をまたいで保持）
        // _spectateSpeed == 0 はステップモード（ユーザーが「次イベント」を押した時だけ進む）
        private float _spectateSpeed = 1f;     // 0=ステップ, 1, 2, 4
        private bool _spectateAutoAdvance = true; // 戦闘終了で次戦線に自動進行

        // §11.5 Step 4-13a A4：ターゲット線トグル。デフォルト OFF・×2/×4 では自動で抑止される。
        private bool _showTargetLines = false;

        // §11.5 Step 4-5：フェーズヘルプの折り畳み状態（動画撮影時に邪魔ならボタン1つで隠せる）
        private bool _phaseHelpCollapsed;

        private enum Phase
        {
            // §11.5 Step 4-9〜4-11（2026-06-03 追加）：演出フェーズ。実体は StoryProgress が制御。
            Title,           // タイトル画面（key-visual＋「ゲームを始める →」）
            Intro,           // 導入チュートリアル（スチル＋ナレ・Skip 可）
            BossPrologue,    // R7 ボス劇場（街襲来イベント・自動進行）
            EndingDefeat,    // 敗北エンディング（badend.png＋「ありがとう」）
            EndingVictory,   // 勝利エンディング（魔王影スチル＋「危機は去った、しかし…」）

            // 既存（H3 検証時に確定したゲーム本体フェーズ）
            InitialDraft,    // ゲーム開始時 3ドラフト
            RoundDraft,      // R1-R6 ラウンド開始ドラフト
            Interior,        // 内政
            Assignment,      // 戦線配置
            Spectating,      // §11.3 戦闘観戦（戦線ごとに順次再生）
            BattleResult,    // 戦闘解決後の結果表示
            GameEnd,         // 過渡的に維持。C5 で EndingDefeat/EndingVictory に分岐置換予定。
        }

        // §11.5 Step 4-9 C1：演出フェーズ共通の再生コントローラ。
        // Title/Intro/BossPrologue/EndingDefeat/EndingVictory はこの Progress を使い回す。
        private readonly StoryProgress _storyProgress = new StoryProgress();
        private float _lastStoryTickTime;

        // §11.5 Step 4-9 C3：音量スライダ枠（4-12 BGM 実装で実値として効く）。
        // 現状は UI 上の見た目だけで、AudioSource にはまだ接続されていない。
        // PlayerPrefs 永続化も 4-12 でまとめて行う。
        private float _bgmVolume = 0.6f;
        private float _seVolume = 0.7f;

        // §11.5 Step 4-13 サンプル先行（A14 ツールチップ試作・2026-06-03）：
        // OnGUI のホバー判定挙動を確認するため、姫騎士 1 兵種にだけホバー → ステータス表示を仕込む。
        // フレームごとに Repaint タイミングでクリア＆再セットする方式で、IMGUI の Layout/Repaint
        // 二重呼び出しに耐える。本実装可否は Step 4-13c で判断する（仕様書 §11.5 Step 4-13）。
        private string _tooltipUnitId;
        private Rect _tooltipAnchorRect;

        // ══════════════════════════════════════════════
        // ライフサイクル
        // ══════════════════════════════════════════════

        private void Start()
        {
            // §11.5 Step 4-9 C3：タイトル画面 UI 確認のため一時的に StartFromTitle に切替。
            // C5 で Title→Intro→R1 の正式動線を組んだ後もこの呼び出しは維持される。
            // タイトルからゲーム本体へ進む経路は現状 StartGameDirect 直行（C5 で Intro 経由に置換）。
            StartFromTitle();
        }

        /// <summary>
        /// ゲーム本体を直接開始する（旧 ResetGame の挙動）。
        /// 用途：開発用「リセット」ボタン／敗北エンディング後のクイックリトライ（C5 で配線）。
        /// </summary>
        private void StartGameDirect()
        {
            _rng = new System.Random();
            _state = new Stage3CampaignState();
            _draftService = new Stage3DraftService(() => _rng.Next());
            _interiorService = new Stage3InteriorService(_draftService);

            // §9.5 改訂（2026-06-02）：姫騎士（固有キャラ）を初期手駒に固定加入させてから
            // 2回ドラフトを行う。前衛セーフティネットは姫騎士で構造的に解消されたため撤回。
            _state.AddUnitToRoster(Stage3Roster.Princess());

            _phase = Phase.InitialDraft;
            _initialDraftIndex = 0;
            _currentOffer = _draftService.DrawInitialPick(_state, _initialDraftIndex);
            _flashMessage = "ゲーム開始。姫騎士に加えて、通常プールから3択ドラフトで2体を選んでください。";
            _battleLogPreview = "";
            _pendingAction = null;
            _selectedUnit = null;
            _lastResolutions = null;

            // §11.3 観戦ステートのクリア
            _spectateQueue = null;
            _currentSpectating = null;
        }

        /// <summary>
        /// タイトル画面から開始する。
        /// 用途：通常の起動経路（C5 で Start() を切り替え）／勝利エンディング後の遷移。
        /// _state は触らず Phase=Title だけセットする（OnGUI 冒頭の Title 分岐が早期 return）。
        /// </summary>
        private void StartFromTitle()
        {
            _phase = Phase.Title;
            _flashMessage = "";
            _storyProgress.Initialize(StoryContent.TitlePages(), null);
            _lastStoryTickTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 導入チュートリアルを再生してからゲーム本体を開始する。
        /// 用途：タイトル「ゲームを始める →」押下時（C5 で配線確定）。
        /// _storyProgress 完了時のコールバックで StartGameDirect を呼ぶ＝完走でも Skip でも同じ遷移先に着く。
        /// </summary>
        private void StartFromIntro()
        {
            _phase = Phase.Intro;
            _flashMessage = "";
            _storyProgress.Initialize(StoryContent.IntroPages(), StartGameDirect);
        }

        /// <summary>
        /// 敗北エンディングを再生してから直接ゲーム再開（タイトル飛ばし＝さっさと再挑戦）。
        /// 用途：戦線累計上限到達 or R7 ボス戦敗北で _state.Result が FrontLost/BossLost になった時。
        /// 完了コールバックで StartGameDirect を呼ぶ＝完走でも Skip でも同じ遷移先（直接 R1）に着く。
        /// </summary>
        private void StartEndingDefeat()
        {
            _phase = Phase.EndingDefeat;
            _flashMessage = "";
            _storyProgress.Initialize(StoryContent.EndingDefeatPages(), StartGameDirect);
        }

        /// <summary>
        /// 勝利エンディングを再生してからタイトル画面へ戻る。
        /// 用途：R7 ボス戦に勝利して _state.Result が Cleared になった時。
        /// 完了コールバックで StartFromTitle を呼ぶ＝完走でも Skip でも同じ遷移先（タイトル経由）に着く。
        /// </summary>
        private void StartEndingVictory()
        {
            _phase = Phase.EndingVictory;
            _flashMessage = "";
            _storyProgress.Initialize(StoryContent.EndingVictoryPages(), StartFromTitle);
        }

        /// <summary>
        /// R7 ボス劇場演出を再生してから通常の内政フェーズへ。
        /// 用途：R6→R7 遷移時（最終ラウンド開始時）に挟む 2-3 秒のフルスクリーン演出。
        /// boss_silhouette.png を表示しつつ「最終決戦」を視覚化し、ローグライト構造のクライマックスを演出する。
        /// 完了コールバックで通常の R7 内政フェーズへ遷移する。
        /// </summary>
        private void StartBossPrologue()
        {
            _phase = Phase.BossPrologue;
            _flashMessage = "";
            _currentOffer = null;
            _storyProgress.Initialize(StoryContent.BossProloguePages(), () =>
            {
                _phase = Phase.Interior;
                _flashMessage = "R7 ボス戦：街にボス出現。内政→配置→決戦。";
            });
        }

        /// <summary>
        /// ゲーム本体（戦闘解決後やラウンド進行）から次フェーズを決めるヘルパ。
        /// _state.Result が決着していればエンディング演出に分岐、None なら戦闘結果フェーズへ。
        /// 旧コードの「_phase = Result != None ? GameEnd : BattleResult」を演出フェーズへ進化させたもの。
        /// </summary>
        private void TransitionAfterBattleResolution()
        {
            if (_state.Result == Stage3CampaignResult.None)
            {
                _phase = Phase.BattleResult;
                return;
            }
            if (_state.Result == Stage3CampaignResult.Cleared) StartEndingVictory();
            else StartEndingDefeat(); // FrontLost / BossLost
        }

        // ══════════════════════════════════════════════
        // OnGUI 全体レイアウト
        // ══════════════════════════════════════════════

        private void OnGUI()
        {
            // WebGL ビルドでも日本語が表示されるよう、毎フレーム冒頭で
            // 日本語フォントを GUI.skin.font とキャッシュ済 GUIStyle に適用する。
            // フォント未配置の Editor 環境では何もしない（既存挙動維持）。
            GuiTheme.EnsureJapaneseFont();

            // §11.5 Step 4-13 サンプル先行：ホバー状態は Repaint 冒頭でクリアし、
            // 各描画パスの中でホバー検知された時のみ再セットする。これでフレーム遷移で
            // 自然に消える（前フレームのホバー判定が翌フレームに残らない）。
            if (Event.current.type == EventType.Repaint) _tooltipUnitId = null;

            // §11.5 Step 4-9〜4-11 演出フェーズ：フルスクリーン専用描画で早期 return。
            // Title/Intro/BossPrologue/EndingDefeat/EndingVictory は StoryProgress でドライブする。
            // C2 時点は枠だけ（プレースホルダー）。C3〜で本実装に差し替える。
            if (_phase == Phase.Title || _phase == Phase.Intro
                || _phase == Phase.BossPrologue
                || _phase == Phase.EndingDefeat || _phase == Phase.EndingVictory)
            {
                DrawStoryPhasePlaceholder();
                return;
            }

            // §11.3 観戦中は専用レイアウト：ヘッダ＋大きな観戦ビュー＋コントロール。
            // 戦線パネル・手駒パネル等のキャンペーンUIは隠して戦闘進行に注意を集中させる。
            if (_phase == Phase.Spectating)
            {
                DrawSpectatingScreen();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

            DrawHeader();
            DrawFlashMessage();

            // 手駒パネルを先に描画（戦線パネルが縦に伸びて押し出される事故防止）
            DrawRosterPanel();

            // §11.5 Step 4-5：フェーズ別の操作ヘルプ枠（折り畳み可）
            DrawPhaseHelp();

            GUILayout.BeginHorizontal();

            // 左カラム：フェーズ別UI
            GUILayout.BeginVertical(GUILayout.Width(Screen.width * 0.55f));
            DrawPhasePanel();
            GUILayout.EndVertical();

            // 右カラム：戦線3パネル
            GUILayout.BeginVertical();
            DrawFrontsPanel();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // 下部：戦闘ログプレビュー
            DrawBattleLogPreview();

            GUILayout.EndArea();

            // §11.5 Step 4-13 サンプル先行：他の描画より後にツールチップを重ねる
            // （IMGUI は描画順がそのまま Z 順なので末尾で確実に最前面に出る）。
            DrawTooltipOverlay();
        }

        /// <summary>
        /// §11.5 Step 4-9〜4-11：演出フェーズの統合ディスパッチ。
        /// Title / Intro / BossPrologue / EndingDefeat / EndingVictory すべて本実装済み。
        /// フォールバック描画は enum 拡張時の保険として残置（通常経路では到達しない）。
        /// </summary>
        private void DrawStoryPhasePlaceholder()
        {
            if (_phase == Phase.Title)
            {
                DrawTitleScreen();
                return;
            }
            if (_phase == Phase.Intro)
            {
                DrawIntroScreen();
                return;
            }
            if (_phase == Phase.BossPrologue)
            {
                DrawBossPrologueScreen();
                return;
            }
            if (_phase == Phase.EndingDefeat || _phase == Phase.EndingVictory)
            {
                DrawEndingScreen();
                return;
            }

            // フォールバック描画（enum に新フェーズ追加で分岐漏れたときの保険）
            var area = new Rect(0, 0, Screen.width, Screen.height);
            GuiTheme.FillRect(area, new Color(0.05f, 0.05f, 0.08f, 1f));

            var msgRect = new Rect(0, Screen.height * 0.35f, Screen.width, 200);
            GUILayout.BeginArea(msgRect);
            GUILayout.FlexibleSpace();

            var centerStyle = new GUIStyle(GuiTheme.Title) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label($"[ {PhaseLabel(_phase)} ]", centerStyle);
            GUILayout.Space(12);
            var noteStyle = new GUIStyle(GuiTheme.Body) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("未対応フェーズ", noteStyle);
            GUILayout.Space(24);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("→ ゲーム本体へ", GUILayout.Width(200), GUILayout.Height(40)))
                StartGameDirect();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // ══════════════════════════════════════════════
        // §11.5 Step 4-9 C3：タイトル画面（key-visual.png ＋「ゲームを始める →」）
        // ══════════════════════════════════════════════

        /// <summary>
        /// タイトル画面：キービジュアル全画面 + 「ゲームを始める →」+ 音量スライダ枠 + バージョン/クレジット。
        /// _storyProgress.CurrentAlpha でフェードイン（TitlePages の FadeIn 0.8 秒）。
        /// </summary>
        private void DrawTitleScreen()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            float alpha = _storyProgress.IsFinished ? 1f : _storyProgress.CurrentAlpha;

            // 1) 背景：キービジュアル全画面（ScaleAndCrop でアスペクト維持して埋める）
            var keyVisual = Resources.Load<Texture2D>(StoryContent.KeyVisualPath);
            if (keyVisual != null)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(screen, keyVisual, ScaleMode.ScaleAndCrop);
                GUI.color = prevColor;
            }
            else
            {
                // フォールバック：単色背景＋タイトル文字
                GuiTheme.FillRect(screen, new Color(0.08f, 0.10f, 0.18f));
                var fallbackStyle = new GUIStyle(GuiTheme.Title)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 36,
                };
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 80),
                    "Echoes of the Lost Kingdom", fallbackStyle);
            }

            // フェードイン途中はボタン等を出さない（読み手のリズムを整える）
            bool readyToShow = alpha >= 0.9f;
            if (!readyToShow) return;

            // 2) 「ゲームを始める →」主ボタン（中央やや下）
            const float btnW = 320f;
            const float btnH = 64f;
            var btnRect = new Rect(
                (Screen.width - btnW) / 2f,
                Screen.height * 0.72f,
                btnW, btnH);
            if (GUI.Button(btnRect, StoryContent.TitleStartButtonLabel, GuiTheme.PrimaryButton))
            {
                // C4 で Intro 経由動線が動作確認できるようになった（StartFromIntro→完了で StartGameDirect）。
                StartFromIntro();
            }

            // 3) バージョン表記とクレジット（左下小さく）
            var infoStyle = new GUIStyle(GuiTheme.Muted)
            {
                alignment = TextAnchor.LowerLeft,
                fontSize = 12,
            };
            GUI.Label(new Rect(20, Screen.height - 52, 400, 20),
                StoryContent.TitleVersionLabel, infoStyle);
            GUI.Label(new Rect(20, Screen.height - 30, 400, 20),
                StoryContent.TitleCreditLabel, infoStyle);

            // 4) 音量スライダ枠（右下・4-12 BGM 実装で実値として効くようになる）
            DrawTitleVolumePanel();
        }

        /// <summary>タイトル右下の音量スライダ 2 本（BGM / SE）。半透明背景で可読性確保。</summary>
        private void DrawTitleVolumePanel()
        {
            const float labelW = 40f;
            const float sliderW = 160f;
            const float valueW = 36f;
            const float lineH = 22f;
            const float pad = 10f;
            const float panelW = labelW + sliderW + valueW + pad * 3f;
            const float panelH = lineH * 2f + pad * 2f + 4f;

            var panelRect = new Rect(
                Screen.width - panelW - 20,
                Screen.height - panelH - 20,
                panelW, panelH);

            // 半透明黒の背景
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = prev;

            GUILayout.BeginArea(panelRect);
            GUILayout.Space(pad);
            _bgmVolume = DrawVolumeSliderRow("BGM", _bgmVolume, labelW, sliderW, valueW);
            GUILayout.Space(4);
            _seVolume  = DrawVolumeSliderRow("SE",  _seVolume,  labelW, sliderW, valueW);
            GUILayout.EndArea();
        }

        private float DrawVolumeSliderRow(string label, float value, float labelW, float sliderW, float valueW)
        {
            GUILayout.BeginHorizontal();
            var labelStyle = new GUIStyle(GuiTheme.Body) { alignment = TextAnchor.MiddleLeft };
            GUILayout.Label(label, labelStyle, GUILayout.Width(labelW), GUILayout.Height(20));
            float v = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.Width(sliderW), GUILayout.Height(20));
            var valStyle = new GUIStyle(GuiTheme.Muted) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label($"{Mathf.RoundToInt(v * 100)}%", valStyle, GUILayout.Width(valueW), GUILayout.Height(20));
            GUILayout.EndHorizontal();
            return v;
        }

        // ══════════════════════════════════════════════
        // §11.5 Step 4-9 C5 / 4-10：エンディング（敗北は直接 R1、勝利はタイトル経由）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 敗北・勝利の共通エンディング画面。スチル＋ナレ＋手動送り＋Skip。
        /// 完了時の遷移先は StartEndingDefeat / StartEndingVictory で渡された onComplete に従う
        /// （Skip でも完走でも同じ遷移先に着く）。
        /// ストーリー未実装サインとして「※ 物語層は試遊版フェーズで本実装」を画面左下に小さく出す。
        /// </summary>
        private void DrawEndingScreen()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);

            // 1) 暗幕＋スチル
            StoryOverlay.DrawBackground(screen, _storyProgress);

            // 2) ナレ帯
            var narrationStyle = new GUIStyle(GuiTheme.Body)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                wordWrap = true,
                fontStyle = FontStyle.Italic,
            };
            StoryOverlay.DrawNarration(screen, _storyProgress, narrationStyle);

            // フェードイン中はボタンを出さない
            float alpha = _storyProgress.IsFinished ? 0f : _storyProgress.CurrentAlpha;
            if (alpha < 0.9f) return;

            // 3) 右上：Skip（押下で onComplete 発火＝遷移先に直接着く）
            var skipRect = new Rect(Screen.width - 120 - 20, 20, 120, 36);
            if (GUI.Button(skipRect, "▶ Skip", GuiTheme.PrimaryButton))
            {
                _storyProgress.Skip();
            }

            // 4) 中央下：手動送り中だけ次へボタン
            //    敗北は「もう一度遊ぶ →」（直接 R1）／勝利は「タイトルへ →」
            if (_storyProgress.IsWaitingForManualAdvance)
            {
                string label = _phase == Phase.EndingDefeat ? "もう一度遊ぶ →" : "タイトルへ →";
                const float btnW = 240f;
                const float btnH = 56f;
                var btnRect = new Rect(
                    (Screen.width - btnW) / 2f,
                    Screen.height - btnH - 32f,
                    btnW, btnH);
                if (GUI.Button(btnRect, label, GuiTheme.PrimaryButton))
                {
                    _storyProgress.NextPage();
                }
            }

            // 5) 左下：ストーリー未実装サイン（控えめに）
            var noteStyle = new GUIStyle(GuiTheme.Muted)
            {
                alignment = TextAnchor.LowerLeft,
                fontSize = 11,
            };
            GUI.Label(new Rect(20, Screen.height - 24, 500, 20),
                StoryContent.StoryUnderConstructionNote, noteStyle);
        }

        // ══════════════════════════════════════════════
        // §11.5 Step 4-11：R7 ボス劇場（スチル＋ナレ＋自動進行＋Skip）
        // ══════════════════════════════════════════════

        /// <summary>
        /// R7 ボス劇場画面。boss_silhouette.png + ナレ「最終決戦／街に、何かが迫る。」を
        /// 2.5 秒自動進行で再生。Skip 可。完了で StartBossPrologue の onComplete が走り Phase.Interior へ。
        /// 「ローグライト構造のクライマックスをここで視覚化する」のが本シーンの狙い（仕様書 §11.5 Step 4-11）。
        /// </summary>
        private void DrawBossPrologueScreen()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);

            // 1) 暗幕＋スチル
            StoryOverlay.DrawBackground(screen, _storyProgress);

            // 2) ナレ帯（中央下〜下部）。エンディングより重い字面（太字・大きめ）で「決戦」感を出す
            var narrationStyle = new GUIStyle(GuiTheme.Body)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            StoryOverlay.DrawNarration(screen, _storyProgress, narrationStyle);

            // フェードイン中は Skip ボタンを出さない
            float alpha = _storyProgress.IsFinished ? 0f : _storyProgress.CurrentAlpha;
            if (alpha < 0.9f) return;

            // 3) 右上：Skip ボタン（押下で onComplete 発火＝Phase.Interior へ）
            var skipRect = new Rect(Screen.width - 120 - 20, 20, 120, 36);
            if (GUI.Button(skipRect, "▶ Skip", GuiTheme.PrimaryButton))
            {
                _storyProgress.Skip();
            }
        }

        // ══════════════════════════════════════════════
        // §11.5 Step 4-9 C4：導入チュートリアル（スチル＋ナレ＋手動送り＋Skip）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 導入チュートリアル画面：StoryOverlay.DrawBackground/DrawNarration で
        /// 暗幕+スチル+ナレ帯を描画し、上部に Skip／中央下に「次へ →」（最終ページは「ゲームを始める →」）を出す。
        /// 完了時の遷移は _storyProgress 完了コールバック（StartFromIntro 時に StartGameDirect を渡している）。
        /// </summary>
        private void DrawIntroScreen()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);

            // 1) 暗幕＋スチル（CurrentAlpha でフェード）
            StoryOverlay.DrawBackground(screen, _storyProgress);

            // 2) ナレ帯（中央下〜下部）
            var narrationStyle = new GUIStyle(GuiTheme.Body)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                wordWrap = true,
            };
            StoryOverlay.DrawNarration(screen, _storyProgress, narrationStyle);

            // ボタンはフェードイン途中では出さない（読み手のリズム整え）
            float alpha = _storyProgress.IsFinished ? 0f : _storyProgress.CurrentAlpha;
            if (alpha < 0.9f) return;

            // 3) 右上：Skip ボタン（常時表示。押下で全ページスキップ→StartGameDirect 発火）
            var skipRect = new Rect(Screen.width - 120 - 20, 20, 120, 36);
            if (GUI.Button(skipRect, "▶ Skip", GuiTheme.PrimaryButton))
            {
                _storyProgress.Skip();
            }

            // 4) 中央下：「次へ →」or「ゲームを始める →」（最終ページかどうかで切替）
            //    手動送りページ（DisplaySeconds==0）が Display 中の時だけ表示する。
            if (_storyProgress.IsWaitingForManualAdvance)
            {
                int pages = StoryContent.IntroPages().Count;
                bool isLast = _storyProgress.CurrentPageIndex >= pages - 1;
                string label = isLast ? "ゲームを始める →" : "次へ →";

                const float btnW = 240f;
                const float btnH = 56f;
                var btnRect = new Rect(
                    (Screen.width - btnW) / 2f,
                    Screen.height - btnH - 32f,
                    btnW, btnH);

                if (GUI.Button(btnRect, label, GuiTheme.PrimaryButton))
                {
                    _storyProgress.NextPage();
                }
            }
        }

        /// <summary>観戦専用画面。Rect ベース描画で観戦ビューを大きく表示する。</summary>
        private void DrawSpectatingScreen()
        {
            const float margin = 10f;
            const float headerH = 36f;
            const float footerH = 56f;

            // ヘッダ：ラウンド・フェーズタグ・残戦線数
            int remain = _spectateQueue?.Count ?? 0;
            var headerRect = new Rect(margin, margin, Screen.width - margin * 2, headerH);
            GuiTheme.FillRect(headerRect, GuiTheme.PanelBg);

            GUILayout.BeginArea(headerRect);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label($"R{_state.CurrentRound}/{_state.Config.MaxRounds}",
                GuiTheme.Title, GUILayout.Width(96));
            GUILayout.Space(8);
            GuiTheme.DrawChip("戦闘観戦", GuiTheme.PhaseColor("戦闘観戦"), 120f, 26f);
            GUILayout.Space(12);
            GUILayout.Label($"残り戦線：{remain}", GuiTheme.Body);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 観戦ビュー本体
            var viewRect = new Rect(
                margin,
                margin + headerH + 4f,
                Screen.width - margin * 2,
                Screen.height - margin * 2 - headerH - footerH - 8f);
            _spectator.DrawGUI(viewRect);

            // フッタコントロール：倍速 / 自動進行 / スキップ
            var footerRect = new Rect(margin, Screen.height - margin - footerH, Screen.width - margin * 2, footerH);
            GUILayout.BeginArea(footerRect);
            GUILayout.BeginHorizontal();

            // 速度トグル：ステップ / ×1 / ×2 / ×4（戦闘間で保持される）
            // ステップ＝Tick停止、ユーザーが「次イベント」ボタンで1つずつ送る
            GUILayout.Label("速度", GUILayout.Width(36));
            DrawSpeedToggle(0f, "ステップ");
            DrawSpeedToggle(1f, "×1");
            DrawSpeedToggle(2f, "×2");
            DrawSpeedToggle(4f, "×4");

            GUILayout.Space(12);

            // 自動進行：戦闘終了から AutoAdvanceDelay 秒後に次戦線へ。ステップモード時はチェックしても効かない。
            GUI.enabled = _spectateSpeed > 0f;
            _spectateAutoAdvance = GUILayout.Toggle(_spectateAutoAdvance, " 自動進行", GUILayout.Width(96));
            GUI.enabled = true;

            GUILayout.Space(12);

            // §11.5 Step 4-13a A4：ターゲット線トグル。ステップ／×1 のみ表示可（×2/×4 は情報過多になるため自動抑止）。
            bool canShowLines = _spectateSpeed <= 1f;
            GUI.enabled = canShowLines;
            _showTargetLines = GUILayout.Toggle(_showTargetLines, " ターゲット線", GUILayout.Width(116));
            GUI.enabled = true;
            // 実効的な表示可否は「速度条件 AND トグル」。観戦ビューにフレーム単位で渡す。
            _spectator.ShowTargetLines = canShowLines && _showTargetLines;

            GUILayout.Space(12);

            // ステップモード時のみ「次イベント →」ボタンを表示
            if (_spectateSpeed <= 0f)
            {
                GUI.enabled = !_spectator.IsFinished;
                if (GUILayout.Button("次イベント →", GUILayout.Width(140), GUILayout.Height(36)))
                    _spectator.StepOne();
                GUI.enabled = true;
                GUILayout.Space(8);
            }

            GUI.enabled = !_spectator.IsFinished;
            if (GUILayout.Button("この戦闘をスキップ", GUILayout.Width(160), GUILayout.Height(36)))
                _spectator.SkipToEnd();
            GUI.enabled = true;

            GUI.enabled = _spectator.IsFinished;
            string nextLabel = (_spectateQueue?.Count ?? 0) > 0 ? "次の戦線 →" : "結果へ →";
            if (GUILayout.Button(nextLabel, GUILayout.Width(160), GUILayout.Height(36)))
                AdvanceSpectator();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("すべてスキップ", GUILayout.Width(140), GUILayout.Height(36)))
            {
                _spectator.SkipToEnd();
                if (_spectateQueue != null) _spectateQueue.Clear();
                _currentSpectating = null;
                _flashMessage = "観戦をスキップしました。結果を確認してください。";
                TransitionAfterBattleResolution();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>速度トグル1個分。アクティブ時は青色背景でハイライト。</summary>
        private void DrawSpeedToggle(float speed, string label)
        {
            bool selected = Mathf.Approximately(_spectateSpeed, speed);
            var prev = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(label, GUILayout.Width(48), GUILayout.Height(36)))
                _spectateSpeed = speed;
            GUI.backgroundColor = prev;
        }

        // ══════════════════════════════════════════════
        // ヘッダ・メッセージ
        // ══════════════════════════════════════════════

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(GuiTheme.Panel, GUILayout.Height(40));

            // ラウンド表示（大見出し）
            GUILayout.Label(
                $"R{_state.CurrentRound}/{_state.Config.MaxRounds}",
                GuiTheme.Title, GUILayout.Width(96));

            // 行動力（数値強調）
            GUILayout.Label("行動力", GuiTheme.Muted, GUILayout.Width(48));
            GUILayout.Label(
                $"{_state.ActionPoints}/{_state.Config.ActionPointsPerRound}",
                GuiTheme.Stat, GUILayout.Width(56));

            GUILayout.Space(12);

            // フェーズタグ（色分けチップ）
            string phaseLabel = PhaseLabel(_phase);
            GuiTheme.DrawChip(phaseLabel, GuiTheme.PhaseColor(phaseLabel), 120f, 28f);

            GUILayout.Space(12);

            // 戦線累計点数の表示
            foreach (var f in _state.Battlefronts)
                GUILayout.Label(
                    $"{f.DisplayName} {f.CumulativePoints}/{f.PointCap} (拠Lv{f.BaseLevel})",
                    GuiTheme.Body, GUILayout.Width(150));

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("リセット", GUILayout.Width(80), GUILayout.Height(30))) StartGameDirect();
            GUILayout.EndHorizontal();
        }

        private void DrawFlashMessage()
        {
            if (string.IsNullOrEmpty(_flashMessage)) return;
            GUILayout.Label(_flashMessage, GuiTheme.Body);
        }

        /// <summary>
        /// §11.5 Step 4-5 A案：現在フェーズの操作説明枠。初見プレイヤーが何をすべきか直接読める。
        /// 動画撮影時に邪魔なら右上のボタンで折り畳める。
        /// </summary>
        private void DrawPhaseHelp()
        {
            string phaseLabel = PhaseLabel(_phase);
            string help = GuideContent.GetPhaseHelp(phaseLabel);
            if (string.IsNullOrEmpty(help)) return;

            GUILayout.BeginVertical(GuiTheme.Panel);
            GUILayout.BeginHorizontal();
            GuiTheme.DrawChip("? このフェーズで何をする", GuiTheme.AccentPrimary, 200f, 24f);
            GUILayout.FlexibleSpace();
            string toggleLabel = _phaseHelpCollapsed ? "▼ 展開" : "▲ 隠す";
            if (GUILayout.Button(toggleLabel, GUILayout.Width(80), GUILayout.Height(24)))
                _phaseHelpCollapsed = !_phaseHelpCollapsed;
            GUILayout.EndHorizontal();
            if (!_phaseHelpCollapsed)
            {
                GUILayout.Space(2);
                GUILayout.Label(help, GuiTheme.Body);
            }
            GUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════
        // フェーズ別 UI
        // ══════════════════════════════════════════════

        private void DrawPhasePanel()
        {
            switch (_phase)
            {
                case Phase.InitialDraft: DrawInitialDraftUI(); break;
                case Phase.RoundDraft:   DrawRoundDraftUI(); break;
                case Phase.Interior:     DrawInteriorUI(); break;
                case Phase.Assignment:   DrawAssignmentUI(); break;
                case Phase.BattleResult: DrawBattleResultUI(); break;
                case Phase.GameEnd:      DrawGameEndUI(); break;
            }
        }

        // ── 初期ドラフト（姫騎士固定加入＋通常プール 2回・§9.5 改訂 2026-06-02）──
        private void DrawInitialDraftUI()
        {
            GUILayout.Label($"初期ドラフト {_initialDraftIndex + 1}/{_state.Config.InitialDraftPicks}（姫騎士は既に加入済）",
                GUI.skin.box);

            DrawDraftCandidates(picked =>
            {
                _initialDraftIndex++;
                if (_initialDraftIndex >= _state.Config.InitialDraftPicks)
                {
                    // 初期ドラフト完了 → R1 開始
                    Stage3RoundManager.StartRound(_state, () => _rng.Next());
                    _phase = Phase.RoundDraft;
                    _currentOffer = _draftService.DrawRoundStartPick(_state);
                    _flashMessage = "R1開始。ラウンド開始ドラフトを選択してください。";
                }
                else
                {
                    _currentOffer = _draftService.DrawInitialPick(_state, _initialDraftIndex);
                    _flashMessage = $"次は {_initialDraftIndex + 1} 回目のドラフトです。";
                }
            });
        }

        // ── ラウンド開始ドラフト ──
        private void DrawRoundDraftUI()
        {
            GUILayout.Label($"R{_state.CurrentRound} ラウンド開始ドラフト" + (_currentOffer.IsRare ? "（★レア）" : ""),
                GUI.skin.box);

            DrawDraftCandidates(picked =>
            {
                _phase = Phase.Interior;
                _flashMessage = "内政フェーズ：行動力2を消費して4コマンドから選択（同一アクション禁止）。";
            });
        }

        private void DrawDraftCandidates(Action<Unit> onPicked)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _currentOffer.Candidates.Count; i++)
            {
                var c = _currentOffer.Candidates[i];
                if (DrawUnitCard(c, $"候補{i + 1}"))
                {
                    var picked = _draftService.AcceptPick(_state, _currentOffer, i);
                    onPicked(picked);
                    return;
                }
                GUILayout.Space(4);
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// §11.5 Step 4-5c：ユニット詳細カード。役割・ステータス・技・おすすめを縦に並べる。
        /// 採用ボタンの押下で true を返す。ドラフトの3択それぞれを並べて表示する。
        /// </summary>
        /// <param name="u">対象ユニット</param>
        /// <param name="label">カード上部に出すラベル（「候補1」等）</param>
        private bool DrawUnitCard(Unit u, string label)
        {
            const float cardW = 230f;
            GUILayout.BeginVertical(GuiTheme.Panel, GUILayout.Width(cardW), GUILayout.MinHeight(300));

            // 候補ラベル＋アイコン＋名前
            GuiTheme.DrawChip(label, GuiTheme.AccentPrimary, 80f, 22f);
            // §11.5 Step 4-1：Resources/Icons/{unitId}.png があれば左に80×80で表示。
            // 無ければ名前だけのまま（横幅を圧縮しない）。
            // チビ全身素材は1:1 でもポーズ＋背景が見えるサイズが望ましいため 80px。
            if (IconRegistry.Get(u.Id) != null)
            {
                GUILayout.BeginHorizontal();
                var iconRect = GUILayoutUtility.GetRect(80f, 80f, GUILayout.Width(80), GUILayout.Height(80));
                IconRegistry.TryDrawIcon(iconRect, u.Id);
                GUILayout.Label(u.Name, GuiTheme.Subtitle);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(u.Name, GuiTheme.Subtitle);
            }

            // 役割・射程・属性のチップ列
            var guide = GuideContent.GetUnitGuide(u.Id);
            GUILayout.BeginHorizontal();
            if (guide != null)
                GuiTheme.DrawChip(guide.RoleLabel, GuiTheme.AccentSuccess, 120f, 22f);
            GuiTheme.DrawChip(GuideContent.RangeLabel(u.Range),
                GuiTheme.TextMuted, 56f, 22f);
            GUILayout.EndHorizontal();

            if (u.UnitElement != Element.None)
            {
                GUILayout.BeginHorizontal();
                GuiTheme.DrawChip("属性 " + GuideContent.ElementLabel(u.UnitElement),
                    GuiTheme.AccentWarning, 80f, 22f);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // ステータス（簡潔1行）
            GUILayout.Label(
                $"HP {u.MaxHP} / ATK {u.BaseATK} / PDEF {u.PDEF} / MDEF {u.MDEF} / SPD {u.BaseSPD}",
                GuiTheme.Body);

            // 役割サマリ
            if (guide != null && !string.IsNullOrEmpty(guide.Summary))
            {
                GUILayout.Space(2);
                GUILayout.Label("▼ 役割", GuiTheme.Muted);
                GUILayout.Label(guide.Summary, GuiTheme.Body);
            }

            // 技一覧
            if (u.BaseWazas != null && u.BaseWazas.Count > 0)
            {
                GUILayout.Space(2);
                GUILayout.Label("▼ 技", GuiTheme.Muted);
                foreach (var w in u.BaseWazas)
                    GUILayout.Label("・" + GuideContent.WazaOneLineSummary(w), GuiTheme.Body);
            }
            else
            {
                GUILayout.Space(2);
                GUILayout.Label("▼ 技", GuiTheme.Muted);
                GUILayout.Label("・通常攻撃のみ", GuiTheme.Body);
            }

            // おすすめ（「—」は省略）
            if (guide != null && !string.IsNullOrEmpty(guide.Recommendation) && guide.Recommendation != "—")
            {
                GUILayout.Space(2);
                GUILayout.Label("▼ おすすめ", GuiTheme.Muted);
                GUILayout.Label(guide.Recommendation, GuiTheme.Body);
            }

            GUILayout.FlexibleSpace();

            // 採用ボタン
            bool picked = GUILayout.Button("この兵を選ぶ",
                GuiTheme.PrimaryButton, GUILayout.Height(36));

            GUILayout.EndVertical();
            return picked;
        }

        // ── 内政 ──
        private void DrawInteriorUI()
        {
            GUILayout.Label("内政フェーズ", GUI.skin.box);

            // 招集オファーが残っていれば消化
            if (_currentOffer != null && _pendingAction == InteriorActionKind.Conscript)
            {
                GUILayout.Label("招集ドラフト：3択から1体選択" + (_currentOffer.IsRare ? "（★レア）" : ""));
                DrawDraftCandidates(picked =>
                {
                    _pendingAction = null;
                    _currentOffer = null;
                    _flashMessage = $"{picked.Name} を招集しました。";
                });
                return;
            }

            // 4コマンドボタン
            GUILayout.BeginHorizontal();
            // 偵察：戦線選択不要・1コマンドで全戦線開示（§10.8 新仕様）
            GUI.enabled = _interiorService.CanScout(_state) && _pendingAction == null;
            if (GUILayout.Button("偵察（全戦線）", GUILayout.Width(150)))
            {
                if (_interiorService.ExecuteScout(_state))
                    _flashMessage = "3戦線すべての敵編成を可視化しました。";
                else _flashMessage = "偵察できません。";
            }

            GUI.enabled = _state.CanExecuteInteriorAction(InteriorActionKind.UpgradeBase) && _pendingAction == null;
            if (GUILayout.Button("拠点強化（戦線選択）", GUILayout.Width(150)))
                _pendingAction = InteriorActionKind.UpgradeBase;

            GUI.enabled = _state.CanExecuteInteriorAction(InteriorActionKind.UpgradeUnitType) && _pendingAction == null;
            if (GUILayout.Button("兵種強化（兵種選択）", GUILayout.Width(150)))
                _pendingAction = InteriorActionKind.UpgradeUnitType;

            GUI.enabled = _state.CanExecuteInteriorAction(InteriorActionKind.Conscript) && _pendingAction == null;
            if (GUILayout.Button("招集（3択ドラフト）", GUILayout.Width(150)))
            {
                _currentOffer = _interiorService.ExecuteConscript(_state);
                if (_currentOffer != null) _pendingAction = InteriorActionKind.Conscript;
                else _flashMessage = "招集を実行できません。";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // 保留中のアクション対象選択UI（偵察は対象選択が不要なので分岐なし）
            if (_pendingAction == InteriorActionKind.UpgradeBase) DrawInteriorTargetFront("拠点強化対象戦線：", front =>
            {
                if (_interiorService.ExecuteUpgradeBase(_state, front))
                    _flashMessage = $"{front.DisplayName} の拠点強化 → Lv{front.BaseLevel}";
                else _flashMessage = "拠点強化できません（Lv上限？）";
                _pendingAction = null;
            });
            if (_pendingAction == InteriorActionKind.UpgradeUnitType) DrawInteriorTargetUnitType();

            GUILayout.Space(8);
            if (GUILayout.Button("内政終了 → 配置フェーズへ", GuiTheme.PrimaryButton, GUILayout.Width(260), GUILayout.Height(40)))
            {
                _phase = Phase.Assignment;
                _selectedUnit = null;
                _flashMessage = "配置フェーズ：手駒を戦線スロットに配置。配置不要なら戦闘実行へ。";
            }
        }

        private void DrawInteriorTargetFront(string label, Action<Battlefront> onPicked)
        {
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            foreach (var f in _state.Battlefronts)
            {
                if (GUILayout.Button(f.DisplayName, GUILayout.Width(110)))
                {
                    onPicked(f);
                    return;
                }
            }
            if (GUILayout.Button("キャンセル", GUILayout.Width(100))) _pendingAction = null;
            GUILayout.EndHorizontal();
        }

        private void DrawInteriorTargetUnitType()
        {
            GUILayout.Label("兵種強化対象：");
            // ロスター内の兵種をユニーク列挙（姫騎士は兵種強化対象外なので除外）
            var uniqueTypes = _state.Roster
                .Select(u => u.Id)
                .Where(id => id != Stage3Roster.PrincessId)
                .Distinct()
                .ToList();
            GUILayout.BeginHorizontal();
            int col = 0;
            foreach (var typeId in uniqueTypes)
            {
                int curLv = _state.GetUnitTypeEnhancementLevel(typeId);
                bool canUpgrade = curLv < 2;
                string label = $"{TypeLabel(typeId)} Lv{curLv}{(canUpgrade ? "" : "*")}";
                GUI.enabled = canUpgrade;
                if (GUILayout.Button(label, GUILayout.Width(140)))
                {
                    if (_interiorService.ExecuteUpgradeUnitType(_state, typeId))
                        _flashMessage = $"{TypeLabel(typeId)} を強化 → Lv{_state.GetUnitTypeEnhancementLevel(typeId)}";
                    else _flashMessage = "兵種強化できません。";
                    _pendingAction = null;
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    return;
                }
                GUI.enabled = true;
                if (++col % 5 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            if (GUILayout.Button("キャンセル", GUILayout.Width(100))) _pendingAction = null;
            GUILayout.EndHorizontal();
        }

        // ── 配置 ──
        // 配置フローを 2クリックに短縮：手駒選択 → 戦線パネル内のスロットを直接クリック。
        // 戦線パネル側 (DrawSingleFront) に常時 6スロットのボタンを表示している。
        // 占有スロットをクリックすると配置解除（手駒に戻る）。
        private void DrawAssignmentUI()
        {
            GUILayout.Label("配置フェーズ", GUI.skin.box);
            if (_selectedUnit == null)
                GUILayout.Label("上の手駒から配置するユニットを選択 → 右の戦線パネルでスロットをクリック。占有スロットのクリックで解除。");
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"配置するユニット：{_selectedUnit.Name}（戦線パネルの空きスロットをクリック）");
                if (GUILayout.Button("選択解除", GUILayout.Width(100))) _selectedUnit = null;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("戦闘実行 → ", GuiTheme.PrimaryButton, GUILayout.Width(260), GUILayout.Height(40)))
                ExecuteBattles();
        }

        private void ExecuteBattles()
        {
            _lastResolutions = Stage3RoundManager.ResolveAllBattles(_state, () => _rng.Next(0, 100));
            _battleLogPreview = BuildBattleLogPreview(_lastResolutions);

            // §11.3 戦闘観戦：BattleReport を持つ戦線（実際に交戦が発生した戦線）を順に再生する。
            // 完全放置・敵不在の戦線はリポートが無いので観戦キューに入れず、結果表示で扱う。
            _spectateQueue = new Queue<Stage3FrontResolution>();
            foreach (var r in _lastResolutions)
                if (r.BattleReport != null) _spectateQueue.Enqueue(r);

            if (_spectateQueue.Count > 0)
            {
                AdvanceSpectator();
                _phase = Phase.Spectating;
                _flashMessage = "戦闘開始。各戦線の戦況を観戦してください。";
            }
            else
            {
                // 交戦なし（全戦線が放置 or 敵不在）→ 直接結果表示／決着時はエンディングへ
                _flashMessage = "全戦線で交戦なし。結果を確認してください。";
                TransitionAfterBattleResolution();
            }
        }

        /// <summary>観戦キューから次の戦線レポートを取り出して再生開始する。空ならフェーズ遷移。</summary>
        private void AdvanceSpectator()
        {
            if (_spectateQueue == null || _spectateQueue.Count == 0)
            {
                _currentSpectating = null;
                _flashMessage = "全戦線の観戦終了。結果を確認してください。";
                TransitionAfterBattleResolution();
                return;
            }
            _currentSpectating = _spectateQueue.Dequeue();
            _spectator.Initialize(_currentSpectating.BattleReport, FrontDisplayName(_currentSpectating.Battlefront));
        }

        private string FrontDisplayName(BattlefrontKind kind)
        {
            var f = _state.Battlefronts.Find(x => x.Kind == kind);
            return f != null ? f.DisplayName : kind.ToString();
        }

        // §11.3 観戦中は毎フレームイベントを進める。倍速・自動進行もここで処理。
        private float _autoAdvanceTimer;
        private const float AutoAdvanceDelay = 1.2f; // 戦闘終了演出を見せる猶予時間

        private void Update()
        {
            // §11.5 Step 4-9 C3：演出フェーズ（Title/Intro/BossPrologue/Ending*）の Tick。
            // StoryProgress のフェード進行を毎フレーム更新し、
            // 自動進行ページ（DisplaySeconds > 0）は自然に次へ送る。
            if (_phase == Phase.Title || _phase == Phase.Intro
                || _phase == Phase.BossPrologue
                || _phase == Phase.EndingDefeat || _phase == Phase.EndingVictory)
            {
                if (!_storyProgress.IsFinished)
                    _storyProgress.Tick(Time.deltaTime);
                return;
            }

            if (_phase != Phase.Spectating || _currentSpectating == null) return;

            // ステップモード（_spectateSpeed == 0）では Tick せず、自動進行も停止。
            // 「次イベント →」ボタン押下で StepOne を呼ぶ手動送り。
            if (_spectateSpeed <= 0f)
            {
                _autoAdvanceTimer = 0f;
                return;
            }

            // 倍速設定：基準 0.5s/イベント を _spectateSpeed で割る
            _spectator.SecondsPerEvent = 0.5f / _spectateSpeed;

            if (!_spectator.IsFinished)
            {
                _spectator.Tick(Time.deltaTime);
                _autoAdvanceTimer = 0f;
            }
            else if (_spectateAutoAdvance)
            {
                _autoAdvanceTimer += Time.deltaTime;
                if (_autoAdvanceTimer >= AutoAdvanceDelay)
                {
                    _autoAdvanceTimer = 0f;
                    AdvanceSpectator();
                }
            }
        }

        // ── 戦闘結果 ──
        private void DrawBattleResultUI()
        {
            GUILayout.Label("戦闘結果", GUI.skin.box);
            if (_lastResolutions != null)
            {
                foreach (var r in _lastResolutions)
                {
                    string label = r.BattleReport == null
                        ? (r.AlliesAbandoned ? "完全放置完敗 +2点"
                                              : "戦闘なし（敵編成なし） 0点")
                        : $"{ResultLabel(r.BattleReport.Result)} +{r.PointsGained}点 ({r.BattleReport.Turns}T)";
                    GUILayout.Label($"{r.Battlefront} : {label}");
                }
            }

            if (GUILayout.Button("次のラウンドへ →", GuiTheme.PrimaryButton, GUILayout.Width(260), GUILayout.Height(40)))
            {
                if (Stage3RoundManager.AdvanceToNextRound(_state))
                {
                    Stage3RoundManager.StartRound(_state, () => _rng.Next());
                    // R7 はドラフトなし。劇場演出（StartBossPrologue）を挟んでから Interior へ。
                    // 完了コールバックで Phase.Interior に遷移する。
                    if (_state.CurrentRound >= _state.Config.MaxRounds)
                    {
                        StartBossPrologue();
                    }
                    else
                    {
                        _phase = Phase.RoundDraft;
                        _currentOffer = _draftService.DrawRoundStartPick(_state);
                        _flashMessage = $"R{_state.CurrentRound} 開始。ラウンド開始ドラフトを選択。";
                    }
                }
                else
                {
                    // 全ラウンド消化済 → エンディング演出（勝利／敗北で分岐）
                    TransitionAfterBattleResolution();
                }
            }
        }

        // ── ゲーム終了 ──
        // 注：C5 で EndingDefeat/EndingVictory 演出フェーズが導入されたため
        // 通常経路ではここに来ない（TransitionAfterBattleResolution 経由でエンディング演出へ）。
        // GameEnd は過渡的に保持（将来削除予定）。万が一来た場合のフォールバックとして
        // 既存のサマリー表示＋「もう一度遊ぶ」でタイトルへ戻す。
        private void DrawGameEndUI()
        {
            string title = _state.Result == Stage3CampaignResult.Cleared ? "★ クリア ★"
                : _state.Result == Stage3CampaignResult.FrontLost ? "戦線崩壊（ゲームオーバー）"
                : _state.Result == Stage3CampaignResult.BossLost ? "ボス戦敗北（ゲームオーバー）"
                : "進行中";
            GUILayout.Label(title, GUI.skin.box);

            if (_lastResolutions != null)
                foreach (var r in _lastResolutions)
                {
                    string label = r.BattleReport == null
                        ? (r.AlliesAbandoned ? "完全放置完敗 +2点" : "戦闘なし 0点")
                        : $"{ResultLabel(r.BattleReport.Result)} +{r.PointsGained}点";
                    GUILayout.Label($"{r.Battlefront} : {label}");
                }

            // C5：このフォールバック経路から抜ける時はタイトル経由に統一する
            // （通常経路はエンディング演出から StartGameDirect/StartFromTitle が呼ばれるため、ここには来ない）
            if (GUILayout.Button("タイトルへ", GuiTheme.PrimaryButton, GUILayout.Width(180), GUILayout.Height(40)))
                StartFromTitle();
        }

        // ══════════════════════════════════════════════
        // 戦線パネル
        // ══════════════════════════════════════════════

        private void DrawFrontsPanel()
        {
            GUILayout.Label("戦線（3）", GUI.skin.box);
            foreach (var f in _state.Battlefronts)
                DrawSingleFront(f);
        }

        private void DrawSingleFront(Battlefront front)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"【{front.DisplayName}】点数 {front.CumulativePoints}/{front.PointCap}  拠Lv{front.BaseLevel}/{front.MaxBaseLevel}  {(front.IsScouted ? "[偵察済]" : "[未偵察]")}");

            // 難度区分（弱/中/強/ボス）はラウンドで事前に確定するので常に表示。
            // パターン詳細（編成・敵なしか否か）は偵察情報なので IsScouted=true でだけ開示。
            string tier = TierShort(front.PatternTier);
            if (front.IsScouted)
            {
                string pat = front.PatternTier == EnemyPatternTier.None ? "敵なし" : front.PatternLabel;
                GUILayout.Label($"{tier}: {pat}");

                if (front.EnemyComposition.Count > 0)
                {
                    var summary = front.EnemyComposition
                        .OrderBy(e => e.SlotIndex)
                        .Select(e => $"{(e.SlotIndex < 3 ? "前" : "後")}{e.SlotIndex}:{e.BaseUnit.Name}");
                    GUILayout.Label("敵: " + string.Join(", ", summary));
                }
            }
            else
            {
                GUILayout.Label($"{tier}: ？（未偵察）");
            }

            // 配置フェーズではスロット6つを常時表示してクリックで配置／解除を即時できる。
            // 他のフェーズではサマリのみ。
            if (_phase == Phase.Assignment)
            {
                DrawFrontSlots(front);
            }
            else if (front.AssignedAllies.Count > 0)
            {
                var summary = front.AssignedAllies
                    .OrderBy(a => a.SlotIndex)
                    .Select(a => $"{(a.SlotIndex < 3 ? "前" : "後")}{a.SlotIndex}:{a.BaseUnit.Name}");
                GUILayout.Label("味方: " + string.Join(", ", summary));
            }
            else GUILayout.Label("味方: 未配置");

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 戦線パネル内に味方スロット6つ（前列3＋後列3の2行）を直接ボタン表示する。
        /// 空き＋手駒選択中 → 青背景＝配置可。占有 → グレー背景＝クリックで解除。
        /// 配置フェーズでだけ呼ばれる。
        /// </summary>
        private void DrawFrontSlots(Battlefront front)
        {
            // 旧「味方スロット（クリックで配置/解除）:」ラベルは削除。
            // 左カラムのフェーズヘルプ枠で同じ情報が伝わるため冗長。
            GUILayout.BeginHorizontal();
            for (int slot = 0; slot < 3; slot++) DrawSlotButton(front, slot);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int slot = 3; slot < 6; slot++) DrawSlotButton(front, slot);
            GUILayout.EndHorizontal();
        }

        private void DrawSlotButton(Battlefront front, int slot)
        {
            var occupied = front.AssignedAllies.FirstOrDefault(a => a.SlotIndex == slot);
            string prefix = slot < 3 ? "前" : "後";

            // 1行表示で縦圧縮：「前0：空」「前0：姫騎士」の形。
            string label;
            var prev = GUI.backgroundColor;
            if (occupied != null)
            {
                label = $"{prefix}{slot}：{occupied.BaseUnit.Name}";
                GUI.backgroundColor = Color.gray;
            }
            else
            {
                label = $"{prefix}{slot}：空";
                if (_selectedUnit != null) GUI.backgroundColor = Color.cyan; // 配置可能を示唆
            }

            if (GUILayout.Button(label, GUILayout.Width(100), GUILayout.Height(32)))
            {
                if (occupied != null)
                {
                    // 占有スロットのクリック＝解除（手駒に戻る）
                    front.AssignedAllies.Remove(occupied);
                    _flashMessage = $"{occupied.BaseUnit.Name} を解除しました。";
                }
                else if (_selectedUnit != null)
                {
                    if (Stage3RoundManager.AssignUnit(_state, front.Kind, _selectedUnit, slot))
                    {
                        _flashMessage = $"{_selectedUnit.Name} を {front.DisplayName} {prefix}{slot} に配置。";
                        _selectedUnit = null;
                    }
                    else _flashMessage = "配置できません。";
                }
                else
                {
                    _flashMessage = "先に手駒を選択してください。";
                }
            }
            GUI.backgroundColor = prev;
        }

        // ══════════════════════════════════════════════
        // 手駒パネル
        // ══════════════════════════════════════════════

        private void DrawRosterPanel()
        {
            GUILayout.Label($"手駒（{_state.Roster.Count}体）", GUI.skin.box);
            // チビアイコン＋名前表示にしたので少し縦を確保（90→112→144）。HPは観戦中に出るので削除。
            // §11.5 Step 4-13a Phase 2 アート改善（2026-06-04）：iconSize 64→96 拡大に追従して 112→144 へ。
            // Canvas 1920×1080 化で縦に余裕ができたため、縦圧縮の問題は再発しない。
            // 配置済/休養中/負傷は背景色＋下のミニラベルで表現。
            //
            // §11.5 Step 4-13a UI スケーリング対応（2026-06-04）：
            // ピクセル固定 144 → Screen.height 比率（基準 1080 で 144）。
            // ブラウザのリサイズ・全画面切替に追従して手駒パネル全体が伸縮する。
            float panelH = Mathf.Max(120f, Screen.height * (144f / 1080f));
            _rosterScroll = GUILayout.BeginScrollView(_rosterScroll, GUILayout.Height(panelH));
            GUILayout.BeginHorizontal();

            // §11.5 改修：仕様書順（前衛→後衛→ボス→雑魚）で表示。配置時の探索コストを下げる。
            // 同 ID 内では取得順を保つため Select((u,i)→pair) で原順インデックスを副キーに使う。
            var rosterSorted = _state.Roster
                .Select((u, i) => new { u, idx = i })
                .OrderBy(x => GuideContent.GetSpecOrderIndex(x.u.Id))
                .ThenBy(x => x.idx)
                .Select(x => x.u);

            foreach (var u in rosterSorted)
            {
                DrawRosterUnit(u);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 手駒1体ぶんの描画。チビアイコン＋名前＋状態バッジで構成し、
        /// 配置済/休養中/負傷は背景色で区別する（HPテキストは戦闘観戦中に出るので冗長として削除）。
        /// 配置フェーズで未配置・非休養なら、クリックで _selectedUnit にセット。
        /// </summary>
        private void DrawRosterUnit(Unit u)
        {
            // §11.5 Step 4-13a Phase 2 アート改善（2026-06-04）：
            // 配置画面のアイコンは GPT イラストの縮小劣化が目立つため、64→96 に拡大。
            // Canvas 1920×1080 化と組み合わせて、戦闘画面と配置画面の両方で
            // GPT 原画 1024px からの縮小比を緩和し、モザイク化を回避する。
            //
            // §11.5 Step 4-13a UI スケーリング対応（2026-06-04）：
            // const → Screen.height 比率化（基準 1080）。
            // ブラウザ拡大時にアイコンも追従し、バッジ（親矩形相対）と一緒に大きくなる。
            // 最小値は const 時代の値の 80% を下限として確保し、極端な縮小時の崩れを防ぐ。
            float h = Screen.height;
            float iconSize = Mathf.Max(76f, h * (96f / 1080f));
            float buttonW  = Mathf.Max(100f, h * (124f / 1080f));
            float buttonH  = Mathf.Max(106f, h * (132f / 1080f));

            bool assigned = _state.Battlefronts.Any(f => f.AssignedAllies.Any(a => a.BaseUnit == u));
            bool resting = u.InjuryStatus == InjuryState.Resting;
            bool injured = u.InjuryStatus == InjuryState.Injured;
            bool isSelected = _selectedUnit == u;

            var prevBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = Color.cyan;
            else if (resting) GUI.backgroundColor = new Color(0.6f, 0.4f, 0.4f); // 休養中は赤茶
            else if (assigned) GUI.backgroundColor = Color.gray;

            // ボタン全体の Rect を確保。アイコンとテキストはこの Rect 内に手で配置する。
            var rect = GUILayoutUtility.GetRect(buttonW, buttonH,
                GUILayout.Width(buttonW), GUILayout.Height(buttonH));

            bool clicked = GUI.Button(rect, GUIContent.none);

            // アイコン：上部に 64×64 で表示（無ければ空白）
            var iconRect = new Rect(
                rect.x + (rect.width - iconSize) * 0.5f,
                rect.y + 4,
                iconSize, iconSize);
            IconRegistry.TryDrawIcon(iconRect, u.Id);

            // §11.5 Step 4-13a A11+B1：射程／かばうバッジをアイコン左上に重ねる（観戦中と完全に同じ見た目）。
            UnitBadgeOverlay.Draw(iconRect, u);

            // 名前＋強化Lv（中央寄せ・1行）
            string nameText = u.Name + (u.EnhancementLevel > 0 ? $"+{u.EnhancementLevel}" : "");
            var nameRect = new Rect(rect.x + 2, iconRect.yMax + 2, rect.width - 4, 16);
            var nameStyle = GUI.skin.label;
            var prevAlign = nameStyle.alignment;
            nameStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(nameRect, nameText, nameStyle);
            nameStyle.alignment = prevAlign;

            // 状態バッジ（配置済/休養中/負傷のみ・通常は省略）
            string badge = assigned ? "[配置済]"
                : resting ? "[休養中]"
                : injured ? "[負傷]" : null;
            if (badge != null)
            {
                var badgeRect = new Rect(rect.x + 2, nameRect.yMax, rect.width - 4, 14);
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                nameStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(badgeRect, badge, nameStyle);
                nameStyle.alignment = prevAlign;
                GUI.color = prevColor;
            }

            GUI.backgroundColor = prevBg;

            // §11.5 Step 4-13 サンプル先行（A14 試作）：姫騎士のみホバー → ツールチップ。
            // Repaint 時にだけ Contains 判定し、ヒットしたら ID と矩形をフレーム保存。
            // 描画はフレーム末尾の DrawTooltipOverlay で行う（最前面確保のため）。
            if (u.Id == Stage3Roster.PrincessId
                && Event.current.type == EventType.Repaint
                && rect.Contains(Event.current.mousePosition))
            {
                _tooltipUnitId = u.Id;
                _tooltipAnchorRect = rect;
            }

            if (clicked)
            {
                if (_phase == Phase.Assignment && !assigned && !resting) _selectedUnit = u;
                else if (resting) _flashMessage = $"{u.Name} は休養中です（このラウンドは配置不可）。";
            }
        }

        private void DrawBattleLogPreview()
        {
            if (string.IsNullOrEmpty(_battleLogPreview)) return;
            GUILayout.Label("戦闘ログ（直近）", GUI.skin.box);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(120));
            GUILayout.TextArea(_battleLogPreview);
            GUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════
        // §11.5 Step 4-13 サンプル先行：ツールチップ試作（A14・姫騎士のみ）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 姫騎士アイコンにホバー中ならステータスをフロート表示する。
        /// OnGUI 末尾で呼ぶことで他の描画より前面に出る（IMGUI は描画順 = Z 順）。
        ///
        /// 【IMGUI 注意】このメソッドは GUILayout 系 API（BeginArea / Label）を一切使わない。
        /// 理由：_tooltipUnitId は Repaint パスでしかセットされないため、Layout パス時と
        /// Repaint パス時で GUILayout の制御数が食い違い「Getting control N's position」例外を起こす。
        /// 絶対座標の GUI.Label / GUI.Box / GUI.DrawTexture は Layout に予約を作らないため、
        /// パス間の整合性問題が原理的に発生しない。
        /// </summary>
        private void DrawTooltipOverlay()
        {
            if (string.IsNullOrEmpty(_tooltipUnitId)) return;
            if (_state == null || _state.Roster == null) return;

            var unit = _state.Roster.FirstOrDefault(x => x.Id == _tooltipUnitId);
            if (unit == null) return;

            const float tooltipW = 280f;
            const float tooltipH = 200f;
            const float gap = 8f;
            const float padX = 10f;
            const float padY = 8f;
            // 日本語フォントの実高さに合わせて余裕を持った行高にする（小さすぎると行が重なる）。
            const float lineH = 22f;  // 通常行の高さ（fontSize 12〜14 の日本語が収まる）
            const float titleH = 28f; // タイトル行の高さ（fontSize 14 bold が収まる）

            // 既定はアンカーの右側に出す。画面右端を超えるなら左側にひっくり返す。
            float tx = _tooltipAnchorRect.xMax + gap;
            if (tx + tooltipW > Screen.width) tx = _tooltipAnchorRect.x - tooltipW - gap;
            if (tx < 0) tx = 0;

            float ty = _tooltipAnchorRect.y;
            if (ty + tooltipH > Screen.height) ty = Screen.height - tooltipH - gap;
            if (ty < 0) ty = 0;

            var tipRect = new Rect(tx, ty, tooltipW, tooltipH);

            // 半透明黒の塗り → 枠線（GUI.DrawTexture / GUI.Box はどちらも Layout に予約しない）
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(tipRect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Box(tipRect, GUIContent.none);
            GUI.color = prevColor;

            // 行ごとに絶対座標で配置（GUILayout を使わない）
            float lineX = tipRect.x + padX;
            float lineW = tipRect.width - padX * 2f;
            float curY = tipRect.y + padY;

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(lineX, curY, lineW, titleH),
                unit.Name + (unit.EnhancementLevel > 0 ? $"  +{unit.EnhancementLevel}" : ""),
                titleStyle);
            curY += titleH;

            GUI.Label(new Rect(lineX, curY, lineW, lineH),
                $"HP {unit.CurrentHP}/{unit.MaxHP}    SPD {unit.BaseSPD}");
            curY += lineH;

            GUI.Label(new Rect(lineX, curY, lineW, lineH),
                $"ATK {unit.BaseATK}    PDEF {unit.PDEF}    MDEF {unit.MDEF}");
            curY += lineH;

            GUI.Label(new Rect(lineX, curY, lineW, lineH),
                $"射程: {RangeLabel(unit.Range)}");
            curY += lineH;

            if (unit.BaseWazas != null && unit.BaseWazas.Count > 0)
            {
                var wazaNames = string.Join("、", unit.BaseWazas.Select(w => w.Name));
                GUI.Label(new Rect(lineX, curY, lineW, lineH), "技: " + wazaNames);
                curY += lineH;
            }
            if (unit.AuraEffect != null)
            {
                GUI.Label(new Rect(lineX, curY, lineW, lineH), "置物オーラ：戦闘開始時に味方全体へバフ");
                curY += lineH;
            }
        }

        private static string RangeLabel(AttackRange r)
        {
            switch (r)
            {
                case AttackRange.Melee:  return "近接（前列で敵前列）";
                case AttackRange.Mid:    return "中射程（敵前列優先）";
                case AttackRange.Ranged: return "遠隔（前後問わず）";
                default: return r.ToString();
            }
        }

        private static string BuildBattleLogPreview(List<Stage3FrontResolution> resolutions)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in resolutions)
            {
                if (r.BattleReport == null) continue;
                sb.AppendLine($"=== {r.Battlefront} ===");
                sb.AppendLine(r.BattleReport.LogText);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ══════════════════════════════════════════════
        // ヘルパー
        // ══════════════════════════════════════════════

        private static string PhaseLabel(Phase p)
        {
            switch (p)
            {
                case Phase.Title:         return "タイトル";
                case Phase.Intro:         return "導入";
                case Phase.BossPrologue:  return "最終決戦";
                case Phase.EndingDefeat:  return "エンディング";
                case Phase.EndingVictory: return "エンディング";
                case Phase.InitialDraft:  return "初期ドラフト";
                case Phase.RoundDraft:    return "ラウンドドラフト";
                case Phase.Interior:      return "内政";
                case Phase.Assignment:    return "配置";
                case Phase.Spectating:    return "戦闘観戦";
                case Phase.BattleResult:  return "戦闘結果";
                case Phase.GameEnd:       return "終了";
                default: return p.ToString();
            }
        }

        private static string TierShort(EnemyPatternTier t)
        {
            switch (t)
            {
                case EnemyPatternTier.Weak: return "弱";
                case EnemyPatternTier.Medium: return "中";
                case EnemyPatternTier.Strong: return "強";
                case EnemyPatternTier.Boss: return "ボス";
                default: return "—";
            }
        }

        private static string ResultLabel(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory:      return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat:      return "惜敗";
                case BattleResult.CrushingDefeat:      return "完敗";
                default: return "—";
            }
        }

        private static string UnitSummaryShort(Unit u)
        {
            string range = u.Range == AttackRange.Melee ? "近"
                : u.Range == AttackRange.Mid ? "中" : "遠";
            return $"HP{u.MaxHP} ATK{u.BaseATK} ({range})";
        }

        private static string TypeLabel(string typeId)
        {
            // ロスター生成側のラベルが必要だが、簡略のためIDを表示
            switch (typeId)
            {
                case "s3_tank_def":  return "重装兵";
                case "s3_tank_hp":   return "大盾兵";
                case "s3_paladin":   return "騎士";
                case "s3_atk_multi": return "双剣士";
                case "s3_samurai":   return "サムライ";
                case "s3_debuffer":  return "大槌兵";
                case "s3_assassin":  return "アサシン";
                case "s3_archer":    return "弓兵";
                case "s3_ninja":     return "忍者";
                case "s3_firemage":  return "炎魔導士";
                case "s3_aoemage":   return "雷魔導士";
                case "s3_healer":    return "司祭";
                case "s3_medic":     return "巫女";
                case "s3_buffer":    return "踊り子";
                case "s3_tactician": return "軍師";
                case "s3_princess":  return "姫騎士";
                default: return typeId;
            }
        }
    }
}
