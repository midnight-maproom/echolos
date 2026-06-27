using Echolos.Domain.Effects;

namespace Echolos.Domain.Battle.Aura
{
    // オーラ機構で 1 対象に付与される 1 効果の定義（基本値）。
    // 実際に陣営に付与される際は AuraApplier が SourceUnit の AppliedUpgrades から
    // 同 BoostKind の Magnitude 合計を加算して最終値を決定する。
    public sealed class AuraBuff
    {
        public EffectKind Kind { get; }
        public int BaseMagnitude { get; }
        public int InitialStacks { get; }

        public AuraBuff(EffectKind kind, int baseMagnitude, int initialStacks = 1)
        {
            Kind = kind;
            BaseMagnitude = baseMagnitude;
            InitialStacks = initialStacks < 1 ? 1 : initialStacks;
        }
    }
}
