// 試遊モード用セーブデータの POCO 定義。
//
// 通常版の MetaProgressState（ラン外永続）＋ VSPrototypeMapState（マップ状態）＋
// Bootstrap が持つラン進行状態（CurrentRound / Roster）を、一括スナップショットとして保持する。
// SO アセット化は行わず、DemoSaveCatalog でコードハードコードで提供する（プロト範囲では SO 化のメリットなし）。
//
// 復元経路：DemoSaveLoader（Phase 3 で実装）が DemoSaveDefinition を読み、Bootstrap の状態を流し込む。
using System.Collections.Generic;

namespace Echolos.UseCase.Demo
{
    public sealed class DemoSaveDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }

        // ラン進行
        public int StartRound { get; }

        // メタ進行（MetaProgressState 相当）
        public int Memories { get; }
        public IReadOnlyDictionary<string, int> AppliedUpgrades { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> AppliedUpgradeChoices { get; }
        public IReadOnlyList<string> UnlockedUnits { get; }
        public bool HasRescuedBalduin { get; }
        public bool HasNotedPendantPower { get; }

        // マップ状態（VSPrototypeMapState 相当）
        public bool IsBridgetRescued { get; }
        public bool IsBalduinRescuePlayed { get; }
        public IReadOnlyList<DemoNodeStateEntry> NodeStates { get; }

        // 初期手駒（Bootstrap._roster 相当）
        public IReadOnlyList<DemoRosterEntry> InitialRoster { get; }

        public DemoSaveDefinition(
            string id,
            string displayName,
            int startRound,
            int memories,
            IReadOnlyDictionary<string, int> appliedUpgrades,
            IReadOnlyDictionary<string, IReadOnlyList<string>> appliedUpgradeChoices,
            IReadOnlyList<string> unlockedUnits,
            bool hasRescuedBalduin,
            bool hasNotedPendantPower,
            bool isBridgetRescued,
            bool isBalduinRescuePlayed,
            IReadOnlyList<DemoNodeStateEntry> nodeStates,
            IReadOnlyList<DemoRosterEntry> initialRoster)
        {
            Id = id;
            DisplayName = displayName;
            StartRound = startRound;
            Memories = memories;
            AppliedUpgrades = appliedUpgrades;
            AppliedUpgradeChoices = appliedUpgradeChoices;
            UnlockedUnits = unlockedUnits;
            HasRescuedBalduin = hasRescuedBalduin;
            HasNotedPendantPower = hasNotedPendantPower;
            IsBridgetRescued = isBridgetRescued;
            IsBalduinRescuePlayed = isBalduinRescuePlayed;
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
        public bool IsFallen { get; }

        public DemoNodeStateEntry(int col, int layer, bool isCaptured = false, bool isFallen = false)
        {
            Col = col;
            Layer = layer;
            IsCaptured = isCaptured;
            IsFallen = isFallen;
        }
    }
}
