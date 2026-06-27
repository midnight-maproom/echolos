// ドラフトプールカタログの Domain 抽象。
// 実装は Echolos.Data.DraftPoolCatalog で、Resources.LoadAll 経由で SO を引き、
// Domain の完成品 DraftPool に変換して返す（UnitCatalog → Unit と同パターン）。
//
// 【依存方向】
// - Domain → Data の逆依存を避けるため、戻り型は Domain 型（DraftPool）。
//   POCO（DraftPoolDefinition / Data.Definitions）は Catalog 実装内部だけが知る。
using System.Collections.Generic;
using Echolos.Domain.Draft;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から DraftPool（Domain 完成品）を引く抽象。</summary>
    public interface IDraftPoolCatalog
    {
        /// <summary>ID から DraftPool を返す。未登録 ID は例外。</summary>
        DraftPool Get(string poolId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string poolId);

        /// <summary>登録済み全 DraftPool（VSプロト範囲では 1 件想定）。</summary>
        IReadOnlyList<DraftPool> GetAll();
    }
}
