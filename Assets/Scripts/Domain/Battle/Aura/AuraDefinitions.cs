using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Aura
{
    // VSプロトのオーラ定義集約。
    // 数値は暫定で、バランス調整フェーズで再調整する。
    public static class AuraDefinitions
    {
        // 王女「王家の加護」：味方全体 DEF +3（メタ強化「王家の加護 +2」を取るたびに +2 上乗せ）。
        public static readonly AuraDefinition PrincessGuardian = new AuraDefinition(
            UniqueUnitIds.Princess,
            "王家の加護",
            new[] { new AuraBuff(EffectKind.DefenseUp, 3) },
            UpgradeKind.AuraBoost);

        // ブリジット「連携」：王女が同時出撃しているときだけ、ブリジット＋王女の 2 体に ATK +3。
        // メタ強化対象外（BoostUpgradeKind=null）＝ブリジットのメタ強化を取ってもオーラ量は固定 +3。
        public static readonly AuraDefinition BridgetCovenant = new AuraDefinition(
            UniqueUnitIds.Bridget,
            "連携",
            new[] { new AuraBuff(EffectKind.AttackUp, 3) },
            boostUpgradeKind: null,
            requiredPartnerUnitIds: new[] { UniqueUnitIds.Princess },
            targetMode: AuraTargetMode.SelfAndPartners);

        public static readonly IReadOnlyList<AuraDefinition> All = new[]
        {
            PrincessGuardian,
            BridgetCovenant,
        };
    }
}
