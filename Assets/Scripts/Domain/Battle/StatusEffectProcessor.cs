using System;
using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 状態異常・継続効果・アンデッド復活を担う Processor。
    //
    // 公開ハンドラのみを持ち、BattleManager のイベントへの結線は呼び出し側責務とする
    // （BattleAssembly が結線・Processor 単体はテスト容易性のため public ハンドラだけ持つ）。
    //
    // 仕様 320 §1.4.4 Burn 蓄積モデル：Shield 貫通で HP 直撃（攻撃ダメージのみ Shield で吸収）。
    // 仕様 320 §4.2 光属性 HealOverTime：ターン終了時に最大 HP × Magnitude % 回復・最大 HP でクランプ。
    // HealOverTime はターン終了フェーズで全対象分を蓄積し、OnHealOverTimePhase で 1 回だけ
    // 集約発火する（観戦ビュー側で陣営単位 1 行ログに集約するため）。
    public class StatusEffectProcessor
    {
        public event Action<BattleContext, RuntimeUnit, int> OnBurnTickDamage;
        public event Action<BattleContext, IReadOnlyList<HealOverTimeTick>> OnHealOverTimePhase;
        public event Action<BattleContext, RuntimeUnit> OnStatusEffectKill;

        public readonly struct HealOverTimeTick
        {
            public readonly RuntimeUnit Unit;
            public readonly int Healed;
            public readonly int HPAfter;
            public readonly string SourceLabel;

            public HealOverTimeTick(RuntimeUnit unit, int healed, int hpAfter, string sourceLabel)
            {
                Unit = unit;
                Healed = healed;
                HPAfter = hpAfter;
                SourceLabel = sourceLabel;
            }
        }

        // 行動開始フック（呪い即死判定＋ SelfGuard 削除）。
        public void HandleActionStart(BattleContext context, RuntimeUnit unit)
        {
            if (unit == null || !unit.IsAlive) return;

            unit.RemoveEffectsWhere(e => e is SelfGuard);

            if (unit.FindEffect(EffectKind.Curse) is CurseEffect curse
                && unit.BaseUnit.CurrentHP <= curse.Stacks * 10)
            {
                ApplyStatusKill(context, unit);
            }
        }

        // 行動スキップ後フック。麻痺発動時のみスタック消去＋許容量倍化。
        public void HandleActionSkipped(BattleContext context, RuntimeUnit unit)
        {
            if (unit == null || !unit.IsAlive) return;
            if (!unit.IsParalyzed) return;

            unit.RemoveEffectsWhere(e => e is ParalysisEffect);
            unit.ParalysisTolerance *= 2;
        }

        // ターン終了フェーズ。Burn 蓄積 → HealOverTime 回復 → 凍結 -1 → 時限 -1 の順で処理。
        public void HandleEndPhase(BattleContext context)
        {
            if (context == null) return;

            int total = context.AllyUnits.Count + context.EnemyUnits.Count;
            var all = new List<RuntimeUnit>(total);
            all.AddRange(context.AllyUnits);
            all.AddRange(context.EnemyUnits);

            var healTicks = new List<HealOverTimeTick>();
            foreach (var unit in all)
            {
                if (unit == null || !unit.IsAlive) continue;

                ApplyBurnDamage(context, unit);
                if (!unit.IsAlive) continue;

                var tick = ApplyHealOverTime(unit);
                if (tick.HasValue) healTicks.Add(tick.Value);
                ReduceFreezeStacks(unit);
                ReduceTimedEffects(unit);
            }
            if (healTicks.Count > 0)
                OnHealOverTimePhase?.Invoke(context, healTicks);
        }

        // ユニット死亡時フック。アンデッド復活を試みる。
        public void HandleUnitDied(BattleContext context, RuntimeUnit unit)
        {
            if (unit == null) return;
            TryReviveUnit(unit);
        }

        // ───── 内部処理 ─────

        private void ApplyBurnDamage(BattleContext context, RuntimeUnit unit)
        {
            if (!(unit.FindEffect(EffectKind.Burn) is ContinuousDot burn) || burn.Stacks <= 0) return;

            int perStack = Math.Max(1, (int)burn.Magnitude);
            int damage = burn.Stacks * perStack;
            if (damage <= 0) return;

            // 先に HP を減算してから通知発火する。Recorder の購読が unit.CurrentHP を
            // TargetHPAfter として読むため、順序を逆にすると Burn 適用前の HP が記録されて
            // 観戦ビューの HP バーが更新されない。
            unit.BaseUnit.CurrentHP -= damage;
            bool died = unit.BaseUnit.CurrentHP <= 0;
            if (died) unit.BaseUnit.CurrentHP = 0;

            OnBurnTickDamage?.Invoke(context, unit, damage);

            if (died) ApplyStatusKill(context, unit);
        }

        // HealOverTime を 1 unit ぶん適用する。値反映は即時実行するが、イベント発火は呼び出し側に委ねる
        // （HandleEndPhase が全 unit ぶんを蓄積して 1 回だけ OnHealOverTimePhase を Invoke するため）。
        // SourceLabel は HealOverTime を持つ effect のうち SourceAbilityName が最初に見つかったものを採用。
        private HealOverTimeTick? ApplyHealOverTime(RuntimeUnit unit)
        {
            int maxHp = unit.MaxHP;
            if (maxHp <= 0) return null;

            int totalHeal = 0;
            string sourceLabel = null;
            foreach (var e in unit.ActiveEffects)
            {
                if (!(e is ContinuousHot hot)) continue;
                int perStack = (int)Math.Round(maxHp * hot.Magnitude / 100.0,
                    MidpointRounding.AwayFromZero);
                totalHeal += perStack * hot.Stacks;
                if (sourceLabel == null && !string.IsNullOrEmpty(hot.SourceAbilityName))
                    sourceLabel = hot.SourceAbilityName;
            }
            if (totalHeal <= 0) return null;

            int before = unit.BaseUnit.CurrentHP;
            int after = Math.Min(maxHp, before + totalHeal);
            int healed = after - before;
            if (healed <= 0) return null;

            unit.BaseUnit.CurrentHP = after;
            return new HealOverTimeTick(unit, healed, after, sourceLabel);
        }

        private static void ReduceFreezeStacks(RuntimeUnit unit)
        {
            if (!(unit.FindEffect(EffectKind.Freeze) is FreezeEffect freeze)) return;
            freeze.Stacks--;
            if (freeze.Stacks <= 0) unit.RemoveEffect(freeze);
        }

        private static void ReduceTimedEffects(RuntimeUnit unit)
        {
            unit.RemoveEffectsWhere(effect =>
            {
                if (effect.RemainingTurns < 0) return false;
                effect.RemainingTurns--;
                return effect.RemainingTurns <= 0;
            });
        }

        private void ApplyStatusKill(BattleContext context, RuntimeUnit unit)
        {
            unit.BaseUnit.CurrentHP = 0;
            unit.BaseUnit.State = UnitState.Dead;
            OnStatusEffectKill?.Invoke(context, unit);
            TryReviveUnit(unit);
        }

        private static void TryReviveUnit(RuntimeUnit unit)
        {
            if (unit.HasReviveInvalid) return;
            if (unit.CurrentReviveCount <= 0) return;

            unit.CurrentReviveCount--;
            unit.BaseUnit.State = UnitState.Active;
            unit.BaseUnit.CurrentHP = Math.Max(1, unit.MaxHP / 2);
            unit.HasActedThisTurn = true;
        }
    }
}
