namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 継続ダメージ（DOT）。ターン終了時に Stacks × max(1, Magnitude) の HP 直接ダメージ。
    /// Shield 貫通（Burn 蓄積モデル §1.4.4）。Stacks 蓄積モデル（MaxStacks=99 等）。
    /// </summary>
    public sealed class ContinuousDot : EffectBase
    {
        public float Magnitude { get; set; }

        public ContinuousDot(EffectKind kind, float magnitude)
        {
            Kind = kind;
            Magnitude = magnitude;
        }

        public override IEffect Clone()
        {
            var copy = new ContinuousDot(Kind, Magnitude);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 継続回復（HOT）。ターン終了時に round(MaxHP × Magnitude/100) × Stacks を回復。
    /// 最大 HP でクランプ。光属性シナジー等。
    /// </summary>
    public sealed class ContinuousHot : EffectBase
    {
        public float Magnitude { get; set; }

        public ContinuousHot(EffectKind kind, float magnitude)
        {
            Kind = kind;
            Magnitude = magnitude;
        }

        public override IEffect Clone()
        {
            var copy = new ContinuousHot(Kind, Magnitude);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
