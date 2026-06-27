// メタ拠点強化 4 種の MetaUpgradeDefinition ファクトリ集。
//
// 役割：
// - メタ強化の SSoT。SO アセット生成ツールがこのリストから Resources/Data/MetaUpgrades/{id}.asset を生成。
// - 4 項目：王女 Lv 強化 / ブリジット Lv 強化 / 行動力 +1 / 初期所持ユニット +1
//
// バランス調整段階でコスト・上限を詰める想定。
using System.Collections.Generic;
using Echolos.Data.Definitions;
using Echolos.Domain.Meta;

namespace Echolos.Data.Roster
{
    /// <summary>メタ拠点強化の MetaUpgradeDefinition ファクトリ集。</summary>
    public static class MetaUpgradeRoster
    {
        public static MetaUpgradeDefinition PrincessLevel() => new MetaUpgradeDefinition
        {
            Id = MetaUpgradeIds.PrincessLevel,
            DisplayName = "王女 Lv 強化",
            EffectText = "ラン開始時の王女初期 Lv +1（購入時に 3 択から強化を選ぶ）",
            Costs = new List<int> { 30, 60 },
            Cap = 2,
        };

        public static MetaUpgradeDefinition BridgetLevel() => new MetaUpgradeDefinition
        {
            Id = MetaUpgradeIds.BridgetLevel,
            DisplayName = "ブリジット Lv 強化",
            EffectText = "ラン開始時のブリジット初期 Lv +1（購入時に 3 択から強化を選ぶ・R5 までに加入していないと無意味）",
            Costs = new List<int> { 30, 60 },
            Cap = 2,
        };

        public static MetaUpgradeDefinition ActionPoints() => new MetaUpgradeDefinition
        {
            Id = MetaUpgradeIds.ActionPoints,
            DisplayName = "行動力 +1",
            EffectText = "毎ラウンドの内政枠 2 → 3",
            Costs = new List<int> { 100 },
            Cap = 1,
        };

        public static MetaUpgradeDefinition InitialUnit() => new MetaUpgradeDefinition
        {
            Id = MetaUpgradeIds.InitialUnit,
            DisplayName = "初期所持ユニット +1",
            EffectText = "ラン開始時にランダムで1体追加（最大3回）",
            Costs = new List<int> { 50, 80, 100 },
            Cap = 3,
        };

        /// <summary>全 4 件の MetaUpgradeDefinition を列挙（SoAssetGenerator 用）。</summary>
        public static IEnumerable<MetaUpgradeDefinition> AllUpgrades()
        {
            yield return PrincessLevel();
            yield return BridgetLevel();
            yield return ActionPoints();
            yield return InitialUnit();
        }
    }
}
