using Echolos.Domain.Effects;

namespace Echolos.Tests.Domain
{
    /// <summary>テスト用 IEffect 生成ヘルパ。旧 StatusEffect/StatusEffect.Create* 相当の薄いラッパ。</summary>
    internal static class TestEff
    {
        /// <summary>
        /// EffectKind + 任意の共通フィールドで派生クラスを生成。
        /// Lifetime / IsCleansable を省略した場合、Cleanse 対象 Kind（Burn / Freeze / Paralysis / Curse /
        /// SearingWound）は仕様準拠で <see cref="Lifetime.Permanent"/> + IsCleansable=true を既定とする。
        /// それ以外は <see cref="Lifetime.Triggered"/> + IsCleansable=false。明示指定があればそれを優先。
        /// </summary>
        public static IEffect Eff(
            EffectKind kind,
            float magnitude = 0f,
            int stacks = 1,
            int remainingTurns = -1,
            int maxStacks = 1,
            string sourceAbilityName = null,
            string auraSourceId = null,
            Lifetime? lifetime = null,
            bool isUndispellable = false,
            bool? isCleansable = null)
        {
            var e = EffectFactory.CreateByKind(kind, magnitude);
            e.Stacks = stacks;
            e.RemainingTurns = remainingTurns;
            e.MaxStacks = maxStacks;
            e.SourceAbilityName = sourceAbilityName;
            e.AuraSourceId = auraSourceId;
            e.Lifetime = lifetime ?? DefaultLifetime(kind);
            e.IsUndispellable = isUndispellable;
            e.IsCleansable = isCleansable ?? IsCleansableKind(kind);
            return e;
        }

        private static bool IsCleansableKind(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Burn:
                case EffectKind.Freeze:
                case EffectKind.Paralysis:
                case EffectKind.Curse:
                case EffectKind.SearingWound:
                    return true;
                default:
                    return false;
            }
        }

        private static Lifetime DefaultLifetime(EffectKind kind)
            => IsCleansableKind(kind) ? Lifetime.Permanent : Lifetime.Triggered;

        /// <summary>常在型（Persistent・解除不能・永続）。</summary>
        public static IEffect Persistent(EffectKind kind, float magnitude, string sourceAbilityName = null, int maxStacks = 1)
            => EffectDefinition.CreatePersistent(kind, magnitude, sourceAbilityName, maxStacks).ToEffect();

        /// <summary>発動型（Triggered・有限ターン・解除可能）。</summary>
        public static IEffect Triggered(EffectKind kind, float magnitude, int remainingTurns, int maxStacks = 1, int stacks = 1)
            => EffectDefinition.CreateTriggered(kind, magnitude, remainingTurns, maxStacks, stacks).ToEffect();

        /// <summary>Cleanse 対象（Burn/Freeze/Paralysis/Curse/SearingWound 等）。永続・蓄積。</summary>
        public static IEffect Cleansable(EffectKind kind, float magnitude = 0f, int stacks = 1, int maxStacks = 1)
            => EffectDefinition.CreateCleansable(kind, magnitude, stacks, maxStacks).ToEffect();

        /// <summary>条件型（Conditional・置物オーラ）。</summary>
        public static IEffect Conditional(EffectKind kind, float magnitude, string auraSourceId, string sourceAbilityName, int maxStacks = 1)
            => EffectDefinition.CreateConditional(kind, magnitude, auraSourceId, sourceAbilityName, maxStacks).ToEffect();

        /// <summary>派生クラスから Magnitude を取り出す（持たないクラスは 0）。</summary>
        public static float MagnitudeOf(IEffect e)
        {
            switch (e)
            {
                case AbilityModifier m:        return m.Magnitude;
                case EvasionModifier m:        return m.Magnitude;
                case OutgoingDamageModifier m: return m.Magnitude;
                case IncomingDamageModifier m: return m.Magnitude;
                case CriticalRateModifier m:   return m.Magnitude;
                case CounterDamageModifier m:  return m.Magnitude;
                case HealReceivedModifier m:   return m.Magnitude;
                case ContinuousDot m:          return m.Magnitude;
                case ContinuousHot m:          return m.Magnitude;
                case SelfGuard m:              return m.Magnitude;
                default:                       return 0f;
            }
        }
    }
}
