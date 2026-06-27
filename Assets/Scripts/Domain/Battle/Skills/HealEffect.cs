using System;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // 回復 Effect。仕様 320 §1.3：素回復量 = √(回復役の現在 HP) × wazaPower。
    // 対象側に HealReceivedModifier（SearingWound 等）があれば最終回復に割引を適用：
    //   最終回復 = 素回復 × max(0, 1 - Σ(Magnitude × Stacks) / 100)
    public sealed class HealEffect : IActionEffect
    {
        private readonly double _wazaPower;

        public HealEffect(double wazaPower = 1.0)
        {
            _wazaPower = wazaPower;
        }

        public void Apply(IActionContext context)
        {
            if (context == null) return;
            var actor = context.Actor;
            var targets = context.Targets;
            if (actor == null || targets == null) return;

            // WazaPowerBoost：Actor.AppliedUpgrades から TargetWazaId 一致の Magnitude 合計を wazaPower に加算。
            double wazaPower = _wazaPower + UpgradeMagnitudeResolver.SumWazaPowerBoost(
                actor.BaseUnit, context.CurrentWazaId);

            double actorHp = actor.CurrentHP < 0 ? 0.0 : actor.CurrentHP;
            int baseHeal = (int)Math.Round(Math.Sqrt(actorHp) * wazaPower, MidpointRounding.AwayFromZero);
            if (baseHeal < 0) baseHeal = 0;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                int healAmount = ApplyReceivedReduction(target, baseHeal);

                int before = target.BaseUnit.CurrentHP;
                int after = before + healAmount;
                if (after > target.MaxHP) after = target.MaxHP;
                int effective = after - before;

                if (effective > 0)
                {
                    target.BaseUnit.CurrentHP = after;
                }

                context.Outcomes.Add(new HitOutcome(
                    target: target,
                    healAmount: effective,
                    targetHPAfter: target.BaseUnit.CurrentHP));
            }
        }

        // 対象に付与された HealReceivedModifier を集計し、素回復に割引を乗算。
        private static int ApplyReceivedReduction(RuntimeUnit target, int baseHeal)
        {
            if (baseHeal <= 0) return baseHeal;

            float totalPercent = 0f;
            foreach (var e in target.ActiveEffects)
            {
                if (e is HealReceivedModifier mod)
                    totalPercent += mod.Magnitude * mod.Stacks;
            }
            if (totalPercent <= 0f) return baseHeal;

            double factor = 1.0 - totalPercent / 100.0;
            if (factor < 0.0) factor = 0.0;
            return (int)Math.Round(baseHeal * factor, MidpointRounding.AwayFromZero);
        }
    }
}
