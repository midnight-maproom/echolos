using NUnit.Framework;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class IncomingDamageDownTests
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
            Assert.AreEqual(100, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void 被ダメ30パーセントカットで70ダメ()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 30f));
            Assert.AreEqual(70, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void 複数バフは加算スタック_20と30で50ダメ()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 20f));
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 30f));
            Assert.AreEqual(50, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void Stacks2はMagnitude2倍カウント()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 10f, stacks: 2, maxStacks: 2));
            Assert.AreEqual(80, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void キャップ80パーセント_合計100でも20ダメ通る()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 50f));
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 50f));
            Assert.AreEqual(20, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void キャップ80パーセント_合計200でも20ダメ通る()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 200f));
            Assert.AreEqual(20, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void 生ダメ0は0のまま()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 50f));
            Assert.AreEqual(0, DamageModifier.ApplyIncomingMultiplier(0, ru));
        }

        [Test]
        public void null_defenderは素通し()
        {
            Assert.AreEqual(100, DamageModifier.ApplyIncomingMultiplier(100, null));
        }

        [Test]
        public void OutgoingDamageUpは被ダメに影響しない()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 50f));
            Assert.AreEqual(100, DamageModifier.ApplyIncomingMultiplier(100, ru));
        }

        [Test]
        public void GetIncomingCutRate_キャップで0_80に丸まる()
        {
            var ru = MakeUnit();
            ru.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 90f));
            Assert.AreEqual(0.80, DamageModifier.GetIncomingCutRate(ru), 0.0001);
        }
    }
}
