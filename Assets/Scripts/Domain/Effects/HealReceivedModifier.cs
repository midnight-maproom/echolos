namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 受け回復量 % モディファイア（HealEffect 内の最終回復量に乗算される割引）。
    /// 計算式：最終回復 = 素回復 × max(0, 1 - Magnitude/100 × Stacks)
    /// プロト Kind は SearingWound（熱傷）のみ。Stacks 蓄積・Cleanse 対象・Permanent。
    /// </summary>
    public sealed class HealReceivedModifier : EffectBase
    {
        public bool IsBuff { get; }
        public float Magnitude { get; set; }

        public HealReceivedModifier(EffectKind kind, float magnitude, bool isBuff = false)
        {
            Kind = kind;
            Magnitude = magnitude;
            IsBuff = isBuff;
        }

        public override IEffect Clone()
        {
            var copy = new HealReceivedModifier(Kind, Magnitude, IsBuff);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
