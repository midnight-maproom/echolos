// Unit / RuntimeUnit のステータス周りの単体テスト。
//
// 検証対象：
// 1. Unit.DEF / AppliedUpgrades(DefBoost) が RuntimeUnit.EffectiveDEF に反映される
// 2. DefenseUp / DefenseDown バフが EffectiveDEF に加減算される（最低 0 クランプ）
// 3. AttackKind（Melee/Ranged）と TargetingDirection（FromFront/FromBack）の独立軸
// 4. RuntimeUnit.ShieldStacks の合計集計（複数 Shield 効果の Stacks 合計）
using NUnit.Framework;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class RuntimeUnitStatTests
    {
        // ── テストヘルパ ──

        private static Unit MakeUnit(int hp = 100, int atk = 20, int def = 10, int spd = 5)
        {
            return new Unit("test_unit", "テストユニット")
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                BaseSPD = spd,
                State = UnitState.Active,
            };
        }

        private static RuntimeUnit MakeRu(Unit u, int slot = 0) => new RuntimeUnit(u, slot);

        // ══════════════════════════════════════════════
        // 1. DEF 統合（PDEF/MDEF → DEF への移行検証）
        // ══════════════════════════════════════════════

        [Test]
        public void EffectiveDEFは基礎値をそのまま返す()
        {
            var u = MakeUnit(def: 15);
            var ru = MakeRu(u);
            Assert.AreEqual(15, ru.EffectiveDEF);
        }

        [Test]
        public void EffectiveDEFはAppliedUpgradesのDefBoostを加算する()
        {
            var u = MakeUnit(def: 10);
            u.AppliedUpgrades.Add(new UnitUpgrade("up_def_a", "DEF +3", "", UpgradeKind.DefBoost, 3));
            u.AppliedUpgrades.Add(new UnitUpgrade("up_def_b", "DEF +3", "", UpgradeKind.DefBoost, 3));
            u.Level = 3;
            var ru = MakeRu(u);
            // 10 + 3 + 3 = 16
            Assert.AreEqual(16, ru.EffectiveDEF);
        }

        [Test]
        public void DefenseUpバフがEffectiveDEFに加算される()
        {
            var u = MakeUnit(def: 10);
            var ru = MakeRu(u);
            ru.AddEffect(TestEff.Eff(EffectKind.DefenseUp, magnitude: 5));
            // 10 + 5 = 15
            Assert.AreEqual(15, ru.EffectiveDEF);
        }

        [Test]
        public void DefenseDownデバフがEffectiveDEFから減算される()
        {
            var u = MakeUnit(def: 10);
            var ru = MakeRu(u);
            ru.AddEffect(TestEff.Eff(EffectKind.DefenseDown, magnitude: 4));
            // 10 - 4 = 6
            Assert.AreEqual(6, ru.EffectiveDEF);
        }

        [Test]
        public void EffectiveDEFは負値にならない_最低0クランプ()
        {
            var u = MakeUnit(def: 5);
            var ru = MakeRu(u);
            ru.AddEffect(TestEff.Eff(EffectKind.DefenseDown, magnitude: 100));
            // 5 - 100 = -95 → クランプで 0
            Assert.AreEqual(0, ru.EffectiveDEF);
        }

        [Test]
        public void DefenseUpとDownはスタック数に比例する()
        {
            var u = MakeUnit(def: 10);
            var ru = MakeRu(u);
            ru.AddEffect(TestEff.Eff(EffectKind.DefenseUp, magnitude: 3, stacks: 4));
            // 10 + 3 × 4 = 22
            Assert.AreEqual(22, ru.EffectiveDEF);
        }

        // ══════════════════════════════════════════════
        // 2. AttackKind × TargetingDirection の独立軸
        // ══════════════════════════════════════════════

        [Test]
        public void AttackKindのデフォルトはMelee()
        {
            var u = MakeUnit();
            Assert.AreEqual(AttackKind.Melee, u.AttackKind);
        }

        [Test]
        public void TargetingDirectionのデフォルトはFromFront()
        {
            var u = MakeUnit();
            Assert.AreEqual(TargetingDirection.FromFront, u.TargetingDirection);
        }

        [Test]
        public void AttackKindとTargetingDirectionは独立に設定できる_近接前狙い()
        {
            var u = MakeUnit();
            u.AttackKind = AttackKind.Melee;
            u.TargetingDirection = TargetingDirection.FromFront;
            Assert.AreEqual(AttackKind.Melee, u.AttackKind);
            Assert.AreEqual(TargetingDirection.FromFront, u.TargetingDirection);
        }

        [Test]
        public void AttackKindとTargetingDirectionは独立に設定できる_遠隔後狙い()
        {
            var u = MakeUnit();
            u.AttackKind = AttackKind.Ranged;
            u.TargetingDirection = TargetingDirection.FromBack;
            Assert.AreEqual(AttackKind.Ranged, u.AttackKind);
            Assert.AreEqual(TargetingDirection.FromBack, u.TargetingDirection);
        }

        [Test]
        public void AttackKindとTargetingDirectionは独立に設定できる_遠隔前狙い_旧Mid置換()
        {
            // 旧 AttackRange.Mid（位置を問わず敵前列を攻撃）は AttackKind.Ranged + FromFront で表現される
            var u = MakeUnit();
            u.AttackKind = AttackKind.Ranged;
            u.TargetingDirection = TargetingDirection.FromFront;
            Assert.AreEqual(AttackKind.Ranged, u.AttackKind);
            Assert.AreEqual(TargetingDirection.FromFront, u.TargetingDirection);
        }

        [Test]
        public void AttackKindとTargetingDirectionは独立に設定できる_近接後狙い_暗殺者枠()
        {
            // 新設計の「近接後狙い」（前衛で反撃食らうリスクを取って敵後衛をピンポイント）
            var u = MakeUnit();
            u.AttackKind = AttackKind.Melee;
            u.TargetingDirection = TargetingDirection.FromBack;
            Assert.AreEqual(AttackKind.Melee, u.AttackKind);
            Assert.AreEqual(TargetingDirection.FromBack, u.TargetingDirection);
        }

        // ══════════════════════════════════════════════
        // 3. Shield Stacks（EffectKind.Shield 集計）
        // ══════════════════════════════════════════════

        [Test]
        public void ShieldStacksのデフォルトは0()
        {
            var ru = MakeRu(MakeUnit());
            Assert.AreEqual(0, ru.ShieldStacks);
        }

        [Test]
        public void Shield効果のStacks値が反映される()
        {
            var ru = MakeRu(MakeUnit());
            ru.AddEffect(TestEff.Eff(EffectKind.Shield, stacks: 3));
            Assert.AreEqual(3, ru.ShieldStacks);
        }

        [Test]
        public void 複数のShield効果のStacksは合計される()
        {
            var ru = MakeRu(MakeUnit());
            ru.AddEffect(TestEff.Eff(EffectKind.Shield, stacks: 2));
            ru.AddEffect(TestEff.Eff(EffectKind.Shield, stacks: 1));
            Assert.AreEqual(3, ru.ShieldStacks);
        }
    }
}
