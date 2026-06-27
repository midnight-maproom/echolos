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
    public class HealAndApplyEffectTests
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

        private static RuntimeUnit Make(string id, int hp = 100, int atk = 30, int def = 10)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        // ── HealEffect ──

        [Test]
        public void Heal_単体回復_実HPが上昇する()
        {
            var actor = Make("a", hp: 100);
            var target = Make("t", hp: 100);
            target.BaseUnit.CurrentHP = 50;
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new HealEffect(wazaPower: 5.0).Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.Greater(ctx.Outcomes[0].HealAmount, 0);
            Assert.Greater(target.CurrentHP, 50);
        }

        [Test]
        public void Heal_全体回復_全員が回復する()
        {
            var actor = Make("a");
            var t1 = Make("t1"); t1.BaseUnit.CurrentHP = 30;
            var t2 = Make("t2"); t2.BaseUnit.CurrentHP = 30;
            var t3 = Make("t3"); t3.BaseUnit.CurrentHP = 30;
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { t1, t2, t3 } };

            new HealEffect(wazaPower: 3.0).Apply(ctx);

            Assert.AreEqual(3, ctx.Outcomes.Count);
            Assert.Greater(t1.CurrentHP, 30);
            Assert.Greater(t2.CurrentHP, 30);
            Assert.Greater(t3.CurrentHP, 30);
        }

        [Test]
        public void Heal_最大HP超過分は無効()
        {
            var actor = Make("a");
            var target = Make("t", hp: 100);
            target.BaseUnit.CurrentHP = 95;
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new HealEffect(wazaPower: 100.0).Apply(ctx);

            Assert.AreEqual(100, target.CurrentHP);
            Assert.AreEqual(5, ctx.Outcomes[0].HealAmount);
        }

        [Test]
        public void Heal_満タンなら回復0_Outcomeは記録される()
        {
            var actor = Make("a");
            var target = Make("t", hp: 100);
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new HealEffect(wazaPower: 10.0).Apply(ctx);

            Assert.AreEqual(100, target.CurrentHP);
            Assert.AreEqual(0, ctx.Outcomes[0].HealAmount);
        }

        [Test]
        public void Heal_死亡ユニットは対象外()
        {
            var actor = Make("a");
            var dead = Make("dead");
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("alive");
            alive.BaseUnit.CurrentHP = 30;
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { dead, alive } };

            new HealEffect(wazaPower: 5.0).Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreEqual(alive, ctx.Outcomes[0].Target);
            Assert.AreEqual(0, dead.CurrentHP);
        }

        [Test]
        public void Heal_actor_targets_null安全()
        {
            Assert.DoesNotThrow(() => new HealEffect().Apply(null));
            var ctxNullActor = new StubContext { Actor = null, Targets = new List<RuntimeUnit> { Make("t") } };
            Assert.DoesNotThrow(() => new HealEffect().Apply(ctxNullActor));
            var ctxNullTargets = new StubContext { Actor = Make("a"), Targets = null };
            Assert.DoesNotThrow(() => new HealEffect().Apply(ctxNullTargets));
        }

        // ── ApplyStatusEffectEffect ──

        [Test]
        public void Apply_StatusEffect_単体に付与される()
        {
            var actor = Make("a");
            var target = Make("t");
            var templates = new List<IEffect>
            {
                TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3),
            };
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new ApplyStatusEffectEffect(templates).Apply(ctx);

            var applied = target.FindEffect(e => e.Kind == EffectKind.AttackDown);
            Assert.IsNotNull(applied);
            Assert.AreEqual(1, applied.Stacks);
            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreEqual(EffectKind.AttackDown, ctx.Outcomes[0].AppliedEffects[0].Kind);
        }

        [Test]
        public void Apply_StatusEffect_全体に付与される()
        {
            var actor = Make("a");
            var t1 = Make("t1");
            var t2 = Make("t2");
            var templates = new List<IEffect>
            {
                TestEff.Eff(EffectKind.AttackUp, magnitude: 5f, stacks: 1, remainingTurns: 3),
            };
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { t1, t2 } };

            new ApplyStatusEffectEffect(templates).Apply(ctx);

            Assert.IsNotNull(t1.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.IsNotNull(t2.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.AreEqual(2, ctx.Outcomes.Count);
        }

        [Test]
        public void Apply_StatusEffect_既存と同種ならスタック加算()
        {
            var actor = Make("a");
            var target = Make("t");
            var tmpl = TestEff.Eff(EffectKind.AttackUp, magnitude: 5f, stacks: 1, remainingTurns: 3, maxStacks: 3);
            var templates = new List<IEffect> { tmpl };
            var ctx1 = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };
            var ctx2 = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new ApplyStatusEffectEffect(templates).Apply(ctx1);
            new ApplyStatusEffectEffect(templates).Apply(ctx2);

            var applied = target.FindEffect(e => e.Kind == EffectKind.AttackUp);
            Assert.AreEqual(2, applied.Stacks);
        }

        [Test]
        public void Apply_StatusEffect_MaxStacks_でクランプ()
        {
            var actor = Make("a");
            var target = Make("t");
            var tmpl = TestEff.Eff(EffectKind.AttackUp, magnitude: 5f, stacks: 1, remainingTurns: 3, maxStacks: 2);
            var templates = new List<IEffect> { tmpl };

            for (int i = 0; i < 5; i++)
                new ApplyStatusEffectEffect(templates).Apply(new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } });

            var applied = target.FindEffect(e => e.Kind == EffectKind.AttackUp);
            Assert.AreEqual(2, applied.Stacks);
        }

        [Test]
        public void Apply_StatusEffect_複数テンプレ全て付与()
        {
            var actor = Make("a");
            var target = Make("t");
            var templates = new List<IEffect>
            {
                TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3),
                TestEff.Eff(EffectKind.DefenseDown, magnitude: 5f, stacks: 1, remainingTurns: 3),
            };
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new ApplyStatusEffectEffect(templates).Apply(ctx);

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.DefenseDown));
            Assert.AreEqual(2, ctx.Outcomes[0].AppliedEffects.Count);
        }

        [Test]
        public void Apply_StatusEffect_状態異常無効ユニットには弾く()
        {
            var actor = Make("a");
            var target = Make("t");
            target.BaseUnit.ImmunityKinds.Add(EffectKind.Burn);
            var templates = new List<IEffect>
            {
                TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
                TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3),
            };
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } };

            new ApplyStatusEffectEffect(templates).Apply(ctx);

            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
            Assert.AreEqual(1, ctx.Outcomes[0].AppliedEffects.Count);
            Assert.AreEqual(EffectKind.AttackDown, ctx.Outcomes[0].AppliedEffects[0].Kind);
        }

        [Test]
        public void Apply_StatusEffect_死亡ユニットは対象外()
        {
            var actor = Make("a");
            var dead = Make("dead");
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("alive");
            var templates = new List<IEffect>
            {
                TestEff.Eff(EffectKind.AttackUp, magnitude: 5f, stacks: 1, remainingTurns: 3),
            };
            var ctx = new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { dead, alive } };

            new ApplyStatusEffectEffect(templates).Apply(ctx);

            Assert.IsNull(dead.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.IsNotNull(alive.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.AreEqual(1, ctx.Outcomes.Count);
        }

        [Test]
        public void Apply_StatusEffect_null_templates_targets安全()
        {
            var actor = Make("a");
            var target = Make("t");

            Assert.DoesNotThrow(() => new ApplyStatusEffectEffect(null).Apply(
                new StubContext { Actor = actor, Targets = new List<RuntimeUnit> { target } }));

            Assert.DoesNotThrow(() => new ApplyStatusEffectEffect(
                new List<IEffect> { TestEff.Eff(EffectKind.AttackUp, magnitude: 5f) })
                .Apply(new StubContext { Actor = actor, Targets = null }));
        }
    }
}
