// 試遊モードの進行スクリプト定義。
//
// シナリオ = セーブのスタート位置 + ラウンドごとのルール辞書。
// ルール = 「このラウンドは全戦闘自動勝利 / ここだけ手動 / 内政スキップ / 強制 Defeat」等を宣言的に指定。
//
// 中身（RoundRules の各値）は Phase 3（Scenario1）／ Phase 4（Scenario2/Retry）で詰める。
// 本ファイルはデータ構造の宣言のみ。
using System.Collections.Generic;

namespace Echolos.UseCase.Demo
{
    public sealed class DemoScenarioDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string StartingSaveId { get; }
        public IReadOnlyDictionary<int, DemoRoundRule> RoundRules { get; }

        public DemoScenarioDefinition(
            string id,
            string displayName,
            string startingSaveId,
            IReadOnlyDictionary<int, DemoRoundRule> roundRules)
        {
            Id = id;
            DisplayName = displayName;
            StartingSaveId = startingSaveId;
            RoundRules = roundRules;
        }
    }

    /// <summary>1 ラウンドの進行ルール。</summary>
    public sealed class DemoRoundRule
    {
        /// <summary>内政フェーズ（ドラフト／強化／自動加入）を丸ごとスキップして即配置フェーズへ。</summary>
        public bool SkipInteriorPhase { get; }

        /// <summary>このラウンド内の全戦闘を自動勝利で解決する。<see cref="ManualBattleNode"/> が指定されていればそれだけ手動。</summary>
        public bool AutoResolveAllBattles { get; }

        /// <summary><see cref="AutoResolveAllBattles"/> の例外＝手動でプレイさせるマス。null なら全マス自動。</summary>
        public DemoNodeAddress ManualBattleNode { get; }

        /// <summary>画面下部の目的バーに表示する 1 行テキスト。null/空なら非表示。</summary>
        public string ObjectiveText { get; }

        public DemoRoundRule(
            bool skipInteriorPhase = false,
            bool autoResolveAllBattles = false,
            DemoNodeAddress manualBattleNode = null,
            string objectiveText = null)
        {
            SkipInteriorPhase = skipInteriorPhase;
            AutoResolveAllBattles = autoResolveAllBattles;
            ManualBattleNode = manualBattleNode;
            ObjectiveText = objectiveText;
        }
    }

    /// <summary>マップ 1 マスの座標（手動戦闘マス指定用）。</summary>
    public sealed class DemoNodeAddress
    {
        public int Col { get; }
        public int Layer { get; }

        public DemoNodeAddress(int col, int layer)
        {
            Col = col;
            Layer = layer;
        }

        public bool Matches(int col, int layer) => Col == col && Layer == layer;
    }
}
