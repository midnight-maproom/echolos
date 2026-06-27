// ストーリーシーンの POCO（複数ページの集約）。
// ScriptableObject（StorySceneDefinitionSO）と Domain クラス（StoryScene）の中間 POCO。
using System.Collections.Generic;

namespace Echolos.Data.Definitions
{
    /// <summary>ストーリーシーンの定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class StorySceneDefinition
    {
        /// <summary>SO 主キー（VSPrototypeStorySceneIds の const と一致）。</summary>
        public string Id;

        /// <summary>表示名（Inspector 識別用・実行時参照なし）。</summary>
        public string Name;

        /// <summary>再生ページ列（順序保持）。</summary>
        public List<StoryPageDefinition> Pages = new List<StoryPageDefinition>();

        /// <summary>
        /// 既見時の短縮ナレーション（1 行・空文字なら未設定扱い＝通常本文再生にフォールバック）。
        /// 2 回目以降の再生で本文の代わりに 1 ページだけ流すフォールバック文。
        /// </summary>
        public string RepeatNarration = "";
    }
}
