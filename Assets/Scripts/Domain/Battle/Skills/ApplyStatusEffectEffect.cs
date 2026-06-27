using System;
using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // IEffect テンプレを context.Targets 全員に付与する Effect。
    // 各 template は Clone され、対象に StatusEffectStacker.ApplyWithStacking で適用される。
    // 攻撃 Waza の付帯効果（炎上付与・ATK デバフ付与等）、バフ Waza の主効果、
    // 単純な付与系の全てを表現する。付与時の弾き（ImmunityKinds）は Stacker が判定。
    public sealed class ApplyStatusEffectEffect : IActionEffect
    {
        private readonly IReadOnlyList<IEffect> _templates;

        public ApplyStatusEffectEffect(IReadOnlyList<IEffect> templates)
        {
            _templates = templates ?? Array.Empty<IEffect>();
        }

        public void Apply(IActionContext context)
        {
            if (context == null) return;
            var targets = context.Targets;
            if (targets == null) return;
            if (_templates.Count == 0) return;

            // WazaPowerBoost：Actor.AppliedUpgrades から TargetWazaId 一致の Magnitude 合計を
            // 各テンプレの Magnitude に加算する。Clone してから Accumulator で破壊的に加算（テンプレ保護）。
            int boost = UpgradeMagnitudeResolver.SumWazaPowerBoost(
                context.Actor?.BaseUnit, context.CurrentWazaId);

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var applied = new List<EffectChange>();
                foreach (var template in _templates)
                {
                    if (template == null) continue;
                    var toApply = template;
                    if (boost != 0)
                    {
                        toApply = template.Clone();
                        EffectMagnitudeAccumulator.Add(toApply, boost);
                    }
                    if (StatusEffectStacker.ApplyWithStacking(target, toApply))
                    {
                        // 対象側に乗った実体（スタック加算後）の値を埋める。同 Kind 効果が既にあった場合の
                        // Stacks リフレッシュ／RemainingTurns リセットが正しく反映されるよう、テンプレートではなく
                        // FindEffect の結果を参照する。
                        // 検索は Kind + SourceAbilityName 一致＝同 Kind の別 Source 効果（連携 +3 等）を
                        // 取り違えない（Stacker 側の Source 分離挙動と整合）。
                        var resolved = target.FindEffect(e =>
                            e.Kind == toApply.Kind
                            && IsSameSource(e.SourceAbilityName, toApply.SourceAbilityName));
                        applied.Add(EffectChange.From(resolved ?? toApply));
                    }
                }

                if (applied.Count == 0) continue;

                context.Outcomes.Add(new HitOutcome(
                    target: target,
                    appliedEffects: applied,
                    targetHPAfter: target.BaseUnit.CurrentHP));
            }
        }

        // SourceAbilityName の同値判定。null と空文字は同一視（Stacker と挙動を揃える）。
        private static bool IsSameSource(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;
            return a == b;
        }
    }
}
