// 中プール（R3-4 帯）バランス評価ツール：
// 「タンク × 前衛 × 後衛」3 体フル分業編成を敵味方とも揃え、
// 個別ユニット性能を構造非対称の影響なしで比較する。
//
// 敵プール定義：
//   タンク P：帝国重装兵・帝国騎士（2 種）
//   前衛 P：帝国双剣士・帝国傭兵・帝国暗殺者（3 種）
//   後衛 P：帝国炎魔導士・帝国弓兵・帝国司祭（3 種）
//
// 敵編成：slot 0=タンクP / slot 1=前衛P / slot 3=後衛P → 2×3×3 = 18 種
//
// 味方編成（敵と完全フル分業）：
//   タンク（slot 0）：重装兵・騎士（2 種）
//   非タンク前衛（slot 1）：王女・双剣士・サムライ・大槌兵・傭兵・忍者（6 種）
//   後衛（slot 3）：弓兵・炎魔導士・雷魔導士・司祭・巫女・踊り子・軍師（7 種）
//   ブリジット除外（救出前提）。組合せ：2×6×7 = 84 編成。
//
// 総戦闘数：84 × 18 = 1512。
//
// 集計：ユニットごとに「そのユニットが編成に含まれる全戦闘のうち味方勝利した割合」を出す。
// 味方視点（味方勝率）と敵視点（敵勝率＝味方敗北率）の 2 表を Console に出力。
//
// Editor メニュー「Echolos/Tools/Balance Report (Mid Pool)」で実行。
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
    public static class MidPoolBalanceReportTool
    {
        /// <summary>味方タンク枠（slot 0・RowCoverTag 持ち 2 体）。</summary>
        private static readonly Func<Unit>[] AllyTankFactories =
        {
            AlliesRoster.GeneralTank,
            AlliesRoster.Paladin,
        };

        /// <summary>味方非タンク前衛枠（slot 1・王女含む 6 体）。</summary>
        private static readonly Func<Unit>[] AllyNonTankFrontFactories =
        {
            AlliesRoster.Princess,
            AlliesRoster.Attacker1,
            AlliesRoster.Samurai,
            AlliesRoster.Debuffer,
            AlliesRoster.Mercenary,
            AlliesRoster.Ninja,
        };

        /// <summary>味方後衛枠（slot 3・Mid/Ranged 7 体）。</summary>
        private static readonly Func<Unit>[] AllyBackFactories =
        {
            AlliesRoster.Archer,
            AlliesRoster.FireMage,
            AlliesRoster.AoeMage,
            AlliesRoster.Healer1,
            AlliesRoster.Healer2,
            AlliesRoster.Buffer,
            AlliesRoster.Tactician,
        };

        private static readonly EnemyVariant[] TankPool =
        {
            new EnemyVariant("tankdef", EnemiesRoster.ImperialTankDef),
            new EnemyVariant("paladin", EnemiesRoster.ImperialPaladin),
        };

        private static readonly EnemyVariant[] FrontPool =
        {
            new EnemyVariant("atk_multi", EnemiesRoster.ImperialAtkMulti),
            new EnemyVariant("samurai",   EnemiesRoster.ImperialSamurai),
            new EnemyVariant("assassin",  EnemiesRoster.ImperialAssassin),
        };

        private static readonly EnemyVariant[] BackPool =
        {
            new EnemyVariant("firemage", EnemiesRoster.ImperialFireMage),
            new EnemyVariant("archer",   EnemiesRoster.ImperialArcher),
            new EnemyVariant("healer",   EnemiesRoster.ImperialHealer),
        };

        [MenuItem("Echolos/Tools/Balance Report (Mid Pool)")]
        public static void Run()
        {
            var allyStats = new Dictionary<string, UnitStats>();
            var enemyStats = new Dictionary<string, UnitStats>();
            int totalBattles = 0;
            int totalAllyWins = 0;

            var patterns = EnumerateEnemyPatterns().ToList();

            foreach (var tankFac in AllyTankFactories)
            {
                foreach (var frontFac in AllyNonTankFrontFactories)
                {
                    foreach (var backFac in AllyBackFactories)
                    {
                        foreach (var pattern in patterns)
                        {
                            var (allyWon, allies, enemies) = RunBattle(tankFac, frontFac, backFac, pattern);

                            totalBattles++;
                            if (allyWon) totalAllyWins++;

                            Accumulate(allyStats, allies, win: allyWon);
                            Accumulate(enemyStats, enemies, win: !allyWon);
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Mid Pool Balance Report ===");
            sb.AppendLine($"総戦闘数 {totalBattles} / 味方勝利 {totalAllyWins}（{Pct(totalAllyWins, totalBattles)}）");
            sb.AppendLine();
            AppendTable(sb, "味方ユニット勝率", allyStats);
            sb.AppendLine();
            AppendTable(sb, "敵ユニット勝率（敵側勝利＝味方敗北）", enemyStats);

            Debug.Log(sb.ToString());
        }

        private static (bool allyWon, List<RuntimeUnit> allies, List<RuntimeUnit> enemies)
            RunBattle(Func<Unit> tankFac, Func<Unit> frontFac, Func<Unit> backFac, EnemyPattern pattern)
        {
            var allies = new List<RuntimeUnit>
            {
                new RuntimeUnit(tankFac(),  0),
                new RuntimeUnit(frontFac(), 1),
                new RuntimeUnit(backFac(),  3),
            };
            var enemies = pattern.BuildEnemies();
            var report = BattleRunner.Run(allies, enemies,
                maxTurns: BalanceReportTool.MaxTurns, random0to99: () => 50);
            bool won = report.Result == BattleResult.PerfectVictory
                    || report.Result == BattleResult.AdvantageousVictory;
            return (won, allies, enemies);
        }

        private static void Accumulate(Dictionary<string, UnitStats> stats, List<RuntimeUnit> party, bool win)
        {
            foreach (var ru in party)
            {
                var id = ru.BaseUnit.Id;
                if (!stats.TryGetValue(id, out var s))
                {
                    s = new UnitStats(ru.BaseUnit.Name);
                    stats[id] = s;
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

        private static IEnumerable<EnemyPattern> EnumerateEnemyPatterns()
        {
            foreach (var tank in TankPool)
                foreach (var front in FrontPool)
                    foreach (var back in BackPool)
                    {
                        var tName = tank.Build().Name;
                        var fName = front.Build().Name;
                        var bName = back.Build().Name;
                        yield return new EnemyPattern(
                            id: $"{tank.Id}_{front.Id}_{back.Id}",
                            label: $"{tName}+{fName}+{bName}",
                            build: BuildPattern(tank, front, back));
                    }
        }

        private static Func<List<RuntimeUnit>> BuildPattern(
            EnemyVariant tank, EnemyVariant front, EnemyVariant back) => () =>
            new List<RuntimeUnit>
            {
                new RuntimeUnit(tank.Build(),  0),
                new RuntimeUnit(front.Build(), 1),
                new RuntimeUnit(back.Build(),  3),
            };

        private class UnitStats
        {
            public string Name { get; }
            public int Wins { get; private set; }
            public int Total { get; private set; }
            public double Rate => Total == 0 ? 0 : (double)Wins / Total;
            public UnitStats(string name) { Name = name; }
            public void Add(bool win) { if (win) Wins++; Total++; }
        }

        private readonly struct EnemyVariant
        {
            public readonly string Id;
            private readonly Func<Unit> _build;
            public EnemyVariant(string id, Func<Unit> build) { Id = id; _build = build; }
            public Unit Build() => _build();
        }

        private readonly struct EnemyPattern
        {
            public readonly string Id;
            public readonly string Label;
            private readonly Func<List<RuntimeUnit>> _build;
            public EnemyPattern(string id, string label, Func<List<RuntimeUnit>> build)
            {
                Id = id; Label = label; _build = build;
            }
            public List<RuntimeUnit> BuildEnemies() => _build();
        }
    }
}
