// ストーリーページ 1 ページ分の POCO（SO シリアライズ用）。
// Domain.Story.StoryPage（get-only プロパティ・SO シリアライズ不可）の中間 POCO。
// StorySceneCatalog.BuildScene 内で `new StoryPage(...)` に変換される。
using UnityEngine;

namespace Echolos.Data.Definitions
{
    /// <summary>ストーリーページ 1 ページ分の定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class StoryPageDefinition
    {
        /// <summary>Resources.Load で参照する画像パス（拡張子なし）。空なら単色背景。</summary>
        public string ImagePath;

        /// <summary>ImagePath が見つからない時のフォールバック先（既存スチル等）。</summary>
        public string FallbackImagePath;

        /// <summary>画面下部に表示するナレーション本文（VSプロトは日本語直書き）。</summary>
        [TextArea(2, 5)]
        public string NarrationText;

        /// <summary>フェードイン秒数。</summary>
        public float FadeInSeconds = 0.6f;

        /// <summary>表示秒数（0 で手動送り）。</summary>
        public float DisplaySeconds = 0f;

        /// <summary>フェードアウト秒数。</summary>
        public float FadeOutSeconds = 0.6f;
    }
}
