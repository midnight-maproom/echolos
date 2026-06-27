namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 効果の永続データ表現（SO シリアライズ可能 POCO）。
    /// Roster / SO アセットで効果のテンプレを記述する用途。
    /// 戦闘開始時または効果付与時に <see cref="ToEffect"/> で派生クラス（IEffect）に変換して使う。
    /// </summary>
    [System.Serializable]
    public class EffectDefinition
    {
        public EffectKind Kind;
        public float Magnitude;
        public int Stacks = 1;
        public int MaxStacks = 1;
        public int RemainingTurns = -1;
        public Lifetime Lifetime = Lifetime.Triggered;
        public bool IsUndispellable;
        public bool IsCleansable;
        public string AuraSourceId;
        public string SourceAbilityName;
        public int CoverTargetSlotIndex = -1;
        public bool CoversSameRow;

        /// <summary>
        /// 発動型（Triggered）のテンプレを生成。
        /// 有限ターン・解除可能・能力バフ／デバフ系で使う。
        /// </summary>
        public static EffectDefinition CreateTriggered(
            EffectKind kind, float magnitude, int remainingTurns, int maxStacks = 1, int stacks = 1)
        {
            return new EffectDefinition
            {
                Kind = kind,
                Magnitude = magnitude,
                Stacks = stacks,
                MaxStacks = maxStacks,
                RemainingTurns = remainingTurns,
                Lifetime = Lifetime.Triggered,
                IsUndispellable = false,
                IsCleansable = false,
            };
        }

        /// <summary>
        /// 常在型（Persistent）のテンプレを生成。
        /// 永続・解除不能・パッシブ／オーラ系で使う。
        /// </summary>
        public static EffectDefinition CreatePersistent(
            EffectKind kind, float magnitude, string sourceAbilityName, int maxStacks = 1)
        {
            return new EffectDefinition
            {
                Kind = kind,
                Magnitude = magnitude,
                Stacks = 1,
                MaxStacks = maxStacks,
                RemainingTurns = -1,
                Lifetime = Lifetime.Permanent,
                IsUndispellable = true,
                IsCleansable = false,
                SourceAbilityName = sourceAbilityName,
            };
        }

        /// <summary>
        /// 条件型（Conditional）のテンプレを生成。
        /// 永続・解除不能・動的再評価される置物オーラ系で使う。
        /// </summary>
        public static EffectDefinition CreateConditional(
            EffectKind kind, float magnitude, string auraSourceId, string sourceAbilityName, int maxStacks = 1)
        {
            return new EffectDefinition
            {
                Kind = kind,
                Magnitude = magnitude,
                Stacks = 1,
                MaxStacks = maxStacks,
                RemainingTurns = -1,
                Lifetime = Lifetime.Permanent,
                IsUndispellable = true,
                IsCleansable = false,
                AuraSourceId = auraSourceId,
                SourceAbilityName = sourceAbilityName,
            };
        }

        /// <summary>
        /// Cleanse 対象効果（Burn / Freeze / Paralysis / Curse / SearingWound 等）のテンプレを生成。
        /// 永続・Cleanse で剥がれる・蓄積型。
        /// </summary>
        public static EffectDefinition CreateCleansable(
            EffectKind kind, float magnitude, int stacks = 1, int maxStacks = 1)
        {
            return new EffectDefinition
            {
                Kind = kind,
                Magnitude = magnitude,
                Stacks = stacks,
                MaxStacks = maxStacks,
                RemainingTurns = -1,
                Lifetime = Lifetime.Permanent,
                IsUndispellable = false,
                IsCleansable = true,
            };
        }

        /// <summary>POCO → 派生クラス（IEffect）変換。共通フィールドを反映する。</summary>
        public EffectBase ToEffect()
        {
            var e = EffectFactory.CreateByKind(Kind, Magnitude);
            e.Stacks = Stacks;
            e.MaxStacks = MaxStacks > 0 ? MaxStacks : 1;
            e.RemainingTurns = RemainingTurns;
            e.Lifetime = Lifetime;
            e.IsUndispellable = IsUndispellable;
            e.IsCleansable = IsCleansable;
            e.AuraSourceId = AuraSourceId;
            e.SourceAbilityName = SourceAbilityName;
            return e;
        }

        public EffectDefinition Clone()
        {
            return new EffectDefinition
            {
                Kind = Kind,
                Magnitude = Magnitude,
                Stacks = Stacks,
                MaxStacks = MaxStacks,
                RemainingTurns = RemainingTurns,
                Lifetime = Lifetime,
                IsUndispellable = IsUndispellable,
                IsCleansable = IsCleansable,
                AuraSourceId = AuraSourceId,
                SourceAbilityName = SourceAbilityName,
                CoverTargetSlotIndex = CoverTargetSlotIndex,
                CoversSameRow = CoversSameRow,
            };
        }
    }
}
