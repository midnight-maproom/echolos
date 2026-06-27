using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class WazaTests
    {
        // ── Waza ──

        [Test]
        public void Waza_Id_Name_初期化()
        {
            var w = new Waza("atk_basic", "通常攻撃");
            Assert.AreEqual("atk_basic", w.Id);
            Assert.AreEqual("通常攻撃", w.Name);
        }

        [Test]
        public void Waza_既定値_HitCount1_TargetCount1_MaxUses無制限()
        {
            var w = new Waza("x", "x");
            Assert.AreEqual(1, w.HitCount);
            Assert.AreEqual(1, w.TargetCount);
            Assert.AreEqual(-1, w.MaxUsesPerBattle);
            Assert.IsFalse(w.IsForcedWhenReady);
        }

        [Test]
        public void Waza_Effects_初期化済の空リスト()
        {
            var w = new Waza("x", "x");
            Assert.IsNotNull(w.Effects);
            Assert.AreEqual(0, w.Effects.Count);
        }

        [Test]
        public void Waza_Effects_に複数Effectを設定可能()
        {
            var w = new Waza("x", "x")
            {
                Effects = new List<IActionEffect>
                {
                    new AttackEffect(wazaMultiplier: 1.2),
                    new ApplyStatusEffectEffect(new List<IEffect>
                    {
                        TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
                    }),
                },
            };
            Assert.AreEqual(2, w.Effects.Count);
            Assert.IsInstanceOf<AttackEffect>(w.Effects[0]);
            Assert.IsInstanceOf<ApplyStatusEffectEffect>(w.Effects[1]);
        }

        [Test]
        public void Waza_DefaultCounter_存在し反撃用に構成済()
        {
            var counter = Waza.DefaultCounter;
            Assert.AreEqual("counter:default", counter.Id);
            Assert.AreEqual(TargetingType.SingleEnemy, counter.TargetingType);
            Assert.AreEqual(1, counter.HitCount);
            Assert.AreEqual(1, counter.Effects.Count);
            Assert.IsInstanceOf<AttackEffect>(counter.Effects[0]);
        }

        [Test]
        public void Waza_DirectionalEnemies_TargetCount明示で範囲表現()
        {
            var w = new Waza("range", "範囲攻撃")
            {
                TargetingType = TargetingType.DirectionalEnemies,
                TargetCount = 3,
            };
            Assert.AreEqual(TargetingType.DirectionalEnemies, w.TargetingType);
            Assert.AreEqual(3, w.TargetCount);
        }

        [Test]
        public void Waza_TargetingCondition_設定して判定可能()
        {
            var w = new Waza("low_hp", "低HP優先")
            {
                TargetingCondition = u => u.CurrentHP < u.MaxHP / 2,
            };
            Assert.IsNotNull(w.TargetingCondition);
        }

        // ── RuntimeWaza ──

        [Test]
        public void RuntimeWaza_BaseWaza_保持()
        {
            var w = new Waza("x", "x");
            var rw = new RuntimeWaza(w);
            Assert.AreSame(w, rw.BaseWaza);
        }

        [Test]
        public void RuntimeWaza_InitialCooldown_でCurrentCooldown初期化()
        {
            var w = new Waza("x", "x") { InitialCooldown = 2 };
            var rw = new RuntimeWaza(w);
            Assert.AreEqual(2, rw.CurrentCooldown);
            Assert.AreEqual(0, rw.CurrentUses);
        }

        [Test]
        public void RuntimeWaza_BaseWazaの不変フィールドを転送()
        {
            var w = new Waza("attack", "攻撃")
            {
                SPD = 50,
                Cooldown = 3,
                InitialCooldown = 1,
                IsForcedWhenReady = true,
                HitCount = 2,
                MaxUsesPerBattle = 5,
                TargetingType = TargetingType.DirectionalEnemies,
                TargetCount = 3,
            };
            var rw = new RuntimeWaza(w);
            Assert.AreEqual("attack", rw.Id);
            Assert.AreEqual("攻撃", rw.Name);
            Assert.AreEqual(50, rw.SPD);
            Assert.AreEqual(3, rw.Cooldown);
            Assert.AreEqual(1, rw.InitialCooldown);
            Assert.IsTrue(rw.IsForcedWhenReady);
            Assert.AreEqual(2, rw.HitCount);
            Assert.AreEqual(5, rw.MaxUsesPerBattle);
            Assert.AreEqual(TargetingType.DirectionalEnemies, rw.TargetingType);
            Assert.AreEqual(3, rw.TargetCount);
        }

        [Test]
        public void RuntimeWaza_Effects_BaseWazaのEffectsを参照()
        {
            var dmg = new AttackEffect(wazaMultiplier: 1.5);
            var w = new Waza("x", "x")
            {
                Effects = new List<IActionEffect> { dmg },
            };
            var rw = new RuntimeWaza(w);
            Assert.AreSame(dmg, rw.Effects[0]);
        }

        [Test]
        public void RuntimeWaza_null_BaseWazaで例外()
        {
            Assert.Throws<ArgumentNullException>(() => new RuntimeWaza(null));
        }
    }
}
