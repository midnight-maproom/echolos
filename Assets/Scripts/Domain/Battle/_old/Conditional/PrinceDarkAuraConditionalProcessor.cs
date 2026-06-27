using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Conditional
{
    /// <summary>
    /// 皇太子（闇）「闇のオーラ」のターン経過連動 AttackUp Processor（必敗演出用）。
    ///
    /// 仕様：
    /// - 毎ターン全体攻撃「闇槍の薙ぎ」を撃ちつつ、3T 経過ごとに自己 AttackUp が +15 スタックする
    /// - スタック数 = floor(CurrentTurn / 3)（青天井）
    /// - Magnitude = 15 × Stacks
    /// - 解除不能（IsUndispellable=true）・発生源死亡（皇太子本人死亡）で自然消滅
    ///
    /// フック：
    /// - BattleStart：初回評価（T1 ではスタック 0 だが、念のため呼ぶ）
    /// - TurnStart：各ターン開始時に CurrentTurn から再評価
    ///
    /// 対象識別：<see cref="DarkAuraTag"/> を持つユニットだけ評価する。
    /// </summary>
    public sealed class PrinceDarkAuraConditionalProcessor : ConditionalBuffProcessor
    {
        /// <summary>闇のオーラの対象を示すタグ。Unit.Tags に付与する。</summary>
        public const string DarkAuraTag = "DarkAura";

        /// <summary>AuraSourceId の prefix（ユニット Id と組み合わせて衝突を避ける）。</summary>
        private const string DarkAuraSourcePrefix = "dark_aura:";

        /// <summary>1 スタックあたりの Magnitude 増分。</summary>
        private const int MagnitudePerStack = 15;

        private static readonly ConditionalBuffHook[] _hooks =
        {
            ConditionalBuffHook.BattleStart,
            ConditionalBuffHook.TurnStart,
        };

        public override IReadOnlyList<ConditionalBuffHook> Hooks => _hooks;

        public override void Refresh(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            RefreshTeam(context.AllyUnits, context.CurrentTurn);
            RefreshTeam(context.EnemyUnits, context.CurrentTurn);
        }

        private static void RefreshTeam(List<RuntimeUnit> team, int currentTurn)
        {
            foreach (var unit in team)
            {
                if (!unit.BaseUnit.Tags.Contains(DarkAuraTag)) continue;
                if (!unit.IsAlive) continue;

                string sourceId = DarkAuraSourcePrefix + unit.BaseUnit.Id;
                int newStacks = ResolveStacks(currentTurn);
                int newMagnitude = newStacks * MagnitudePerStack;

                var existing = unit.FindEffect(e => e.AuraSourceId == sourceId
                    && e.EffectType == StatusEffectType.AttackUp);
                int currentMagnitude = existing != null ? (int)existing.Magnitude : 0;

                // 強度に変化がなければ剥奪＋再付与をスキップ
                if (currentMagnitude == newMagnitude) continue;

                unit.RemoveEffectsWhere(e => e.AuraSourceId == sourceId);

                if (newMagnitude <= 0) continue;

                // MaxStacks は newStacks（青天井に追随）で付与し、現状のスタック保持機構と整合させる
                unit.AddEffect(StatusEffect.CreateConditional(
                    StatusEffectType.AttackUp, newMagnitude, sourceId, "闇のオーラ", maxStacks: Math.Max(1, newStacks)));
            }
        }

        /// <summary>
        /// 経過ターンからスタック数を決める：floor(currentTurn / 3)。青天井。
        /// T1=0, T2=0, T3=1, T6=2, T9=3, T12=4, ...
        /// </summary>
        public static int ResolveStacks(int currentTurn)
        {
            if (currentTurn <= 0) return 0;
            return currentTurn / 3;
        }
    }
}
