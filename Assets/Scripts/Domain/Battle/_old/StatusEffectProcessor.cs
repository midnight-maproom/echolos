// 状態異常・属性干渉・アンデッド復活の処理。
//
// 【設計】
// - BattleManager / ActionExecutor のイベントに登録して使用する（疎結合）
// - テスト容易性のため各ハンドラメソッドを public として公開する
// - 呼び出し例:
//     var processor = new StatusEffectProcessor();
//     processor.RegisterTo(battleManager, actionExecutor);

using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    /// <summary>
    /// 状態異常（燃焼・凍結・麻痺・呪い）の発動ロジック、
    /// 属性干渉による状態異常解除、アンデッド復活を担うクラス。
    ///
    /// 各フックの発火タイミング：
    /// - OnActionStart   → 呪い即死判定
    /// - OnActionSkipped → 麻痺発動処理（スタック消去＋許容量倍化）
    /// - OnActionEnd     → 燃焼ダメージ
    /// - OnEndPhase      → 凍結スタック -1、時限効果の残ターン減算
    /// - OnHitLanded     → 火属性で凍結解除、水/氷属性で燃焼解除
    /// - OnUnitDied      → アンデッド復活判定
    /// </summary>
    public class StatusEffectProcessor
    {
        /// <summary>
        /// 状態異常（呪い・燃焼のオーバーキル）によってユニットが死亡した際に発火する。
        /// 装備品返還などをここにフックする。
        /// 引数: (context, 死亡したユニット)
        /// Note: この時点でユニットは State=Dead。復活スキルがあれば直後に Active に戻る。
        /// </summary>
        public event Action<BattleContext, RuntimeUnit> OnStatusEffectKill;

        /// <summary>
        /// 燃焼（毒）の継続ダメージが適用された時に発火する（ログ/可視化用）。
        /// 引数: (context, 対象ユニット, 与えた燃焼ダメージ量)
        /// </summary>
        public event Action<BattleContext, RuntimeUnit, int> OnBurnTickDamage;

        // 登録API

        /// <summary>
        /// BattleManagerとActionExecutorの各イベントにすべてのハンドラを登録する。
        /// バトル開始前に一度だけ呼び出すこと。
        /// </summary>
        public void RegisterTo(BattleManager battleManager, ActionExecutor actionExecutor)
        {
            battleManager.OnActionStart   += HandleActionStart;
            battleManager.OnActionSkipped += HandleActionSkipped;
            battleManager.OnActionEnd     += HandleActionEnd;
            battleManager.OnEndPhase      += HandleEndPhase;
            actionExecutor.OnHitLanded    += HandleHitLanded;
            actionExecutor.OnUnitDied     += HandleUnitDied;
        }

        // 行動開始フック（呪い判定）

        /// <summary>
        /// BattleManager.OnActionStartから呼ばれる。
        ///
        /// 呪い（Curse）即死判定のみを行う：
        ///   現在HP（シールドを含まない BaseUnit.CurrentHP）が「スタック数×10」以下なら即死。
        ///
        /// 麻痺の発動処理（スタック削除＋許容量倍化）は <see cref="HandleActionSkipped"/> で
        /// BattleManager.OnActionSkipped を購読して行う。これにより BattleManager 側を
        /// 一切改修せずに済む（既存の IsParalyzed 経路でスキップ→スキップイベント発火→
        /// 本処理で消化、の自然な流れ）。
        ///
        /// 加えて ActionGuard（自己防御一過性）の削除も担う：付与した自分の行動順は
        /// ApplySupportEffects の後で OnActionStart より後に来るため自己ヒットしない。
        /// 「次の自分の行動順」が来た瞬間にここで削除されることで仕様が満たされる。
        /// </summary>
        public void HandleActionStart(BattleContext context, RuntimeUnit unit)
        {
            if (!unit.IsAlive) return;

            // ActionGuard 削除：このユニットに前ターン以前に付与された自己防御を、自分の番が来た冒頭で解除。
            unit.RemoveEffectsWhere(e => e.Category == BuffCategory.ActionGuard);

            // 呪い判定: 現在HP <= スタック数×10 なら即死。シールドは無視する。
            var curseEffect = FindEffect(unit, StatusEffectType.Curse);
            if (curseEffect != null && unit.BaseUnit.CurrentHP <= curseEffect.Stacks * 10)
                ApplyStatusKill(context, unit);
        }

        /// <summary>
        /// BattleManager.OnActionSkippedから呼ばれる（待機／麻痺／凍結いずれでも発火）。
        ///
        /// 麻痺発動による行動不能だった場合のみ、麻痺仕様に基づき：
        ///   (a) Paralysis 効果を全て削除（スタック 0 化）
        ///   (b) ParalysisTolerance を倍化（1→2→4→8…）
        /// を実行する。判別は IsParalyzed (スタック合計 ≥ 許容量) で行う。
        /// 凍結や単純待機による行動スキップでは何もしない（凍結は別系統の状態異常）。
        /// </summary>
        public void HandleActionSkipped(BattleContext context, RuntimeUnit unit)
        {
            if (!unit.IsAlive) return;
            if (!unit.IsParalyzed) return; // 凍結や待機による行動スキップは対象外

            unit.RemoveEffectsWhere(e => e.EffectType == StatusEffectType.Paralysis);
            unit.ParalysisTolerance *= 2;
        }

        // 行動終了フック（燃焼ダメージ）

        /// <summary>
        /// BattleManager.OnActionEndから呼ばれる（スキップ時も含む）。
        ///
        /// 燃焼（Burn）ダメージ:
        ///   1スタックあたりのダメージ量は Magnitude（未設定なら1）。
        ///   実ダメージ = Stacks × max(1, Magnitude)。防御力無視・シールド優先消費。
        ///   AttackUp等の「1スタックあたりの効果量＝Magnitude」と同じ規約。
        ///   例：男爵の毒霧は Magnitude=2 / MaxStacks=5 → 2,4,6,8,10と立ち上がり最大10/T。
        ///   このダメージでHPが0になった場合、ApplyStatusKillで死亡処理を行う。
        /// </summary>
        public void HandleActionEnd(BattleContext context, RuntimeUnit unit)
        {
            if (!unit.IsAlive) return;

            var burnEffect = FindEffect(unit, StatusEffectType.Burn);
            if (burnEffect == null || burnEffect.Stacks <= 0) return;

            // 燃焼ダメージ = スタック数 × 1スタックあたりのダメージ（Magnitude、未設定なら1）
            // Magnitude は float のため int に丸めてから Max でフォールバック1を担保する。
            int damagePerStack = Math.Max(1, (int)burnEffect.Magnitude);
            int burnDamage = burnEffect.Stacks * damagePerStack;
            OnBurnTickDamage?.Invoke(context, unit, burnDamage);

            // シールド優先消費
            if (unit.CurrentShield >= burnDamage)
            {
                unit.CurrentShield -= burnDamage;
                return; // シールドが全吸収、HPへの影響なし
            }

            // シールドを削り切り、超過分をHPへ適用
            int overflow = burnDamage - unit.CurrentShield;
            unit.CurrentShield = 0;
            unit.BaseUnit.CurrentHP -= overflow;

            if (unit.BaseUnit.CurrentHP <= 0)
            {
                unit.BaseUnit.CurrentHP = 0;
                ApplyStatusKill(context, unit);
            }
        }

        // ターン終了フック（スタック減算）

        /// <summary>
        /// BattleManager.OnEndPhaseから呼ばれる。
        ///
        /// 処理1 - 凍結（Freeze）スタック減算:
        ///   毎ターン終了時にスタックを1減らす。0以下で効果消滅。
        ///
        /// 処理2 - 時限効果（RemainingTurns >= 0）の残ターン減算:
        ///   バフ/デバフ等の時限効果を毎ターン1減らし、0で消滅させる。
        ///   永続効果（RemainingTurns == -1。麻痺/凍結/燃焼/呪い/かばう等）は対象外。
        ///
        /// 麻痺のスタック減算は行わない（許容量倍化方式・<see cref="HandleActionSkipped"/> 参照）。
        /// </summary>
        public void HandleEndPhase(BattleContext context)
        {
            var allUnits = GetAllUnits(context);
            foreach (var unit in allUnits)
            {
                if (!unit.IsAlive) continue;
                ReduceFreezeStacks(unit);
                ReduceTimedEffects(unit);
            }
        }

        // 被ダメージフック（属性干渉による状態異常解除）

        /// <summary>
        /// ActionExecutor.OnHitLandedから呼ばれる（1ヒット命中ごと）。
        ///
        /// 属性干渉処理:
        /// - 火（Fire）属性の技を受けた場合   → 凍結（Freeze）を全スタック解除
        /// - 水（Water）/氷（Ice）属性の技を受けた場合 → 燃焼（Burn）を全スタック解除
        /// </summary>
        public void HandleHitLanded(
            BattleContext context,
            RuntimeUnit attacker,
            RuntimeUnit target,
            int damage,
            Element element)
        {
            switch (element)
            {
                case Element.Fire:
                    // 火属性 → 凍結を全解除
                    target.RemoveEffectsWhere(e => e.EffectType == StatusEffectType.Freeze);
                    break;

                case Element.Water:
                case Element.Ice:
                    // 水/氷属性 → 燃焼を全解除
                    target.RemoveEffectsWhere(e => e.EffectType == StatusEffectType.Burn);
                    break;
            }
        }

        // 死亡フック（アンデッド復活）

        /// <summary>
        /// ActionExecutor.OnUnitDiedから呼ばれる（ダメージによる死亡時）。
        ///
        /// アンデッド復活判定:
        /// - ReviveInvalidデバフがある場合: 復活不可（完全死亡確定）
        /// - CurrentReviveCount > 0 の場合: 回数を1消費してHPを最大HPの50%に回復し復活
        ///   行動権は次ターンから（HasActedThisTurn = true）
        /// </summary>
        public void HandleUnitDied(BattleContext context, RuntimeUnit unit)
        {
            TryReviveUnit(unit);
            RemoveDeadAuraSource(context, unit);
        }

        // 内部ユーティリティ

        /// <summary>
        /// 状態異常（呪い・燃焼オーバーキル）によるユニット死亡処理。
        /// HP=0にクランプし、State=Deadにして死亡通知を発火する。
        /// その後、復活判定を行う。
        /// </summary>
        private void ApplyStatusKill(BattleContext context, RuntimeUnit unit)
        {
            unit.BaseUnit.CurrentHP = 0;
            unit.BaseUnit.State = UnitState.Dead;

            // 死亡通知（装備返還などをここにフック）
            OnStatusEffectKill?.Invoke(context, unit);

            // 復活判定（通知後に行うことでActionExecutorの処理順と一致させる）
            TryReviveUnit(unit);
            RemoveDeadAuraSource(context, unit);
        }

        /// <summary>
        /// 死亡した置物オーラ源の効果を全ユニットから剥奪する。
        /// 復活している場合は剥奪しない。オーラ源でなければ何もしない。
        /// </summary>
        private static void RemoveDeadAuraSource(BattleContext context, RuntimeUnit unit)
        {
            if (unit.IsAlive) return; // 復活していればオーラ継続
            if (unit.BaseUnit.AuraEffect == null) return;

            string sourceId = unit.BaseUnit.Id;
            foreach (var u in GetAllUnits(context))
                u.RemoveEffectsWhere(e => e.AuraSourceId == sourceId);
        }

        /// <summary>
        /// アンデッド復活を試みる。
        /// ReviveInvalidデバフがある、またはCurrentReviveCountが0以下の場合は何もしない。
        /// 復活成功時: State を Active に戻し、HPを最大HPの50%に回復する（最低1）。
        /// 行動権は次ターンから（HasActedThisTurn = true）。
        /// </summary>
        private static void TryReviveUnit(RuntimeUnit unit)
        {
            // 復活無効化デバフが付与されている場合は復活しない
            if (unit.HasReviveInvalid) return;

            // 復活回数が残っていない場合は復活しない
            if (unit.CurrentReviveCount <= 0) return;

            // 復活処理
            unit.CurrentReviveCount--;
            unit.BaseUnit.State = UnitState.Active;
            unit.BaseUnit.CurrentHP = Math.Max(1, unit.MaxHP / 2); // 最大HPの50%回復
            unit.HasActedThisTurn = true; // 行動権は次のターンから
        }

        /// <summary>凍結スタックをターンごとに1減算する。0以下で効果を除去する</summary>
        private static void ReduceFreezeStacks(RuntimeUnit unit)
        {
            var freeze = FindEffect(unit, StatusEffectType.Freeze);
            if (freeze == null) return;

            freeze.Stacks--;
            if (freeze.Stacks <= 0)
                unit.RemoveEffect(freeze);
        }

        /// <summary>
        /// 時限効果（RemainingTurns >= 0）の残ターンを1減らし、0で効果を除去する。
        /// 永続効果（RemainingTurns == -1）は対象外。
        /// </summary>
        private static void ReduceTimedEffects(RuntimeUnit unit)
        {
            // RemoveEffectsWhere の述語内で副作用（RemainingTurns--）を行う。
            // 述語の評価順は後方走査なのでインデックスベース削除の挙動と一致する。
            unit.RemoveEffectsWhere(effect =>
            {
                if (effect.RemainingTurns < 0) return false; // 永続は減算しない
                effect.RemainingTurns--;
                return effect.RemainingTurns <= 0;
            });
        }

        /// <summary>
        /// 指定タイプの状態異常を ActiveEffects から検索して返す。
        /// 見つからない場合は null を返す。
        /// </summary>
        private static StatusEffect FindEffect(RuntimeUnit unit, StatusEffectType effectType)
        {
            return unit.FindEffect(effectType);
        }

        /// <summary>味方・敵全ユニットを結合したリストを返す</summary>
        private static List<RuntimeUnit> GetAllUnits(BattleContext context)
        {
            var result = new List<RuntimeUnit>(context.AllyUnits.Count + context.EnemyUnits.Count);
            result.AddRange(context.AllyUnits);
            result.AddRange(context.EnemyUnits);
            return result;
        }
    }
}
