using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DeclareActionTests
    {
        private static RuntimeUnit Make(string id, int slot, int hp = 100, int spd = 50,
            TargetingDirection dir = TargetingDirection.FromFront)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseSPD = spd,
                AttackKind = AttackKind.Melee,
                TargetingDirection = dir,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static RuntimeWaza Waza(string id, TargetingType type,
            bool forced = false, int cd = 0, int initialCd = 0, int maxUses = -1)
        {
            var w = new Waza(id, id)
            {
                TargetingType = type,
                IsForcedWhenReady = forced,
                Cooldown = cd,
                InitialCooldown = initialCd,
                MaxUsesPerBattle = maxUses,
                Effects = new List<IActionEffect> { new AttackEffect() },
            };
            return new RuntimeWaza(w);
        }

        // ── 通常評価 ──

        [Test]
        public void 通常Waza1個_有効ターゲットありで採用()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var waza = Waza("atk", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { waza },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.IsFalse(ad.IsWaiting);
            Assert.AreSame(waza, ad.DeclaredWaza);
            Assert.AreEqual(1, ad.Targets.Count);
            Assert.AreSame(enemy, ad.Targets[0]);
        }

        [Test]
        public void 複数Waza_最初の有効なものを採用()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var first = Waza("first", TargetingType.SingleEnemy);
            var second = Waza("second", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { first, second },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreSame(first, ad.DeclaredWaza);
        }

        [Test]
        public void 最初がCD中_次の有効Wazaを採用()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var inCd = Waza("inCd", TargetingType.SingleEnemy, cd: 3, initialCd: 2);
            var ready = Waza("ready", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { inCd, ready },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreSame(ready, ad.DeclaredWaza);
        }

        [Test]
        public void 最初が対象不在_次の有効Wazaを採用()
        {
            var actor = Make("a", 0);
            var ally = Make("ally", 1);
            var noTargetCondition = new Waza("none", "none")
            {
                TargetingType = TargetingType.SingleEnemy,
                TargetingCondition = u => false,
                Effects = new List<IActionEffect> { new AttackEffect() },
            };
            var noEnemyWaza = new RuntimeWaza(noTargetCondition);
            var buffWaza = Waza("buff", TargetingType.SingleAlly);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { noEnemyWaza, buffWaza },
                new List<RuntimeUnit> { actor, ally }, new List<RuntimeUnit>());
            Assert.AreSame(buffWaza, ad.DeclaredWaza);
        }

        // ── 優先発動（IsForcedWhenReady）──

        [Test]
        public void Forced_CD0なら通常より優先()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var forced = Waza("special", TargetingType.SingleEnemy, forced: true);
            var normal = Waza("normal", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { normal, forced },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreSame(forced, ad.DeclaredWaza);
        }

        [Test]
        public void Forced_CD中なら通常評価へフォールスルー()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var forced = Waza("special", TargetingType.SingleEnemy, forced: true, cd: 3, initialCd: 2);
            var normal = Waza("normal", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { normal, forced },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreSame(normal, ad.DeclaredWaza);
        }

        [Test]
        public void Forced_対象不在ならフォールスルー()
        {
            var actor = Make("a", 0);
            var forced = Waza("special", TargetingType.SingleEnemy, forced: true);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { forced },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit>());
            Assert.IsFalse(ad.IsWaiting);
            Assert.AreEqual("def_guard", ad.DeclaredWaza.Id);
        }

        // ── 行動不能 ──

        [Test]
        public void 凍結完全_待機()
        {
            var actor = Make("a", 0);
            for (int i = 0; i < 10; i++)
                actor.AddEffect(TestEff.Eff(EffectKind.Freeze, stacks: 1, remainingTurns: -1, maxStacks: 99));
            var enemy = Make("e", 0);
            var waza = Waza("atk", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { waza },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.IsTrue(ad.IsWaiting);
            Assert.IsNull(ad.DeclaredWaza);
        }

        [Test]
        public void 麻痺_待機()
        {
            var actor = Make("a", 0);
            actor.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 1, remainingTurns: -1, maxStacks: 99));
            var enemy = Make("e", 0);
            var waza = Waza("atk", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { waza },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.IsTrue(ad.IsWaiting);
        }

        // ── def_guard フォールバック ──

        [Test]
        public void BattleWazasなし_def_guardフォールバック()
        {
            var actor = Make("a", 0);
            var ad = TargetEvaluator.DeclareAction(actor, null,
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit>());
            Assert.IsFalse(ad.IsWaiting);
            Assert.AreEqual("def_guard", ad.DeclaredWaza.Id);
            Assert.AreSame(actor, ad.Targets[0]);
        }

        [Test]
        public void 全Wazaが空振り_def_guardフォールバック()
        {
            var actor = Make("a", 0);
            var w1 = Waza("w1", TargetingType.SingleEnemy);
            var w2 = Waza("w2", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { w1, w2 },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit>());
            Assert.AreEqual("def_guard", ad.DeclaredWaza.Id);
        }

        [Test]
        public void def_guard_独立インスタンスでユニット間共有なし()
        {
            var a1 = Make("a1", 0);
            var a2 = Make("a2", 1);
            var ad1 = TargetEvaluator.DeclareAction(a1, null,
                new List<RuntimeUnit> { a1 }, new List<RuntimeUnit>());
            var ad2 = TargetEvaluator.DeclareAction(a2, null,
                new List<RuntimeUnit> { a2 }, new List<RuntimeUnit>());
            Assert.AreNotSame(ad1.DeclaredWaza, ad2.DeclaredWaza);
        }

        // ── 実効 SPD ──

        [Test]
        public void EffectiveSPD_BaseSPDをそのまま返す()
        {
            var actor = Make("a", 0, spd: 30);
            var enemy = Make("e", 0);
            var waza = Waza("atk", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { waza },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreEqual(30, ad.EffectiveSPD);
        }

        [Test]
        public void EffectiveSPD_Freeze5スタックで50パーセント減衰()
        {
            var actor = Make("a", 0, spd: 100);
            for (int i = 0; i < 5; i++)
                actor.AddEffect(TestEff.Eff(EffectKind.Freeze, stacks: 1, remainingTurns: -1, maxStacks: 99));
            var enemy = Make("e", 0);
            var waza = Waza("atk", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { waza },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreEqual(50, ad.EffectiveSPD);
        }

        // ── MaxUsesPerBattle ──

        [Test]
        public void MaxUses上限到達_スキップ()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var limited = Waza("once", TargetingType.SingleEnemy, maxUses: 1);
            limited.CurrentUses = 1;
            var fallback = Waza("normal", TargetingType.SingleEnemy);
            var ad = TargetEvaluator.DeclareAction(actor,
                new List<RuntimeWaza> { limited, fallback },
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            Assert.AreSame(fallback, ad.DeclaredWaza);
        }

        // ── null 安全 ──

        [Test]
        public void actor_null_safe()
        {
            var ad = TargetEvaluator.DeclareAction(null, null, null, null);
            Assert.IsTrue(ad.IsWaiting);
            Assert.IsNull(ad.DeclaredWaza);
        }
    }
}
