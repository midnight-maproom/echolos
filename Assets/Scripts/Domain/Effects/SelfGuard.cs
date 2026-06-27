namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 自己防御一過性（DefenseUp 一過性）。
    /// 付与から「次の自分の行動順」まで持続し、StatusEffectProcessor.HandleActionStart で削除される。
    /// 自然な時限減算の対象外（RemainingTurns=-1）。技と効果が 1:1 対応で自明のためログ/バッジ抑制。
    /// 想定用途：def_guard（防御フォールバック）。
    /// </summary>
    public sealed class SelfGuard : EffectBase
    {
        public float Magnitude { get; set; }

        public SelfGuard(float magnitude)
        {
            Kind = EffectKind.SelfDefenseGuard;
            Magnitude = magnitude;
            Lifetime = Lifetime.Permanent;       // 時限減算除外
            RemainingTurns = -1;
            IsUndispellable = true;
            MaxStacks = 1;
        }

        public override IEffect Clone()
        {
            var copy = new SelfGuard(Magnitude);
            CopyCommonFieldsTo(copy);
            return copy;
        }
    }
}
