// IUnitCatalog の Resources.LoadAll 実装。
//
// 役割：
// - Resources/Data/Units 配下の UnitDefinitionSO を lazy init で一括ロード。
// - Get(unitId) で Unit インスタンスを構築して返す（呼ぶたび新しい実体）。
//
// DI：
// - IWazaCatalog をコンストラクタ注入で受け取り、UnitDefinition.WazaIds を解決して
//   BaseWazas に組み立てる。
// - Composition Root（Presentation/Bootstrap）で new UnitCatalog(new WazaCatalog()) と配線。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;

namespace Echolos.Data
{
    /// <summary>IUnitCatalog の Resources.LoadAll 実装。</summary>
    public sealed class UnitCatalog : IUnitCatalog
    {
        private const string ResourcesPath = "Data/Units";
        private readonly IWazaCatalog _wazaCatalog;
        private readonly IUnitUpgradeCatalog _upgradeCatalog;
        private Dictionary<string, UnitDefinition> _cache;

        public UnitCatalog(IWazaCatalog wazaCatalog, IUnitUpgradeCatalog upgradeCatalog)
        {
            _wazaCatalog = wazaCatalog ?? throw new ArgumentNullException(nameof(wazaCatalog));
            _upgradeCatalog = upgradeCatalog ?? throw new ArgumentNullException(nameof(upgradeCatalog));
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
                Description = def.Description ?? "",
                MaxHP = def.MaxHP,
                CurrentHP = def.MaxHP, // ラン開始時は満タン
                BaseATK = def.BaseATK,
                DEF = def.DEF,
                BaseSPD = def.BaseSPD,
                AttackKind = def.AttackKind,
                TargetingDirection = def.TargetingDirection,
                BaseParalysisTolerance = def.BaseParalysisTolerance,
                State = UnitState.Active,

                PlacementHint = def.PlacementHint,
                SortOrder = def.SortOrder,
            };

            // List 系はコピーで詰める（SO 内テンプレートの共有破壊を防ぐ）
            if (def.Tags != null) u.Tags.AddRange(def.Tags);
            if (def.CombatRoles != null) u.CombatRoles.AddRange(def.CombatRoles);
            if (def.AbilityLabels != null) u.AbilityLabels.AddRange(def.AbilityLabels);

            // 所有 Waza は注入された IWazaCatalog 経由で構築（各 Waza は新しい実体）
            if (def.WazaIds != null)
                foreach (var wazaId in def.WazaIds)
                    if (!string.IsNullOrEmpty(wazaId))
                        u.BaseWazas.Add(_wazaCatalog.Get(wazaId));

            // パッシブ効果テンプレは Clone して詰める（戦闘開始時に RuntimeUnit へ複製付与する用途）
            if (def.PersistentEffects != null)
                foreach (var e in def.PersistentEffects)
                    if (e != null) u.PersistentEffects.Add(e.Clone());

            // 付与時の弾き対象 EffectKind 集合
            if (def.ImmunityKinds != null)
                foreach (var k in def.ImmunityKinds)
                    u.ImmunityKinds.Add(k);

            // Lv 強化選択肢を ID 解決して詰める（Lv1 起点・AppliedUpgrades は空）
            if (def.AvailableUpgradeIds != null)
                foreach (var upgradeId in def.AvailableUpgradeIds)
                    if (!string.IsNullOrEmpty(upgradeId))
                        u.AvailableUpgrades.Add(_upgradeCatalog.Get(upgradeId));

            return u;
        }
    }
}
