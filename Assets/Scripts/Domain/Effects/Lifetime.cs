namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 効果の持続モデル。「自然消滅するか」だけを表す単純な 2 値軸。
    /// 解除経路（Dispel / Cleanse）や条件依存（オーラ）はこれと独立した別フィールドで管理する。
    /// </summary>
    public enum Lifetime
    {
        /// <summary>有限ターン。RemainingTurns が 0 で自然消滅する。</summary>
        Triggered,

        /// <summary>永続。RemainingTurns=-1。自然消滅しない。解除はフラグ次第。</summary>
        Permanent,
    }
}
