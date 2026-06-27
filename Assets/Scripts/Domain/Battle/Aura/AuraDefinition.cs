using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Aura
{
    // 固有ユニット 1 体が陣営に常時付与するオーラの定義。
    // SourceUnitId が陣営に生存していて、かつ RequiredPartnerUnitIds の全員が在席している場合のみ発動。
    // TargetMode が AllAllies なら陣営の生存全員、SelfAndPartners なら SourceUnit と Partners だけが対象。
    // BoostUpgradeKind が指定されていれば、SourceUnit.AppliedUpgrades のうち同 Kind の Magnitude 合計を
    // 全 Buffs.BaseMagnitude に加算する（メタ強化で「王家の加護 +2」を選んだ分だけバフ量が増える）。
    // null（既定）の場合は Boost なし＝メタ強化はオーラ量に影響しない（連携系などはこちら）。
    public sealed class AuraDefinition
    {
        public string SourceUnitId { get; }
        public string SourceAbilityName { get; }
        public IReadOnlyList<AuraBuff> Buffs { get; }
        public UpgradeKind? BoostUpgradeKind { get; }
        public IReadOnlyList<string> RequiredPartnerUnitIds { get; }
        public AuraTargetMode TargetMode { get; }

        public AuraDefinition(
            string sourceUnitId,
            string sourceAbilityName,
            IReadOnlyList<AuraBuff> buffs,
            UpgradeKind? boostUpgradeKind = null,
            IReadOnlyList<string> requiredPartnerUnitIds = null,
            AuraTargetMode targetMode = AuraTargetMode.AllAllies)
        {
            SourceUnitId = sourceUnitId;
            SourceAbilityName = sourceAbilityName;
            Buffs = buffs;
            BoostUpgradeKind = boostUpgradeKind;
            RequiredPartnerUnitIds = requiredPartnerUnitIds ?? System.Array.Empty<string>();
            TargetMode = targetMode;
        }
    }
}
