using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Conditional
{
    /// <summary>
    /// ブリジット「攻防一体」のバフ連動型 Conditional バフ Processor。
    /// 対象ユニット（<see cref="OffenseDefenseLinkTag"/>）の DefenseUp バフ合計 Magnitude を
    /// AttackUp に等量転写する。
    ///
    /// 「デバフを受けても減少はしない」＝ DefenseDown 系を参照しないので自然に達成。
    /// 自分自身が付与した AttackUp（同 AuraSourceId）は集計から除外することで
    /// BuffApplied フックの自己ループを構造的に止める（差分判定で値変化なし → early return）。
    ///
    /// フック：
    /// - BattleStart：戦闘開始時の初回評価（王女オーラ等が ApplyAuras で配布された後に呼ばれる）
    /// - TurnStart：ターン頭で再評価
    /// - BuffApplied / BuffRemoved：DefenseUp の変動にリアルタイム追従
    /// </summary>
    public sealed class OffenseDefenseLinkConditionalProcessor : ConditionalBuffProcessor
    {
        /// <summary>攻防一体パッシブの対象を示すタグ。Unit.Tags に付与する。</summary>
        public const string OffenseDefenseLinkTag = "OffenseDefenseLink";

        /// <summary>AuraSourceId の prefix（ユニット Id と組み合わせて衝突を避ける）。</summary>
        private const string LinkSourcePrefix = "off_def_link:";

        /// <summary>表示用の能力名（戦闘ログのバフ付与・剥奪メッセージで使う）。</summary>
        public const string AbilityName = "攻防一体";

        private static readonly ConditionalBuffHook[] _hooks =
        {
            ConditionalBuffHook.BattleStart,
            ConditionalBuffHook.TurnStart,
            ConditionalBuffHook.BuffApplied,
            ConditionalBuffHook.BuffRemoved,
        };

        public override IReadOnlyList<ConditionalBuffHook> Hooks => _hooks;

        public override void Refresh(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            RefreshTeam(context.AllyUnits);
            RefreshTeam(context.EnemyUnits);
        }

        private static void RefreshTeam(List<RuntimeUnit> team)
        {
            foreach (var unit in team)
            {
                if (!unit.BaseUnit.Tags.Contains(OffenseDefenseLinkTag)) continue;

                string sourceId = LinkSourcePrefix + unit.BaseUnit.Id;
                int newMagnitude = unit.IsAlive ? SumDefenseUpMagnitudeExcept(unit, sourceId) : 0;

                var existing = unit.FindEffect(e => e.AuraSourceId == sourceId
                    && e.EffectType == StatusEffectType.AttackUp);
                int currentMagnitude = existing != null ? (int)existing.Magnitude : 0;

                if (currentMagnitude == newMagnitude) continue;

                unit.RemoveEffectsWhere(e => e.AuraSourceId == sourceId);

                if (!unit.IsAlive) continue;
                if (newMagnitude <= 0) continue;

                unit.AddEffect(StatusEffect.CreateConditional(
                    StatusEffectType.AttackUp, newMagnitude, sourceId, AbilityName));
            }
        }

        /// <summary>
        /// 指定ユニットの DefenseUp バフ Magnitude×Stacks の合計を返す。
        /// 自身が付与した AttackUp とは別 EffectType だが、自 AuraSourceId 由来の
        /// DefenseUp が混入した場合に備えて除外する（防御的）。
        /// </summary>
        private static int SumDefenseUpMagnitudeExcept(RuntimeUnit unit, string ownSourceId)
        {
            int sum = 0;
            foreach (var e in unit.ActiveEffects)
            {
                if (e.EffectType != StatusEffectType.DefenseUp) continue;
                if (e.AuraSourceId == ownSourceId) continue;
                sum += (int)e.Magnitude * e.Stacks;
            }
            return sum;
        }
    }
}
