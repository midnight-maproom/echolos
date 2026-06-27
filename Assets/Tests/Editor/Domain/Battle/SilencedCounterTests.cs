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
    public class SilencedCounterTests
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

        private static RuntimeUnit Make(string id, int slot, int atk, int def, int hp = 200)
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
            return new RuntimeUnit(u, slot);
        }

        private static IEffect SilencedCounterPassive()
        {
            return TestEff.Persistent(
                EffectKind.SilencedCounter,
                magnitude: 0f,
                sourceAbilityName: "専守");
        }

        // ── CanCounterAttack 純関数 ──

        [Test]
        public void SilencedCounterなし_両者MeleeなのでCanCounterAttackはtrue()
        {
            var attacker = Make("a", 0, atk: 30, def: 0);
            var defender = Make("d", 0, atk: 30, def: 0);
            Assert.IsTrue(CounterAttackResolver.CanCounterAttack(attacker, defender));
        }

        [Test]
        public void SilencedCounterあり_反撃禁止_CanCounterAttackはfalse()
        {
            var attacker = Make("a", 0, atk: 30, def: 0);
            var defender = Make("d", 0, atk: 30, def: 0);
            defender.AddEffect(SilencedCounterPassive());
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(attacker, defender));
        }

        [Test]
        public void SilencedCounterは被弾側のフラグ_攻撃側にあっても反撃発動()
        {
            var attacker = Make("a", 0, atk: 30, def: 0);
            attacker.AddEffect(SilencedCounterPassive());
            var defender = Make("d", 0, atk: 30, def: 0);
            Assert.IsTrue(CounterAttackResolver.CanCounterAttack(attacker, defender));
        }

        // ── AttackEffect 結合 ──

        [Test]
        public void 水の大盾兵_専守_反撃発動しない_outcomes1件()
        {
            var attacker = Make("a", 0, atk: 50, def: 0);
            var defender = Make("d", 0, atk: 50, def: 0);
            defender.AddEffect(SilencedCounterPassive());
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext
            {
                Battle = battle,
                Actor = attacker,
                Targets = new List<RuntimeUnit> { defender },
            };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreSame(defender, ctx.Outcomes[0].Target);
            Assert.AreEqual(200, attacker.CurrentHP);
        }

        [Test]
        public void 水の大盾兵_被ダメは通常通り受ける_HPは減る()
        {
            var attacker = Make("a", 0, atk: 50, def: 0);
            var defender = Make("d", 0, atk: 50, def: 0);
            defender.AddEffect(SilencedCounterPassive());
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext
            {
                Battle = battle,
                Actor = attacker,
                Targets = new List<RuntimeUnit> { defender },
            };

            new AttackEffect().Apply(ctx);

            Assert.Greater(ctx.Outcomes[0].Damage, 0);
            Assert.Less(defender.CurrentHP, 200);
        }
    }
}
