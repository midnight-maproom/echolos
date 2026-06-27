// ストーリーシーンカタログの Domain 抽象。
// 実装は Echolos.Data.StorySceneCatalog で、Resources.LoadAll 経由で SO を引き、
// Domain の完成品 StoryScene に変換して返す（UnitCatalog → Unit と同パターン）。
//
// 【依存方向】
// - Domain → Data の逆依存を避けるため、戻り型は Domain 型（StoryScene）。
//   POCO（StorySceneDefinition / Data.Definitions）は Catalog 実装内部だけが知る。
using System.Collections.Generic;
using Echolos.Domain.Story;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から StoryScene（Domain 完成品）を引く抽象。</summary>
    public interface IStorySceneCatalog
    {
        /// <summary>ID から StoryScene を返す。未登録 ID は例外。</summary>
        StoryScene Get(string sceneId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string sceneId);

        /// <summary>登録済み全 StoryScene（VSプロト範囲では 5 件想定）。</summary>
        IReadOnlyList<StoryScene> GetAll();
    }
}
