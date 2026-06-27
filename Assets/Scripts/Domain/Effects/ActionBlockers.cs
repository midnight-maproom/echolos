namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 凍結。SPD を Stacks × 10% 低下させ、Stacks ≥ 10 で行動不能。ターン終了時 Stacks -1。
    /// </summary>
    public sealed class FreezeEffect : EffectBase
    {
        public FreezeEffect()
        {
            Kind = EffectKind.Freeze;
        }

        public override IEffect Clone()
        {
            var copy = new FreezeEffect();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 麻痺。Stacks 合計が許容量（ParalysisTolerance）以上で行動不能。
    /// 発動のたびに許容量が倍化（1→2→4→8...）。麻痺発動時にスタック消去。
    /// </summary>
    public sealed class ParalysisEffect : EffectBase
    {
        public ParalysisEffect()
        {
            Kind = EffectKind.Paralysis;
        }

        public override IEffect Clone()
        {
            var copy = new ParalysisEffect();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 呪い。HP ≤ Stacks × 10 で行動開始前に即死（Shield 無視）。
    /// </summary>
    public sealed class CurseEffect : EffectBase
    {
        public CurseEffect()
        {
            Kind = EffectKind.Curse;
        }

        public override IEffect Clone()
        {
            var copy = new CurseEffect();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
