using Echolos.Domain.Effects;

namespace Echolos.Domain.Battle
{
    // 効果の付与／解除を Event で伝える際の値スナップショット。
    // 観戦ビュー側はバッジ表示・ホバー時のスタック数／残ターン数表示にこの値を使う。
    //
    // 元の IEffect 参照ではなく値コピーで持つことで、戦闘ロジック終了後に
    // IEffect の状態が変わっても再生時点のスナップショットを正しく復元できる。
    public sealed class EffectChange
    {
        public EffectKind Kind { get; }
        public int Stacks { get; }
        public int RemainingTurns { get; }
        public Lifetime Lifetime { get; }
        public string SourceAbilityName { get; }
        public bool IsCleansable { get; }
        public bool IsUndispellable { get; }

        public EffectChange(EffectKind kind, int stacks, int remainingTurns,
            Lifetime lifetime, string sourceAbilityName = null,
            bool isCleansable = false, bool isUndispellable = false)
        {
            Kind = kind;
            Stacks = stacks;
            RemainingTurns = remainingTurns;
            Lifetime = lifetime;
            SourceAbilityName = sourceAbilityName;
            IsCleansable = isCleansable;
            IsUndispellable = isUndispellable;
        }

        public static EffectChange From(IEffect eff)
        {
            if (eff == null) return null;
            return new EffectChange(eff.Kind, eff.Stacks, eff.RemainingTurns,
                eff.Lifetime, eff.SourceAbilityName,
                eff.IsCleansable, eff.IsUndispellable);
        }
    }
}
