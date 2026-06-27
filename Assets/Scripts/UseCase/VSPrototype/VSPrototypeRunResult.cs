// ラウンド／ラン進行の結果を集約する値オブジェクト。
using System.Collections.Generic;
using Echolos.Domain.Battle;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>1マスの戦闘解決結果。</summary>
    public sealed class VSPrototypeNodeResult
    {
        public int Col { get; set; }
        public int Layer { get; set; }
        public MapNodeKind Kind { get; set; }

        /// <summary>このマスの戦闘モード（StartRound でセットされた値のスナップショット）。</summary>
        public MapNodeBattleMode BattleMode { get; set; }

        /// <summary>戦闘リポート（null = 味方未配置で不戦敗 or 戦闘スキップ）。</summary>
        public BattleReport BattleReport { get; set; }

        /// <summary>プレイヤー側が勝利したか（味方未配置の不戦敗は false）。</summary>
        public bool PlayerWon { get; set; }

        /// <summary>このマスが今回の解決でプレイヤーに制圧されたか（敵領／敵拠点）。</summary>
        public bool MarkedCaptured { get; set; }

        /// <summary>このマスが今回の解決で陥落したか（自領）。</summary>
        public bool MarkedFallen { get; set; }

        /// <summary>このマスが今回の解決で取り戻されたか（占領済み敵領／敵拠点の奪還戦敗北 or 未配置）。</summary>
        public bool MarkedReverted { get; set; }

        /// <summary>この陥落自領が今回の取り戻し戦勝利で奪還されたか。</summary>
        public bool MarkedRecovered { get; set; }
    }

    /// <summary>1ラウンドの戦闘解決＋本拠地連続防衛＋エンディング判定の集約結果。</summary>
    public sealed class VSPrototypeRoundResult
    {
        public int Round { get; set; }

        /// <summary>R1〜R6：各マスの戦闘結果。R7 では空（ボス戦のみ）。</summary>
        public List<VSPrototypeNodeResult> NodeResults { get; } = new List<VSPrototypeNodeResult>();

        /// <summary>本拠地連続防衛戦のリポート（陥落自領数分・R1〜R6）／R7 はボス戦リポート1件。</summary>
        public List<BattleReport> HomeBattleReports { get; } = new List<BattleReport>();

        /// <summary>本拠地連続防衛で1戦でも敗北したか（即ラン敗北）。</summary>
        public bool HomeCollapsed { get; set; }

        /// <summary>R7 ボス戦の勝敗（R7 のみ意味を持つ）。</summary>
        public bool BossDefeated { get; set; }

        /// <summary>このラウンドの解決でエンディングが確定したか・その種別（None=継続）。</summary>
        public VSPrototypeEndingKind EndingKind { get; set; } = VSPrototypeEndingKind.None;
    }
}
