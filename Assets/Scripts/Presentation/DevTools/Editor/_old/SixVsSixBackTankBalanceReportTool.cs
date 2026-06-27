// 6v6 BackTank バランス評価ツール：後列にも重装兵を置いて柔らかい後衛を守らせた場合の勝率を集計する。
//
// 味方 6 体（slot 0-5）：
//   前列：重装兵 / 双剣士 / 入れ替え枠（王女・騎士・双剣士・大槌兵・サムライ・忍者・計 6）
//   後列：重装兵 / 入れ替え枠 × 2（弓兵・司祭・踊り子・軍師・雷魔導士・巫女・炎魔導士・計 7・重複あり）
//   組合せ：6 × 7 × 7 = 294 編成
//
// 敵 6 体（slot 0-5）：
//   前列：帝国重装兵 / 帝国双剣士 / 入れ替え枠（帝国騎士・帝国傭兵・帝国暗殺者・計 3）
//   後列：帝国重装兵 / 入れ替え枠 × 2（帝国の影・帝国弓兵・帝国大魔導士・帝国司祭・帝国炎魔導士・計 5・重複あり）
//   組合せ：3 × 5 × 5 = 75 編成
//
// 総戦闘数：294 × 75 = 22050。
// 集計対象は **入れ替え枠で登場したユニットのみ**（固定枠の重装兵・双剣士等は集計外）。
//
// Editor メニュー「Echolos/Tools/Balance Report (6v6 BackTank)」で実行。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Models;
using Echolos.Domain.Prototype;

namespace Echolos.Presentation.DevTools.Editor
{
    public static class SixVsSixBackTankBalanceReportTool
    {
        private class MatchupQuery
        {
            public string Label;
            public string[] AllyAnyOf;
            public string[] EnemyAnyOf;
        }

        /// <summary>味方前列入れ替え枠（slot 2・6 体）。</summary>
        private static readonly Func<Unit>[] AllyFrontSwapFactories =
        {
            AlliesRoster.Princess,
            AlliesRoster.Paladin,
            AlliesRoster.Attacker1,
            AlliesRoster.Debuffer,
            AlliesRoster.Samurai,
            AlliesRoster.Ninja,
        };

        /// <summary>味方後列入れ替え枠（slot 4 と slot 5・各 7 体・重複あり）。</summary>
        private static readonly Func<Unit>[] AllyBackSwapFactories =
        {
            AlliesRoster.Archer,
            AlliesRoster.Healer1,
            AlliesRoster.Buffer,
            AlliesRoster.Tactician,
            AlliesRoster.AoeMage,
            AlliesRoster.Healer2,
            AlliesRoster.FireMage,
        };

        /// <summary>敵前列入れ替え枠（slot 2・3 体）。</summary>
        private static readonly Func<Unit>[] EnemyFrontSwapFactories =
        {
            EnemiesRoster.ImperialPaladin,
            EnemiesRoster.ImperialSamurai,
            EnemiesRoster.ImperialAssassin,
        };

        /// <summary>敵後列入れ替え枠（slot 4 と slot 5・各 5 体・重複あり）。</summary>
        private static readonly Func<Unit>[] EnemyBackSwapFactories =
        {
            EnemiesRoster.ImperialShadow,
            EnemiesRoster.ImperialArcher,
            EnemiesRoster.ImperialAoeMage,
            EnemiesRoster.ImperialHealer,
            EnemiesRoster.ImperialFireMage,
        };

        /// <summary>特定マッチアップ集計。味方側に AllyAnyOf のいずれか1体以上＋敵側に EnemyAnyOf のいずれか1体以上
        /// が登場する戦闘の勝率を別集計する。役割設計の意図確認用（例：忍者 vs 魔導士で勝率が上がるか）。</summary>
        private static readonly MatchupQuery[] Matchups =
        {
            new MatchupQuery
            {
                Label = "忍者 vs (帝国炎魔導士 or 帝国大魔導士)",
                AllyAnyOf = new[] { "ninja" },
                EnemyAnyOf = new[] { "imperial_firemage", "imperial_aoemage" },
            },
            new MatchupQuery
            {
                Label = "巫女 vs 帝国傭兵",
                AllyAnyOf = new[] { "medic" },
                EnemyAnyOf = new[] { "imperial_samurai" },
            },
        };

        [MenuItem("Echolos/Tools/Balance Report (6v6 BackTank)")]
        public static void Run()
        {
            var allyStats = new Dictionary<string, UnitStats>();
            var enemyStats = new Dictionary<string, UnitStats>();
            var matchupStats = new Dictionary<string, UnitStats>();
            foreach (var q in Matchups) matchupStats[q.Label] = new UnitStats(q.Label);
            int totalBattles = 0;
            int totalAllyWins = 0;

            foreach (var allyFrontFac in AllyFrontSwapFactories)
            {
                foreach (var allyBack1Fac in AllyBackSwapFactories)
                {
                    foreach (var allyBack2Fac in AllyBackSwapFactories)
                    {
                        foreach (var enemyFrontFac in EnemyFrontSwapFactories)
                        {
                            foreach (var enemyBack1Fac in EnemyBackSwapFactories)
                            {
                                foreach (var enemyBack2Fac in EnemyBackSwapFactories)
                                {
                                    var allies = BuildAllies(allyFrontFac, allyBack1Fac, allyBack2Fac);
                                    var enemies = BuildEnemies(enemyFrontFac, enemyBack1Fac, enemyBack2Fac);

                                    var report = BattleRunner.Run(allies, enemies,
                                        maxTurns: BalanceReportTool.MaxTurns, random0to99: () => 50);
                                    bool allyWon = report.Result == BattleResult.PerfectVictory
                                                || report.Result == BattleResult.AdvantageousVictory;

                                    totalBattles++;
                                    if (allyWon) totalAllyWins++;

                                    AccumulateUnique(allyStats, new[]
                                    {
                                        allies[2].BaseUnit, allies[4].BaseUnit, allies[5].BaseUnit,
                                    }, win: allyWon);
                                    AccumulateUnique(enemyStats, new[]
                                    {
                                        enemies[2].BaseUnit, enemies[4].BaseUnit, enemies[5].BaseUnit,
                                    }, win: !allyWon);
                                    AccumulateMatchups(matchupStats, allies, enemies, win: allyWon);
                                }
                            }
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== 6v6 BackTank Balance Report ===");
            sb.AppendLine($"総戦闘数 {totalBattles} / 味方勝利 {totalAllyWins}（{Pct(totalAllyWins, totalBattles)}）");
            sb.AppendLine();
            AppendTable(sb, "味方入れ替え枠ユニット勝率", allyStats);
            sb.AppendLine();
            AppendTable(sb, "敵入れ替え枠ユニット勝率（敵側勝利＝味方敗北）", enemyStats);
            sb.AppendLine();
            AppendMatchupTable(sb, "特定マッチアップ勝率（味方視点）", matchupStats);

            Debug.Log(sb.ToString());
        }

        private static void AccumulateMatchups(Dictionary<string, UnitStats> stats,
            List<RuntimeUnit> allies, List<RuntimeUnit> enemies, bool win)
        {
            var allyIds = new HashSet<string>();
            foreach (var u in allies) allyIds.Add(u.BaseUnit.Id);
            var enemyIds = new HashSet<string>();
            foreach (var u in enemies) enemyIds.Add(u.BaseUnit.Id);

            foreach (var q in Matchups)
            {
                bool allyHit = false;
                foreach (var id in q.AllyAnyOf) if (allyIds.Contains(id)) { allyHit = true; break; }
                if (!allyHit) continue;
                bool enemyHit = false;
                foreach (var id in q.EnemyAnyOf) if (enemyIds.Contains(id)) { enemyHit = true; break; }
                if (!enemyHit) continue;
                stats[q.Label].Add(win);
            }
        }

        private static void AppendMatchupTable(StringBuilder sb, string title, Dictionary<string, UnitStats> stats)
        {
            sb.AppendLine($"--- {title} ---");
            sb.AppendLine("Matchup\tBattles\tWins\tRate");
            foreach (var q in Matchups)
            {
                var s = stats[q.Label];
                sb.AppendLine($"{s.Name}\t{s.Total}\t{s.Wins}\t{Pct(s.Wins, s.Total)}");
            }
        }

        private static List<RuntimeUnit> BuildAllies(Func<Unit> frontSwap, Func<Unit> back1Swap, Func<Unit> back2Swap)
        {
            return new List<RuntimeUnit>
            {
                new RuntimeUnit(AlliesRoster.GeneralTank(),  0),
                new RuntimeUnit(AlliesRoster.Attacker1(),    1),
                new RuntimeUnit(frontSwap(),                 2),
                new RuntimeUnit(AlliesRoster.GeneralTank(),  3),
                new RuntimeUnit(back1Swap(),                 4),
                new RuntimeUnit(back2Swap(),                 5),
            };
        }

        private static List<RuntimeUnit> BuildEnemies(Func<Unit> frontSwap, Func<Unit> back1Swap, Func<Unit> back2Swap)
        {
            return new List<RuntimeUnit>
            {
                new RuntimeUnit(EnemiesRoster.ImperialTankDef(),  0),
                new RuntimeUnit(EnemiesRoster.ImperialAtkMulti(), 1),
                new RuntimeUnit(frontSwap(),                      2),
                new RuntimeUnit(EnemiesRoster.ImperialTankDef(),  3),
                new RuntimeUnit(back1Swap(),                      4),
                new RuntimeUnit(back2Swap(),                      5),
            };
        }

        private static void AccumulateUnique(Dictionary<string, UnitStats> stats, IEnumerable<Unit> units, bool win)
        {
            var seen = new HashSet<string>();
            foreach (var u in units)
            {
                if (!seen.Add(u.Id)) continue;
                if (!stats.TryGetValue(u.Id, out var s))
                {
                    s = new UnitStats(u.Name);
                    stats[u.Id] = s;
                }
                s.Add(win);
            }
        }

        private static void AppendTable(StringBuilder sb, string title, Dictionary<string, UnitStats> stats)
        {
            sb.AppendLine($"--- {title} ---");
            sb.AppendLine("Name\tParticipated\tWins\tRate");
            foreach (var kv in stats.OrderByDescending(kv => kv.Value.Rate))
            {
                var s = kv.Value;
                sb.AppendLine($"{s.Name}\t{s.Total}\t{s.Wins}\t{Pct(s.Wins, s.Total)}");
            }
        }

        private static string Pct(int numerator, int denominator) =>
            denominator == 0 ? "—" : $"{100.0 * numerator / denominator:F1}%";

        private class UnitStats
        {
            public string Name { get; }
            public int Wins { get; private set; }
            public int Total { get; private set; }
            public double Rate => Total == 0 ? 0 : (double)Wins / Total;
            public UnitStats(string name) { Name = name; }
            public void Add(bool win) { if (win) Wins++; Total++; }
        }
    }
}
