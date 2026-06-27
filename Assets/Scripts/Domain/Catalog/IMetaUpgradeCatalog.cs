// メタ拠点強化カタログの Domain 抽象。
// 実装は Echolos.Data.MetaUpgradeCatalog で、Resources.LoadAll 経由で SO を引き、
// Domain の完成品 MetaUpgrade に変換して返す（UnitCatalog → Unit と同パターン）。
//
// 【依存方向】
// - Domain → Data の逆依存を避けるため、戻り型は Domain 型（MetaUpgrade）。
//   POCO（MetaUpgradeDefinition / Data.Definitions）は Catalog 実装内部だけが知る。
using System.Collections.Generic;
using Echolos.Domain.Meta;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から MetaUpgrade（Domain 完成品）を引く抽象。</summary>
    public interface IMetaUpgradeCatalog
    {
        /// <summary>ID から MetaUpgrade を返す。未登録 ID は例外。</summary>
        MetaUpgrade Get(string upgradeId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string upgradeId);

        /// <summary>登録済み全 MetaUpgrade（VSプロト範囲では 3 件想定）。</summary>
        IReadOnlyList<MetaUpgrade> GetAll();
    }
}
