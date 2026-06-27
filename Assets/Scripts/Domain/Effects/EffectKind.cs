namespace Echolos.Domain.Effects
{
    /// <summary>
    /// 個別効果の識別子。派生クラス（振る舞いの型）とは別軸で、Unit.ImmunityKinds の参照キー、
    /// 観戦ビュー UI の効果バッジ識別、ログ表記、SO データの判別タグとして使う。
    /// 1 派生クラスが複数 Kind を持つことがある（例：AbilityModifier は AttackUp/AttackDown/DefenseUp/DefenseDown）。
    /// </summary>
    public enum EffectKind
    {
        // ── 能力値モディファイア（AbilityModifier）
        AttackUp,
        AttackDown,
        DefenseUp,
        DefenseDown,

        // ── 回避モディファイア（EvasionModifier）
        EvasionUp,
        EvasionDown,

        // ── ダメージ計算モディファイア
        OutgoingDamageUp,      // OutgoingDamageModifier
        IncomingDamageDown,    // IncomingDamageModifier
        CriticalRateUp,        // CriticalRateModifier
        CounterDamageUp,       // CounterDamageModifier

        // ── 受け回復モディファイア（HealReceivedModifier）
        SearingWound,          // 熱傷（Stacks × 10% 回復低下・最大 9・Cleanse 対象）

        // ── 継続効果
        Burn,                  // ContinuousDot
        HealOverTime,          // ContinuousHot

        // ── 行動阻害
        Freeze,
        Paralysis,
        Curse,

        // ── メーター
        Shield,

        // ── フラグ系
        SilencedCounter,
        ReviveInvalid,
        IgnoreCounter,         // 自分の攻撃に対する相手の反撃を無効化（ブリジット「王家のペンダント」）

        // ── 自己防御一過性
        SelfDefenseGuard,
    }
}
