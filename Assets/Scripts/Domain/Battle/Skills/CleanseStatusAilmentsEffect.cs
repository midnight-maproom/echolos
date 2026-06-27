using System.Collections.Generic;
using Echolos.Domain.Effects;

namespace Echolos.Domain.Battle.Skills
{
    // 味方対象の状態異常（IsCleansable=true な効果）を解除する Effect。
    // 新設計では「状態異常」概念は廃止し、IsCleansable フラグで対象判定する。
    // プロト範囲では Burn / Freeze / Paralysis / Curse / SearingWound が IsCleansable=true。
    public sealed class CleanseStatusAilmentsEffect : IActionEffect
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
                    if (e.IsCleansable) toRemove.Add(e);
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
    }
}
