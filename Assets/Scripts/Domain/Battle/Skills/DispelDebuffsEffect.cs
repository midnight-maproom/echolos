using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // 味方対象の能力デバフを解除する Effect。
    // 対象：AbilityModifier / EvasionModifier / OutgoingDamageModifier / IncomingDamageModifier
    //       / CriticalRateModifier / CounterDamageModifier のうち IsBuff=false なもの。
    // IsUndispellable=true は剥がせない。
    // 状態異常（IsCleansable=true）は解除しない（cleanse は CleanseStatusAilmentsEffect が担当）。
    public sealed class DispelDebuffsEffect : IActionEffect
    {
        public void Apply(IActionContext context)
        {
            if (context == null) return;
            var targets = context.Targets;
            if (targets == null) return;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var toRemove = new List<IEffect>();
                foreach (var e in target.ActiveEffects)
                {
                    if (IsDebuffDispelTarget(e) && !e.IsUndispellable)
                        toRemove.Add(e);
                }
                if (toRemove.Count == 0) continue;

                var removed = new List<EffectChange>();
                foreach (var e in toRemove)
                {
                    removed.Add(EffectChange.From(e));
                    target.RemoveEffect(e);
                }

                context.Outcomes.Add(new HitOutcome(
                    target: target,
                    targetHPAfter: target.BaseUnit.CurrentHP,
                    removedEffects: removed));
            }
        }

        private static bool IsDebuffDispelTarget(IEffect e)
        {
            switch (e)
            {
                case AbilityModifier mod:         return !mod.IsBuff;
                case EvasionModifier mod:         return !mod.IsBuff;
                case OutgoingDamageModifier mod:  return !mod.IsBuff;
                case IncomingDamageModifier mod:  return !mod.IsBuff;
                case CriticalRateModifier mod:    return !mod.IsBuff;
                case CounterDamageModifier mod:   return !mod.IsBuff;
                case HealReceivedModifier mod:    return !mod.IsBuff && !mod.IsCleansable;
                default: return false;
            }
        }
    }
}
