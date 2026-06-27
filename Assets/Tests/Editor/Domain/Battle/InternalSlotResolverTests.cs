// 内部スロット解決ヘルパの単体テスト。
//
// 配置 UI は SlotIndex 0..5（空き含む）。内部スロットは生存者だけを
// SlotIndex 順に並べた 0 ベース連番。配置 ATK 補正・ターゲティング・
// 反撃判定は内部スロットで行う。戦闘中欠けで動的再計算。
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class InternalSlotResolverTests
    {
        private static RuntimeUnit MakeRu(int slot, int hp = 100)
        {
            var u = new Unit("u" + slot, "U" + slot) { MaxHP = hp, CurrentHP = hp, State = UnitState.Active };
            return new RuntimeUnit(u, slot);
        }

        private static void Kill(RuntimeUnit ru) { ru.BaseUnit.CurrentHP = 0; }

        [Test]
        public void 全員生存_SlotIndexに空きあり_詰めて連番化()
        {
            var a = MakeRu(0); var b = MakeRu(2); var c = MakeRu(5);
            var side = new List<RuntimeUnit> { a, b, c };

            Assert.AreEqual(0, InternalSlotResolver.GetInternalSlotIndex(side, a));
            Assert.AreEqual(1, InternalSlotResolver.GetInternalSlotIndex(side, b));
            Assert.AreEqual(2, InternalSlotResolver.GetInternalSlotIndex(side, c));
        }

        [Test]
        public void 中央死亡_生存者だけ詰め直し_死亡はマイナス1()
        {
            var a = MakeRu(0); var b = MakeRu(2); var c = MakeRu(5);
            Kill(b);
            var side = new List<RuntimeUnit> { a, b, c };

            Assert.AreEqual(0, InternalSlotResolver.GetInternalSlotIndex(side, a));
            Assert.AreEqual(-1, InternalSlotResolver.GetInternalSlotIndex(side, b));
            Assert.AreEqual(1, InternalSlotResolver.GetInternalSlotIndex(side, c));
        }

        [Test]
        public void 自陣にいないユニットはマイナス1()
        {
            var a = MakeRu(0); var b = MakeRu(2);
            var outsider = MakeRu(3);
            var side = new List<RuntimeUnit> { a, b };

            Assert.AreEqual(-1, InternalSlotResolver.GetInternalSlotIndex(side, outsider));
        }

        [Test]
        public void 全員死亡_GetAliveCountは0()
        {
            var a = MakeRu(0); var b = MakeRu(2);
            Kill(a); Kill(b);
            var side = new List<RuntimeUnit> { a, b };

            Assert.AreEqual(0, InternalSlotResolver.GetAliveCount(side));
            Assert.AreEqual(-1, InternalSlotResolver.GetInternalSlotIndex(side, a));
        }

        [Test]
        public void GetAliveCount_3生存1死亡で3()
        {
            var a = MakeRu(0); var b = MakeRu(1); var c = MakeRu(2); var d = MakeRu(5);
            Kill(c);
            var side = new List<RuntimeUnit> { a, b, c, d };

            Assert.AreEqual(3, InternalSlotResolver.GetAliveCount(side));
        }

        [Test]
        public void null入力は例外を出さず安全に処理()
        {
            Assert.AreEqual(-1, InternalSlotResolver.GetInternalSlotIndex(null, MakeRu(0)));
            Assert.AreEqual(-1, InternalSlotResolver.GetInternalSlotIndex(new List<RuntimeUnit> { MakeRu(0) }, null));
            Assert.AreEqual(0, InternalSlotResolver.GetAliveCount(null));
        }
    }
}
