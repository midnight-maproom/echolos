using NUnit.Framework;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DamageModifierTests
    {
        private static RuntimeUnit MakeUnit()
        {
            var u = new Unit("u1", "u1")
            {
                MaxHP = 100,
                CurrentHP = 100,
                BaseATK = 50,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        [Test]
        public void バフ無しは素通し()
        {
            var ru = MakeUnit();
            Assert.AreEqual(100, DamageModifier.ApplyOutgoingMultiplier(100, ru));
        }

        [Test]
        public void 与ダメ20パーセントで120ダメ()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 20f));
            Assert.AreEqual(120, DamageModifier.ApplyOutgoingMultiplier(100, ru));
        }

        [Test]
        public void 複数バフは加算スタック_10と20で130()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 10f));
            ru.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 20f));
            Assert.AreEqual(130, DamageModifier.ApplyOutgoingMultiplier(100, ru));
        }

        [Test]
        public void Stacks2はMagnitude2倍カウント()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 10f, stacks: 2, maxStacks: 2));
            Assert.AreEqual(120, DamageModifier.ApplyOutgoingMultiplier(100, ru));
        }

        [Test]
        public void 生ダメ0は0のまま()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 50f));
            Assert.AreEqual(0, DamageModifier.ApplyOutgoingMultiplier(0, ru));
        }

        [Test]
        public void null_attackerは素通し()
        {
            Assert.AreEqual(100, DamageModifier.ApplyOutgoingMultiplier(100, null));
        }
    }
}
