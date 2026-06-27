using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Conditional
{
    /// <summary>
    /// 「孤高の戦士」パッシブ（味方人数 ≤ 3 の時に自身を強化）の Conditional バフ Processor。
    /// 傭兵（mercenary）が <see cref="LonerTag"/> を持ち、本クラスが評価する。
    ///
    /// フック：
    /// - BattleStart：戦闘開始時に両陣営を初回評価（編成時点で 3 体以下でも発動するため）
    /// - UnitDied：死亡ユニットの陣営だけ再評価（他陣営の生存数は変わらないため）
    ///
    /// 付与効果は <see cref="StatusEffect.CreateConditional"/> 経由で
    /// IsUndispellable=true / AuraSourceId="loner:&lt;id&gt;" / Category=Conditional の整合状態で生成する。
    /// 差分判定で「Magnitude が変わらない時は剥奪＋再付与しない」ことで戦闘ログの無駄を抑止する。
    /// </summary>
    public sealed class LonerWolfConditionalProcessor : ConditionalBuffProcessor
    {
        /// <summary>Loner パッシブの対象を示すタグ。Unit.Tags に付与する。</summary>
        public const string LonerTag = "Loner";

        /// <summary>AuraSourceId の prefix（ユニット Id と組み合わせて衝突を避ける）。</summary>
        private const string LonerSourcePrefix = "loner:";

        private static readonly ConditionalBuffHook[] _hooks =
        {
            ConditionalBuffHook.BattleStart,
            ConditionalBuffHook.UnitDied,
        };

        public override IReadOnlyList<ConditionalBuffHook> Hooks => _hooks;

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

        private static void RefreshTeam(List<RuntimeUnit> team)
        {
            int aliveCount = 0;
            foreach (var u in team)
                if (u.IsAlive) aliveCount++;

            foreach (var unit in team)
            {
                if (!unit.BaseUnit.Tags.Contains(LonerTag)) continue;

                string sourceId = LonerSourcePrefix + unit.BaseUnit.Id;
                int newAtkMag = unit.IsAlive ? ResolveAttackMagnitude(aliveCount) : 0;
                int newDefMag = unit.IsAlive ? ResolveDefenseMagnitude(aliveCount) : 0;

                // AttackUp / DefenseUp は同タイミングで変動するため AttackUp 側を代表として差分判定。
                var existing = unit.FindEffect(e => e.AuraSourceId == sourceId
                    && e.EffectType == StatusEffectType.AttackUp);
                int currentAtkMag = existing != null ? (int)existing.Magnitude : 0;

                if (currentAtkMag == newAtkMag) continue;

                unit.RemoveEffectsWhere(e => e.AuraSourceId == sourceId);

                if (!unit.IsAlive) continue;
                if (newAtkMag <= 0) continue;

                unit.AddEffect(StatusEffect.CreateConditional(
                    StatusEffectType.AttackUp, newAtkMag, sourceId, "孤高の戦士"));
                unit.AddEffect(StatusEffect.CreateConditional(
                    StatusEffectType.DefenseUp, newDefMag, sourceId, "孤高の戦士"));
            }
        }

        /// <summary>
        /// 陣営生存数から AttackUp の Magnitude を決める。
        /// 3 体→+10、2 体→+20、1 体→+30。4 体以上は 0（効果なし）。
        /// </summary>
        public static int ResolveAttackMagnitude(int aliveCount)
        {
            if (aliveCount <= 0) return 0;
            if (aliveCount > 3) return 0;
            return (4 - aliveCount) * 10;
        }

        /// <summary>
        /// 陣営生存数から DefenseUp の Magnitude を決める。AttackUp の半分。
        /// 3 体→+5、2 体→+10、1 体→+15。4 体以上は 0（効果なし）。
        /// 1 体時の物理ほぼ完封状態を避け、現実的な耐久に収める。
        /// </summary>
        public static int ResolveDefenseMagnitude(int aliveCount)
        {
            return ResolveAttackMagnitude(aliveCount) / 2;
        }
    }
}
