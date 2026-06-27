using NUnit.Framework;
using System.Collections.Generic;
using Echolos.Domain.Models;
using Echolos.Domain.Battle.Synergy;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class ElementCounterTests
    {
        private static RuntimeUnit Make(Element el, int slot, int hp = 100)
        {
            var u = new Unit($"u{slot}", $"u{slot}", el)
            {
                MaxHP = hp,
                CurrentHP = hp,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        [Test]
        public void 火3体_水2体_火カウント3_水カウント2()
        {
            var list = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0), Make(Element.Water, 1), Make(Element.Fire, 2),
                Make(Element.Water, 3), Make(Element.Fire, 4),
            };
            Assert.AreEqual(3, ElementCounter.CountAliveByElement(list, Element.Fire));
            Assert.AreEqual(2, ElementCounter.CountAliveByElement(list, Element.Water));
            Assert.AreEqual(0, ElementCounter.CountAliveByElement(list, Element.None));
        }

        [Test]
        public void 死亡ユニットはカウント外()
        {
            var dead = Make(Element.Fire, 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make(Element.Fire, 1);
            var list = new List<RuntimeUnit> { dead, alive };
            Assert.AreEqual(1, ElementCounter.CountAliveByElement(list, Element.Fire));
        }

        [Test]
        public void null入力は0を返す()
        {
            Assert.AreEqual(0, ElementCounter.CountAliveByElement(null, Element.Fire));
        }

        [Test]
        public void 空リストは0を返す()
        {
            Assert.AreEqual(0, ElementCounter.CountAliveByElement(new List<RuntimeUnit>(), Element.Fire));
        }
    }
}
