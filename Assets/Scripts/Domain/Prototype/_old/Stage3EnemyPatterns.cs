// Assets/Scripts/Core/Prototype/Stage3EnemyPatterns.cs
// 段階3：敵編成パターンの定義と抽選（仕様 120 §10.11）。
//
// 弱・中・強の各3案を編成ファクトリとして定義し、ラウンドに応じて3戦線にユニークに割り当てる。
// R7 は街固定でボス（男爵 or サムライをランダム）、平原・砦は敵なし（最終決戦）。
//
// 強度逓増（§10.6 直交の原則）：ラウンドR で HP/ATK × (1 + 0.15 × (R-1))。
//   R1 ×1.00, R2 ×1.15, R3 ×1.30, R4 ×1.45, R5 ×1.60, R6 ×1.75, R7 ×1.90
// パターン抽選とは別軸で適用する。
using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>1つの敵編成パターン（編成ファクトリ＋識別情報）。</summary>
    public sealed class EnemyPattern
    {
        public EnemyPattern(string id, string label, EnemyPatternTier tier,
            Func<List<RuntimeUnit>> createEnemies)
        {
            Id = id;
            Label = label;
            Tier = tier;
            CreateEnemies = createEnemies;
        }

        /// <summary>パターン識別ID（例："weak_skirm3", "med_tank_archer"）。</summary>
        public string Id { get; }

        /// <summary>UI・ログ用ラベル（例："散兵×3"）。</summary>
        public string Label { get; }

        /// <summary>強度区分（弱/中/強/ボス/None）。</summary>
        public EnemyPatternTier Tier { get; }

        /// <summary>編成生成ファクトリ。呼ぶたびに新しい RuntimeUnit リストを返す。</summary>
        public Func<List<RuntimeUnit>> CreateEnemies { get; }
    }

    /// <summary>戦線への割り当て結果（抽選後・1ラウンド分）。</summary>
    public sealed class PatternAssignment
    {
        public PatternAssignment(BattlefrontKind battlefront, EnemyPattern pattern)
        {
            Battlefront = battlefront;
            Pattern = pattern;
        }

        public BattlefrontKind Battlefront { get; }
        public EnemyPattern Pattern { get; }
    }

    /// <summary>
    /// 敵パターンの定義と抽選を担う静的クラス（純C#・MonoBehaviour非依存）。
    /// すべてのパターンは Stage3Roster.cs のユニットファクトリを呼び出して編成を作る。
    /// </summary>
    public static class Stage3EnemyPatterns
    {
        // ══════════════════════════════════════════════
        // 弱パターン (R1-R2)（§10.11）
        // ══════════════════════════════════════════════

        /// <summary>弱①：敵なし戦線。その戦線には攻めてこない。</summary>
        public static EnemyPattern WeakEmpty() => new EnemyPattern(
            id: "weak_empty",
            label: "敵なし",
            tier: EnemyPatternTier.Weak,
            createEnemies: () => new List<RuntimeUnit>());

        /// <summary>弱②：散兵×3（前列横並び）。1体なら厳しい、2体で大体守れる。</summary>
        public static EnemyPattern WeakSkirmisher3() => new EnemyPattern(
            id: "weak_skirm3",
            label: "散兵×3",
            tier: EnemyPatternTier.Weak,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.Skirmisher(), 0),
                new RuntimeUnit(Stage3Roster.Skirmisher(), 1),
                new RuntimeUnit(Stage3Roster.Skirmisher(), 2),
            });

        /// <summary>弱③：散兵+サムライ+散兵（前列横並び・同順）。アタッカー必須。</summary>
        public static EnemyPattern WeakSkirmisherSamurai() => new EnemyPattern(
            id: "weak_skirm_samurai",
            label: "散兵+サムライ+散兵",
            tier: EnemyPatternTier.Weak,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.Skirmisher(), 0),
                new RuntimeUnit(Stage3Roster.Samurai(),    1),
                new RuntimeUnit(Stage3Roster.Skirmisher(), 2),
            });

        // ══════════════════════════════════════════════
        // 中パターン (R3-R4)（§10.11）
        // ══════════════════════════════════════════════

        /// <summary>中①：散兵×2（前列）。1体で対処可・軽い救済枠。</summary>
        public static EnemyPattern MediumSkirmisher2() => new EnemyPattern(
            id: "med_skirm2",
            label: "散兵×2",
            tier: EnemyPatternTier.Medium,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.Skirmisher(), 0),
                new RuntimeUnit(Stage3Roster.Skirmisher(), 1),
            });

        /// <summary>
        /// 中②：散兵×2＋重装兵＋炎魔導士。重装兵が前列保護、炎魔導士は前列のみ攻撃。
        /// 配置：前列 散兵/散兵/重装兵、後列 (空)/(空)/炎魔導士（重装兵の真後ろ）。
        /// </summary>
        public static EnemyPattern MediumTankFireMage() => new EnemyPattern(
            id: "med_tank_fire",
            label: "散兵×2＋重装兵＋炎魔導士",
            tier: EnemyPatternTier.Medium,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.Skirmisher(),  0),
                new RuntimeUnit(Stage3Roster.Skirmisher(),  1),
                new RuntimeUnit(Stage3Roster.GeneralTank(), 2),
                new RuntimeUnit(Stage3Roster.FireMage(),    5), // 重装兵 slot 2 の真後ろ
            });

        /// <summary>
        /// 中③：散兵×2＋大盾兵＋弓兵。HPタンクが時間を稼ぐ間、弓兵が必中遠隔で後列を狙撃。
        /// 配置：前列 散兵/散兵/大盾兵、後列 (空)/(空)/弓兵（大盾兵の真後ろ）。
        /// </summary>
        public static EnemyPattern MediumHpTankArcher() => new EnemyPattern(
            id: "med_hptank_archer",
            label: "散兵×2＋大盾兵＋弓兵",
            tier: EnemyPatternTier.Medium,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.Skirmisher(), 0),
                new RuntimeUnit(Stage3Roster.Skirmisher(), 1),
                new RuntimeUnit(Stage3Roster.HpTank(),     2),
                new RuntimeUnit(Stage3Roster.Archer(),     5), // 大盾兵 slot 2 の真後ろ
            });

        // ══════════════════════════════════════════════
        // 強パターン (R5-R6)（§10.11）
        // ══════════════════════════════════════════════

        /// <summary>強①：双剣士＋弓兵（前 双剣士、後 弓兵）。救済枠だが中①より上。</summary>
        public static EnemyPattern StrongDualArcher() => new EnemyPattern(
            id: "strong_dual_archer",
            label: "双剣士＋弓兵",
            tier: EnemyPatternTier.Strong,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.Attacker1(), 0),
                new RuntimeUnit(Stage3Roster.Archer(),    3),
            });

        /// <summary>
        /// 強②：大盾兵＋双剣士＋司祭＋炎魔導士＋重装兵（後列タンク）。
        /// 後列タンクが弓兵を無効化。アサシン主軸 or 純殴り合い。
        /// 配置：前 大盾兵/双剣士/(空)、後 司祭/炎魔導士/重装兵。
        /// </summary>
        public static EnemyPattern StrongRearTank() => new EnemyPattern(
            id: "strong_rear_tank",
            label: "大盾兵＋双剣士＋司祭＋炎魔導士＋重装兵(後列タンク)",
            tier: EnemyPatternTier.Strong,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.HpTank(),      0),
                new RuntimeUnit(Stage3Roster.Attacker1(),   1),
                new RuntimeUnit(Stage3Roster.Healer1(),     3),
                new RuntimeUnit(Stage3Roster.FireMage(),    4),
                new RuntimeUnit(Stage3Roster.GeneralTank(), 5),
            });

        /// <summary>
        /// 強③：重装兵＋サムライ＋アサシン＋雷魔導士×2。敵アサシンで魔導士後衛即死。
        /// サンダー連打が脅威・3Tまでに雷魔導士1体処理が鍵。
        /// 配置：前 重装兵/サムライ/アサシン、後 雷魔導士/雷魔導士/(空)。
        /// </summary>
        public static EnemyPattern StrongAssassinAoeMage() => new EnemyPattern(
            id: "strong_assassin_aoe",
            label: "重装兵＋サムライ＋アサシン＋雷魔導士×2",
            tier: EnemyPatternTier.Strong,
            createEnemies: () => new List<RuntimeUnit>
            {
                new RuntimeUnit(Stage3Roster.GeneralTank(), 0),
                new RuntimeUnit(Stage3Roster.Samurai(),     1),
                new RuntimeUnit(Stage3Roster.Assassin(),    2),
                new RuntimeUnit(Stage3Roster.AoeMage(),     3),
                new RuntimeUnit(Stage3Roster.AoeMage(),     4),
            });

        // ══════════════════════════════════════════════
        // ボスパターン (R7)
        // ══════════════════════════════════════════════

        /// <summary>R7ボス：どくどく男爵編成（Stage3Roster.PoisonBaronParty 流用）。</summary>
        public static EnemyPattern BossPoisonBaron() => new EnemyPattern(
            id: "boss_baron",
            label: "どくどく男爵編成",
            tier: EnemyPatternTier.Boss,
            createEnemies: () => Stage3Roster.PoisonBaronParty());

        /// <summary>R7ボス：隻眼のサムライ編成（Stage3Roster.SamuraiParty 流用）。</summary>
        public static EnemyPattern BossOneEyedSamurai() => new EnemyPattern(
            id: "boss_samurai",
            label: "隻眼のサムライ編成",
            tier: EnemyPatternTier.Boss,
            createEnemies: () => Stage3Roster.SamuraiParty());

        // ══════════════════════════════════════════════
        // パターンプール（難度別の3案セット）
        // ══════════════════════════════════════════════

        /// <summary>難度に応じた3案のパターンセットを返す（R1-R6 用）。</summary>
        public static IReadOnlyList<EnemyPattern> GetPoolFor(EnemyPatternTier tier)
        {
            switch (tier)
            {
                case EnemyPatternTier.Weak:
                    return new[] { WeakEmpty(), WeakSkirmisher3(), WeakSkirmisherSamurai() };
                case EnemyPatternTier.Medium:
                    return new[] { MediumSkirmisher2(), MediumTankFireMage(), MediumHpTankArcher() };
                case EnemyPatternTier.Strong:
                    return new[] { StrongDualArcher(), StrongRearTank(), StrongAssassinAoeMage() };
                default:
                    return Array.Empty<EnemyPattern>();
            }
        }

        // ══════════════════════════════════════════════
        // ラウンド→難度区分マッピング（§10.6 教習ラダー）
        // ══════════════════════════════════════════════

        /// <summary>ラウンド番号から難度区分を返す。R1-2 弱／R3-4 中／R5-6 強／R7 ボス。</summary>
        public static EnemyPatternTier GetTierForRound(int round)
        {
            if (round <= 0) return EnemyPatternTier.None;
            if (round <= 2) return EnemyPatternTier.Weak;
            if (round <= 4) return EnemyPatternTier.Medium;
            if (round <= 6) return EnemyPatternTier.Strong;
            return EnemyPatternTier.Boss;
        }

        // ══════════════════════════════════════════════
        // ラウンド開始時の戦線割り当て抽選
        // ══════════════════════════════════════════════

        /// <summary>
        /// 指定ラウンドの 3戦線（平原/街/砦）への敵パターン割り当てを抽選する。
        /// R1-R6：難度プールの3案を3戦線にユニークに（重複なし）シャッフル配分。
        /// R7：街にボス（男爵 or サムライをランダム）、平原と砦は EnemyPatternTier.None（敵なし）。
        ///
        /// rng は 0〜99 の整数を返す乱数源（テスト時は固定可能）。
        /// </summary>
        public static List<PatternAssignment> AssignPatternsForRound(int round, Func<int> rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var tier = GetTierForRound(round);
            var result = new List<PatternAssignment>();

            if (tier == EnemyPatternTier.Boss)
            {
                // R7：街固定でボス。平原・砦は敵なし。
                int pick = rng() % 2;
                var boss = pick == 0 ? BossPoisonBaron() : BossOneEyedSamurai();
                result.Add(new PatternAssignment(BattlefrontKind.Plain,    EmptyForNoEnemy()));
                result.Add(new PatternAssignment(BattlefrontKind.City,     boss));
                result.Add(new PatternAssignment(BattlefrontKind.Fortress, EmptyForNoEnemy()));
                return result;
            }

            // R1-R6：プール3案を3戦線にユニーク配分（重複なし）。
            var pool = new List<EnemyPattern>(GetPoolFor(tier));
            var fronts = new[] { BattlefrontKind.Plain, BattlefrontKind.City, BattlefrontKind.Fortress };

            // Fisher-Yates シャッフル（poolを乱数で並び替えて3戦線に対応させる）
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng() % (i + 1);
                if (j != i) (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            for (int i = 0; i < fronts.Length; i++)
                result.Add(new PatternAssignment(fronts[i], pool[i]));

            return result;
        }

        /// <summary>「敵なし戦線」を表すプレースホルダ EnemyPattern。</summary>
        private static EnemyPattern EmptyForNoEnemy() => new EnemyPattern(
            id: "none",
            label: "（敵なし）",
            tier: EnemyPatternTier.None,
            createEnemies: () => new List<RuntimeUnit>());

        // ══════════════════════════════════════════════
        // 強度逓増（§10.6・§9.4）
        // ══════════════════════════════════════════════

        /// <summary>
        /// ラウンド番号からの強度倍率（§9.4・§10.6）。
        /// 難度ブロック（弱R1-R2／中R3-R4／強R5-R6）の1ラウンド目で 1.00、2ラウンド目で 1.05。
        /// R7 ボスは「強の集大成」として 1.05 を踏襲。
        ///
        /// 設計：当初 0.15/R の決定的逓増だったが、パターン強化（弱→中→強の編成質UP）と
        /// 二軸で乗算的に重くなり、プレイヤー強化機会との釣り合いが取れなかった。
        /// 難度ブロック境界でリセットすることで、ブロック切替の段差は編成質の変化が、
        /// ブロック内の小さな伸びは強度倍率が担う＝二重課金を解消。
        ///
        /// R1=1.00, R2=1.05, R3=1.00, R4=1.05, R5=1.00, R6=1.05, R7=1.05。
        /// </summary>
        public static float GetStrengthScale(int round)
        {
            if (round <= 0) return 1f;
            if (round >= 7) return 1.05f; // R7 ボスは強R6 と同じ
            // 2ラウンド1ブロック構成：偶数R（R2/R4/R6）が +5%
            return (round % 2 == 0) ? 1.05f : 1.00f;
        }

        /// <summary>
        /// 敵編成に強度倍率を適用する（HP・ATK を scale 倍にする）。
        /// 各 RuntimeUnit が保持する BaseUnit（毎回 Stage3Roster で新規生成されるテンプレ）を直接書き換える。
        /// ロスター（プレイヤー側）のテンプレ汚染を避けるため、敵編成生成直後に呼ぶこと。
        /// 丸めは Math.Round（最近接整数）。1.30f の float 表現誤差で「50×1.30 → 64」になるのを防ぐ。
        /// </summary>
        public static void ApplyStrengthScale(List<RuntimeUnit> enemies, float scale)
        {
            if (enemies == null || scale == 1f) return;
            foreach (var ru in enemies)
            {
                var u = ru.BaseUnit;
                u.MaxHP = (int)Math.Round(u.MaxHP * scale);
                u.CurrentHP = u.MaxHP;
                u.BaseATK = (int)Math.Round(u.BaseATK * scale);
            }
        }
    }
}
