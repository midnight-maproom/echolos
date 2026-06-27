namespace Echolos.Domain.Effects
{
    /// <summary>
    /// IEffect の Magnitude プロパティを派生型別に加算するヘルパー。
    /// IEffect インターフェースに Magnitude を追加する大規模リファクタを避けるための型 switch。
    /// Magnitude を持たない Flag 系（IgnoreCounterFlag / SilencedCounterFlag / ReviveInvalidFlag）は
    /// 対象外で何もしない。
    /// </summary>
    public static class EffectMagnitudeAccumulator
    {
        /// <summary>
        /// effect の Magnitude プロパティに amount を加算する（破壊的変更）。
        /// 呼び出し側は事前に Clone 済みインスタンスを渡す前提（テンプレを破壊しない）。
        /// </summary>
        public static void Add(IEffect effect, float amount)
        {
            if (effect == null || amount == 0f) return;
            switch (effect)
            {
                case AbilityModifier e:        e.Magnitude += amount; break;
                case OutgoingDamageModifier e: e.Magnitude += amount; break;
                case IncomingDamageModifier e: e.Magnitude += amount; break;
                case CriticalRateModifier e:   e.Magnitude += amount; break;
                case CounterDamageModifier e:  e.Magnitude += amount; break;
                case EvasionModifier e:        e.Magnitude += amount; break;
                case HealReceivedModifier e:   e.Magnitude += amount; break;
                case SelfGuard e:              e.Magnitude += amount; break;
                case ContinuousDot e:          e.Magnitude += amount; break;
                case ContinuousHot e:          e.Magnitude += amount; break;
                // Flag 系（IgnoreCounterFlag / SilencedCounterFlag / ReviveInvalidFlag）は
                // Magnitude を持たないので無視。Freeze/Paralysis/Curse/Shield も対象外。
            }
        }
    }
}
