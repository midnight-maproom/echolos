// ストーリーオーバーレイ 1 ページ分の値オブジェクト。StoryProgress が時系列順に再生する。
using System;

namespace Echolos.Domain.Story
{
    /// <summary>ストーリーオーバーレイ 1 ページ分の値オブジェクト。</summary>
    public sealed class StoryPage
    {
        /// <summary>Resources.Load で参照する画像パス（拡張子なし）。空なら単色背景。</summary>
        public string ImagePath { get; }

        /// <summary>
        /// ImagePath の Resources.Load が失敗した時に試すフォールバックパス。
        /// GPT 生成途中の素材（intro_1 / boss_silhouette 等）でも見栄えを維持するため、
        /// 配置済みのキービジュアル等にフォールバックする用途で使う。
        /// </summary>
        public string FallbackImagePath { get; }

        /// <summary>画面下部に表示するナレーション本文。空なら非表示。</summary>
        public string NarrationText { get; }

        /// <summary>フェードイン秒数（0 で即時表示）。</summary>
        public float FadeInSeconds { get; }

        /// <summary>表示秒数（0 で手動送り。Tick では Display ステージに留まる）。</summary>
        public float DisplaySeconds { get; }

        /// <summary>フェードアウト秒数（0 で即時消去）。</summary>
        public float FadeOutSeconds { get; }

        public StoryPage(
            string imagePath,
            string narrationText,
            float fadeInSeconds = 0.6f,
            float displaySeconds = 0f,
            float fadeOutSeconds = 0.6f,
            string fallbackImagePath = null)
        {
            ImagePath = imagePath ?? string.Empty;
            FallbackImagePath = fallbackImagePath ?? string.Empty;
            NarrationText = narrationText ?? string.Empty;
            FadeInSeconds = Math.Max(0f, fadeInSeconds);
            DisplaySeconds = Math.Max(0f, displaySeconds);
            FadeOutSeconds = Math.Max(0f, fadeOutSeconds);
        }
    }
}
