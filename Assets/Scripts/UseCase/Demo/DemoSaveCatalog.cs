// 試遊モード用セーブデータの集約。
//
// 試遊版は救出戦＋ B-d ブリジット加入演出のみに縮約（2026-06-20 確定）。
// バッドエンド体験／皇太子戦リトライは動画側で見せるためデモから除外。
//
// 動画撮影用に Rec_* セーブ群も同居（[Docs/020_video_script.md](../../../Docs/020_video_script.md)）。
// 試遊版（Save2）とは独立に運用される。
using System;
using System.Collections.Generic;
using Echolos.Domain.Meta;
using Echolos.Domain.Models;

namespace Echolos.UseCase.Demo
{
    public static class DemoSaveCatalog
    {
        public const string Save2Id = "demo_save_2";

        // 動画撮影用セーブ群
        public const string RecR5BB2Id     = "demo_rec_r5_bb2";
        public const string RecR7AC1Id     = "demo_rec_r7_ac1";
        public const string RecR6RescueId  = "demo_rec_r6_rescue";
        public const string RecR7TrueId    = "demo_rec_r7_true";

        /// <summary>セーブ 2：救出戦体験用（R4 開始・メタ強化最大・救出戦直前）。</summary>
        public static DemoSaveDefinition Save2 => _save2.Value;

        /// <summary>撮影用：R5 開始（救援未済・メタ強化なし）→ B-b2 演出撮影。</summary>
        public static DemoSaveDefinition RecR5BB2 => _recR5BB2.Value;

        /// <summary>撮影用：R7 開始（救援未済・HasNotedPendantPower=false）→ A-c1 必敗 → Defeat → A-b1。</summary>
        public static DemoSaveDefinition RecR7AC1 => _recR7AC1.Value;

        /// <summary>撮影用：R6 開始（メタ強化最大・左列敵領制圧済）→ R6 救出戦勝利 → B-d。</summary>
        public static DemoSaveDefinition RecR6Rescue => _recR6Rescue.Value;

        /// <summary>撮影用：R7 開始（救援済・ブリジット加入・HasNotedPendantPower=false）→ B-e → A-c2 → 皇太子戦勝利 → A-d。</summary>
        public static DemoSaveDefinition RecR7True => _recR7True.Value;

        private static readonly Lazy<DemoSaveDefinition> _save2       = new Lazy<DemoSaveDefinition>(BuildSave2);
        private static readonly Lazy<DemoSaveDefinition> _recR5BB2    = new Lazy<DemoSaveDefinition>(BuildRecR5BB2);
        private static readonly Lazy<DemoSaveDefinition> _recR7AC1    = new Lazy<DemoSaveDefinition>(BuildRecR7AC1);
        private static readonly Lazy<DemoSaveDefinition> _recR6Rescue = new Lazy<DemoSaveDefinition>(BuildRecR6Rescue);
        private static readonly Lazy<DemoSaveDefinition> _recR7True   = new Lazy<DemoSaveDefinition>(BuildRecR7True);

        public static DemoSaveDefinition Get(string id)
        {
            switch (id)
            {
                case Save2Id:       return Save2;
                case RecR5BB2Id:    return RecR5BB2;
                case RecR7AC1Id:    return RecR7AC1;
                case RecR6RescueId: return RecR6Rescue;
                case RecR7TrueId:   return RecR7True;
                default: throw new ArgumentException($"未知のデモセーブ ID: {id}", nameof(id));
            }
        }

        private static DemoSaveDefinition BuildSave2()
        {
            // セーブ 2：R4 開始時点。「あと一手で救出できる手前」状態。
            // 左列敵領は前ラウンドまでに制圧済＝ R4 で左列敵拠点に攻め込んで救出戦。
            // R4 開始時にラウンド開始演出なし（R2 B-a / R3 B-b1 / R5 B-b2 / R6 B-c のいずれも該当せず）。
            // R5 B-b2 を経由しないので [330 §3.4](Docs/330_vsprototype_storyplot.md) の
            // IsBalduinSurrendered ガードで B-c も発火しない。
            // メタ強化最大（行動力 +2／初期所持 +2）＋手駒 12 体（王女 Lv3／他 Lv2）。
            // 王女の Lv3 は initialRoster で直接指定するため appliedUpgradeChoices は使わない。
            return new DemoSaveDefinition(
                id: Save2Id,
                displayName: "セーブ 2：救出戦体験",
                startRound: 4,
                memories: 0,
                appliedUpgrades: new Dictionary<string, int>
                {
                    [MetaUpgradeIds.ActionPoints] = 2,
                    [MetaUpgradeIds.InitialUnit] = 2,
                },
                appliedUpgradeChoices: new Dictionary<string, IReadOnlyList<string>>(),
                unlockedUnits: Array.Empty<string>(),
                hasRescuedBalduin: false,
                hasNotedPendantPower: false,
                isBridgetRescued: false,
                isBalduinRescuePlayed: false,
                nodeStates: new[]
                {
                    // 左列敵領 (col=0, layer=2=LayerEnemyTerritory) を制圧済
                    new DemoNodeStateEntry(col: 0, layer: 2, isCaptured: true),
                },
                initialRoster: BuildSave2Roster());
        }

        private static IReadOnlyList<DemoRosterEntry> BuildSave2Roster()
        {
            return new[]
            {
                new DemoRosterEntry(UniqueUnitIds.Princess, 3),
                new DemoRosterEntry("fire_swordsman",  2),
                new DemoRosterEntry("fire_archer",     2),
                new DemoRosterEntry("fire_tank",       2),
                new DemoRosterEntry("fire_buffer",     2),
                new DemoRosterEntry("water_swordsman", 2),
                new DemoRosterEntry("water_archer",    2),
                new DemoRosterEntry("water_tank",      2),
                new DemoRosterEntry("water_buffer",    2),
                new DemoRosterEntry("water_healer",    2),
                new DemoRosterEntry("light_paladin",   2),
                new DemoRosterEntry("light_priest",    2),
            };
        }

        private static DemoSaveDefinition BuildRecR5BB2()
        {
            // 撮影用：R5 開始直後に B-b2 演出を撮るためのセーブ。
            // メタ強化なし・救援未済の素朴な状態。手駒は標準的な 6 体（王女＋ Normal 5 体）。
            return new DemoSaveDefinition(
                id: RecR5BB2Id,
                displayName: "録画：R5 B-b2 から",
                startRound: 5,
                memories: 0,
                appliedUpgrades: new Dictionary<string, int>(),
                appliedUpgradeChoices: new Dictionary<string, IReadOnlyList<string>>(),
                unlockedUnits: Array.Empty<string>(),
                hasRescuedBalduin: false,
                hasNotedPendantPower: false,
                isBridgetRescued: false,
                isBalduinRescuePlayed: false,
                nodeStates: Array.Empty<DemoNodeStateEntry>(),
                initialRoster: BuildRecModestRoster());
        }

        private static DemoSaveDefinition BuildRecR7AC1()
        {
            // 撮影用：R7 開始直後に A-c1 闇皇太子襲来 → 必敗 → Defeat → A-b1 を撮るためのセーブ。
            // 救援未済＋ HasNotedPendantPower=false で A-c1 経路に直行。
            return new DemoSaveDefinition(
                id: RecR7AC1Id,
                displayName: "録画：R7 A-c1 必敗から",
                startRound: 7,
                memories: 0,
                appliedUpgrades: new Dictionary<string, int>(),
                appliedUpgradeChoices: new Dictionary<string, IReadOnlyList<string>>(),
                unlockedUnits: Array.Empty<string>(),
                hasRescuedBalduin: false,
                hasNotedPendantPower: false,
                isBridgetRescued: false,
                isBalduinRescuePlayed: false,
                nodeStates: Array.Empty<DemoNodeStateEntry>(),
                initialRoster: BuildRecModestRoster());
        }

        private static DemoSaveDefinition BuildRecR6Rescue()
        {
            // 撮影用：R6 開始時点で「救出戦に攻め込める」状態。試遊版 Save2（R4 開始）の R6 版。
            // メタ強化最大＋手駒 12 体で救出戦を確実に勝てる構成。
            // R5 B-b2 を経由していない＝ IsBalduinSurrendered=false で B-c も発火しない。
            return new DemoSaveDefinition(
                id: RecR6RescueId,
                displayName: "録画：R6 救出戦から",
                startRound: 6,
                memories: 0,
                appliedUpgrades: new Dictionary<string, int>
                {
                    [MetaUpgradeIds.ActionPoints] = 2,
                    [MetaUpgradeIds.InitialUnit] = 2,
                },
                appliedUpgradeChoices: new Dictionary<string, IReadOnlyList<string>>(),
                unlockedUnits: Array.Empty<string>(),
                hasRescuedBalduin: false,
                hasNotedPendantPower: false,
                isBridgetRescued: false,
                isBalduinRescuePlayed: false,
                nodeStates: new[]
                {
                    new DemoNodeStateEntry(col: 0, layer: 2, isCaptured: true),
                },
                initialRoster: BuildSave2Roster());
        }

        private static DemoSaveDefinition BuildRecR7True()
        {
            // 撮影用：R7 開始時点で「B-e → A-c2 → 皇太子戦勝利」を撮るためのセーブ。
            // 救援済＋ブリジット加入＋ HasNotedPendantPower=false で R7 開始時に B-e 連鎖発火
            // → A-c2 BossPurify → 通常皇太子戦（撃破可能）→ A-d 抱擁。
            return new DemoSaveDefinition(
                id: RecR7TrueId,
                displayName: "録画：R7 トゥルー直前から",
                startRound: 7,
                memories: 0,
                appliedUpgrades: new Dictionary<string, int>
                {
                    [MetaUpgradeIds.ActionPoints] = 2,
                    [MetaUpgradeIds.InitialUnit] = 2,
                },
                appliedUpgradeChoices: new Dictionary<string, IReadOnlyList<string>>(),
                unlockedUnits: Array.Empty<string>(),
                hasRescuedBalduin: false,
                hasNotedPendantPower: false,
                isBridgetRescued: true,
                isBalduinRescuePlayed: true,
                nodeStates: new[]
                {
                    new DemoNodeStateEntry(col: 0, layer: 2, isCaptured: true),
                    new DemoNodeStateEntry(col: 0, layer: 3, isCaptured: true),
                },
                initialRoster: BuildRecR7TrueRoster());
        }

        private static IReadOnlyList<DemoRosterEntry> BuildRecR7TrueRoster()
        {
            return new[]
            {
                new DemoRosterEntry(UniqueUnitIds.Princess, 3),
                new DemoRosterEntry(UniqueUnitIds.Bridget,  3),
                new DemoRosterEntry("fire_swordsman",  2),
                new DemoRosterEntry("fire_archer",     2),
                new DemoRosterEntry("fire_tank",       2),
                new DemoRosterEntry("fire_buffer",     2),
                new DemoRosterEntry("water_swordsman", 2),
                new DemoRosterEntry("water_archer",    2),
                new DemoRosterEntry("water_tank",      2),
                new DemoRosterEntry("water_buffer",    2),
                new DemoRosterEntry("water_healer",    2),
                new DemoRosterEntry("light_paladin",   2),
                new DemoRosterEntry("light_priest",    2),
            };
        }

        // 撮影用の標準手駒（メタ強化なし・標準的な進行で R5/R7 まで集まる想定の 6 体）。
        // バランス重視より「絵が映える編成」を優先。
        private static IReadOnlyList<DemoRosterEntry> BuildRecModestRoster()
        {
            return new[]
            {
                new DemoRosterEntry(UniqueUnitIds.Princess, 1),
                new DemoRosterEntry("fire_swordsman",  1),
                new DemoRosterEntry("fire_archer",     1),
                new DemoRosterEntry("fire_tank",       1),
                new DemoRosterEntry("water_swordsman", 1),
                new DemoRosterEntry("water_tank",      1),
            };
        }
    }
}
