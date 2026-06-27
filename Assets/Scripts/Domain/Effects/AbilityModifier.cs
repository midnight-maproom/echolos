using System;

namespace Echolos.Domain.Effects
{
    /// <summary>基礎ステータス（ATK / DEF）への加算で参照されるバフ・デバフ。</summary>
    public enum AbilityStat
    {
        Attack,
        Defense,
    }

    /// <summary>
    /// 基礎値加算系（AttackUp / AttackDown / DefenseUp / DefenseDown）。
    /// 実効ステ計算で `基礎値 + Σ(Magnitude × Stacks)` を加算する。
    /// Stat / IsBuff は Kind から自動判定。
    /// </summary>
    public sealed class AbilityModifier : EffectBase
    {
        public AbilityStat Stat { get; private set; }
        public bool IsBuff { get; private set; }
        public float Magnitude { get; set; }

        public AbilityModifier(EffectKind kind, float magnitude)
        {
            Kind = kind;
            Magnitude = magnitude;
            (Stat, IsBuff) = MetaFromKind(kind);
        }

        private static (AbilityStat, bool) MetaFromKind(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.AttackUp:    return (AbilityStat.Attack,  true);
                case EffectKind.AttackDown:  return (AbilityStat.Attack,  false);
                case EffectKind.DefenseUp:   return (AbilityStat.Defense, true);
                case EffectKind.DefenseDown: return (AbilityStat.Defense, false);
                default:
                    throw new ArgumentException(
                        $"AbilityModifier の Kind ではない: {kind}", nameof(kind));
            }
        }

        public override IEffect Clone()
        {
            var copy = new AbilityModifier(Kind, Magnitude);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
