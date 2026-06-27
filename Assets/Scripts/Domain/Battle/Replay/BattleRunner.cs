using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    // 1 戦闘の完全な実行ループ。BattleAssembly が生成・結線を担い、Recorder がイベント
    // を BattleReport に書き出し、本 Runner はターン進行と終了判定だけを行う薄い構成。
    //
    // 終了判定：
    // - 敵全滅 → PerfectVictory
    // - 味方全滅 → CrushingDefeat
    // - ターン制限到達 → VictoryEvaluator（攻め=1 撃破勝利／守り=0 被撃破勝利）に従い
    //   AdvantageousVictory / MarginalDefeat に振り分け
    public static class BattleRunner
    {
        public static BattleReport Run(
            IEnumerable<RuntimeUnit> allies,
            IEnumerable<RuntimeUnit> enemies,
            int maxTurns,
            IDictionary<RuntimeUnit, IList<RuntimeWaza>> battleWazasByUnit = null,
            TerrainKind terrain = TerrainKind.Neutral,
            TerrainStrength terrainStrength = TerrainStrength.Light,
            bool isAttackingSide = false,
            Func<int> random0to99 = null,
            IReadOnlyList<SynergyDefinition> synergyDefinitions = null)
        {
            if (allies == null) throw new ArgumentNullException(nameof(allies));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));

            var assembly = new BattleAssembly(
                allies, enemies, maxTurns,
                terrain: terrain,
                terrainStrength: terrainStrength,
                isAttackingSide: isAttackingSide,
                random0to99: random0to99,
                synergyDefinitions: synergyDefinitions);

            var report = new BattleReport
            {
                AllyLineup = assembly.Context.AllyUnits.ToList(),
                EnemyLineup = assembly.Context.EnemyUnits.ToList(),
            };

            var nameOf = BattleLogFormatter.CreateNameResolver(assembly.Allies);
            var recorder = new BattleEventRecorder(report, nameOf);

            // 購読順：先に Recorder を Attach してから WireBattleLogic / InitializeBattle へ。
            recorder.AttachToManager(assembly.Manager);
            recorder.AttachToExecutor(assembly.Executor);
            recorder.AttachToStatusProcessor(assembly.StatusProcessor);
            foreach (var u in assembly.AllUnits)
            {
                recorder.SubscribeUnitEffects(u, assembly.Context);
            }

            assembly.WireBattleLogic();
            assembly.Manager.InitializeBattle(assembly.Context);

            // InitializeBattle 内で OnBattleStart → SynergyApplier.ApplyAll が走り、
            // Recorder が個別の OnEffectAdded をバッファに溜めている。ここで集約 1 行に Flush。
            recorder.FlushBattleStartBuffer();

            BattleResult result;
            int turn = 0;
            while (true)
            {
                if (assembly.Context.IsEnemyWiped) { result = BattleResult.PerfectVictory; break; }
                if (assembly.Context.IsAllyWiped) { result = BattleResult.CrushingDefeat; break; }
                if (assembly.Context.CurrentTurn > maxTurns)
                {
                    result = assembly.Context.IsAdvantageousVictoryCondition
                        ? BattleResult.AdvantageousVictory
                        : BattleResult.MarginalDefeat;
                    break;
                }

                assembly.Manager.ProcessTurn(assembly.Context, battleWazasByUnit);
                turn = assembly.Context.CurrentTurn - 1;

                // ProcessTurn 終了直後の死亡反映を即時判定。
                if (assembly.Context.IsEnemyWiped) { result = BattleResult.PerfectVictory; break; }
                if (assembly.Context.IsAllyWiped) { result = BattleResult.CrushingDefeat; break; }
            }

            assembly.Context.Result = result;
            report.Result = result;
            report.Turns = turn;

            recorder.RecordBattleEnd(result, turn,
                allySurvivors: BattleLogFormatter.SurvivorSummary(assembly.Allies),
                enemySurvivors: BattleLogFormatter.SurvivorSummary(assembly.Enemies));

            return report;
        }
    }
}
