// 弱プール（R1-2 帯）バランス評価ツール：
// 敵編成「偵察兵 2 + ランダム差分 1」を 7 通り展開し、味方 2 体ペア × 配置 2 通りで総当たり。
//
// 想定バランス：味方 2 体ぐらいで勝てる弱プールを基準にする。「2 体で勝てない組み合わせ」が
// 多すぎる／少なすぎる場合、ランダム差分候補から弱体化／強化を追加除外する。
//
// 敵差分候補：
//   ランダム前衛 = 帝国重装兵 / 帝国騎士 / 帝国双剣士 / 帝国傭兵（暗殺者は特殊性能のため除外）
//   ランダム後衛 = 帝国炎魔導士 / 帝国弓兵 / 帝国司祭（大魔導士・影は特殊性能のため除外）
//
// 味方ペア：ブリジット除外（救出前は未加入）。同種ペア含む。王女×2 不可・攻撃なし2 体ペア除外。
//
// 集計：ユニットごとに「そのユニットが編成に含まれる全戦闘のうち味方勝利した割合」を出す。
// 味方視点（味方勝率）と敵視点（敵勝率＝味方敗北率）の 2 表を Console に出力。
// Front2／Std の配置差は合算（同ユニットを別配置で出した戦闘は両方カウント）。
//
// Editor メニュー「Echolos/Tools/Balance Report (Weak Pool)」で実行。
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
    public static class WeakPoolBalanceReportTool
    {
        /// <summary>ブリジットを除外した味方ファクトリ。</summary>
        private static readonly Func<Unit>[] AllyFactories =
        {
            AlliesRoster.Princess,
            AlliesRoster.GeneralTank,
            AlliesRoster.Paladin,
            AlliesRoster.Attacker1,
            AlliesRoster.Samurai,
            AlliesRoster.Debuffer,
            AlliesRoster.Mercenary,
            AlliesRoster.Archer,
            AlliesRoster.Ninja,
            AlliesRoster.FireMage,
            AlliesRoster.AoeMage,
            AlliesRoster.Healer1,
            AlliesRoster.Healer2,
            AlliesRoster.Buffer,
            AlliesRoster.Tactician,
        };

        /// <summary>パターン 1：偵察兵 2 + 中央前衛 1 のランダム差分候補。</summary>
        private static readonly EnemyVariant[] FrontVariants =
        {
            new EnemyVariant("paladin",   EnemiesRoster.ImperialPaladin),
            new EnemyVariant("tankdef",   EnemiesRoster.ImperialTankDef),
            new EnemyVariant("atk_multi", EnemiesRoster.ImperialAtkMulti),
            new EnemyVariant("samurai",   EnemiesRoster.ImperialSamurai),
        };

        /// <summary>パターン 2：偵察兵 2 + 中央後衛 1 のランダム差分候補。</summary>
        private static readonly EnemyVariant[] BackVariants =
        {
            new EnemyVariant("firemage", EnemiesRoster.ImperialFireMage),
            new EnemyVariant("archer",   EnemiesRoster.ImperialArcher),
            new EnemyVariant("healer",   EnemiesRoster.ImperialHealer),
        };

        [MenuItem("Echolos/Tools/Balance Report (Weak Pool)")]
        public static void Run()
        {
            var allyStats = new Dictionary<string, UnitStats>();
            var enemyStats = new Dictionary<string, UnitStats>();
            int totalBattles = 0;
            int totalAllyWins = 0;

            for (int i = 0; i < AllyFactories.Length; i++)
            {
                for (int j = i; j < AllyFactories.Length; j++)
                {
                    var facA = AllyFactories[i];
                    var facB = AllyFactories[j];
                    var probeA = facA();
                    var probeB = facB();

                    if (!IsFrontPreferred(probeA) && IsFrontPreferred(probeB))
                    {
                        (facA, facB) = (facB, facA);
                        (probeA, probeB) = (probeB, probeA);
                    }

                    if (probeA.Id == AlliesRoster.PrincessId && probeB.Id == AlliesRoster.PrincessId) continue;
                    if (!HasAttack(probeA) && !HasAttack(probeB)) continue;

                    foreach (var pattern in EnumerateEnemyPatterns())
                    {
                        RunAndAccumulate(BuildFront2(facA, facB), pattern,
                            allyStats, enemyStats, ref totalBattles, ref totalAllyWins);

                        if (TryPickMeleeAndBack(probeA, probeB, facA, facB,
                            out var meleeFactory, out var backFactory))
                        {
                            RunAndAccumulate(BuildStd(meleeFactory, backFactory), pattern,
                                allyStats, enemyStats, ref totalBattles, ref totalAllyWins);
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Weak Pool Balance Report ===");
            sb.AppendLine($"総戦闘数 {totalBattles} / 味方勝利 {totalAllyWins}（{Pct(totalAllyWins, totalBattles)}）");
            sb.AppendLine();
            AppendTable(sb, "味方ユニット勝率", allyStats);
            sb.AppendLine();
            AppendTable(sb, "敵ユニット勝率（敵側勝利＝味方敗北）", enemyStats);

            Debug.Log(sb.ToString());
        }

        private static void RunAndAccumulate(List<RuntimeUnit> allies, EnemyPattern pattern,
            Dictionary<string, UnitStats> allyStats, Dictionary<string, UnitStats> enemyStats,
            ref int totalBattles, ref int totalAllyWins)
        {
            var enemies = pattern.BuildEnemies();
            var report = BattleRunner.Run(allies, enemies,
                maxTurns: BalanceReportTool.MaxTurns, random0to99: () => 50);
            bool won = report.Result == BattleResult.PerfectVictory
                    || report.Result == BattleResult.AdvantageousVictory;
            totalBattles++;
            if (won) totalAllyWins++;
            AccumulateUnique(allyStats, allies, win: won);
            AccumulateUnique(enemyStats, enemies, win: !won);
        }

        private static List<RuntimeUnit> BuildFront2(Func<Unit> a, Func<Unit> b) =>
            new List<RuntimeUnit>
            {
                new RuntimeUnit(a(), 0),
                new RuntimeUnit(b(), 1),
            };

        private static List<RuntimeUnit> BuildStd(Func<Unit> melee, Func<Unit> back) =>
            new List<RuntimeUnit>
            {
                new RuntimeUnit(melee(), 0),
                new RuntimeUnit(back(),  4),
            };

        private static void AccumulateUnique(Dictionary<string, UnitStats> stats,
            IEnumerable<RuntimeUnit> party, bool win)
        {
            var seen = new HashSet<string>();
            foreach (var ru in party)
            {
                var id = ru.BaseUnit.Id;
                if (!seen.Add(id)) continue;
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

        /// <summary>BaseATK が 1 以上 = ダメージを出せるユニット。
        /// atk=0 のタンク／ヒーラー／バッファー／軍師は false。</summary>
        private static bool HasAttack(Unit u) => u.BaseATK > 0;

        /// <summary>前衛向け：Melee／RowCover／Infiltrator のみ。
        /// Mid 射程のユニット（炎魔導士など）は本ツールでは後衛枠として扱う
        /// （ユーザー指定の「Mid/Ranged 7 体」分類に合わせる）。</summary>
        private static bool IsFrontPreferred(Unit u) =>
            u.Tags.Contains(BattleManager.RowCoverTag)
            || u.Tags.Contains(BattleManager.InfiltratorTag)
            || u.Range == AttackRange.Melee;

        /// <summary>Std 配置（前列 1 + 後列 1）にできるペアか判定し、Melee 側と Back 側に振り分ける。</summary>
        private static bool TryPickMeleeAndBack(Unit probeA, Unit probeB,
            Func<Unit> facA, Func<Unit> facB,
            out Func<Unit> meleeFactory, out Func<Unit> backFactory)
        {
            bool aFront = IsFrontPreferred(probeA);
            bool bFront = IsFrontPreferred(probeB);
            if (aFront && !bFront)
            {
                meleeFactory = facA; backFactory = facB; return true;
            }
            if (!aFront && bFront)
            {
                meleeFactory = facB; backFactory = facA; return true;
            }
            meleeFactory = null; backFactory = null; return false;
        }

        private static IEnumerable<EnemyPattern> EnumerateEnemyPatterns()
        {
            foreach (var v in FrontVariants)
                yield return new EnemyPattern("P1_" + v.Id, $"偵察兵×2+{v.Build().Name}",
                    BuildFrontPattern(v));
            foreach (var v in BackVariants)
                yield return new EnemyPattern("P2_" + v.Id, $"偵察兵×2+{v.Build().Name}",
                    BuildBackPattern(v));
        }

        private static Func<List<RuntimeUnit>> BuildFrontPattern(EnemyVariant v) => () =>
            new List<RuntimeUnit>
            {
                new RuntimeUnit(EnemiesRoster.Skirmisher(), 0),
                new RuntimeUnit(v.Build(),                  1),
                new RuntimeUnit(EnemiesRoster.Skirmisher(), 2),
            };

        private static Func<List<RuntimeUnit>> BuildBackPattern(EnemyVariant v) => () =>
            new List<RuntimeUnit>
            {
                new RuntimeUnit(EnemiesRoster.Skirmisher(), 0),
                new RuntimeUnit(EnemiesRoster.Skirmisher(), 1),
                new RuntimeUnit(v.Build(),                  4),
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
