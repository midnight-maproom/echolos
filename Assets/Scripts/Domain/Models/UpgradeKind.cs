namespace Echolos.Domain.Models
{
    /// <summary>
    /// ユニット強化（Lv アップ時の 1 段階分）の種別。
    /// Magnitude と組み合わせて RuntimeUnit.EffectiveXxx の集計時に加算される。
    /// WazaPowerBoost は TargetWazaId を併用して対象 Waza を特定し、HealEffect の wazaPower
    /// および ApplyStatusEffectEffect の付与効果 Magnitude に加算される。
    /// PersistentEffectBoost は TargetSourceAbilityName + TargetEffectKind で対象を特定し、
    /// Bootstrap.PrepareForBattle で PersistentEffect 付与時に Magnitude に加算される。
    /// AuraBoost は AuraApplier が SourceUnit の AppliedUpgrades から合計を読み取り、
    /// AuraDefinition の Buffs[].Magnitude に加算して陣営全員に付与する（自分のステは変えない）。
    /// </summary>
    public enum UpgradeKind
    {
        AtkBoost,
        DefBoost,
        HpBoost,
        WazaPowerBoost,
        AuraBoost,
        PersistentEffectBoost,
    }
}
