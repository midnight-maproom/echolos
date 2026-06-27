using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から UnitUpgrade（Domain 完成品）を引く抽象。</summary>
    public interface IUnitUpgradeCatalog
    {
        /// <summary>ID から UnitUpgrade を生成して返す（呼ぶたび新しい実体）。</summary>
        UnitUpgrade Get(string upgradeId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string upgradeId);

        /// <summary>登録済みの全 ID。</summary>
        IEnumerable<string> GetAllIds();
    }
}
