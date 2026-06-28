// R4 ショートカット用セーブの POCO 定義。
//
// ラン中の一時状態（StartRound / NodeStates / InitialRoster）だけを持つ。
// メタ進行（PlayerPrefs）は触らず、既存進行を維持したまま指定ラウンドから一時的にラン開始する。
using System.Collections.Generic;

namespace Echolos.UseCase.Demo
{
    public sealed class DemoSaveDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int StartRound { get; }
        public IReadOnlyList<DemoNodeStateEntry> NodeStates { get; }
        public IReadOnlyList<DemoRosterEntry> InitialRoster { get; }

        public DemoSaveDefinition(
            string id,
            string displayName,
            int startRound,
            IReadOnlyList<DemoNodeStateEntry> nodeStates,
            IReadOnlyList<DemoRosterEntry> initialRoster)
        {
            Id = id;
            DisplayName = displayName;
            StartRound = startRound;
            NodeStates = nodeStates;
            InitialRoster = initialRoster;
        }
    }

    /// <summary>初期手駒 1 体分。Level=1〜3。</summary>
    public sealed class DemoRosterEntry
    {
        public string UnitId { get; }
        public int Level { get; }

        public DemoRosterEntry(string unitId, int level)
        {
            UnitId = unitId;
            Level = level;
        }
    }

    /// <summary>マップ 1 マスの初期状態。Col/Layer は VSPrototypeMapState の規約に従う。</summary>
    public sealed class DemoNodeStateEntry
    {
        public int Col { get; }
        public int Layer { get; }
        public bool IsCaptured { get; }

        public DemoNodeStateEntry(int col, int layer, bool isCaptured)
        {
            Col = col;
            Layer = layer;
            IsCaptured = isCaptured;
        }
    }
}
