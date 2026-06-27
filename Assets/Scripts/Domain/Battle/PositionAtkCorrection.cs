using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 配置 ATK 補正カーブ。距離 0..5 で 100/100/95/85/75/65%。
    // 近接：内部スロット 0（最前）からの距離
    // 遠隔：最後尾の内部スロットからの距離
    // 6 体未満の編成では最後尾基準が前にシフトするため、少人数で補正がほぼ消える（仕様 320 §2.4）
    public static class PositionAtkCorrection
    {
        private static readonly double[] Curve = { 1.0, 1.0, 0.95, 0.85, 0.75, 0.65 };

        public static double GetCorrection(int internalSlotIndex, int aliveCountOnSide, AttackKind kind)
        {
            // None は攻撃 Waza を発動しないため AttackEffect から呼ばれない想定だが、
            // 防御的に補正なし（1.0 倍）を返す。
            if (kind == AttackKind.None) return 1.0;
            int distance;
            if (kind == AttackKind.Melee)
            {
                distance = internalSlotIndex;
            }
            else
            {
                distance = (aliveCountOnSide - 1) - internalSlotIndex;
            }
            return GetCorrectionByDistance(distance);
        }

        public static double GetCorrectionByDistance(int distanceFromBase)
        {
            if (distanceFromBase < 0) return Curve[0];
            if (distanceFromBase >= Curve.Length) return Curve[Curve.Length - 1];
            return Curve[distanceFromBase];
        }
    }
}
