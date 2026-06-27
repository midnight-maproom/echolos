// ストーリーシーンの Domain 完成品クラス。
// Catalog（Data 層）が SO/POCO から組み立てて Presentation/UseCase 層に渡す Domain 型。
// StoryProgress.Initialize に渡す再生ページ列を保持する不変オブジェクト。
using System;
using System.Collections.Generic;

namespace Echolos.Domain.Story
{
    /// <summary>ストーリーシーンの Domain 完成品（StoryProgress に再生させるページ列の不変コンテナ）。</summary>
    public sealed class StoryScene
    {
        /// <summary>SO 主キー（VSprototypeStorySceneIds の const と一致）。</summary>
        public string Id { get; }

        /// <summary>再生ページ列（順序保持）。</summary>
        public IReadOnlyList<StoryPage> Pages { get; }

        /// <summary>
        /// 既見時の短縮ナレーション（1 行・空文字なら未設定扱い）。
        /// 2 回目以降の再生で本文の代わりに 1 ページだけ流すフォールバック文。
        /// </summary>
        public string RepeatNarration { get; }

        public StoryScene(string id, IReadOnlyList<StoryPage> pages, string repeatNarration = "")
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id は必須", nameof(id));
            Id = id;
            Pages = pages ?? Array.Empty<StoryPage>();
            RepeatNarration = repeatNarration ?? string.Empty;
        }
    }
}
