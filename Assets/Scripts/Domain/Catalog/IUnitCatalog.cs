// ユニットカタログの Domain 抽象。
// 実装は Echolos.Data.UnitCatalog で、Resources.LoadAll 経由で SO を引く。
// UseCase 層はこの抽象を通してユニットを取得する。
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から Unit インスタンスを引く抽象。</summary>
    public interface IUnitCatalog
    {
        /// <summary>ID から Unit インスタンスを生成する（呼ぶたび新しい実体）。</summary>
        Unit Get(string unitId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string unitId);

        /// <summary>登録済みの全 ID。</summary>
        IEnumerable<string> GetAllIds();
    }
}
