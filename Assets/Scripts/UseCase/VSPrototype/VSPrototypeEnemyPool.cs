// 敵編成プールの POCO 定義（VSプロト）。
//
// 役割：
// - 「弱／中／強」3 プールを「Required（確定枠）＋ Random（抽選枠）」二段構造で定義する。
// - マス種別ごとの編成（VSPrototypeEnemyPatterns）はここから抽選して RuntimeUnit を構築する。
// - ラスボス固定編成（VSPrototypeBossLineups）も本ファイルで一括管理する。
//
// 【設計方針】
// - Required は「候補グループから 1 体ずつ抽選する確定枠」（例：タンク候補から 1 体）。
//   未設定（空グループ）の場合は Required 抽選を行わない。
// - Random は「目標体数 − Required 数」分を Fisher–Yates で抽選する抽選枠。
// - Lv は敵味方共通の Unit.Level（1〜3）軸。Lv 上昇時は AvailableUpgrades 先頭から順に適用する。
// - 抽選後の並びは VSPrototypeEnemyPatterns 側で Unit.SortOrder により stable sort される
//   （ラスボス固定編成のみ例外：SortOrder ソートを bypass して List 順そのまま）。
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.UseCase.VSPrototype
{
    /// <summary>プールに含まれる 1 エントリ（Unit ID + Lv 指定）。</summary>
    public sealed class VSPrototypeEnemyPoolEntry
    {
        public string UnitId { get; }
        public int Level { get; }

        public VSPrototypeEnemyPoolEntry(string unitId, int level)
        {
            UnitId = unitId;
            Level = level;
        }
    }

    /// <summary>
    /// 確定枠の 1 候補グループ。
    /// 候補リストから 1 体を抽選し、対応する Lv を適用する。
    /// </summary>
    public sealed class VSPrototypeEnemyRequirementGroup
    {
        public string Name { get; }
        public IReadOnlyList<VSPrototypeEnemyPoolEntry> Candidates { get; }

        public VSPrototypeEnemyRequirementGroup(string name, IReadOnlyList<VSPrototypeEnemyPoolEntry> candidates)
        {
            Name = name;
            Candidates = candidates ?? new List<VSPrototypeEnemyPoolEntry>();
        }
    }

    /// <summary>敵プール（弱／中／強のいずれか）。</summary>
    public sealed class VSPrototypeEnemyPool
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>確定枠（各グループから 1 体ずつ抽選）。空なら確定枠なし。</summary>
        public IReadOnlyList<VSPrototypeEnemyRequirementGroup> Required { get; }

        /// <summary>抽選枠（目標体数 − Required 数を Fisher–Yates で重複なく抽選）。</summary>
        public IReadOnlyList<VSPrototypeEnemyPoolEntry> Random { get; }

        public VSPrototypeEnemyPool(
            string id,
            string name,
            IReadOnlyList<VSPrototypeEnemyRequirementGroup> required,
            IReadOnlyList<VSPrototypeEnemyPoolEntry> random)
        {
            Id = id;
            Name = name;
            Required = required ?? new List<VSPrototypeEnemyRequirementGroup>();
            Random = random ?? new List<VSPrototypeEnemyPoolEntry>();
        }
    }

    /// <summary>VSプロトの 3 段階敵プール（弱／中／強）のファクトリ集。</summary>
    public static class VSPrototypeEnemyPools
    {
        // ─── ID 定数 ────────────────────────────────────
        private const string FireTank      = "imperial_fire_tank";
        private const string WaterTank     = "imperial_water_tank";
        private const string FireBuffer    = "imperial_fire_buffer";
        private const string WaterBuffer   = "imperial_water_buffer";
        private const string FireSwordsman = "imperial_fire_swordsman";
        private const string WaterSwordsman= "imperial_water_swordsman";
        private const string FireArcher    = "imperial_fire_archer";
        private const string WaterArcher   = "imperial_water_archer";
        private const string LightPriest   = "imperial_light_priest";
        private const string LightPaladin  = "imperial_light_paladin";

        // ═══════════════════════════════════════════════
        // 弱プール（R1-R2 / 自領）
        //   Required: なし
        //   Random[5] Lv1 から 2 抽選 = 5C2 = 10 通り
        // ═══════════════════════════════════════════════
        public static VSPrototypeEnemyPool Weak() => new VSPrototypeEnemyPool(
            id: "weak",
            name: "弱プール",
            required: new List<VSPrototypeEnemyRequirementGroup>(),
            random: new List<VSPrototypeEnemyPoolEntry>
            {
                new VSPrototypeEnemyPoolEntry(FireSwordsman,  1),
                new VSPrototypeEnemyPoolEntry(WaterSwordsman, 1),
                new VSPrototypeEnemyPoolEntry(FireArcher,     1),
                new VSPrototypeEnemyPoolEntry(WaterArcher,    1),
                new VSPrototypeEnemyPoolEntry(LightPriest,    1),
            });

        // ═══════════════════════════════════════════════
        // 中プール（R3-R4 / 敵領）
        //   Required[1] = タンク候補（火・水）から 1 体 Lv1
        //   Random[6] Lv1 から 3 抽選 = 6C3 × 2 = 40 通り
        // ═══════════════════════════════════════════════
        public static VSPrototypeEnemyPool Mid() => new VSPrototypeEnemyPool(
            id: "mid",
            name: "中プール",
            required: new List<VSPrototypeEnemyRequirementGroup>
            {
                new VSPrototypeEnemyRequirementGroup("タンク",
                    new List<VSPrototypeEnemyPoolEntry>
                    {
                        new VSPrototypeEnemyPoolEntry(FireTank,  1),
                        new VSPrototypeEnemyPoolEntry(WaterTank, 1),
                    }),
            },
            random: new List<VSPrototypeEnemyPoolEntry>
            {
                new VSPrototypeEnemyPoolEntry(FireSwordsman,  1),
                new VSPrototypeEnemyPoolEntry(WaterSwordsman, 1),
                new VSPrototypeEnemyPoolEntry(FireArcher,     1),
                new VSPrototypeEnemyPoolEntry(WaterArcher,    1),
                new VSPrototypeEnemyPoolEntry(LightPriest,    1),
                new VSPrototypeEnemyPoolEntry(LightPaladin,   1),
            });

        // ═══════════════════════════════════════════════
        // 強プール（R6 自領 ＆ 敵拠点 ＆ 本拠地戦）
        //   Required[2] = タンク候補（火・水）から 1 体 Lv1
        //               ＋ バッファ候補（火・水）から 1 体 Lv1
        //   Random[6] Lv1 から 3 抽選 = 6C3 × 2 × 2 = 80 通り
        //   ※ 試遊バランス調整中は全エントリ Lv1 で運用（Lv 上昇は後で再検討）
        // ═══════════════════════════════════════════════
        public static VSPrototypeEnemyPool Strong() => new VSPrototypeEnemyPool(
            id: "strong",
            name: "強プール",
            required: new List<VSPrototypeEnemyRequirementGroup>
            {
                new VSPrototypeEnemyRequirementGroup("タンク",
                    new List<VSPrototypeEnemyPoolEntry>
                    {
                        new VSPrototypeEnemyPoolEntry(FireTank,  1),
                        new VSPrototypeEnemyPoolEntry(WaterTank, 1),
                    }),
                new VSPrototypeEnemyRequirementGroup("バッファ",
                    new List<VSPrototypeEnemyPoolEntry>
                    {
                        new VSPrototypeEnemyPoolEntry(FireBuffer,  1),
                        new VSPrototypeEnemyPoolEntry(WaterBuffer, 1),
                    }),
            },
            random: new List<VSPrototypeEnemyPoolEntry>
            {
                new VSPrototypeEnemyPoolEntry(FireSwordsman,  1),
                new VSPrototypeEnemyPoolEntry(WaterSwordsman, 1),
                new VSPrototypeEnemyPoolEntry(FireArcher,     1),
                new VSPrototypeEnemyPoolEntry(WaterArcher,    1),
                new VSPrototypeEnemyPoolEntry(LightPriest,    1),
                new VSPrototypeEnemyPoolEntry(LightPaladin,   1),
            });
    }

    /// <summary>ラスボス固定編成（R7 本拠地戦）の定義集。</summary>
    /// <remarks>
    /// プール抽選とは別軸。順番固定（SortOrder ソート bypass）で List 順そのまま
    /// SlotIndex に割り付ける。VSPrototypeEnemyPatterns.CreateBossPattern から参照。
    /// </remarks>
    public static class VSPrototypeBossLineups
    {
        // 編成順は戦術意図：水の大盾兵（最前で吸う）→ 皇太子（攻撃役）→ 炎の鼓舞師（皇太子に ATK バフ撒き）
        // → 光の司祭（後衛から回復）→ 焦熱の大魔導士（後衛範囲攻撃）→ 光の騎士（最後尾で
        // プレイヤー弓兵から後衛を守るサブタンク）。
        public static List<VSPrototypeEnemyPoolEntry> R7FinalBoss() => new List<VSPrototypeEnemyPoolEntry>
        {
            new VSPrototypeEnemyPoolEntry("imperial_water_tank",    2),
            new VSPrototypeEnemyPoolEntry(UniqueUnitIds.Prince,     1),
            new VSPrototypeEnemyPoolEntry("imperial_fire_buffer",   2),
            new VSPrototypeEnemyPoolEntry("imperial_light_priest",  2),
            new VSPrototypeEnemyPoolEntry("imperial_fire_mage",     2),
            new VSPrototypeEnemyPoolEntry("imperial_light_paladin", 2),
        };

        /// <summary>A-c1 必敗形態（HasNotedPendantPower=false）の R7 編成：通常版と同じ取り巻きに皇太子だけ闇皇太子へ置換。
        /// 編成見栄えのため取り巻きを揃えるが、闇皇太子の AttackUp +15 永続スタックが累積し
        /// T4 で全滅級ダメージ＝取り巻きを倒しても辛勝不能（構造的に詰む）。</summary>
        public static List<VSPrototypeEnemyPoolEntry> R7FinalBossDark() => new List<VSPrototypeEnemyPoolEntry>
        {
            new VSPrototypeEnemyPoolEntry("imperial_water_tank",    2),
            new VSPrototypeEnemyPoolEntry(UniqueUnitIds.PrinceDark, 1),
            new VSPrototypeEnemyPoolEntry("imperial_fire_buffer",   2),
            new VSPrototypeEnemyPoolEntry("imperial_light_priest",  2),
            new VSPrototypeEnemyPoolEntry("imperial_fire_mage",     2),
            new VSPrototypeEnemyPoolEntry("imperial_light_paladin", 2),
        };
    }
}
