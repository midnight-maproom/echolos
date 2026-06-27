// タイトル／導入チュートリアル／R7ボス劇場／敗北・勝利エンディングの
// 状態機械（フェードイン → 表示 → フェードアウト → 次ページ → 完了）を Unity API 非依存で扱う。
//
// 純 C#（Domain asmdef は noEngineReferences=true）。描画は Presentation/StoryOverlay の静的メソッドが
// Progress を読んで行う。ロジック層を分離することで StoryProgressTests でフェード値・遷移・Skip 動作を
// 網羅検証できる。
using System;
using System.Collections.Generic;

namespace Echolos.Domain.Story
{
    /// <summary>
    /// スチル＋ナレ＋フェードの再生コントローラ（純ロジック）。
    /// MonoBehaviour および UnityEngine.* 全般に非依存（Clamp01 は自前実装）。
    /// </summary>
    public sealed class StoryProgress
    {
        private IReadOnlyList<StoryPage> _pages;
        private Action _onComplete;
        private int _pageIndex;
        private StoryStage _stage;
        private float _stageElapsed;
        private bool _completedFired;

        /// <summary>全ページ再生が完了したか。</summary>
        public bool IsFinished => _stage == StoryStage.Done;

        /// <summary>現在再生中のページ index（0 始まり）。終了後は最終 index を保持。</summary>
        public int CurrentPageIndex => _pageIndex;

        /// <summary>現在のページ（IsFinished の場合は null）。</summary>
        public StoryPage CurrentPage =>
            (_pages == null || _pageIndex < 0 || _pageIndex >= _pages.Count || IsFinished)
                ? null : _pages[_pageIndex];

        /// <summary>現在のステージ（FadeIn / Display / FadeOut / Done）。</summary>
        public StoryStage CurrentStage => _stage;

        /// <summary>
        /// 現在のフェード値（0=完全透明, 1=完全表示）。
        /// FadeIn は 0→1 ／ Display は 1 ／ FadeOut は 1→0 ／ Done は 0。
        /// </summary>
        public float CurrentAlpha
        {
            get
            {
                var p = CurrentPage;
                if (p == null) return 0f;
                switch (_stage)
                {
                    case StoryStage.FadeIn:
                        return p.FadeInSeconds <= 0f ? 1f
                            : Clamp01(_stageElapsed / p.FadeInSeconds);
                    case StoryStage.Display:
                        return 1f;
                    case StoryStage.FadeOut:
                        return p.FadeOutSeconds <= 0f ? 0f
                            : Clamp01(1f - _stageElapsed / p.FadeOutSeconds);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// 現在のページが手動送りモード（DisplaySeconds == 0）にあるか。
        /// true の間は Tick では Display を抜けず、NextPage を呼ぶことで FadeOut に進む。
        /// </summary>
        public bool IsWaitingForManualAdvance =>
            _stage == StoryStage.Display
            && CurrentPage != null
            && CurrentPage.DisplaySeconds <= 0f;

        /// <summary>ページ列を渡して初期化。pages が空なら即 Done。</summary>
        public void Initialize(IReadOnlyList<StoryPage> pages, Action onComplete)
        {
            _pages = pages;
            _onComplete = onComplete;
            _pageIndex = 0;
            _stageElapsed = 0f;
            _completedFired = false;

            if (_pages == null || _pages.Count == 0)
            {
                _stage = StoryStage.Done;
                FireCompleteOnce();
                return;
            }

            _stage = StoryStage.FadeIn;
        }

        /// <summary>時間進行。Display 中で手動送りモードなら何もせず待つ。</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || IsFinished || _pages == null || _pages.Count == 0) return;

            // 1 フレームに複数ステージをまたぐ大きな delta も吸収する。
            float remaining = deltaSeconds;
            while (remaining > 0f && !IsFinished)
            {
                var page = _pages[_pageIndex];
                switch (_stage)
                {
                    case StoryStage.FadeIn:
                    {
                        float left = page.FadeInSeconds - _stageElapsed;
                        if (page.FadeInSeconds <= 0f || remaining >= left)
                        {
                            remaining -= Math.Max(0f, left);
                            _stageElapsed = 0f;
                            _stage = StoryStage.Display;
                        }
                        else
                        {
                            _stageElapsed += remaining;
                            remaining = 0f;
                        }
                        break;
                    }
                    case StoryStage.Display:
                    {
                        // 手動送りモードはここで止まる
                        if (page.DisplaySeconds <= 0f)
                        {
                            remaining = 0f;
                            break;
                        }
                        float left = page.DisplaySeconds - _stageElapsed;
                        if (remaining >= left)
                        {
                            remaining -= left;
                            _stageElapsed = 0f;
                            _stage = StoryStage.FadeOut;
                        }
                        else
                        {
                            _stageElapsed += remaining;
                            remaining = 0f;
                        }
                        break;
                    }
                    case StoryStage.FadeOut:
                    {
                        float left = page.FadeOutSeconds - _stageElapsed;
                        if (page.FadeOutSeconds <= 0f || remaining >= left)
                        {
                            remaining -= Math.Max(0f, left);
                            AdvancePage();
                        }
                        else
                        {
                            _stageElapsed += remaining;
                            remaining = 0f;
                        }
                        break;
                    }
                    default:
                        remaining = 0f;
                        break;
                }
            }
        }

        /// <summary>
        /// 手動送り（DisplaySeconds == 0 のページで「次へ」ボタンが押されたとき）。
        /// 現在ページの FadeOut に強制遷移する。Display 中以外で呼ばれた場合は無視。
        /// </summary>
        public void NextPage()
        {
            if (IsFinished || _pages == null || _pages.Count == 0) return;
            if (_stage != StoryStage.Display) return;
            _stage = StoryStage.FadeOut;
            _stageElapsed = 0f;
        }

        /// <summary>全ページをスキップして即完了させる。完了コールバックは一度だけ発火。</summary>
        public void Skip()
        {
            if (IsFinished) return;
            _pageIndex = (_pages == null || _pages.Count == 0) ? -1 : _pages.Count - 1;
            _stage = StoryStage.Done;
            _stageElapsed = 0f;
            FireCompleteOnce();
        }

        private void AdvancePage()
        {
            _pageIndex++;
            _stageElapsed = 0f;
            if (_pageIndex >= _pages.Count)
            {
                _pageIndex = _pages.Count - 1;
                _stage = StoryStage.Done;
                FireCompleteOnce();
                return;
            }
            _stage = StoryStage.FadeIn;
        }

        private void FireCompleteOnce()
        {
            if (_completedFired) return;
            _completedFired = true;
            _onComplete?.Invoke();
        }

        // Domain asmdef は noEngineReferences=true で UnityEngine.Mathf を使えないため、
        // 0〜1 クランプを自前で持つ。System.Math.Clamp は .NET 5+ で .NET Standard 2.1 にはない。
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
