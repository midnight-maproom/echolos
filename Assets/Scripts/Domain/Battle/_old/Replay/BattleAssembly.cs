using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Conditional;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>
    /// 戦闘サブシステム（BattleContext / BattleManager / TargetEvaluator / ActionExecutor /
    /// StatusEffectProcessor）の生成と結線をカプセル化する。
    /// BattleRunner.Refactored.BattleRunner.Run（Phase R-2 で実装）が薄いまま保たれるよう、
    /// オブジェクト生成と戦闘ロジック結線の責務をここに集約する。
    ///
    /// 利用順序：
    ///   1. new BattleAssembly(allies, enemies, maxTurns, conditionalProcessors, rng)
    ///   2. Recorder の Attach*／Subscribe*（ログヘッダを実行より先に並べるため）
    ///   3. WireBattleLogic()（ExecuteAction 登録・StatusProcessor 登録・UnitDied dispatch）
    ///   4. Manager.InitializeBattle(Context) → ProcessTurn ループ
    /// </summary>
    public sealed class BattleAssembly
    {
        public BattleContext Context { get; }
        public BattleManager Manager { get; }
        public TargetEvaluator Evaluator { get; }
        public ActionExecutor Executor { get; }
        public StatusEffectProcessor StatusProcessor { get; }

        public BattleAssembly(
            IEnumerable<RuntimeUnit> allies,
            IEnumerable<RuntimeUnit> enemies,
            int maxTurns,
            IReadOnlyList<ConditionalBuffProcessor> conditionalProcessors,
            Func<int> random0to99 = null)
        {
            if (allies == null) throw new ArgumentNullException(nameof(allies));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));

            Context = new BattleContext(maxTurns);
            Context.AllyUnits.AddRange(allies);
            Context.EnemyUnits.AddRange(enemies);

            Evaluator = new TargetEvaluator();
            Manager = new BattleManager(Evaluator, conditionalProcessors);
            Executor = new ActionExecutor(random0to99);
            StatusProcessor = new StatusEffectProcessor();
        }

        /// <summary>
        /// 戦闘ロジックの結線（ログ購読の後に呼ぶ）。
        /// - Manager.OnActionExecuting → Executor.ExecuteAction
        /// - StatusProcessor を Manager / Executor の各フックに登録
        /// - Executor.OnUnitDied → Manager.DispatchConditional(UnitDied)
        /// </summary>
        public void WireBattleLogic()
        {
            Manager.OnActionExecuting += Executor.ExecuteAction;
            StatusProcessor.RegisterTo(Manager, Executor);
            Executor.OnUnitDied += (ctx, deadUnit)
                => Manager.DispatchConditional(ConditionalBuffHook.UnitDied, ctx, deadUnit);
        }

        /// <summary>味方陣営の生存リスト（読み取り専用）。</summary>
        public IReadOnlyList<RuntimeUnit> Allies => Context.AllyUnits;

        /// <summary>敵陣営の生存リスト（読み取り専用）。</summary>
        public IReadOnlyList<RuntimeUnit> Enemies => Context.EnemyUnits;

        /// <summary>陣営問わず全ユニットを順に列挙（初期効果スナップショット用）。</summary>
        public IEnumerable<RuntimeUnit> AllUnits => Allies.Concat(Enemies);
    }
}
