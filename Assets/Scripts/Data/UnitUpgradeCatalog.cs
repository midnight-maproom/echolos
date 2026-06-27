using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;

namespace Echolos.Data
{
    /// <summary>IUnitUpgradeCatalog の Resources.LoadAll 実装（UnitUpgrade 返却）。</summary>
    public sealed class UnitUpgradeCatalog : IUnitUpgradeCatalog
    {
        private const string ResourcesPath = "Data/Upgrades";
        private Dictionary<string, UnitUpgradeDefinition> _cache;

        public UnitUpgrade Get(string upgradeId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(upgradeId, out var def))
                throw new KeyNotFoundException($"UnitUpgradeDefinition not found: {upgradeId}");
            return def.ToUpgrade();
        }

        public bool IsRegistered(string upgradeId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(upgradeId);
        }

        public IEnumerable<string> GetAllIds()
        {
            EnsureLoaded();
            return _cache.Keys;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, UnitUpgradeDefinition>();
            var sos = Resources.LoadAll<UnitUpgradeDefinitionSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var id = so.Definition.Id;
                if (string.IsNullOrEmpty(id)) continue;
                _cache[id] = so.Definition;
            }
        }
    }
}
