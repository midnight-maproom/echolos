using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DirectionalAndStrategyTests
    {
        private static RuntimeUnit Make(string id, int slot, int hp = 100, int atk = 30, int def = 10,
            TargetingDirection dir = TargetingDirection.FromFront)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = AttackKind.Melee,
                TargetingDirection = dir,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static RuntimeWaza WazaDir(int count)
        {
            var w = new Waza("range", "range")
            {
                TargetingType = TargetingType.DirectionalEnemies,
                TargetCount = count,
            };
            return new RuntimeWaza(w);
        }

        private static RuntimeWaza WazaSelect(TargetingType type, TargetSelection sel)
        {
            var w = new Waza("x", "x")
            {
                TargetingType = type,
                TargetSelection = sel,
            };
            return new RuntimeWaza(w);
        }

        // ── DirectionalEnemies ──

        [Test]
        public void Directional_FromFront_前から3体()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromFront);
            var e0 = Make("e0", 0);
            var e1 = Make("e1", 1);
            var e2 = Make("e2", 2);
            var e3 = Make("e3", 3);
            var r = TargetEvaluator.GetValidTargets(actor, WazaDir(3),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2, e3 });
            Assert.AreEqual(3, r.Count);
            Assert.AreSame(e0, r[0]);
            Assert.AreSame(e1, r[1]);
            Assert.AreSame(e2, r[2]);
        }

        [Test]
        public void Directional_FromBack_後ろから2体()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromBack);
            var e0 = Make("e0", 0);
            var e1 = Make("e1", 1);
            var e2 = Make("e2", 2);
            var r = TargetEvaluator.GetValidTargets(actor, WazaDir(2),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2 });
            Assert.AreEqual(2, r.Count);
            Assert.AreSame(e1, r[0]);
            Assert.AreSame(e2, r[1]);
        }

        [Test]
        public void Directional_対象数より敵が少なければ全員()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromFront);
            var e0 = Make("e0", 0);
            var r = TargetEvaluator.GetValidTargets(actor, WazaDir(3),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e0, r[0]);
        }

        [Test]
        public void Directional_死亡敵は除外して詰める()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromFront);
            var dead = Make("dead", 0);
            dead.BaseUnit.CurrentHP = 0;
            var e1 = Make("e1", 1);
            var e2 = Make("e2", 2);
            var e3 = Make("e3", 3);
            var r = TargetEvaluator.GetValidTargets(actor, WazaDir(2),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { dead, e1, e2, e3 });
            Assert.AreEqual(2, r.Count);
            Assert.AreSame(e1, r[0]);
            Assert.AreSame(e2, r[1]);
        }

        [Test]
        public void Directional_TargetCount0以下は1体に矯正()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromFront);
            var e0 = Make("e0", 0);
            var w = new Waza("x", "x")
            {
                TargetingType = TargetingType.DirectionalEnemies,
                TargetCount = 0,
            };
            var r = TargetEvaluator.GetValidTargets(actor, new RuntimeWaza(w),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0 });
            Assert.AreEqual(1, r.Count);
        }

        // ── TargetSelection（敵側）──

        [Test]
        public void SingleEnemy_HighestAtk_最大ATKの敵()
        {
            var actor = Make("a", 0);
            var e0 = Make("e0", 0, atk: 10);
            var e1 = Make("e1", 1, atk: 50);
            var e2 = Make("e2", 2, atk: 30);
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleEnemy, TargetSelection.HighestAtk),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e1, r[0]);
        }

        [Test]
        public void SingleEnemy_HighestDef_最大DEFの敵()
        {
            var actor = Make("a", 0);
            var e0 = Make("e0", 0, def: 5);
            var e1 = Make("e1", 1, def: 20);
            var e2 = Make("e2", 2, def: 10);
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleEnemy, TargetSelection.HighestDef),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e1, r[0]);
        }

        [Test]
        public void SingleEnemy_HighestAtk_TargetingDirection無視()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromBack);
            var e0 = Make("e0", 0, atk: 50);
            var e1 = Make("e1", 1, atk: 10);
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleEnemy, TargetSelection.HighestAtk),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e0, r[0]);
        }

        // ── TargetSelection（味方側）──

        [Test]
        public void SingleAlly_Default_最低HP割合()
        {
            var actor = Make("a", 0);
            var a1 = Make("a1", 1, hp: 100);
            a1.BaseUnit.CurrentHP = 80;
            var a2 = Make("a2", 2, hp: 100);
            a2.BaseUnit.CurrentHP = 30;
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleAlly, TargetSelection.Default),
                new List<RuntimeUnit> { actor, a1, a2 }, new List<RuntimeUnit>());
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(a2, r[0]);
        }

        [Test]
        public void SingleAlly_HighestAtk_最大ATKの味方()
        {
            var actor = Make("a", 0, atk: 20);
            var a1 = Make("a1", 1, atk: 80);
            var a2 = Make("a2", 2, atk: 50);
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleAlly, TargetSelection.HighestAtk),
                new List<RuntimeUnit> { actor, a1, a2 }, new List<RuntimeUnit>());
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(a1, r[0]);
        }

        [Test]
        public void SingleAlly_LowestHpRatio_最低HP割合()
        {
            var actor = Make("a", 0);
            actor.BaseUnit.CurrentHP = 90;
            var a1 = Make("a1", 1, hp: 100);
            a1.BaseUnit.CurrentHP = 40;
            var a2 = Make("a2", 2, hp: 50);
            a2.BaseUnit.CurrentHP = 30;  // 60%
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleAlly, TargetSelection.LowestHpRatio),
                new List<RuntimeUnit> { actor, a1, a2 }, new List<RuntimeUnit>());
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(a1, r[0]);
        }

        // ── タイブレーク ──

        [Test]
        public void HighestAtk_同値タイブレークは入力順()
        {
            var actor = Make("a", 0);
            var e0 = Make("e0", 0, atk: 50);
            var e1 = Make("e1", 1, atk: 50);
            var e2 = Make("e2", 2, atk: 50);
            var r = TargetEvaluator.GetValidTargets(actor,
                WazaSelect(TargetingType.SingleEnemy, TargetSelection.HighestAtk),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2 });
            Assert.AreSame(e0, r[0]);
        }
    }
}
