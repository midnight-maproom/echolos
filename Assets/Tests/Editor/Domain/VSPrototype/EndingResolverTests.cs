// VSPrototypeEndingResolver の分岐ロジック・メタ反映副作用を検証する。
using System;
using NUnit.Framework;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class EndingResolverTests
    {
        private VSPrototypeEndingResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _resolver = new VSPrototypeEndingResolver();
        }

        // ── 本拠地連続防衛の判定 ──

        [Test]
        public void ResolveAfterHomeDefense_全勝_None()
        {
            var r = _resolver.ResolveAfterHomeDefense(allWon: true);
            Assert.AreEqual(VSPrototypeEndingKind.None, r);
        }

        [Test]
        public void ResolveAfterHomeDefense_1戦でも敗北_Defeat()
        {
            var r = _resolver.ResolveAfterHomeDefense(allWon: false);
            Assert.AreEqual(VSPrototypeEndingKind.Defeat, r);
        }

        // ── R7 ボス戦後の分岐 ──

        [Test]
        public void ResolveAfterBossRound_ボス敗北_Defeat()
        {
            var r = _resolver.ResolveAfterBossRound(bossWon: false);
            Assert.AreEqual(VSPrototypeEndingKind.Defeat, r);
        }

        [Test]
        public void ResolveAfterBossRound_ボス勝利_True()
        {
            // R7 勝利は構造的に A-c2 経路でのみ可能（HasNotedPendantPower=true）。
            // Bitter は廃止されたため、勝利＝必ず True。
            var r = _resolver.ResolveAfterBossRound(bossWon: true);
            Assert.AreEqual(VSPrototypeEndingKind.True, r);
        }

        // ── ApplyEndingToMeta の副作用 ──

        [Test]
        public void ApplyEndingToMeta_Defeat救出済_Bridget解禁_RunCount増加()
        {
            var meta = new MetaProgressState();
            _resolver.ApplyEndingToMeta(
                VSPrototypeEndingKind.Defeat, bridgetRescuedThisRun: true, meta);

            Assert.IsTrue(meta.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, meta.RunCount);
            Assert.IsFalse(meta.HasReachedTrueEnd);
        }

        [Test]
        public void ApplyEndingToMeta_Defeat救出失敗_Bridget未解禁_RunCount増加()
        {
            var meta = new MetaProgressState();
            _resolver.ApplyEndingToMeta(
                VSPrototypeEndingKind.Defeat, bridgetRescuedThisRun: false, meta);

            Assert.IsFalse(meta.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, meta.RunCount);
            Assert.IsFalse(meta.HasReachedTrueEnd);
        }

        [Test]
        public void ApplyEndingToMeta_True_Bridget解禁_TrueEndフラグ_RunCount増加()
        {
            var meta = new MetaProgressState();
            _resolver.ApplyEndingToMeta(
                VSPrototypeEndingKind.True, bridgetRescuedThisRun: true, meta);

            Assert.IsTrue(meta.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, meta.RunCount);
            Assert.IsTrue(meta.HasReachedTrueEnd);
        }

        [Test]
        public void ApplyEndingToMeta_None_例外()
        {
            var meta = new MetaProgressState();
            Assert.Throws<InvalidOperationException>(() =>
                _resolver.ApplyEndingToMeta(
                    VSPrototypeEndingKind.None, bridgetRescuedThisRun: false, meta));
        }

        [Test]
        public void ApplyEndingToMeta_meta_null_例外()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _resolver.ApplyEndingToMeta(
                    VSPrototypeEndingKind.Defeat, bridgetRescuedThisRun: false, meta: null));
        }

        [Test]
        public void ApplyEndingToMeta_既解禁の状態で再度Bridget救出_冪等()
        {
            var meta = new MetaProgressState();
            meta.UnlockUnit(MetaUnitIds.Bridget);

            _resolver.ApplyEndingToMeta(
                VSPrototypeEndingKind.True, bridgetRescuedThisRun: true, meta);

            Assert.IsTrue(meta.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, meta.UnlockedUnits.Count); // 重複追加されない
            Assert.AreEqual(1, meta.RunCount);
        }
    }
}
