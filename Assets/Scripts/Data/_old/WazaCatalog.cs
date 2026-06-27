// IWazaCatalog の Resources.LoadAll 実装。
//
// 【役割】
// - Resources/Data/Wazas 配下の WazaDefinitionSO を lazy init で一括ロード。
// - Get(wazaId) で Waza インスタンスを構築して返す。
// - 既存 Waza クラスは無変更（CalculateBaseDamage / CalculateHealAmount / TargetingCondition
//   に Registry から解決したデリゲートを詰めて流し込む）。
//
// 【Heal 系の扱い】
// - Category=Heal の場合は CalculateHealAmount に、それ以外は CalculateBaseDamage に
//   DamageFormulaRegistry の結果を詰める。
// - heal_flat はダメージ式 Registry に同居しているが、回復量として使う運用。
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Catalog;
using Echolos.Domain.Formula;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>IWazaCatalog の Resources.LoadAll 実装。</summary>
    public sealed class WazaCatalog : IWazaCatalog
    {
        private const string ResourcesPath = "Data/Wazas";
        private Dictionary<string, WazaDefinition> _cache;

        public Waza Get(string wazaId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(wazaId, out var def))
                throw new KeyNotFoundException($"WazaDefinition not found: {wazaId}");
            return BuildWaza(def);
        }

        public bool IsRegistered(string wazaId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(wazaId);
        }

        public IEnumerable<string> GetAllIds()
        {
            EnsureLoaded();
            return _cache.Keys;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, WazaDefinition>();
            var sos = Resources.LoadAll<WazaDefinitionSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var id = so.Definition.Id;
                if (string.IsNullOrEmpty(id)) continue;
                _cache[id] = so.Definition;
            }
        }

        private static Waza BuildWaza(WazaDefinition def)
        {
            var w = new Waza(def.Id, def.Name)
            {
                Category = def.Category,
                SPD = def.SPD,
                Cooldown = def.Cooldown,
                InitialCooldown = def.InitialCooldown,
                HitCount = def.HitCount,
                MaxUsesPerBattle = def.MaxUsesPerBattle,
                IsSureHit = def.IsSureHit,
                IsForcedWhenReady = def.IsForcedWhenReady,
                TargetingType = def.TargetingType,
                CleansesStatusAilments = def.CleansesStatusAilments,
            };

            // 付帯効果テンプレートは複製（呼び出し元で共有して破壊しないため）
            foreach (var e in def.AppliedEffects)
                if (e != null) w.AppliedEffects.Add(e.Clone());

            // ダメージ計算式を Registry から解決して詰める
            var damageParams = FormulaParam.ToDictionary(def.DamageFormulaParams);
            var damageFormula = DamageFormulaRegistry.Get(def.DamageFormulaId);

            if (def.Category == WazaCategory.Heal)
                w.CalculateHealAmount = (a, t) => damageFormula(a, t, damageParams);
            else
                w.CalculateBaseDamage = (a, t) => damageFormula(a, t, damageParams);

            // 発動条件（always_true なら null のまま＝既存ロジックで「常に有効」扱い）
            if (!string.IsNullOrEmpty(def.TriggerConditionId)
                && def.TriggerConditionId != "always_true")
            {
                var conditionParams = FormulaParam.ToDictionary(def.TriggerConditionParams);
                var trigger = TriggerConditionRegistry.Get(def.TriggerConditionId);
                w.TargetingCondition = t => trigger(t, conditionParams);
            }

            return w;
        }
    }
}
