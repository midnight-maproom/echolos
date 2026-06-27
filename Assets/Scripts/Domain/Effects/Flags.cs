namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 反撃禁止フラグ。被弾しても反撃を発動しない（水の大盾兵の専守等）。
    /// パッシブ Persistent で持つことが多い。
    /// </summary>
    public sealed class SilencedCounterFlag : EffectBase
    {
        public SilencedCounterFlag()
        {
            Kind = EffectKind.SilencedCounter;
        }

        public override IEffect Clone()
        {
            var copy = new SilencedCounterFlag();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 復活無効化フラグ。HP0 時に復活スキルを無効化して即ロスト。
    /// </summary>
    public sealed class ReviveInvalidFlag : EffectBase
    {
        public ReviveInvalidFlag()
        {
            Kind = EffectKind.ReviveInvalid;
        }

        public override IEffect Clone()
        {
            var copy = new ReviveInvalidFlag();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 反撃無効フラグ（攻撃側）。本効果を持つユニットが攻撃を当てたとき、被弾側の反撃を発動させない。
    /// SilencedCounterFlag が「自分が反撃しない（被弾側）」なのに対し、本フラグは「相手に反撃させない（攻撃側）」で意味的に直交。
    /// ブリジット「王家のペンダント」がパッシブ Persistent で持つ。
    /// </summary>
    public sealed class IgnoreCounterFlag : EffectBase
    {
        public IgnoreCounterFlag()
        {
            Kind = EffectKind.IgnoreCounter;
        }

        public override IEffect Clone()
        {
            var copy = new IgnoreCounterFlag();
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
