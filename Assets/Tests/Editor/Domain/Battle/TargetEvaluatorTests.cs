using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class TargetEvaluatorTests
    {
        private static RuntimeUnit Make(string id, int slot, int hp = 100, TargetingDirection dir = TargetingDirection.FromFront)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                AttackKind = AttackKind.Melee,
                TargetingDirection = dir,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static RuntimeWaza Waza(TargetingType type, System.Func<RuntimeUnit, bool> cond = null)
        {
            var w = new Waza("x", "x") { TargetingType = type };
            if (cond != null) w.TargetingCondition = cond;
            return new RuntimeWaza(w);
        }

        // ── Self ──

        [Test]
        public void Self_actorのみ()
        {
            var actor = Make("a", 0);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.Self),
                new List<RuntimeUnit>(), new List<RuntimeUnit>());
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(actor, r[0]);
        }

        [Test]
        public void Self_actor死亡で空()
        {
            var actor = Make("a", 0);
            actor.BaseUnit.CurrentHP = 0;
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.Self),
                new List<RuntimeUnit>(), new List<RuntimeUnit>());
            Assert.AreEqual(0, r.Count);
        }

        // ── AllEnemies / AllAllies ──

        [Test]
        public void AllEnemies_生存敵全員()
        {
            var actor = Make("a", 0);
            var e1 = Make("e1", 0);
            var e2 = Make("e2", 1);
            var e3 = Make("e3", 2);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.AllEnemies),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e1, e2, e3 });
            Assert.AreEqual(3, r.Count);
        }

        [Test]
        public void AllEnemies_死亡敵は除外()
        {
            var actor = Make("a", 0);
            var dead = Make("dead", 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("alive", 1);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.AllEnemies),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { dead, alive });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(alive, r[0]);
        }

        [Test]
        public void AllAllies_生存味方全員()
        {
            var actor = Make("a", 0);
            var a1 = Make("a1", 1);
            var a2 = Make("a2", 2);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.AllAllies),
                new List<RuntimeUnit> { actor, a1, a2 }, new List<RuntimeUnit>());
            Assert.AreEqual(3, r.Count);
        }

        // ── SingleEnemy ──

        [Test]
        public void SingleEnemy_FromFront_最前敵()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromFront);
            var e0 = Make("e0", 0);
            var e1 = Make("e1", 1);
            var e2 = Make("e2", 2);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.SingleEnemy),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e0, r[0]);
        }

        [Test]
        public void SingleEnemy_FromBack_最後尾敵()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromBack);
            var e0 = Make("e0", 0);
            var e1 = Make("e1", 1);
            var e2 = Make("e2", 2);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.SingleEnemy),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { e0, e1, e2 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e2, r[0]);
        }

        [Test]
        public void SingleEnemy_最前死亡なら次の生存最前()
        {
            var actor = Make("a", 0, dir: TargetingDirection.FromFront);
            var dead = Make("dead", 0);
            dead.BaseUnit.CurrentHP = 0;
            var e1 = Make("e1", 1);
            var e2 = Make("e2", 2);
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.SingleEnemy),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { dead, e1, e2 });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(e1, r[0]);
        }

        [Test]
        public void SingleEnemy_敵全滅で空()
        {
            var actor = Make("a", 0);
            var d1 = Make("d1", 0);
            d1.BaseUnit.CurrentHP = 0;
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.SingleEnemy),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { d1 });
            Assert.AreEqual(0, r.Count);
        }

        // ── SingleAlly ──

        [Test]
        public void SingleAlly_最低HP割合の味方()
        {
            var actor = Make("a", 0);
            var a1 = Make("a1", 1, hp: 100);
            a1.BaseUnit.CurrentHP = 80;
            var a2 = Make("a2", 2, hp: 100);
            a2.BaseUnit.CurrentHP = 30;
            var a3 = Make("a3", 3, hp: 100);
            a3.BaseUnit.CurrentHP = 90;
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.SingleAlly),
                new List<RuntimeUnit> { actor, a1, a2, a3 }, new List<RuntimeUnit>());
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(a2, r[0]);
        }

        [Test]
        public void SingleAlly_死亡味方は対象外()
        {
            var actor = Make("a", 0);
            var dead = Make("dead", 1);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("alive", 2);
            alive.BaseUnit.CurrentHP = 50;
            var r = TargetEvaluator.GetValidTargets(actor, Waza(TargetingType.SingleAlly),
                new List<RuntimeUnit> { actor, dead, alive }, new List<RuntimeUnit>());
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(alive, r[0]);
        }

        // ── TargetingCondition ──

        [Test]
        public void TargetingCondition_HP半分以下の敵のみフィルタ()
        {
            var actor = Make("a", 0);
            var full = Make("full", 0, hp: 100);
            var half = Make("half", 1, hp: 100);
            half.BaseUnit.CurrentHP = 40;
            var r = TargetEvaluator.GetValidTargets(actor,
                Waza(TargetingType.AllEnemies, cond: u => u.CurrentHP * 2 <= u.MaxHP),
                new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { full, half });
            Assert.AreEqual(1, r.Count);
            Assert.AreSame(half, r[0]);
        }

        // ── null 安全 ──

        [Test]
        public void null_waza_actorで例外なし()
        {
            var actor = Make("a", 0);
            var r1 = TargetEvaluator.GetValidTargets(actor, null,
                new List<RuntimeUnit>(), new List<RuntimeUnit>());
            Assert.AreEqual(0, r1.Count);

            var r2 = TargetEvaluator.GetValidTargets(null, Waza(TargetingType.SingleEnemy),
                null, null);
            Assert.AreEqual(0, r2.Count);
        }
    }
}
