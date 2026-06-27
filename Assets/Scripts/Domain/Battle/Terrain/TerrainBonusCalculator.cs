using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Terrain
{
    // 地形補正（環境項）の純関数。DamageFormula.ComputeBaseDamage の terrainBonus 引数に
    // 渡す数値を、対象ユニットの属性 × 地形種別 × 層別強度 から算出する。
    // 環境項はダメージ式の分母にのみ作用する（正＝被ダメ減 / 負＝被ダメ増）。
    public static class TerrainBonusCalculator
    {
        public static int GetTerrainBonus(Element unitElement, TerrainKind terrain, TerrainStrength strength)
        {
            if (terrain == TerrainKind.Neutral) return 0;
            if (unitElement != Element.Fire && unitElement != Element.Water) return 0;

            int idx = (int)strength;
            if (idx < 0 || idx >= TerrainConstants.AlphaByStrength.Count) return 0;
            int alpha = TerrainConstants.AlphaByStrength[idx];

            bool isSelfAdvantage =
                (terrain == TerrainKind.FireAdvantage && unitElement == Element.Fire) ||
                (terrain == TerrainKind.WaterAdvantage && unitElement == Element.Water);
            if (isSelfAdvantage) return alpha;

            bool isOpposite =
                (terrain == TerrainKind.FireAdvantage && unitElement == Element.Water) ||
                (terrain == TerrainKind.WaterAdvantage && unitElement == Element.Fire);
            if (isOpposite) return -alpha;

            return 0;
        }
    }
}
