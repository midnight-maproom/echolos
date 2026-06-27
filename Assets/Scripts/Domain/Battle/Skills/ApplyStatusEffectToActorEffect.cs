using System;
using System.Collections.Generic;
using Echolos.Domain.Effects;

namespace Echolos.Domain.Battle.Skills
{
    // IEffect テンプレを context.Actor 自身に付与する Effect。
    //
    // ApplyStatusEffectEffect が context.Targets（攻撃対象）に適用するのに対し、
    // 本 Effect は実行者（context.Actor）に適用する。1 つの Waza で「全体攻撃 + caster 自己バフ」を
    // チェインしたい場合に使う（例：闇皇太子の闇槍の薙ぎ＝全体物理＋自己 AttackUp 永続スタック）。
    //
    // 既存 def_guard（TargetingType=Self の防御コマンド）と棲み分け：
    // - def_guard は単体 Waza として TargetingType=Self ＋ ApplyStatusEffectEffect で完結
    // - 本 Effect は AllEnemies 等の他ターゲット Waza に「ついでに自己バフ」を混ぜたいときに使う
    public sealed class ApplyStatusEffectToActorEffect : IActionEffect
    {
        private readonly IReadOnlyList<IEffect> _templates;

        public ApplyStatusEffectToActorEffect(IReadOnlyList<IEffect> templates)
        {
            _templates = templates ?? Array.Empty<IEffect>();
        }

        public void Apply(IActionContext context)
        {
            if (context == null) return;
            var actor = context.Actor;
            if (actor == null || !actor.IsAlive) return;
            if (_templates.Count == 0) return;

            var applied = new List<EffectChange>();
            foreach (var template in _templates)
            {
                if (template == null) continue;
                if (StatusEffectStacker.ApplyWithStacking(actor, template))
                {
                    // 対象側に乗った実体（スタック加算後）の値を埋める。
                    // 同 Kind + 同 SourceAbilityName で FindEffect すれば最新値が取れる（Stacker と整合）。
                    var resolved = actor.FindEffect(e =>
                        e.Kind == template.Kind
                        && IsSameSource(e.SourceAbilityName, template.SourceAbilityName));
                    applied.Add(EffectChange.From(resolved ?? template));
                }
            }

            if (applied.Count == 0) return;

            // 自己バフは Outcomes に target=actor で記録（ログ表示用・UI が caster=target でバッジ更新できる）。
            context.Outcomes.Add(new HitOutcome(
                target: actor,
                appliedEffects: applied,
                targetHPAfter: actor.BaseUnit.CurrentHP));
        }

        // SourceAbilityName の同値判定（null と空文字は同一視）。
        private static bool IsSameSource(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;
            return a == b;
        }
    }
}
