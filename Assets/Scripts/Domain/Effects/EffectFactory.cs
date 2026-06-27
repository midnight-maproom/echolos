using System;

namespace Echolos.Domain.Effects
{
    /// <summary>
    /// <see cref="EffectKind"/> と数値から派生クラスのインスタンスを生成する標準ファクトリ。
    /// 共通フィールド（Lifetime / IsUndispellable / Stacks 等）は呼び出し側で初期化する。
    /// </summary>
    public static class EffectFactory
    {
        public static EffectBase CreateByKind(EffectKind kind, float magnitude)
        {
            switch (kind)
            {
                case EffectKind.AttackUp:
                case EffectKind.AttackDown:
                case EffectKind.DefenseUp:
                case EffectKind.DefenseDown:
                    return new AbilityModifier(kind, magnitude);

                case EffectKind.EvasionUp:
                case EffectKind.EvasionDown:
                    return new EvasionModifier(kind, magnitude);

                case EffectKind.OutgoingDamageUp:
                    return new OutgoingDamageModifier(magnitude);
                case EffectKind.IncomingDamageDown:
                    return new IncomingDamageModifier(magnitude);
                case EffectKind.CriticalRateUp:
                    return new CriticalRateModifier(magnitude);
                case EffectKind.CounterDamageUp:
                    return new CounterDamageModifier(magnitude);

                case EffectKind.SearingWound:
                    return new HealReceivedModifier(EffectKind.SearingWound, magnitude);

                case EffectKind.Burn:
                    return new ContinuousDot(EffectKind.Burn, magnitude);
                case EffectKind.HealOverTime:
                    return new ContinuousHot(EffectKind.HealOverTime, magnitude);

                case EffectKind.Freeze:           return new FreezeEffect();
                case EffectKind.Paralysis:        return new ParalysisEffect();
                case EffectKind.Curse:            return new CurseEffect();
                case EffectKind.Shield:           return new ShieldEffect();
                case EffectKind.SilencedCounter:  return new SilencedCounterFlag();
                case EffectKind.ReviveInvalid:    return new ReviveInvalidFlag();
                case EffectKind.IgnoreCounter:    return new IgnoreCounterFlag();
                case EffectKind.SelfDefenseGuard: return new SelfGuard(magnitude);

                default:
                    throw new ArgumentException($"未対応の EffectKind: {kind}", nameof(kind));
            }
        }
    }
}
