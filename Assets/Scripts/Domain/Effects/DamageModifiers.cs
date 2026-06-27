namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 与ダメージ % 加算（DamageFormula 出力後に乗算）。
    /// 加算スタックで合算され青天井（攻撃側にキャップなし）。
    /// </summary>
    public sealed class OutgoingDamageModifier : EffectBase
    {
        public bool IsBuff { get; }
        public float Magnitude { get; set; }

        public OutgoingDamageModifier(float magnitude, bool isBuff = true)
        {
            Kind = EffectKind.OutgoingDamageUp;
            Magnitude = magnitude;
            IsBuff = isBuff;
        }

        public override IEffect Clone()
        {
            var copy = new OutgoingDamageModifier(Magnitude, IsBuff);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 被ダメージ % カット（DamageFormula 出力後に乗算）。
    /// 加算スタックで合算され、防御側キャップ 80%。
    /// </summary>
    public sealed class IncomingDamageModifier : EffectBase
    {
        public bool IsBuff { get; }
        public float Magnitude { get; set; }

        public IncomingDamageModifier(float magnitude, bool isBuff = true)
        {
            Kind = EffectKind.IncomingDamageDown;
            Magnitude = magnitude;
            IsBuff = isBuff;
        }

        public override IEffect Clone()
        {
            var copy = new IncomingDamageModifier(Magnitude, IsBuff);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// クリティカル率モディファイア（% 値）。
    /// 加算スタックで合算され、クリ判定で参照される（クリ倍率は別軸）。
    /// </summary>
    public sealed class CriticalRateModifier : EffectBase
    {
        public bool IsBuff { get; }
        public float Magnitude { get; set; }

        public CriticalRateModifier(float magnitude, bool isBuff = true)
        {
            Kind = EffectKind.CriticalRateUp;
            Magnitude = magnitude;
            IsBuff = isBuff;
        }

        public override IEffect Clone()
        {
            var copy = new CriticalRateModifier(Magnitude, IsBuff);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 反撃ダメージ % 増加（自身の反撃 Waza の最終出力にのみ乗算）。
    /// 通常攻撃には乗らない。炎の大盾兵パッシブ等。
    /// </summary>
    public sealed class CounterDamageModifier : EffectBase
    {
        public bool IsBuff { get; }
        public float Magnitude { get; set; }

        public CounterDamageModifier(float magnitude, bool isBuff = true)
        {
            Kind = EffectKind.CounterDamageUp;
            Magnitude = magnitude;
            IsBuff = isBuff;
        }

        public override IEffect Clone()
        {
            var copy = new CounterDamageModifier(Magnitude, IsBuff);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
