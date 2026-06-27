using System;
using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 戦闘進行のオーケストレータ。
    //
    // 1 ターンの流れ：
    //   ProcessTurn 入口で HasActedThisTurn リセット
    //   StartPhase（OnStartPhase 発火）
    //   MainPhase：動的順序方式の繰り返し
    //     SpdOrderResolver.SelectNext で「未行動かつ生存」から実行時 SPD で 1 体選定 →
    //     その場で DeclareAction（ターゲット選定も最新状態）→
    //     OnActionStart → (待機なら OnActionSkipped / それ以外は ActionExecutor.Execute) →
    //     HasActedThisTurn=true → OnActionEnd
    //     ターン中の SPD 変動・ターゲット死亡・状態異常付与等が次のステップに即反映される。
    //   EndPhase（OnEndPhase 発火 → CD 減算）
    //   CurrentTurn++
    //
    // 反撃は AttackEffect 内部完結のため ReactionStack 経路は使わない。
    // 旧 ConditionalBuffProcessor 結線も属性シナジー Persistent 化により本クラスでは扱わない。
    public sealed class BattleManager
    {
        private readonly ActionExecutor _executor;

        /// <summary>戦闘開始時に 1 回だけ発火。SynergyApplier の結線対象。</summary>
        public event Action<BattleContext> OnBattleStart;

        /// <summary>各ターンの StartPhase 開始時に発火。</summary>
        public event Action<BattleContext> OnStartPhase;

        /// <summary>1 ユニットの行動開始直前に発火。StatusEffectProcessor.HandleActionStart の結線対象。</summary>
        public event Action<BattleContext, RuntimeUnit> OnActionStart;

        /// <summary>行動が待機（麻痺・凍結）でスキップされた時に発火。StatusEffectProcessor.HandleActionSkipped の結線対象。</summary>
        public event Action<BattleContext, RuntimeUnit> OnActionSkipped;

        /// <summary>1 ユニットの行動終了直後に発火。</summary>
        public event Action<BattleContext, RuntimeUnit> OnActionEnd;

        /// <summary>各ターンの EndPhase 開始時に発火。StatusEffectProcessor.HandleEndPhase の結線対象。</summary>
        public event Action<BattleContext> OnEndPhase;

        public BattleManager(ActionExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public void InitializeBattle(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            context.InitialAllyCount = CountAlive(context.AllyUnits);
            context.InitialEnemyCount = CountAlive(context.EnemyUnits);
            context.CurrentPhase = PhaseState.Start;

            OnBattleStart?.Invoke(context);
        }

        public void ProcessTurn(
            BattleContext context,
            IDictionary<RuntimeUnit, IList<RuntimeWaza>> battleWazasByUnit)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            ResetActedFlags(context.AllyUnits);
            ResetActedFlags(context.EnemyUnits);

            context.CurrentPhase = PhaseState.Start;
            OnStartPhase?.Invoke(context);

            context.CurrentPhase = PhaseState.Main;
            ProcessMainPhase(context, battleWazasByUnit);

            // MainPhase 中に陣営全滅したら EndPhase を丸ごとスキップして即決着。
            // HOT 回復 / Burn / SearingWound 等のターン末処理と CD 減算は不要
            // （戦闘終了後の演出はカット・CD は次戦闘で RuntimeWaza 再構築時にリセット）。
            // 戦闘終了判定そのもの（BattleResult 変換）は BattleRunner が担う責務分離は維持。
            if (!context.IsEnemyWiped && !context.IsAllyWiped)
            {
                context.CurrentPhase = PhaseState.End;
                OnEndPhase?.Invoke(context);
                ReduceCooldowns(battleWazasByUnit);
            }

            context.CurrentTurn++;
        }

        private void ProcessMainPhase(
            BattleContext context,
            IDictionary<RuntimeUnit, IList<RuntimeWaza>> battleWazasByUnit)
        {
            // 動的順序方式：各ステップで「未行動かつ生存」のユニットから実行時 SPD で次の 1 体を
            // 選び、その場で DeclareAction → Execute する。ターン中の SPD 変動／ターゲット死亡が
            // 即反映される。事前に行動順をソートして固定する旧方式は廃止。
            while (true)
            {
                var unit = SpdOrderResolver.SelectNext(
                    context.AllyUnits,
                    context.EnemyUnits,
                    context.IsAttackingSide,
                    getSpd: TargetEvaluator.ComputeEffectiveSpd);
                if (unit == null) break;

                var ownSide = ResolveOwnSide(context, unit);
                var opponentSide = ownSide == context.AllyUnits ? context.EnemyUnits : context.AllyUnits;
                var wazas = GetBattleWazas(unit, battleWazasByUnit);
                var decl = TargetEvaluator.DeclareAction(unit, wazas, ownSide, opponentSide);

                OnActionStart?.Invoke(context, unit);
                if (!unit.IsAlive)
                {
                    // OnActionStart 内（StatusEffectProcessor.HandleActionStart）で呪い即死した場合に備えて
                    // HasActedThisTurn を立ててループ脱出可能にする（無限ループ防止）。
                    unit.HasActedThisTurn = true;
                    OnActionEnd?.Invoke(context, unit);
                    continue;
                }

                if (decl.IsWaiting)
                    OnActionSkipped?.Invoke(context, unit);
                else
                    _executor.Execute(decl, context);

                unit.HasActedThisTurn = true;
                OnActionEnd?.Invoke(context, unit);

                // ターン中に陣営全滅したら以降の未行動ユニットは行動させない
                // （敵全滅後の防御フォールバック発動・無意味な攻撃を抑止）。
                // EndPhase（HOT 回復・Burn 等のターン末処理）は ProcessTurn 末尾で通常通り走る。
                if (context.IsEnemyWiped || context.IsAllyWiped) break;
            }
        }

        private static IList<RuntimeUnit> ResolveOwnSide(BattleContext context, RuntimeUnit unit)
        {
            if (context.AllyUnits != null && context.AllyUnits.Contains(unit)) return context.AllyUnits;
            return context.EnemyUnits;
        }

        private static IList<RuntimeWaza> GetBattleWazas(
            RuntimeUnit unit,
            IDictionary<RuntimeUnit, IList<RuntimeWaza>> battleWazasByUnit)
        {
            if (battleWazasByUnit == null) return null;
            return battleWazasByUnit.TryGetValue(unit, out var list) ? list : null;
        }

        private static void ResetActedFlags(IList<RuntimeUnit> side)
        {
            if (side == null) return;
            foreach (var u in side)
                if (u != null) u.HasActedThisTurn = false;
        }

        private static void ReduceCooldowns(
            IDictionary<RuntimeUnit, IList<RuntimeWaza>> battleWazasByUnit)
        {
            if (battleWazasByUnit == null) return;
            foreach (var pair in battleWazasByUnit)
            {
                var list = pair.Value;
                if (list == null) continue;
                foreach (var w in list)
                {
                    if (w == null) continue;
                    if (w.CurrentCooldown > 0) w.CurrentCooldown--;
                }
            }
        }

        private static int CountAlive(IList<RuntimeUnit> side)
        {
            if (side == null) return 0;
            int n = 0;
            foreach (var u in side)
                if (u != null && u.IsAlive) n++;
            return n;
        }
    }
}
