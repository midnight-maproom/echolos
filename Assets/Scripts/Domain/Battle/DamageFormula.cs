using System;

namespace Echolos.Domain.Battle
{
    // 戦闘ダメージ式の純関数静的クラス。
    // 仕様: (ATK * 倍率 + ConstA) * sqrt(HP) / (DEF + ConstB + 環境項)
    // 配置 ATK 補正は呼び出し側で乗算／環境項は terrainBonus 引数で渡す。
    public static class DamageFormula
    {
        public const double ConstA = 0.0;
        public const double ConstB = 10.0;

        public static int ComputeBaseDamage(
            double attackerAtk,
            double wazaMultiplier,
            double defenderCurrentHp,
            double defenderDef,
            double positionAtkCorrection = 1.0,
            double terrainBonus = 0.0)
        {
            double effectiveHp = defenderCurrentHp < 0.0 ? 0.0 : defenderCurrentHp;
            double effectiveDef = defenderDef < 0.0 ? 0.0 : defenderDef;

            double numerator = (attackerAtk * positionAtkCorrection * wazaMultiplier + ConstA) * Math.Sqrt(effectiveHp);
            double denominator = effectiveDef + ConstB + terrainBonus;
            if (denominator < 1.0) denominator = 1.0;

            double raw = numerator / denominator;
            if (raw < 0.0) raw = 0.0;

            return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        }
    }
}
