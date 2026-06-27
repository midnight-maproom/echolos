// 1 戦闘の結果とターンログ。BattleRunner が生成し、観戦ビューが再生する。
using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>1 戦闘の結果とターンログ。</summary>
    public sealed class BattleReport
    {
        public BattleResult Result { get; set; }
        public int Turns { get; set; }

        /// <summary>
        /// 戦闘可視化のための構造化イベント列。BattleRunner が時系列順に記録する。
        /// 観戦ビューはこれを再生してアニメーションを駆動する。戦闘開始時の編成（AllyLineup/EnemyLineup）と
        /// 合わせて初期状態を復元できる。
        /// </summary>
        public List<BattleEvent> Events { get; } = new List<BattleEvent>();

        /// <summary>
        /// テキストログ表示用の派生プロパティ。Events[].LogLine（非空）の順次集約。
        /// 内部で都度新規 List を生成するため Add 等のミューテーションはできない（書き込みは
        /// Recorder が AddEvent 経由で Events に追加し、ログ行も Event 内に格納される）。
        /// </summary>
        public IReadOnlyList<string> Log
        {
            get
            {
                var list = new List<string>();
                foreach (var ev in Events)
                    if (ev != null && !string.IsNullOrEmpty(ev.LogLine))
                        list.Add(ev.LogLine);
                return list;
            }
        }

        /// <summary>戦闘開始時の味方編成スナップショット（順序＝SlotIndex）。観戦ビューの初期表示に使う。</summary>
        public List<RuntimeUnit> AllyLineup { get; internal set; }
        /// <summary>戦闘開始時の敵編成スナップショット。</summary>
        public List<RuntimeUnit> EnemyLineup { get; internal set; }

        /// <summary>味方の勝利（完勝・辛勝）か。</summary>
        public bool AllyWon =>
            Result == BattleResult.PerfectVictory || Result == BattleResult.AdvantageousVictory;

        public string LogText => string.Join("\n", Log);
    }
}
