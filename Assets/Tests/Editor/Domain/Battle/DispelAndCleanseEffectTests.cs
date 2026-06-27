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
    public class DispelAndCleanseEffectTests
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

        private static RuntimeUnit Make(string id, int hp = 100)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        // ── DispelBuffsEffect ──

        [Test]
        public void DispelBuffs_能力バフを解除する()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));
            target.AddEffect(TestEff.Eff(EffectKind.DefenseUp, magnitude: 5f, stacks: 1, remainingTurns: 3));

            new DispelBuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.DefenseUp));
        }

        [Test]
        public void DispelBuffs_Undispellableは残る()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Persistent(EffectKind.AttackUp, magnitude: 10f, sourceAbilityName: "王家の加護"));
            target.AddEffect(TestEff.Eff(EffectKind.DefenseUp, magnitude: 5f, stacks: 1, remainingTurns: 3));

            new DispelBuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.DefenseUp));
        }

        [Test]
        public void DispelBuffs_状態異常やデバフは残る()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3));
            target.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));

            new DispelBuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.AttackUp));
        }

        [Test]
        public void DispelBuffs_複数ターゲット全員に適用()
        {
            var t1 = Make("t1");
            var t2 = Make("t2");
            t1.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));
            t2.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));

            new DispelBuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { t1, t2 } });

            Assert.IsNull(t1.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.IsNull(t2.FindEffect(e => e.Kind == EffectKind.AttackUp));
        }

        [Test]
        public void DispelBuffs_死亡ターゲットはスキップ()
        {
            var dead = Make("dead");
            dead.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));
            dead.BaseUnit.CurrentHP = 0;

            new DispelBuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { dead } });

            Assert.IsNotNull(dead.FindEffect(e => e.Kind == EffectKind.AttackUp));
        }

        // ── DispelDebuffsEffect ──

        [Test]
        public void DispelDebuffs_能力デバフを解除する()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3));
            target.AddEffect(TestEff.Eff(EffectKind.DefenseDown, magnitude: 5f, stacks: 1, remainingTurns: 3));

            new DispelDebuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.DefenseDown));
        }

        [Test]
        public void DispelDebuffs_Burnは解除しない()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3));

            new DispelDebuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
        }

        [Test]
        public void DispelDebuffs_Undispellableデバフは残る()
        {
            var target = Make("t");
            var undispellable = TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3, isUndispellable: true);
            target.AddEffect(undispellable);

            new DispelDebuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
        }

        [Test]
        public void DispelDebuffs_バフは残る()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));
            target.AddEffect(TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3));

            new DispelDebuffsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackUp));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
        }

        // ── CleanseStatusAilmentsEffect ──

        [Test]
        public void Cleanse_状態異常を解除する()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.Freeze, stacks: 5, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 1, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.Curse, stacks: 1, remainingTurns: -1, maxStacks: 99));

            new CleanseStatusAilmentsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Freeze));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Paralysis));
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Curse));
        }

        [Test]
        public void Cleanse_能力デバフは残る()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3));

            new CleanseStatusAilmentsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
        }

        [Test]
        public void Cleanse_バフは残る()
        {
            var target = Make("t");
            target.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3, remainingTurns: -1, maxStacks: 99));
            target.AddEffect(TestEff.Eff(EffectKind.AttackUp, magnitude: 10f, stacks: 1, remainingTurns: 3));

            new CleanseStatusAilmentsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { target } });

            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackUp));
        }

        [Test]
        public void Cleanse_複数ターゲット全員から状態異常解除()
        {
            var t1 = Make("t1");
            var t2 = Make("t2");
            t1.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3, remainingTurns: -1, maxStacks: 99));
            t2.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 1, remainingTurns: -1, maxStacks: 99));

            new CleanseStatusAilmentsEffect().Apply(new StubContext { Targets = new List<RuntimeUnit> { t1, t2 } });

            Assert.IsNull(t1.FindEffect(e => e.Kind == EffectKind.Burn));
            Assert.IsNull(t2.FindEffect(e => e.Kind == EffectKind.Paralysis));
        }

        // ── 共通 null 安全 ──

        [Test]
        public void 全effectsとも_null_context_targetsで例外なし()
        {
            Assert.DoesNotThrow(() => new DispelBuffsEffect().Apply(null));
            Assert.DoesNotThrow(() => new DispelDebuffsEffect().Apply(null));
            Assert.DoesNotThrow(() => new CleanseStatusAilmentsEffect().Apply(null));

            Assert.DoesNotThrow(() => new DispelBuffsEffect().Apply(new StubContext { Targets = null }));
            Assert.DoesNotThrow(() => new DispelDebuffsEffect().Apply(new StubContext { Targets = null }));
            Assert.DoesNotThrow(() => new CleanseStatusAilmentsEffect().Apply(new StubContext { Targets = null }));
        }
    }
}
