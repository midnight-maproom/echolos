// IUnitCatalog の Resources.LoadAll 実装。
//
// 【役割】
// - Resources/Data/Units 配下の UnitDefinitionSO を lazy init で一括ロード。
// - Get(unitId) で Unit インスタンスを構築して返す（呼ぶたび新しい実体）。
//
// 【DI】
// - IWazaCatalog をコンストラクタ注入で受け取り、UnitDefinition.WazaIds を解決して BaseWazas に組み立てる。
// - Composition Root（Presentation/Bootstrap）で new UnitCatalog(new WazaCatalog()) として配線する。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>IUnitCatalog の Resources.LoadAll 実装。</summary>
    public sealed class UnitCatalog : IUnitCatalog
    {
        private const string ResourcesPath = "Data/Units";
        private readonly IWazaCatalog _wazaCatalog;
        private Dictionary<string, UnitDefinition> _cache;

        public UnitCatalog(IWazaCatalog wazaCatalog)
        {
            _wazaCatalog = wazaCatalog ?? throw new ArgumentNullException(nameof(wazaCatalog));
        }

        public Unit Get(string unitId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(unitId, out var def))
                throw new KeyNotFoundException($"UnitDefinition not found: {unitId}");
            return BuildUnit(def);
        }

        public bool IsRegistered(string unitId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(unitId);
        }

        public IEnumerable<string> GetAllIds()
        {
            EnsureLoaded();
            return _cache.Keys;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, UnitDefinition>();
            var sos = Resources.LoadAll<UnitDefinitionSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var id = so.Definition.Id;
                if (string.IsNullOrEmpty(id)) continue;
                _cache[id] = so.Definition;
            }
        }

        private Unit BuildUnit(UnitDefinition def)
        {
            var u = new Unit(def.Id, def.Name, def.UnitElement)
            {
                MaxHP = def.MaxHP,
                CurrentHP = def.MaxHP, // ラン開始時は満タン
                BaseATK = def.BaseATK,
                DEF = def.DEF,
                BaseSPD = def.BaseSPD,
                BaseEvasion = def.BaseEvasion,
                AttackKind = def.AttackKind,
                TargetingDirection = def.TargetingDirection,
                ImmuneToStatusAilments = def.ImmuneToStatusAilments,
                State = UnitState.Active,

                PlacementHint = def.PlacementHint,

                EnhancementHPPerLevel = def.EnhancementHPPerLevel,
                EnhancementATKPerLevel = def.EnhancementATKPerLevel,
                EnhancementDEFPerLevel = def.EnhancementDEFPerLevel,
                EnhancementEvasionPerLevel = def.EnhancementEvasionPerLevel,
                EnhancementHealPerLevel = def.EnhancementHealPerLevel,
                EnhancementMagnitudePerLevel = def.EnhancementMagnitudePerLevel,
            };

            // List 系はコピーで詰める（SO 内テンプレートの共有破壊を防ぐ）
            if (def.Tags != null) u.Tags.AddRange(def.Tags);
            if (def.CombatRoles != null) u.CombatRoles.AddRange(def.CombatRoles);
            if (def.AbilityLabels != null) u.AbilityLabels.AddRange(def.AbilityLabels);

            // 置物オーラは複製（戦闘中の Magnitude 操作で SO 側を破壊しないため）。
            // SO 側は常に non-null な StatusEffect インスタンスを持つので、Stacks>0 で
            // 「オーラあり」と判定する（コンバータと UnitCatalog で取り決め）。
            if (def.AuraEffect != null && def.AuraEffect.Stacks > 0)
                u.AuraEffect = def.AuraEffect.Clone();

            // 所有 Waza は注入された IWazaCatalog 経由で構築（各 Waza は新しい実体）
            if (def.WazaIds != null)
                foreach (var wazaId in def.WazaIds)
                    if (!string.IsNullOrEmpty(wazaId))
                        u.BaseWazas.Add(_wazaCatalog.Get(wazaId));

            // 反撃 Waza。空文字なら null のまま（共通フォールバック Waza.DefaultCounter が使われる）
            if (!string.IsNullOrEmpty(def.CounterWazaId))
                u.CounterWaza = _wazaCatalog.Get(def.CounterWazaId);

            return u;
        }
    }
}
