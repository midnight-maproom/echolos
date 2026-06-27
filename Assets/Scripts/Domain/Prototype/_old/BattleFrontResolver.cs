// Assets/Scripts/Core/Prototype/BattleFrontResolver.cs
// プロト 段階2-Inc2: IFrontResolver の本実装（既存の戦闘Coreを各戦線で回す・純C#）
//
// 割り当て味方 vs 戦線の敵編成 を、既存の BattleManager/TargetEvaluator/ActionExecutor で
// 1戦闘ぶん完走させ、勝敗（戦況評価）と戦闘不能ユニットを FrontResolution に詰めて返す。
using System;
using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>
    /// 既存の戦闘Coreを使って1戦線を解決する IFrontResolver の本実装。
    /// 戦闘RNG（回避判定）は注入可能（テストで固定値を渡せる）。
    /// </summary>
    public sealed class BattleFrontResolver : IFrontResolver
    {
        private readonly int _maxTurnLimit;
        private readonly Func<int> _random0to99;

        /// <param name="maxTurnLimit">1戦線の戦闘ターン上限（到達時は戦況評価で優勢/劣勢を判定）。</param>
        /// <param name="random0to99">回避判定用の0〜99乱数。nullなら戦闘側のデフォルト乱数。</param>
        public BattleFrontResolver(int maxTurnLimit = 20, Func<int> random0to99 = null)
        {
            _maxTurnLimit = maxTurnLimit;
            _random0to99 = random0to99;
        }

        public FrontResolution Resolve(IReadOnlyList<RuntimeUnit> assignedAllies, FrontState front)
        {
            // 戦線ごとの陣形として、リスト順にスロット0..nを振り直す（前列/後列＝射程が正しく機能するように）。
            ReslotByOrder(assignedAllies);
            ReslotByOrder(front.EnemyDivision);

            var context = new BattleContext(_maxTurnLimit);
            context.AllyUnits.AddRange(assignedAllies);
            context.EnemyUnits.AddRange(front.EnemyDivision);

            var evaluator = new TargetEvaluator();
            var battleManager = new BattleManager(evaluator);
            var executor = new ActionExecutor(_random0to99);
            battleManager.OnActionExecuting += executor.ExecuteAction;

            battleManager.InitializeBattle(context, allyLeaderUnitId: null);

            BattleResult result;
            int safety = 0;
            do
            {
                result = battleManager.ProcessTurn(context);
            }
            while (result == BattleResult.None && ++safety <= _maxTurnLimit + 5);

            bool held = result == BattleResult.PerfectVictory
                     || result == BattleResult.AdvantageousVictory;

            var report = new FrontResolution
            {
                Held = held,
                BattleResult = result,
            };
            foreach (var ally in assignedAllies)
                if (ally.CurrentHP <= 0) report.DownedAllies.Add(ally);

            return report;
        }

        /// <summary>ユニット群にリスト順でスロット番号（0,1,2,...）を割り当てる。</summary>
        private static void ReslotByOrder(IReadOnlyList<RuntimeUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
                units[i].SlotIndex = i;
        }
    }
}
