// 試遊モード進行スクリプトの集約。
//
// 試遊版は R6 救出戦のみに縮約（2026-06-20 確定）。
// R7 連鎖（B-e / A-c2 / 皇太子戦）はカット＝動画で見せる役割分担。
//
// 動画撮影用に Rec_* シナリオも同居（RoundRules 空＝通常進行・場面別撮影用）。
using System;
using System.Collections.Generic;

namespace Echolos.UseCase.Demo
{
    public static class DemoScenarioCatalog
    {
        public const string Scenario2Id = "demo_scenario_2";

        // 動画撮影用シナリオ（RoundRules 空＝通常進行・対応するセーブをロードして撮影）
        public const string RecR5BB2Id     = "demo_rec_r5_bb2_scenario";
        public const string RecR7AC1Id     = "demo_rec_r7_ac1_scenario";
        public const string RecR6RescueId  = "demo_rec_r6_rescue_scenario";
        public const string RecR7TrueId    = "demo_rec_r7_true_scenario";

        /// <summary>シナリオ 2：救出戦体験（セーブ 2 連動・R4 救出戦＋ B-d ブリジット加入演出のみ）。</summary>
        public static DemoScenarioDefinition Scenario2 => _scenario2.Value;

        /// <summary>撮影用：R5 B-b2 撮影シナリオ。</summary>
        public static DemoScenarioDefinition RecR5BB2 => _recR5BB2.Value;

        /// <summary>撮影用：R7 A-c1 必敗撮影シナリオ。</summary>
        public static DemoScenarioDefinition RecR7AC1 => _recR7AC1.Value;

        /// <summary>撮影用：R6 救出戦撮影シナリオ。</summary>
        public static DemoScenarioDefinition RecR6Rescue => _recR6Rescue.Value;

        /// <summary>撮影用：R7 トゥルー撮影シナリオ。</summary>
        public static DemoScenarioDefinition RecR7True => _recR7True.Value;

        private static readonly Lazy<DemoScenarioDefinition> _scenario2
            = new Lazy<DemoScenarioDefinition>(BuildScenario2);
        private static readonly Lazy<DemoScenarioDefinition> _recR5BB2
            = new Lazy<DemoScenarioDefinition>(() => BuildRecScenario(RecR5BB2Id, "録画：R5 B-b2 から", DemoSaveCatalog.RecR5BB2Id));
        private static readonly Lazy<DemoScenarioDefinition> _recR7AC1
            = new Lazy<DemoScenarioDefinition>(() => BuildRecScenario(RecR7AC1Id, "録画：R7 A-c1 必敗から", DemoSaveCatalog.RecR7AC1Id));
        private static readonly Lazy<DemoScenarioDefinition> _recR6Rescue
            = new Lazy<DemoScenarioDefinition>(() => BuildRecScenario(RecR6RescueId, "録画：R6 救出戦から", DemoSaveCatalog.RecR6RescueId));
        private static readonly Lazy<DemoScenarioDefinition> _recR7True
            = new Lazy<DemoScenarioDefinition>(() => BuildRecScenario(RecR7TrueId, "録画：R7 トゥルー直前から", DemoSaveCatalog.RecR7TrueId));

        public static DemoScenarioDefinition Get(string id)
        {
            switch (id)
            {
                case Scenario2Id:   return Scenario2;
                case RecR5BB2Id:    return RecR5BB2;
                case RecR7AC1Id:    return RecR7AC1;
                case RecR6RescueId: return RecR6Rescue;
                case RecR7TrueId:   return RecR7True;
                default: throw new ArgumentException($"未知のデモシナリオ ID: {id}", nameof(id));
            }
        }

        private static DemoScenarioDefinition BuildScenario2()
        {
            // セーブ 2 用シナリオ：R4 で左列敵拠点（救出戦）のみ手動／他列自動勝利／内政スキップ。
            // 救出戦勝利 → 既存 B-d 演出（ラウンド末）→ ブリジット加入 → 試遊版終了でタイトル戻り
            // （R7 連鎖 B-e/A-c2/皇太子戦はカット・動画側で見せる役割分担）。
            // R5 B-b2 を経由させない＝ 330 §3.4 の B-c 発火条件にも該当せず救出体験が破綻しない。
            // 左列敵拠点＝col:0、敵拠点 Layer=3
            var manualR4 = new DemoNodeAddress(col: 0, layer: 3);
            var rules = new Dictionary<int, DemoRoundRule>
            {
                [4] = new DemoRoundRule(
                    skipInteriorPhase: true,
                    autoResolveAllBattles: true,
                    manualBattleNode: manualR4,
                    objectiveText: "目的：左下の敵拠点を制圧してバルドゥインを救援せよ"),
            };
            return new DemoScenarioDefinition(
                id: Scenario2Id,
                displayName: "シナリオ 2：救出戦体験",
                startingSaveId: DemoSaveCatalog.Save2Id,
                roundRules: rules);
        }

        // 撮影用シナリオ：RoundRules 空＝通常進行のままセーブをロード。
        // 内政スキップ・自動勝利・目的バー表示なし＝撮影者が任意のタイミングで操作・編集側で字幕付与。
        private static DemoScenarioDefinition BuildRecScenario(string id, string displayName, string startingSaveId)
        {
            return new DemoScenarioDefinition(
                id: id,
                displayName: displayName,
                startingSaveId: startingSaveId,
                roundRules: new Dictionary<int, DemoRoundRule>());
        }
    }
}
