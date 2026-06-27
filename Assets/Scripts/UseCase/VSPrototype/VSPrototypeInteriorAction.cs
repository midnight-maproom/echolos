// 内政フェーズで実行可能なアクション種別。
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロト内政フェーズのアクション種別。</summary>
    public enum VSPrototypeInteriorAction
    {
        /// <summary>召集：3択ドラフトで王国軍を1体補強（行動力1・同一ラウンド1回まで）。</summary>
        Conscript,
        /// <summary>ユニット強化：個別ユニットの Lv 強化（行動力1・同一ラウンド1回まで・Lv 上限 3）。</summary>
        UpgradeUnitType,
    }
}
