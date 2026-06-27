namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 全派生クラスの共通基底。共通フィールドの保持と既定値の集約。
    /// Kind は派生クラスのコンストラクタで設定する（同一型で複数 Kind を持つため）。
    /// </summary>
    public abstract class EffectBase : IEffect
    {
        public EffectKind Kind { get; protected set; }
        public Lifetime Lifetime { get; set; } = Lifetime.Triggered;
        public int RemainingTurns { get; set; } = 3;
        public bool IsUndispellable { get; set; } = false;
        public bool IsCleansable { get; set; } = false;
        public string AuraSourceId { get; set; }
        public string SourceAbilityName { get; set; }
        public int Stacks { get; set; } = 1;
        public int MaxStacks { get; set; } = 1;

        public abstract IEffect Clone();

        /// <summary>共通フィールドのコピーをヘルパーとして提供（派生 Clone から呼ぶ）。</summary>
        protected void CopyCommonFieldsTo(EffectBase dst)
        {
            dst.Kind = Kind;
            dst.Lifetime = Lifetime;
            dst.RemainingTurns = RemainingTurns;
            dst.IsUndispellable = IsUndispellable;
            dst.IsCleansable = IsCleansable;
            dst.AuraSourceId = AuraSourceId;
            dst.SourceAbilityName = SourceAbilityName;
            dst.Stacks = Stacks;
            dst.MaxStacks = MaxStacks;
        }
    }
}
