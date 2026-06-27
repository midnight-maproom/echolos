using NUnit.Framework;
using Echolos.Domain.Models;
using Echolos.Domain.Battle.Terrain;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class TerrainBonusCalculatorTests
    {
        // ── 中立地形 ──

        [Test]
        public void 中立地形は補正なし_火属性()
        {
            Assert.AreEqual(0, TerrainBonusCalculator.GetTerrainBonus(
                Element.Fire, TerrainKind.Neutral, TerrainStrength.Heavy));
        }

        [Test]
        public void 中立地形は補正なし_水属性()
        {
            Assert.AreEqual(0, TerrainBonusCalculator.GetTerrainBonus(
                Element.Water, TerrainKind.Neutral, TerrainStrength.Heavy));
        }

        // ── 自属性 = +α ──

        [Test]
        public void 火属性_火有利_Light_プラス5()
        {
            Assert.AreEqual(5, TerrainBonusCalculator.GetTerrainBonus(
                Element.Fire, TerrainKind.FireAdvantage, TerrainStrength.Light));
        }

        [Test]
        public void 水属性_水有利_Medium_プラス10()
        {
            Assert.AreEqual(10, TerrainBonusCalculator.GetTerrainBonus(
                Element.Water, TerrainKind.WaterAdvantage, TerrainStrength.Medium));
        }

        [Test]
        public void 火属性_火有利_Heavy_プラス15()
        {
            Assert.AreEqual(15, TerrainBonusCalculator.GetTerrainBonus(
                Element.Fire, TerrainKind.FireAdvantage, TerrainStrength.Heavy));
        }

        // ── 逆属性 = -α ──

        [Test]
        public void 水属性_火有利_Light_マイナス5()
        {
            Assert.AreEqual(-5, TerrainBonusCalculator.GetTerrainBonus(
                Element.Water, TerrainKind.FireAdvantage, TerrainStrength.Light));
        }

        [Test]
        public void 火属性_水有利_Medium_マイナス10()
        {
            Assert.AreEqual(-10, TerrainBonusCalculator.GetTerrainBonus(
                Element.Fire, TerrainKind.WaterAdvantage, TerrainStrength.Medium));
        }

        [Test]
        public void 水属性_火有利_Heavy_マイナス15()
        {
            Assert.AreEqual(-15, TerrainBonusCalculator.GetTerrainBonus(
                Element.Water, TerrainKind.FireAdvantage, TerrainStrength.Heavy));
        }

        // ── 補助属性（None / Light / Dark 等）= 補正なし ──

        [Test]
        public void 補助属性None_火有利地形でも補正なし()
        {
            Assert.AreEqual(0, TerrainBonusCalculator.GetTerrainBonus(
                Element.None, TerrainKind.FireAdvantage, TerrainStrength.Heavy));
        }

        [Test]
        public void 補助属性Light_水有利地形でも補正なし()
        {
            Assert.AreEqual(0, TerrainBonusCalculator.GetTerrainBonus(
                Element.Light, TerrainKind.WaterAdvantage, TerrainStrength.Heavy));
        }

        [Test]
        public void 補助属性Dark_火有利地形でも補正なし()
        {
            Assert.AreEqual(0, TerrainBonusCalculator.GetTerrainBonus(
                Element.Dark, TerrainKind.FireAdvantage, TerrainStrength.Heavy));
        }
    }
}
