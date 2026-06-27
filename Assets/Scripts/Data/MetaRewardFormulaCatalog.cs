// IMetaRewardFormulaCatalog の Resources.LoadAll 実装。
//
// 【役割】
// - Resources/Data/MetaReward 配下の MetaRewardFormulaSO を lazy init で一括ロード。
// - SO の Definition（POCO）から Domain 完成品 MetaRewardFormula に変換して返す
//   （UnitCatalog → Unit と同パターン）。
//
// 【設計方針】
// - DI 抽象なし（依存ゼロ）。Composition Root（Bootstrap）で new MetaRewardFormulaCatalog() するだけ。
// - UnitCatalog / WazaCatalog と同じ lazy init パターン。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Catalog;
using Echolos.Domain.Formula;

namespace Echolos.Data
{
    /// <summary>IMetaRewardFormulaCatalog の Resources.LoadAll 実装。</summary>
    public sealed class MetaRewardFormulaCatalog : IMetaRewardFormulaCatalog
    {
        private const string ResourcesPath = "Data/MetaReward";

        private Dictionary<string, MetaRewardFormula> _cache;
        private List<MetaRewardFormula> _list;

        public MetaRewardFormula Get(string formulaId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(formulaId, out var f))
                throw new KeyNotFoundException($"MetaRewardFormula not found: {formulaId}");
            return f;
        }

        public bool IsRegistered(string formulaId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(formulaId);
        }

        public IReadOnlyList<MetaRewardFormula> GetAll()
        {
            EnsureLoaded();
            return _list;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, MetaRewardFormula>();
            _list = new List<MetaRewardFormula>();
            var sos = Resources.LoadAll<MetaRewardFormulaSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var def = so.Definition;
                if (string.IsNullOrEmpty(def.Id)) continue;
                var formula = BuildFormula(def);
                _cache[def.Id] = formula;
                _list.Add(formula);
            }
        }

        private static MetaRewardFormula BuildFormula(MetaRewardFormulaDefinition def)
        {
            var paramDict = FormulaParam.ToDictionary(def.Params);
            return new MetaRewardFormula(def.Id, def.FormulaId, paramDict);
        }
    }
}
