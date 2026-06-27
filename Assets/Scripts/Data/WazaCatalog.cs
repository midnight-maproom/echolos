// IWazaCatalog の Resources.LoadAll 実装。
//
// 役割：
// - Resources/Data/Wazas 配下の WazaDefinitionSO を lazy init で一括ロード。
// - Get(wazaId) で Waza を構築して返す（呼ぶたび新しい実体）。
//
// 設計：
// - WazaDefinition.Pattern に応じて Waza.Effects を組み立てる（switch case）。
// - 新パターンは WazaPattern enum + 本クラス switch case の 2 箇所追加で対応可能。
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Catalog;

namespace Echolos.Data
{
    /// <summary>IWazaCatalog の Resources.LoadAll 実装（Waza 返却）。</summary>
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

        // WazaDefinition POCO（SO シリアライズ可能）から Waza（Domain 完成品）への変換。
        // Pattern enum に応じて Effects 構成を組み立てる。
        private static Waza BuildWaza(WazaDefinition def)
        {
            var w = new Waza(def.Id, def.Name)
            {
                SPD = def.SPD,
                Cooldown = def.Cooldown,
                InitialCooldown = def.InitialCooldown,
                HitCount = def.HitCount,
                MaxUsesPerBattle = def.MaxUsesPerBattle,
                IsForcedWhenReady = def.IsForcedWhenReady,
                TargetingType = def.TargetingType,
                TargetCount = def.TargetCount,
                TargetSelection = def.TargetSelection,
                Effects = BuildEffects(def),
            };
            return w;
        }

        private static IList<IActionEffect> BuildEffects(WazaDefinition def)
        {
            var list = new List<IActionEffect>();
            switch (def.Pattern)
            {
                case WazaPattern.Attack:
                    list.Add(new AttackEffect(def.WazaMultiplier, def.IsSureHit));
                    break;

                case WazaPattern.AttackWithStatusRider:
                {
                    var riders = new List<IActionEffect>();
                    if (def.RiderEffect != null)
                        riders.Add(new ApplyStatusEffectEffect(new[] { (Echolos.Domain.Effects.IEffect)def.RiderEffect.ToEffect() }));
                    list.Add(new AttackEffect(def.WazaMultiplier, def.IsSureHit, onHitRiders: riders));
                    break;
                }

                case WazaPattern.AttackWithSelfStatusRider:
                {
                    list.Add(new AttackEffect(def.WazaMultiplier, def.IsSureHit));
                    if (def.RiderEffect != null)
                        list.Add(new ApplyStatusEffectToActorEffect(
                            new[] { (Echolos.Domain.Effects.IEffect)def.RiderEffect.ToEffect() }));
                    break;
                }

                case WazaPattern.Heal:
                    list.Add(new HealEffect(def.WazaPower));
                    break;

                case WazaPattern.HealAndDispelDebuffs:
                    list.Add(new HealEffect(def.WazaPower));
                    list.Add(new DispelDebuffsEffect());
                    break;

                case WazaPattern.ApplyStatusEffect:
                    if (def.RiderEffect != null)
                        list.Add(new ApplyStatusEffectEffect(new[] { (Echolos.Domain.Effects.IEffect)def.RiderEffect.ToEffect() }));
                    break;

                case WazaPattern.DispelEnemyBuffs:
                    list.Add(new DispelBuffsEffect());
                    break;

                case WazaPattern.DispelAllyDebuffs:
                    list.Add(new DispelDebuffsEffect());
                    break;

                case WazaPattern.CleanseAllyStatusAilments:
                    list.Add(new CleanseStatusAilmentsEffect());
                    break;

                case WazaPattern.Charge:
                    // 空 Effects：IsForcedWhenReady=true でターン消費型のチャージとして機能。
                    break;
            }
            return list;
        }
    }
}
