// ApplyStatusEffectToActorEffect の動作テスト。
// 「caster 自身にバフ適用」と「複数 Effect チェイン時の独立性」が主検証ポイント。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class ApplyStatusEffectToActorEffectTests
    {
        private sealed class StubContext : IActionContext
        {
            public BattleContext Battle { get; set; }
            public RuntimeUnit Actor { get; set; }
            public IList<RuntimeUnit> Targets { get; set; } = new List<RuntimeUnit>();
            public Func<int> Random0To99 { get; set; } = () => 50;
            public IList<HitOutcome> Outcomes { get; set; } = new List<HitOutcome>();
            public string CurrentWazaId { get; set; }
        }

        private static RuntimeUnit Make(string id, int hp = 100, int atk = 30)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = 5,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        private static IEffect MakeAttackUpPermanent(int magnitude, string source = "テスト自己バフ")
        {
            return new AbilityModifier(EffectKind.AttackUp, magnitude)
            {
                Stacks = 1,
                MaxStacks = 999,
                Lifetime = Lifetime.Permanent,
                RemainingTurns = -1,
                IsCleansable = false,
                IsUndispellable = true,
                SourceAbilityName = source,
            };
        }

        [Test]
        public void Actor自身にAttackUpが付与される()
        {
            var actor = Make("a", atk: 30);
            // 攻撃対象（Targets）は別ユニット＝そちらには付与されないことも確認
            var target = Make("t");
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new ApplyStatusEffectToActorEffect(new List<IEffect> { MakeAttackUpPermanent(20) }).Apply(ctx);

            Assert.IsNotNull(actor.FindEffect(e => e.Kind == EffectKind.AttackUp),
                "Actor 自身に AttackUp が乗る");
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.AttackUp),
                "Targets には適用されない");
        }

        [Test]
        public void 連続適用で同SourceのStacksが積み上がる()
        {
            var actor = Make("a", atk: 30);
            var ctx = new StubContext { Actor = actor };

            var effect = new ApplyStatusEffectToActorEffect(new List<IEffect> { MakeAttackUpPermanent(20) });

            effect.Apply(ctx);
            effect.Apply(ctx);
            effect.Apply(ctx);

            var found = actor.FindEffect(e => e.Kind == EffectKind.AttackUp);
            Assert.IsNotNull(found);
            Assert.AreEqual(3, found.Stacks, "3 回適用で 3 スタック");
        }

        [Test]
        public void Outcomesにtarget_actorで記録される()
        {
            var actor = Make("a");
            var ctx = new StubContext { Actor = actor };

            new ApplyStatusEffectToActorEffect(new List<IEffect> { MakeAttackUpPermanent(20) }).Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreSame(actor, ctx.Outcomes[0].Target, "Outcomes.Target は actor 自身");
            Assert.AreEqual(1, ctx.Outcomes[0].AppliedEffects.Count);
        }

        [Test]
        public void Actor_dead_適用されない()
        {
            var actor = Make("a");
            actor.BaseUnit.CurrentHP = 0;
            actor.BaseUnit.State = UnitState.Dead;
            var ctx = new StubContext { Actor = actor };

            new ApplyStatusEffectToActorEffect(new List<IEffect> { MakeAttackUpPermanent(20) }).Apply(ctx);

            Assert.AreEqual(0, ctx.Outcomes.Count);
            Assert.IsNull(actor.FindEffect(e => e.Kind == EffectKind.AttackUp));
        }

        [Test]
        public void Actor_null_例外なし()
        {
            var ctx = new StubContext { Actor = null };
            // 例外なく無視されることを確認
            Assert.DoesNotThrow(() =>
                new ApplyStatusEffectToActorEffect(new List<IEffect> { MakeAttackUpPermanent(20) }).Apply(ctx));
            Assert.AreEqual(0, ctx.Outcomes.Count);
        }

        [Test]
        public void テンプレ空_適用されない()
        {
            var actor = Make("a");
            var ctx = new StubContext { Actor = actor };

            new ApplyStatusEffectToActorEffect(new List<IEffect>()).Apply(ctx);

            Assert.AreEqual(0, ctx.Outcomes.Count);
        }
    }
}
