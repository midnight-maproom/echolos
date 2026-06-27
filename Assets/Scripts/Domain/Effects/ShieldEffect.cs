namespace Echolos.Domain.Effects
{
    /// <summary>
    /// シールド。Stacks が残数を表し、1 ヒット単位で消費される（多段で剥がれる）。
    /// 攻撃ダメージのみ吸収（DOT 貫通）。Magnitude は使われない（残数管理は Stacks）。
    /// </summary>
    public sealed class ShieldEffect : EffectBase
    {
        public ShieldEffect()
        {
            Kind = EffectKind.Shield;
        }

        public override IEffect Clone()
        {
            var copy = new ShieldEffect();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
