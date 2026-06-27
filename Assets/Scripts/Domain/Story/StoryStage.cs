namespace Echolos.Domain.Story
{
    /// <summary>ストーリーオーバーレイ 1 ページの再生段階。</summary>
    public enum StoryStage
    {
        FadeIn,
        Display,
        FadeOut,
        Done, // 全ページ再生完了
    }
}
