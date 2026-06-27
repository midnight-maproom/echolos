using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Conditional
{
    /// <summary>
    /// ブリジット「王家のペンダント」のクロスユニット型 Conditional バフ Processor。
    /// ペンダント所持者（<see cref="PendantOwnerTag"/>）と指定 Partner Id を持つユニットの
    /// 両方が生存しているとき、両者に PDEF +<see cref="PendantMagnitude"/> を付与する。
    ///
    /// 既存 Processor（Loner/DarkAura）が「条件→自分にバフ」だったのに対し、
    /// 本 Processor は「条件→自分＋他ユニットにバフ」のクロスユニット型。
    /// 単一の <see cref="AuraSourceId"/> で両者のバフを束ねるので、剥奪は全陣営走査の
    /// RemoveEffectsWhere 一発で済む。
    ///
    /// フック：
    /// - BattleStart：戦闘開始時に初回評価
    /// - UnitDied：所持者 or Partner の死亡で剥奪条件を満たすため
    /// </summary>
    public sealed class PendantConditionalProcessor : ConditionalBuffProcessor
    {
        /// <summary>ペンダント所持者を示すタグ。Unit.Tags に付与する。</summary>
        public const string PendantOwnerTag = "PendantOwner";

        /// <summary>AuraSourceId の prefix（所持者 Id と組み合わせて衝突を避ける）。</summary>
        private const string PendantSourcePrefix = "pendant:";

        /// <summary>1 件あたりの PDEF Magnitude。</summary>
        public const int PendantMagnitude = 3;

        private static readonly ConditionalBuffHook[] _hooks =
        {
            ConditionalBuffHook.BattleStart,
            ConditionalBuffHook.UnitDied,
        };

        public override IReadOnlyList<ConditionalBuffHook> Hooks => _hooks;

        private readonly string _partnerUnitId;
        private readonly string _sourceAbilityName;

        /// <param name="partnerUnitId">所持者がバフを共有する相手の Unit.Id（例：王女）。</param>
        /// <param name="sourceAbilityName">バフ表示名（例：「王家のペンダント」）。</param>
        public PendantConditionalProcessor(string partnerUnitId, string sourceAbilityName)
        {
            if (string.IsNullOrEmpty(partnerUnitId))
                throw new ArgumentException("partnerUnitId is required", nameof(partnerUnitId));
            _partnerUnitId = partnerUnitId;
            _sourceAbilityName = sourceAbilityName ?? string.Empty;
        }

        public override void Refresh(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            RefreshTeam(context.AllyUnits);
            RefreshTeam(context.EnemyUnits);
        }

        public override void OnUnitDied(BattleContext context, RuntimeUnit deadUnit)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (deadUnit == null) return;

            if (context.AllyUnits.Contains(deadUnit))
                RefreshTeam(context.AllyUnits);
            else if (context.EnemyUnits.Contains(deadUnit))
                RefreshTeam(context.EnemyUnits);
        }

        private void RefreshTeam(List<RuntimeUnit> team)
        {
            foreach (var owner in team)
            {
                if (!owner.BaseUnit.Tags.Contains(PendantOwnerTag)) continue;

                string sourceId = PendantSourcePrefix + owner.BaseUnit.Id;
                RuntimeUnit partner = FindLivingPartner(team);
                bool shouldApply = owner.IsAlive && partner != null;

                bool ownerHas = HasEffect(owner, sourceId);
                bool partnerHas = partner != null && HasEffect(partner, sourceId);

                if (shouldApply)
                {
                    if (!ownerHas)
                        owner.AddEffect(StatusEffect.CreateConditional(
                            StatusEffectType.DefenseUp, PendantMagnitude, sourceId, _sourceAbilityName));
                    if (!partnerHas)
                        partner.AddEffect(StatusEffect.CreateConditional(
                            StatusEffectType.DefenseUp, PendantMagnitude, sourceId, _sourceAbilityName));
                }
                else
                {
                    foreach (var u in team)
                        u.RemoveEffectsWhere(e => e.AuraSourceId == sourceId);
                }
            }
        }

        private RuntimeUnit FindLivingPartner(List<RuntimeUnit> team)
        {
            foreach (var u in team)
            {
                if (!u.IsAlive) continue;
                if (u.BaseUnit.Id == _partnerUnitId) return u;
            }
            return null;
        }

        private static bool HasEffect(RuntimeUnit unit, string sourceId)
        {
            return unit.FindEffect(e => e.AuraSourceId == sourceId
                && e.EffectType == StatusEffectType.DefenseUp) != null;
        }
    }
}
