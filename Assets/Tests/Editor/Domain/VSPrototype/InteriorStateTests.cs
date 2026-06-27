// VSPrototypeInteriorState の状態遷移と契約検証。
using System;
using NUnit.Framework;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class InteriorStateTests
    {
        [Test]
        public void InitializeForRun_行動力上限と初期ドラフト残数を設定()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(actionPointsPerRound: 3, initialDraftCount: 6);

            Assert.AreEqual(3, s.ActionPointsPerRound);
            Assert.AreEqual(6, s.InitialDraftRemaining);
            // ResetForNewRound 前は ActionPoints=0
            Assert.AreEqual(0, s.ActionPoints);
        }

        [Test]
        public void InitializeForRun_行動力0以下_例外()
        {
            var s = new VSPrototypeInteriorState();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => s.InitializeForRun(actionPointsPerRound: 0, initialDraftCount: 5));
        }

        [Test]
        public void InitializeForRun_初期ドラフト数_負_例外()
        {
            var s = new VSPrototypeInteriorState();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => s.InitializeForRun(actionPointsPerRound: 2, initialDraftCount: -1));
        }

        [Test]
        public void ResetForNewRound_行動力を上限に_実行済をクリア()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(actionPointsPerRound: 2, initialDraftCount: 5);
            s.ResetForNewRound();
            s.MarkActionExecuted(VSPrototypeInteriorAction.Conscript);

            s.ResetForNewRound();

            Assert.AreEqual(2, s.ActionPoints);
            Assert.AreEqual(0, s.ExecutedActions.Count);
        }

        // ══════════════════════════════════════════════
        // 初期ドラフト
        // ══════════════════════════════════════════════

        [Test]
        public void ConsumeInitialDraft_残数を1減らす()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(2, initialDraftCount: 3);
            s.ConsumeInitialDraft();
            Assert.AreEqual(2, s.InitialDraftRemaining);
            Assert.IsTrue(s.HasInitialDraftRemaining);
        }

        [Test]
        public void ConsumeInitialDraft_残数ゼロで例外()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(2, initialDraftCount: 1);
            s.ConsumeInitialDraft();
            Assert.IsFalse(s.HasInitialDraftRemaining);
            Assert.Throws<InvalidOperationException>(() => s.ConsumeInitialDraft());
        }

        // ══════════════════════════════════════════════
        // 行動力・実行履歴
        // ══════════════════════════════════════════════

        [Test]
        public void MarkActionExecuted_行動力を1消費_実行集合に追加()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(2, 5);
            s.ResetForNewRound();

            bool ok = s.MarkActionExecuted(VSPrototypeInteriorAction.Conscript);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, s.ActionPoints);
            Assert.IsTrue(s.HasExecutedThisRound(VSPrototypeInteriorAction.Conscript));
            Assert.IsFalse(s.HasExecutedThisRound(VSPrototypeInteriorAction.UpgradeUnitType));
        }

        [Test]
        public void MarkActionExecuted_同一アクション2回目はfalse()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(3, 5);
            s.ResetForNewRound();
            s.MarkActionExecuted(VSPrototypeInteriorAction.Conscript);

            bool second = s.MarkActionExecuted(VSPrototypeInteriorAction.Conscript);

            Assert.IsFalse(second);
            Assert.AreEqual(2, s.ActionPoints, "失敗時は行動力を消費しない");
        }

        [Test]
        public void MarkActionExecuted_行動力ゼロでfalse()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(1, 5);
            s.ResetForNewRound();
            s.MarkActionExecuted(VSPrototypeInteriorAction.Conscript);

            bool second = s.MarkActionExecuted(VSPrototypeInteriorAction.UpgradeUnitType);

            Assert.IsFalse(second);
            Assert.AreEqual(0, s.ActionPoints);
        }

        [Test]
        public void CanExecuteAction_副作用なし()
        {
            var s = new VSPrototypeInteriorState();
            s.InitializeForRun(2, 5);
            s.ResetForNewRound();

            for (int i = 0; i < 5; i++) s.CanExecuteAction(VSPrototypeInteriorAction.Conscript);

            Assert.AreEqual(2, s.ActionPoints);
            Assert.AreEqual(0, s.ExecutedActions.Count);
        }

        // 個別ユニット Lv 強化は InteriorService 側でテストする
        // （Unit.Level / Unit.AvailableUpgrades の操作なので State の責務ではない）。
    }
}
