using Echolos.Domain.Effects;

namespace Echolos.Domain.Battle.Synergy
{
    // シナジー段階で 1 対象に付与される 1 効果の定義。
    // Kind / Magnitude / InitialStacks の 3 つで「どの効果を / どの強度で / 何スタックで」を表す。
    // 同段階で複数効果を付与したい場合（水属性の DEF+ と Shield 等）は SynergyTier.Buffs に複数要素として並べる。
    public sealed class SynergyBuff
    {
        public EffectKind Kind { get; }
        public int Magnitude { get; }
        public int InitialStacks { get; }

        public SynergyBuff(EffectKind kind, int magnitude, int initialStacks = 1)
        {
            Kind = kind;
            Magnitude = magnitude;
            InitialStacks = initialStacks < 1 ? 1 : initialStacks;
        }
    }
}
