// VSPrototypeInteriorService の個別 Lv 強化（ExecuteUpgradeUnit）契約検証。
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Models;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class InteriorServiceTests
    {
        private static Unit MakeUnit(string id = "fire_swordsman", int level = 1, int upgradeCount = 3)
        {
            var u = new Unit(id, $"name_{id}") { Level = level };
            for (int i = 0; i < upgradeCount; i++)
                u.AvailableUpgrades.Add(
                    new UnitUpgrade($"up_{i}", $"強化 {i}", "", UpgradeKind.AtkBoost, 5));
            return u;
        }

        private static VSPrototypeInteriorState MakeStateWithAp(int ap = 2)
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(actionPointsPerRound: ap, initialDraftCount: 0);
            s.ResetForNewRound();
            return s;
        }

        [Test]
        public void ExecuteUpgradeUnit_成功_Lv上昇_AppliedUpgrades追加_AvailableUpgrades減少()
        {
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp();
            var unit = MakeUnit();
            var pick = unit.AvailableUpgrades[1];

            bool ok = svc.ExecuteUpgradeUnit(s, unit, pick);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, unit.Level);
            Assert.Contains(pick, (List<UnitUpgrade>)unit.AppliedUpgrades);
            Assert.AreEqual(2, unit.AvailableUpgrades.Count);
            Assert.AreEqual(1, s.ActionPoints, "2 ap → 1 ap");
        }

        [Test]
        public void ExecuteUpgradeUnit_固有ユニット_false()
        {
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp();
            var unit = MakeUnit(id: UniqueUnitIds.Princess);
            var pick = unit.AvailableUpgrades[0];

            Assert.IsFalse(svc.ExecuteUpgradeUnit(s, unit, pick));
            Assert.AreEqual(1, unit.Level, "Princess は内政画面の強化対象外");
        }

        [Test]
        public void ExecuteUpgradeUnit_Lv上限到達_false()
        {
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp();
            var unit = MakeUnit(level: VSPrototypeInteriorState.MaxUnitLevel);
            var pick = unit.AvailableUpgrades[0];

            Assert.IsFalse(svc.ExecuteUpgradeUnit(s, unit, pick));
        }

        [Test]
        public void ExecuteUpgradeUnit_AvailableUpgrades空_false()
        {
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp();
            var unit = MakeUnit(upgradeCount: 0);

            Assert.IsFalse(svc.ExecuteUpgradeUnit(s, unit, null));
        }

        [Test]
        public void ExecuteUpgradeUnit_AvailableUpgradesに無いUpgrade_false()
        {
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp();
            var unit = MakeUnit();
            var alien = new UnitUpgrade("alien", "外部強化", "", UpgradeKind.AtkBoost, 5);

            Assert.IsFalse(svc.ExecuteUpgradeUnit(s, unit, alien));
        }

        [Test]
        public void ExecuteUpgradeUnit_同一ラウンド複数回_行動力の続く限り全て成功()
        {
            // 強化は召集と違い同一ラウンド複数回可。1 体目を Lv2 → Lv3 まで、別ユニットも 1 段階強化できる。
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp(ap: 3);
            var unitA = MakeUnit(id: "fire_swordsman");
            var unitB = MakeUnit(id: "water_swordsman");

            bool r1 = svc.ExecuteUpgradeUnit(s, unitA, unitA.AvailableUpgrades[0]);
            bool r2 = svc.ExecuteUpgradeUnit(s, unitA, unitA.AvailableUpgrades[0]);
            bool r3 = svc.ExecuteUpgradeUnit(s, unitB, unitB.AvailableUpgrades[0]);

            Assert.IsTrue(r1);
            Assert.IsTrue(r2);
            Assert.IsTrue(r3);
            Assert.AreEqual(3, unitA.Level);
            Assert.AreEqual(2, unitB.Level);
            Assert.AreEqual(0, s.ActionPoints, "3 ap → 0 ap");
        }

        [Test]
        public void ExecuteUpgradeUnit_実行履歴に記録しない()
        {
            // TryConsumeActionPoint 経由なので _executedActions には積まれない。
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp(ap: 2);
            var unit = MakeUnit();

            svc.ExecuteUpgradeUnit(s, unit, unit.AvailableUpgrades[0]);

            Assert.IsFalse(s.HasExecutedThisRound(VSPrototypeInteriorAction.UpgradeUnitType),
                "強化アクションは実行履歴に記録されない（複数回可能のため）");
        }

        [Test]
        public void ExecuteUpgradeUnit_行動力ゼロ_false()
        {
            var svc = new VSPrototypeInteriorService();
            var s = MakeStateWithAp(ap: 1);
            s.MarkActionExecuted(VSPrototypeInteriorAction.Conscript); // ap を 0 にする
            var unit = MakeUnit();

            Assert.IsFalse(svc.ExecuteUpgradeUnit(s, unit, unit.AvailableUpgrades[0]));
        }
    }
}
