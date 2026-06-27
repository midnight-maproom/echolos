// 技カタログの Domain 抽象。
// 実装は Echolos.Data.WazaCatalog で、Resources.LoadAll 経由で SO を引く。
// UnitCatalog 実装は IWazaCatalog をコンストラクタ注入で受け取る。
using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;

namespace Echolos.Domain.Catalog
{
    /// <summary>ID から Waza インスタンスを引く抽象。</summary>
    public interface IWazaCatalog
    {
        /// <summary>ID から Waza インスタンスを生成する（呼ぶたび新しい実体）。</summary>
        Waza Get(string wazaId);

        /// <summary>指定 ID が登録されているか。</summary>
        bool IsRegistered(string wazaId);

        /// <summary>登録済みの全 ID。</summary>
        IEnumerable<string> GetAllIds();
    }
}
