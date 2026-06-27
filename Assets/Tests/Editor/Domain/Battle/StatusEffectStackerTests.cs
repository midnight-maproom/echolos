using NUnit.Framework;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class StatusEffectStackerTests
    {
        private static RuntimeUnit MakeUnit(int hp = 100)
        {
            var u = new Unit("u", "u", Element.None)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = 20,
                DEF = 0,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        private static AbilityModifier MakeBuff(EffectKind kind, float magnitude,
            string sourceAbilityName, Lifetime lifetime = Lifetime.Triggered,
            int remainingTurns = 3, bool isUndispellable = false)
        {
            return new AbilityModifier(kind, magnitude)
            {
                Lifetime = lifetime,
                RemainingTurns = remainingTurns,
                IsUndispellable = isUndispellable,
                SourceAbilityName = sourceAbilityName,
                Stacks = 1,
                MaxStacks = 1,
            };
        }

        [Test]
        public void 同KindでSourceAbilityName異なれば共存_連携と鼓舞両方が効く()
        {
            var u = MakeUnit();

            var covenant = MakeBuff(EffectKind.AttackUp, 3f, "連携",
                lifetime: Lifetime.Permanent, remainingTurns: -1, isUndispellable: true);
            var roar = MakeBuff(EffectKind.AttackUp, 6f, "鼓舞");

            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, covenant));
            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, roar));

            int attackUpCount = 0;
            foreach (var e in u.ActiveEffects)
                if (e.Kind == EffectKind.AttackUp) attackUpCount++;
            Assert.AreEqual(2, attackUpCount, "連携と鼓舞は別効果として共存");

            // 実効 ATK は両方合算（BaseATK=20 + 3 + 6 = 29）
            Assert.AreEqual(29, u.EffectiveATK);
        }

        [Test]
        public void 同KindかつSourceAbilityNameも同じならリフレッシュ()
        {
            var u = MakeUnit();

            var first = MakeBuff(EffectKind.AttackUp, 5f, "鼓舞", remainingTurns: 1);
            var second = MakeBuff(EffectKind.AttackUp, 5f, "鼓舞", remainingTurns: 3);

            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, first));
            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, second));

            int attackUpCount = 0;
            foreach (var e in u.ActiveEffects)
                if (e.Kind == EffectKind.AttackUp) attackUpCount++;
            Assert.AreEqual(1, attackUpCount, "同 Source 再付与はリフレッシュ＝1 件のまま");

            var eff = u.FindEffect(EffectKind.AttackUp);
            Assert.AreEqual(3, eff.RemainingTurns, "残ターンは最新付与の値で上書き");
        }

        [Test]
        public void 異なるKindは別効果_当然共存()
        {
            var u = MakeUnit();

            var atk = MakeBuff(EffectKind.AttackUp, 3f, "連携");
            var def = MakeBuff(EffectKind.DefenseUp, 3f, "王家の加護");

            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, atk));
            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, def));

            Assert.AreEqual(2, u.ActiveEffects.Count);
        }

        [Test]
        public void SourceAbilityNameが空文字とnullは同一視()
        {
            // 旧来の無名 Source 同士はマージされる（後方互換）。
            var u = MakeUnit();

            var nullSrc = MakeBuff(EffectKind.AttackUp, 3f, null);
            var emptySrc = MakeBuff(EffectKind.AttackUp, 5f, "");

            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, nullSrc));
            Assert.IsTrue(StatusEffectStacker.ApplyWithStacking(u, emptySrc));

            int attackUpCount = 0;
            foreach (var e in u.ActiveEffects)
                if (e.Kind == EffectKind.AttackUp) attackUpCount++;
            Assert.AreEqual(1, attackUpCount, "null/空文字 Source 同士は同一視＝マージ");
        }

        [Test]
        public void ImmunityKindsで弾く()
        {
            var u = MakeUnit();
            u.BaseUnit.ImmunityKinds.Add(EffectKind.Burn);

            var burn = new ContinuousDot(EffectKind.Burn, 5f) { Stacks = 1, MaxStacks = 5 };
            Assert.IsFalse(StatusEffectStacker.ApplyWithStacking(u, burn));
            Assert.AreEqual(0, u.ActiveEffects.Count);
        }
    }
}
