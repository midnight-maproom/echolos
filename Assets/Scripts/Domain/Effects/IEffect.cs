namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 全効果型の共通インターフェース。RuntimeUnit.ActiveEffects はこの型のリストで保持する。
    /// 各派生クラスの具体的な振る舞いは型自身に閉じ込め、消費側は pattern match で型判定する。
    /// </summary>
    public interface IEffect
    {
        /// <summary>個別効果の識別子。</summary>
        EffectKind Kind { get; }

        /// <summary>持続モデル（自然消滅するか）。</summary>
        Lifetime Lifetime { get; set; }

        /// <summary>残りターン数。Triggered なら 0 以上、Permanent なら -1。</summary>
        int RemainingTurns { get; set; }

        /// <summary>Dispel 系の解除技で剥がれない印（パッシブ・オーラ等）。</summary>
        bool IsUndispellable { get; set; }

        /// <summary>Cleanse 系の解除技で剥がれる印。Burn / Freeze / SearingWound 等。</summary>
        bool IsCleansable { get; set; }

        /// <summary>オーラ起因の効果。発生源ユニット ID。空文字 / null なら非オーラ。</summary>
        string AuraSourceId { get; set; }

        /// <summary>効果の発生源能力名（表示用・「光の共鳴 Lv6」「焦熱波」等）。</summary>
        string SourceAbilityName { get; set; }

        /// <summary>スタック数。派生クラスごとに意味が違う。</summary>
        int Stacks { get; set; }

        /// <summary>スタック上限。派生クラスごとに既定値・意味が違う。</summary>
        int MaxStacks { get; set; }

        /// <summary>同一インスタンスの深いコピーを返す。</summary>
        IEffect Clone();
    }
}
