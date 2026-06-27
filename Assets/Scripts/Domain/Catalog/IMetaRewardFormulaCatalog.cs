// メタ通貨獲得式カタログの Domain 抽象。
// 実装は Echolos.Data.MetaRewardFormulaCatalog で、Resources.LoadAll 経由で SO を引き、
// Domain の完成品 MetaRewardFormula に変換して返す（UnitCatalog → Unit と同パターン）。
//
// 【依存方向】
// - Domain → Data の逆依存を避けるため、戻り型は Domain 型（MetaRewardFormula）。
//   POCO（MetaRewardFormulaDefinition / Data.Definitions）は Catalog 実装内部だけが知る。
using System.Collections.Generic;
using Echolos.Domain.Formula;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から MetaRewardFormula（Domain 完成品）を引く抽象。</summary>
    public interface IMetaRewardFormulaCatalog
    {
        /// <summary>ID から MetaRewardFormula を返す。未登録 ID は例外。</summary>
        MetaRewardFormula Get(string formulaId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string formulaId);

        /// <summary>登録済み全 Formula（VSプロト範囲では 1 件想定）。</summary>
        IReadOnlyList<MetaRewardFormula> GetAll();
    }
}
