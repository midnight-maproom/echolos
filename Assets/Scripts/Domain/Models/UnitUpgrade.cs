using Echolos.Domain.Effects;

namespace Echolos.Domain.Models
{
    /// <summary>
    /// ユニットの Lv アップ時に選択できる強化オプション 1 件（Domain 完成品）。
    /// Lv2 で 1 つ・Lv3 で残り 2 択から 1 つ選択し、Unit.AppliedUpgrades に積み上げる。
    /// Unit.EffectiveXxx は AppliedUpgrades を Kind 別に集計して反映する。
    /// </summary>
    public sealed class UnitUpgrade
    {
        public string UpgradeId { get; }
        public string Name { get; }
        public string Description { get; }
        public UpgradeKind Kind { get; }
        public int Magnitude { get; }

        /// <summary>WazaPowerBoost で対象 Waza を特定するための ID（その他 Kind では未使用）。</summary>
        public string TargetWazaId { get; }

        /// <summary>PersistentEffectBoost で対象 PersistentEffect を特定するための SourceAbilityName（その他 Kind では未使用）。</summary>
        public string TargetSourceAbilityName { get; }

        /// <summary>PersistentEffectBoost で対象 PersistentEffect を特定するための EffectKind（その他 Kind では未使用）。</summary>
        public EffectKind TargetEffectKind { get; }

        public UnitUpgrade(
            string upgradeId,
            string name,
            string description,
            UpgradeKind kind,
            int magnitude,
            string targetWazaId = null,
            string targetSourceAbilityName = null,
            EffectKind targetEffectKind = default)
        {
            UpgradeId = upgradeId;
            Name = name;
            Description = description;
            Kind = kind;
            Magnitude = magnitude;
            TargetWazaId = targetWazaId;
            TargetSourceAbilityName = targetSourceAbilityName;
            TargetEffectKind = targetEffectKind;
        }
    }
}
