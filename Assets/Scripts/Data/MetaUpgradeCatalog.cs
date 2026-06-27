// IMetaUpgradeCatalog の Resources.LoadAll 実装。
//
// 【役割】
// - Resources/Data/MetaUpgrades 配下の MetaUpgradeDefinitionSO を lazy init で一括ロード。
// - SO の Definition（POCO）から Domain 完成品 MetaUpgrade に変換して返す
//   （UnitCatalog → Unit / MetaRewardFormulaCatalog → MetaRewardFormula と同パターン）。
//
// 【設計方針】
// - DI 抽象なし（依存ゼロ）。Composition Root（Bootstrap）で new MetaUpgradeCatalog() するだけ。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Catalog;
using Echolos.Domain.Meta;

namespace Echolos.Data
{
    /// <summary>IMetaUpgradeCatalog の Resources.LoadAll 実装。</summary>
    public sealed class MetaUpgradeCatalog : IMetaUpgradeCatalog
    {
        private const string ResourcesPath = "Data/MetaUpgrades";

        private Dictionary<string, MetaUpgrade> _cache;
        private List<MetaUpgrade> _list;

        public MetaUpgrade Get(string upgradeId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(upgradeId, out var u))
                throw new KeyNotFoundException($"MetaUpgrade not found: {upgradeId}");
            return u;
        }

        public bool IsRegistered(string upgradeId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(upgradeId);
        }

        public IReadOnlyList<MetaUpgrade> GetAll()
        {
            EnsureLoaded();
            return _list;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, MetaUpgrade>();
            _list = new List<MetaUpgrade>();
            var sos = Resources.LoadAll<MetaUpgradeDefinitionSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var def = so.Definition;
                if (string.IsNullOrEmpty(def.Id)) continue;
                var upgrade = BuildUpgrade(def);
                _cache[def.Id] = upgrade;
                _list.Add(upgrade);
            }
        }

        private static MetaUpgrade BuildUpgrade(MetaUpgradeDefinition def)
        {
            return new MetaUpgrade(
                def.Id,
                def.DisplayName,
                def.EffectText,
                def.Costs != null ? def.Costs.ToArray() : new int[0],
                def.Cap);
        }
    }
}
