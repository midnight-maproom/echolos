// IDraftPoolCatalog の Resources.LoadAll 実装。
//
// 【役割】
// - Resources/Data/DraftPools 配下の DraftPoolDefinitionSO を lazy init で一括ロード。
// - SO の Definition（POCO）から Domain 完成品 DraftPool に変換して返す
//   （UnitCatalog → Unit と同パターン）。
//
// 【設計方針】
// - DI 抽象なし（依存ゼロ）。Composition Root（Bootstrap）で new DraftPoolCatalog() するだけ。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Catalog;
using Echolos.Domain.Draft;

namespace Echolos.Data
{
    /// <summary>IDraftPoolCatalog の Resources.LoadAll 実装。</summary>
    public sealed class DraftPoolCatalog : IDraftPoolCatalog
    {
        private const string ResourcesPath = "Data/DraftPools";

        private Dictionary<string, DraftPool> _cache;
        private List<DraftPool> _list;

        public DraftPool Get(string poolId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(poolId, out var p))
                throw new KeyNotFoundException($"DraftPool not found: {poolId}");
            return p;
        }

        public bool IsRegistered(string poolId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(poolId);
        }

        public IReadOnlyList<DraftPool> GetAll()
        {
            EnsureLoaded();
            return _list;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, DraftPool>();
            _list = new List<DraftPool>();
            var sos = Resources.LoadAll<DraftPoolDefinitionSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var def = so.Definition;
                if (string.IsNullOrEmpty(def.Id)) continue;
                var pool = BuildPool(def);
                _cache[def.Id] = pool;
                _list.Add(pool);
            }
        }

        private static DraftPool BuildPool(DraftPoolDefinition def)
        {
            var rarePerSlot = def.RarePerSlotProbabilities != null
                ? def.RarePerSlotProbabilities.ToArray()
                : Array.Empty<float>();
            return new DraftPool(
                def.Id,
                def.NormalUnitIds != null ? def.NormalUnitIds.ToArray() : Array.Empty<string>(),
                def.RareUnitIds   != null ? def.RareUnitIds.ToArray()   : Array.Empty<string>(),
                def.AllRareSpecialProbability,
                rarePerSlot,
                def.CandidatesPerOffer);
        }
    }
}
