using NUnit.Framework;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class ShieldConsumerTests
    {
        private static RuntimeUnit MakeUnit()
        {
            var u = new Unit("u1", "u1")
            {
                MaxHP = 100,
                CurrentHP = 100,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        private static IEffect MakeShield(int stacks, int maxStacks)
        {
            var e = TestEff.Eff(EffectKind.Shield, stacks: stacks, remainingTurns: -1, maxStacks: maxStacks);
            return e;
        }

        [Test]
        public void Shield無しは素通し()
        {
            var ru = MakeUnit();
            var r = ShieldConsumer.Consume(50, ru);
            Assert.AreEqual(50, r.FinalDamage);
            Assert.IsFalse(r.ShieldConsumed);
        }

        [Test]
        public void Shield1_1ヒット吸収後にStacks0で剥奪()
        {
            var ru = MakeUnit();
            ru.AddEffect(MakeShield(1, 1));
            var r = ShieldConsumer.Consume(50, ru);
            Assert.AreEqual(0, r.FinalDamage);
            Assert.IsTrue(r.ShieldConsumed);
            Assert.AreEqual(0, ru.ShieldStacks);
        }

        [Test]
        public void Shield3_1ヒット吸収後にStacks2残存()
        {
            var ru = MakeUnit();
            ru.AddEffect(MakeShield(3, 3));
            var r = ShieldConsumer.Consume(50, ru);
            Assert.AreEqual(0, r.FinalDamage);
            Assert.IsTrue(r.ShieldConsumed);
            Assert.AreEqual(2, ru.ShieldStacks);
        }

        [Test]
        public void 多段_3ヒットでShield3全消費後素通し()
        {
            var ru = MakeUnit();
            ru.AddEffect(MakeShield(3, 3));
            var r1 = ShieldConsumer.Consume(50, ru);
            var r2 = ShieldConsumer.Consume(50, ru);
            var r3 = ShieldConsumer.Consume(50, ru);
            var r4 = ShieldConsumer.Consume(50, ru); // 4 ヒット目は素通し

            Assert.AreEqual(0, r1.FinalDamage); Assert.IsTrue(r1.ShieldConsumed);
            Assert.AreEqual(0, r2.FinalDamage); Assert.IsTrue(r2.ShieldConsumed);
            Assert.AreEqual(0, r3.FinalDamage); Assert.IsTrue(r3.ShieldConsumed);
            Assert.AreEqual(50, r4.FinalDamage); Assert.IsFalse(r4.ShieldConsumed);
            Assert.AreEqual(0, ru.ShieldStacks);
        }

        [Test]
        public void 生ダメ0はShield消費なし()
        {
            var ru = MakeUnit();
            ru.AddEffect(MakeShield(3, 3));
            var r = ShieldConsumer.Consume(0, ru);
            Assert.AreEqual(0, r.FinalDamage);
            Assert.IsFalse(r.ShieldConsumed);
            Assert.AreEqual(3, ru.ShieldStacks);
        }

        [Test]
        public void null_defenderは素通し()
        {
            var r = ShieldConsumer.Consume(50, null);
            Assert.AreEqual(50, r.FinalDamage);
            Assert.IsFalse(r.ShieldConsumed);
        }
    }
}
