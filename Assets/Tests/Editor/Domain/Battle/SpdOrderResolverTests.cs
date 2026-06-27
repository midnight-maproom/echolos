using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class SpdOrderResolverTests
    {
        private static RuntimeUnit Make(string idPrefix, int spd, int slot, int hp = 100)
        {
            var u = new Unit($"{idPrefix}_{slot}", $"{idPrefix}_{slot}")
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseSPD = spd,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        [Test]
        public void SPD降順で並ぶ()
        {
            var allies = new List<RuntimeUnit> { Make("a", 10, 0), Make("a", 30, 1), Make("a", 20, 2) };
            var order = SpdOrderResolver.OrderByTurnPriority(allies, null, true);
            Assert.AreEqual("a_1", order[0].BaseUnit.Id);
            Assert.AreEqual("a_2", order[1].BaseUnit.Id);
            Assert.AreEqual("a_0", order[2].BaseUnit.Id);
        }

        [Test]
        public void 同SPDは攻め側陣営優先_味方攻め()
        {
            var allies = new List<RuntimeUnit> { Make("a", 20, 0) };
            var enemies = new List<RuntimeUnit> { Make("e", 20, 0) };
            var order = SpdOrderResolver.OrderByTurnPriority(allies, enemies, isAlliesAttackingSide: true);
            Assert.AreEqual("a_0", order[0].BaseUnit.Id);
            Assert.AreEqual("e_0", order[1].BaseUnit.Id);
        }

        [Test]
        public void 同SPDは攻め側陣営優先_敵攻め()
        {
            var allies = new List<RuntimeUnit> { Make("a", 20, 0) };
            var enemies = new List<RuntimeUnit> { Make("e", 20, 0) };
            var order = SpdOrderResolver.OrderByTurnPriority(allies, enemies, isAlliesAttackingSide: false);
            Assert.AreEqual("e_0", order[0].BaseUnit.Id);
            Assert.AreEqual("a_0", order[1].BaseUnit.Id);
        }

        [Test]
        public void 同SPD同陣営はSlotIndex昇順()
        {
            var allies = new List<RuntimeUnit> { Make("a", 20, 3), Make("a", 20, 1), Make("a", 20, 0) };
            var order = SpdOrderResolver.OrderByTurnPriority(allies, null, true);
            Assert.AreEqual(0, order[0].SlotIndex);
            Assert.AreEqual(1, order[1].SlotIndex);
            Assert.AreEqual(3, order[2].SlotIndex);
        }

        [Test]
        public void 死亡ユニットは並びから除外()
        {
            var dead = Make("a", 30, 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("a", 20, 1);
            var order = SpdOrderResolver.OrderByTurnPriority(new List<RuntimeUnit> { dead, alive }, null, true);
            Assert.AreEqual(1, order.Count);
            Assert.AreEqual("a_1", order[0].BaseUnit.Id);
        }

        [Test]
        public void getSpd指定で実効SPDを差し替えできる()
        {
            var allies = new List<RuntimeUnit> { Make("a", 10, 0), Make("a", 30, 1) };
            // 凍結相当：slot=1 の SPD を 10% に減衰
            var order = SpdOrderResolver.OrderByTurnPriority(allies, null, true,
                getSpd: u => u.SlotIndex == 1 ? (int)(u.BaseUnit.BaseSPD * 0.1) : u.BaseUnit.BaseSPD);
            Assert.AreEqual("a_0", order[0].BaseUnit.Id); // SPD 10
            Assert.AreEqual("a_1", order[1].BaseUnit.Id); // SPD 3
        }

        [Test]
        public void null入力安全_両陣営null()
        {
            var order = SpdOrderResolver.OrderByTurnPriority(null, null, true);
            Assert.AreEqual(0, order.Count);
        }

        // ───── SelectNext（動的順序方式） ─────

        [Test]
        public void SelectNext_未行動かつ生存の最高SPDを返す()
        {
            var slow = Make("a", 10, 0);
            var fast = Make("a", 30, 1);
            var mid = Make("a", 20, 2);
            var picked = SpdOrderResolver.SelectNext(
                new List<RuntimeUnit> { slow, fast, mid }, null, true);
            Assert.AreEqual("a_1", picked.BaseUnit.Id);
        }

        [Test]
        public void SelectNext_既に行動済みのユニットは除外()
        {
            var done = Make("a", 30, 0);
            done.HasActedThisTurn = true;
            var alive = Make("a", 20, 1);
            var picked = SpdOrderResolver.SelectNext(
                new List<RuntimeUnit> { done, alive }, null, true);
            Assert.AreEqual("a_1", picked.BaseUnit.Id);
        }

        [Test]
        public void SelectNext_死亡ユニットは除外()
        {
            var dead = Make("a", 30, 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("a", 20, 1);
            var picked = SpdOrderResolver.SelectNext(
                new List<RuntimeUnit> { dead, alive }, null, true);
            Assert.AreEqual("a_1", picked.BaseUnit.Id);
        }

        [Test]
        public void SelectNext_全員行動済みならnull()
        {
            var u = Make("a", 30, 0);
            u.HasActedThisTurn = true;
            var picked = SpdOrderResolver.SelectNext(
                new List<RuntimeUnit> { u }, null, true);
            Assert.IsNull(picked);
        }

        [Test]
        public void SelectNext_同SPDタイブレークは攻め側優先_slot昇順()
        {
            var ally0 = Make("a", 20, 0);
            var ally1 = Make("a", 20, 1);
            var enemy0 = Make("e", 20, 0);
            var picked = SpdOrderResolver.SelectNext(
                new List<RuntimeUnit> { ally0, ally1 },
                new List<RuntimeUnit> { enemy0 },
                isAlliesAttackingSide: true);
            Assert.AreEqual("a_0", picked.BaseUnit.Id);
        }

        [Test]
        public void SelectNext_getSpdで動的SPDを反映する()
        {
            // ターン中に SPD 変動した状況を再現：a_1 の実 SPD を 5 に下げる
            var u0 = Make("a", 10, 0);
            var u1 = Make("a", 30, 1);
            var picked = SpdOrderResolver.SelectNext(
                new List<RuntimeUnit> { u0, u1 }, null, true,
                getSpd: u => u.SlotIndex == 1 ? 5 : u.BaseUnit.BaseSPD);
            Assert.AreEqual("a_0", picked.BaseUnit.Id, "u_1 は SPD 5 に下げられて u_0 (SPD 10) より遅くなる");
        }

        [Test]
        public void 完全決定論_同入力で同順序()
        {
            var allies1 = new List<RuntimeUnit> { Make("a", 20, 0), Make("a", 20, 1), Make("a", 20, 2) };
            var enemies1 = new List<RuntimeUnit> { Make("e", 20, 0), Make("e", 20, 1) };
            var order1 = SpdOrderResolver.OrderByTurnPriority(allies1, enemies1, true);

            var allies2 = new List<RuntimeUnit> { Make("a", 20, 0), Make("a", 20, 1), Make("a", 20, 2) };
            var enemies2 = new List<RuntimeUnit> { Make("e", 20, 0), Make("e", 20, 1) };
            var order2 = SpdOrderResolver.OrderByTurnPriority(allies2, enemies2, true);

            Assert.AreEqual(order1.Count, order2.Count);
            for (int i = 0; i < order1.Count; i++)
                Assert.AreEqual(order1[i].BaseUnit.Id, order2[i].BaseUnit.Id, $"i={i}");
        }
    }
}
