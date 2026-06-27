using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Data.Definitions
{
    /// <summary>
    /// ユニット強化定義データ（純 POCO・SO シリアライズ可能）。
    /// UnitUpgradeDefinitionSO がこれをラップして .asset 化される。
    /// UnitUpgradeCatalog.Get(id) が UnitUpgrade（Domain 完成品）に変換して返す。
    /// </summary>
    [System.Serializable]
    public class UnitUpgradeDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public UpgradeKind Kind = UpgradeKind.AtkBoost;
        public int Magnitude;

        /// <summary>WazaPowerBoost 用の対象 Waza ID（その他 Kind では空でよい）。</summary>
        public string TargetWazaId;

        /// <summary>PersistentEffectBoost 用の対象 SourceAbilityName（その他 Kind では空でよい）。</summary>
        public string TargetSourceAbilityName;

        /// <summary>PersistentEffectBoost 用の対象 EffectKind（その他 Kind では未使用）。</summary>
        public EffectKind TargetEffectKind = EffectKind.AttackUp;

        /// <summary>POCO から Domain 完成品（UnitUpgrade）に変換する。</summary>
        public UnitUpgrade ToUpgrade()
        {
            return new UnitUpgrade(
                Id, Name, Description, Kind, Magnitude,
                TargetWazaId, TargetSourceAbilityName, TargetEffectKind);
        }
    }
}
