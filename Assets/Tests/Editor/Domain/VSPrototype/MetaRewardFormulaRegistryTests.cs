// MetaRewardFormulaRegistry の獲得式と典型シナリオ検証。
// Params 辞書経由で仕様通りの獲得量が算出されることを保証する。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Formula;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MetaRewardFormulaRegistryTests
    {
        // VSプロト標準パラメタ（SoAssetGenerator / StubMetaRewardFormulaCatalog と同値）
        private static IReadOnlyDictionary<string, float> StandardParams()
            => new Dictionary<string, float>
            {
                ["perRound"]      = 10f,
                ["reachedBoss"]   = 50f,
                ["bridgetRescue"] = 100f,
                ["bossDefeated"]  = 200f,
                ["trueEnd"]       = 150f,
            };

        private static int Calc(MetaRewardContext ctx)
        {
            var f = MetaRewardFormulaRegistry.Get("vsproto_standard_v1");
            return f(ctx, StandardParams());
        }

        // ── Registry API ──

        [Test]
        public void Get_未登録ID_例外()
        {
            Assert.Throws<KeyNotFoundException>(() => MetaRewardFormulaRegistry.Get("unknown_id_xyz"));
        }

        [Test]
        public void Get_null_例外()
        {
            Assert.Throws<ArgumentException>(() => MetaRewardFormulaRegistry.Get(null));
        }

        [Test]
        public void IsRegistered_標準ID_true()
        {
            Assert.IsTrue(MetaRewardFormulaRegistry.IsRegistered("vsproto_standard_v1"));
        }

        [Test]
        public void GetAllIds_標準ID含む()
        {
            CollectionAssert.Contains(MetaRewardFormulaRegistry.GetAllIds(), "vsproto_standard_v1");
        }

        // ── 個別ボーナス（旧 const 単独テストの SO 経路版） ──

        [Test]
        public void 経過ラウンド0_全フラグfalse_0()
        {
            Assert.AreEqual(0, Calc(new MetaRewardContext(0, false, false, false, false)));
        }

        [Test]
        public void 経過ラウンドのみ_perRound_乗算()
        {
            Assert.AreEqual(30, Calc(new MetaRewardContext(3, false, false, false, false)));
        }

        [Test]
        public void ボス到達ボーナス単独()
        {
            Assert.AreEqual(50, Calc(new MetaRewardContext(0, true, false, false, false)));
        }

        [Test]
        public void バルドゥイン救出ボーナス単独()
        {
            Assert.AreEqual(100, Calc(new MetaRewardContext(0, false, true, false, false)));
        }

        [Test]
        public void ボス勝利ボーナス単独()
        {
            Assert.AreEqual(200, Calc(new MetaRewardContext(0, false, false, true, false)));
        }

        [Test]
        public void トゥルーエンドボーナス単独()
        {
            Assert.AreEqual(150, Calc(new MetaRewardContext(0, false, false, false, true)));
        }

        // ── 典型シナリオ ──

        [Test]
        public void シナリオ1_1周目R3で本拠地陥落_30()
        {
            var ctx = new MetaRewardContext(3, false, false, false, false);
            Assert.AreEqual(30, Calc(ctx));
        }

        [Test]
        public void シナリオ2_R7到達ボス敗北_120()
        {
            var ctx = new MetaRewardContext(7, true, false, false, false);
            Assert.AreEqual(120, Calc(ctx));
        }

        [Test]
        public void シナリオ3_ビターエンドクリア_320()
        {
            var ctx = new MetaRewardContext(7, true, false, true, false);
            Assert.AreEqual(320, Calc(ctx));
        }

        [Test]
        public void シナリオ4_トゥルーエンドクリア_570()
        {
            var ctx = new MetaRewardContext(7, true, true, true, true);
            Assert.AreEqual(570, Calc(ctx));
        }

        // ── 不正値 ──

        [Test]
        public void MetaRewardContext_負の経過ラウンド_例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MetaRewardContext(-1, false, false, false, false));
        }

        // ── 加算の独立性 ──

        [Test]
        public void 救出のみ_ボス未到達でも100点入る()
        {
            var ctx = new MetaRewardContext(5, false, true, false, false);
            Assert.AreEqual(150, Calc(ctx)); // 5*10 + 100
        }

        [Test]
        public void TrueEndボーナスはBoss勝利と独立()
        {
            // 異常組み合わせでも素直に加算（呼び出し側で防ぐ前提）
            var ctx = new MetaRewardContext(7, true, true, false, true);
            Assert.AreEqual(370, Calc(ctx)); // 7*10 + 50 + 100 + 150
        }

        // ── パラメタ欠損時のフォールバック ──

        [Test]
        public void パラメタ欠損時_既定値で計算される()
        {
            // perRound だけ与えて他は欠損 → 既定値（50/100/200/150）が使われる
            var partial = new Dictionary<string, float> { ["perRound"] = 10f };
            var f = MetaRewardFormulaRegistry.Get("vsproto_standard_v1");
            var ctx = new MetaRewardContext(7, true, true, true, true);
            // 7*10 + 50 + 100 + 200 + 150 = 570
            Assert.AreEqual(570, f(ctx, partial));
        }
    }
}
