// ダメージ式の単体テスト。
//
// 式: (ATK * 倍率 + ConstA) * sqrt(HP) / (DEF + ConstB + 環境項)
// ConstA=0 / ConstB=10
//
// 検証対象：
// 1. 基本式（ConstA/ConstB 込み）の素直な評価
// 2. sqrt(HP) の効き
// 3. DEF が分母として効く
// 4. Waza 倍率の乗算
// 5. 配置 ATK 補正の乗算
// 6. 環境項の分母加算
// 7. ATK=0 はダメージ 0
// 8. 負値クランプ（HP / DEF 異常入力でも分母 0 にならない）
using NUnit.Framework;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DamageFormulaTests
    {
        [Test]
        public void 基本式_ATK50_mult1_HP100_DEF0_の素直な評価()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: 100.0, defenderDef: 0.0);
            Assert.AreEqual(50, damage);
        }

        [Test]
        public void HP4倍でダメージ2倍_sqrtHP_の反映確認()
        {
            int baseDmg = DamageFormula.ComputeBaseDamage(50.0, 1.0, 100.0, 0.0);
            int quadHpDmg = DamageFormula.ComputeBaseDamage(50.0, 1.0, 400.0, 0.0);
            Assert.AreEqual(50, baseDmg);
            Assert.AreEqual(100, quadHpDmg);
        }

        [Test]
        public void DEF10で分母が倍になりダメージ半減()
        {
            int damage = DamageFormula.ComputeBaseDamage(50.0, 1.0, 100.0, 10.0);
            Assert.AreEqual(25, damage);
        }

        [Test]
        public void Waza倍率2倍でダメージ2倍()
        {
            int damage = DamageFormula.ComputeBaseDamage(50.0, 2.0, 100.0, 0.0);
            Assert.AreEqual(100, damage);
        }

        [Test]
        public void 配置ATK補正0_85倍で線形にダメージ減衰()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: 100.0, defenderDef: 0.0,
                positionAtkCorrection: 0.85);
            Assert.AreEqual(43, damage);
        }

        [Test]
        public void 環境項10で分母増加しダメージ半減()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: 100.0, defenderDef: 0.0,
                terrainBonus: 10.0);
            Assert.AreEqual(25, damage);
        }

        [Test]
        public void ATK0はダメージ0()
        {
            int damage = DamageFormula.ComputeBaseDamage(0.0, 1.0, 100.0, 0.0);
            Assert.AreEqual(0, damage);
        }

        [Test]
        public void 負値DEFは0クランプで分母が_ConstB_のみ()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: 100.0, defenderDef: -5.0);
            Assert.AreEqual(50, damage);
        }

        [Test]
        public void 負値HPは0クランプでダメージ0()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: -100.0, defenderDef: 0.0);
            Assert.AreEqual(0, damage);
        }

        // 分母 1 クランプ：負の terrainBonus + DEF=0 で分母 0 以下になる除算事故を防ぐ。

        [Test]
        public void 環境項マイナス15_DEF0_で分母1クランプ_ダメージ青天井()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: 100.0, defenderDef: 0.0,
                terrainBonus: -15.0);
            Assert.AreEqual(500, damage);
        }

        [Test]
        public void 環境項マイナス1000の極端値でも分母1クランプ()
        {
            int damage = DamageFormula.ComputeBaseDamage(
                attackerAtk: 50.0, wazaMultiplier: 1.0,
                defenderCurrentHp: 100.0, defenderDef: 0.0,
                terrainBonus: -1000.0);
            Assert.AreEqual(500, damage);
        }
    }
}
