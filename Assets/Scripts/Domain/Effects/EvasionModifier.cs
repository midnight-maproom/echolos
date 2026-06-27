using System;

namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 回避率モディファイア（確率パラメタ系）。
    /// 命中・回避判定の段階で Magnitude を % 値として加算（複数効果は加算合算・キャップ 50%）。
    /// </summary>
    public sealed class EvasionModifier : EffectBase
    {
        public bool IsBuff { get; private set; }
        public float Magnitude { get; set; }

        public EvasionModifier(EffectKind kind, float magnitude)
        {
            Kind = kind;
            Magnitude = magnitude;
            IsBuff = MetaFromKind(kind);
        }

        private static bool MetaFromKind(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.EvasionUp:   return true;
                case EffectKind.EvasionDown: return false;
                default:
                    throw new ArgumentException(
                        $"EvasionModifier の Kind ではない: {kind}", nameof(kind));
            }
        }

        public override IEffect Clone()
        {
            var copy = new EvasionModifier(Kind, Magnitude);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
