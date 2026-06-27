// ラウンド開始時演出のシーン ID 決定ロジック検証。
using System;
using NUnit.Framework;
using Echolos.Tests.Domain.Helpers;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class RoundStartEventResolverTests
    {
        private StubMetaProgressView _meta;
        private VSPrototypeMapState _state;

        [SetUp]
        public void SetUp()
        {
            _meta = new StubMetaProgressView();
            _state = new VSPrototypeMapState();
        }

        // ── R1：演出なし ──

        [Test]
        public void R1_演出なし()
        {
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(1, _meta, _state));
        }

        // ── R2：B-a バルドゥインの背景 ──

        [Test]
        public void R2_HasRescuedBalduin_false_BalduinIntro()
        {
            _meta.HasRescuedBalduin = false;
            Assert.AreEqual(VSPrototypeStorySceneIds.BalduinIntro,
                VSPrototypeRoundStartEventResolver.Resolve(2, _meta, _state));
        }

        [Test]
        public void R2_HasRescuedBalduin_true_null()
        {
            _meta.HasRescuedBalduin = true;
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(2, _meta, _state));
        }

        [Test]
        public void R2_当該ラン救出済_null()
        {
            // R1 で先行救援した場合、R2 開始時にバルドゥインの背景イベントは出さない（矛盾回避）。
            _meta.HasRescuedBalduin = false;
            _state.MarkBridgetRescued();
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(2, _meta, _state));
        }

        // ── R3：B-b1 戦況悪化＋手紙握りつぶし ──

        [Test]
        public void R3_HasRescuedBalduin_false_BalduinLetter()
        {
            _meta.HasRescuedBalduin = false;
            Assert.AreEqual(VSPrototypeStorySceneIds.BalduinLetter,
                VSPrototypeRoundStartEventResolver.Resolve(3, _meta, _state));
        }

        [Test]
        public void R3_HasRescuedBalduin_true_null()
        {
            _meta.HasRescuedBalduin = true;
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(3, _meta, _state));
        }

        [Test]
        public void R3_当該ラン救出済_null()
        {
            // 本ラン R1-R2 で先行救援した場合、R3 開始時に「救援の手紙握りつぶし」イベントは出さない
            //（既に救援成功している状況で手紙が握りつぶされる演出は矛盾）。
            _meta.HasRescuedBalduin = false;
            _state.MarkBridgetRescued();
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(3, _meta, _state));
        }

        // ── R4：演出なし ──

        [Test]
        public void R4_演出なし()
        {
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(4, _meta, _state));
        }

        // ── R5：B-b2 バルドゥイン降伏（救援失敗時のみ） ──

        [Test]
        public void R5_未救出かつHasRescuedBalduin_false_BalduinSurrender()
        {
            _meta.HasRescuedBalduin = false;
            // _state.IsBridgetRescued は初期 false
            Assert.AreEqual(VSPrototypeStorySceneIds.BalduinSurrender,
                VSPrototypeRoundStartEventResolver.Resolve(5, _meta, _state));
        }

        [Test]
        public void R5_当該ラン救出済_null()
        {
            _meta.HasRescuedBalduin = false;
            _state.MarkBridgetRescued();
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(5, _meta, _state));
        }

        [Test]
        public void R5_HasRescuedBalduin_true_null()
        {
            _meta.HasRescuedBalduin = true;
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(5, _meta, _state));
        }

        // ── R6：分岐が複雑 ──

        [Test]
        public void R6_HasRescuedBalduin_true_かつ_HasNotedPendantPower_false_SwordEmpowered()
        {
            // 救援済かつペンダント未気づき → B-e SwordEmpowered（気づき＋強化を 1 シーンで）
            _meta.HasRescuedBalduin = true;
            _meta.HasNotedPendantPower = false;
            Assert.AreEqual(VSPrototypeStorySceneIds.SwordEmpowered,
                VSPrototypeRoundStartEventResolver.Resolve(6, _meta, _state));
        }

        [Test]
        public void R6_当該ラン救出済_SwordEmpowered()
        {
            // 当該ラン中に R5 までに救出した場合、永続フラグが立つ前でも SwordEmpowered を流す
            // → 同周回で R7 A-c2 経路（皇太子撃破可能）に到達できる。
            _meta.HasRescuedBalduin = false;
            _meta.HasNotedPendantPower = false;
            _state.MarkBridgetRescued();
            Assert.AreEqual(VSPrototypeStorySceneIds.SwordEmpowered,
                VSPrototypeRoundStartEventResolver.Resolve(6, _meta, _state));
        }

        [Test]
        public void R6_当該ラン救出済_HasNotedPendantPower_true_null()
        {
            // 当該ラン救出済でも既に気づき済なら R6 開始時演出はなし（同周回で 2 回出さない）。
            _meta.HasRescuedBalduin = false;
            _meta.HasNotedPendantPower = true;
            _state.MarkBridgetRescued();
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(6, _meta, _state));
        }

        [Test]
        public void R6_HasRescuedBalduin_true_かつ_HasNotedPendantPower_true_null()
        {
            // ペンダント気づき済 → R6 開始時演出なし（A-c2 経路は R7 開始時に発火）
            _meta.HasRescuedBalduin = true;
            _meta.HasNotedPendantPower = true;
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(6, _meta, _state));
        }

        [Test]
        public void R6_未救援かつ未当該ラン救出_かつBalduinSurrendered_MysteriousGirl()
        {
            // 未救援世界＆未救出＆ R5 B-b2 既発火 → B-c 謎の少女
            _meta.HasRescuedBalduin = false;
            _state.MarkBalduinSurrendered();
            Assert.AreEqual(VSPrototypeStorySceneIds.MysteriousGirl,
                VSPrototypeRoundStartEventResolver.Resolve(6, _meta, _state));
        }

        [Test]
        public void R6_未救援かつ未当該ラン救出_かつBalduinSurrenderedでない_null()
        {
            // 未救援世界＆未救出だが R5 を経由していない（B-b2 未発火）→ B-c は発火しない。
            // 試遊版 R4 開始セーブ等のケース：R5 をスキップしてラン開始するシナリオを救済。
            _meta.HasRescuedBalduin = false;
            // _state.IsBalduinSurrendered は初期 false（MarkBalduinSurrendered 呼ばない）
            Assert.IsNull(VSPrototypeRoundStartEventResolver.Resolve(6, _meta, _state));
        }

        // 当該ラン救出済時の R6 挙動は `R6_当該ラン救出済_SwordEmpowered` 側で検証
        // （旧仕様「既加入ループでは出さない」は 2026-06-18 撤廃済み）。

        // ── R7：B-e 連鎖／A-c1（必敗）/ A-c2（戦える）三分岐 ──

        [Test]
        public void R7_未救援かつ未当該ラン救出_BossAttack()
        {
            // 救援なし＆未気づき → A-c1 必敗
            _meta.HasNotedPendantPower = false;
            _meta.HasRescuedBalduin = false;
            // _state.IsBridgetRescued は初期 false
            Assert.AreEqual(VSPrototypeStorySceneIds.BossAttack,
                VSPrototypeRoundStartEventResolver.Resolve(7, _meta, _state));
        }

        [Test]
        public void R7_HasNotedPendantPower_true_BossPurify()
        {
            // ペンダント気づき済 → A-c2 戦える
            _meta.HasNotedPendantPower = true;
            Assert.AreEqual(VSPrototypeStorySceneIds.BossPurify,
                VSPrototypeRoundStartEventResolver.Resolve(7, _meta, _state));
        }

        [Test]
        public void R7_HasRescuedBalduin_true_かつ_HasNotedPendantPower_false_SwordEmpowered()
        {
            // 永続救援済かつペンダント未気づき → R7 開始時に B-e 連鎖発火
            // （R6 で B-e が発火していない経路。例：R6 で救出した場合や R7 から再開した場合）
            _meta.HasRescuedBalduin = true;
            _meta.HasNotedPendantPower = false;
            Assert.AreEqual(VSPrototypeStorySceneIds.SwordEmpowered,
                VSPrototypeRoundStartEventResolver.Resolve(7, _meta, _state));
        }

        [Test]
        public void R7_当該ラン救出済_HasNotedPendantPower_false_SwordEmpowered()
        {
            // 当該ラン中（R6 等で）救出かつペンダント未気づき → R7 開始時に B-e 連鎖
            // → Bootstrap が SwordEmpowered 完了後に同 R7 を再判定し A-c2 BossPurify に流す
            _meta.HasRescuedBalduin = false;
            _meta.HasNotedPendantPower = false;
            _state.MarkBridgetRescued();
            Assert.AreEqual(VSPrototypeStorySceneIds.SwordEmpowered,
                VSPrototypeRoundStartEventResolver.Resolve(7, _meta, _state));
        }

        // ── 引数例外 ──

        [Test]
        public void meta_null_例外()
        {
            Assert.Throws<ArgumentNullException>(
                () => VSPrototypeRoundStartEventResolver.Resolve(2, null, _state));
        }

        [Test]
        public void mapState_null_例外()
        {
            Assert.Throws<ArgumentNullException>(
                () => VSPrototypeRoundStartEventResolver.Resolve(2, _meta, null));
        }
    }
}
