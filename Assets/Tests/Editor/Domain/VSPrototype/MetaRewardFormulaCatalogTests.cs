// MetaRewardFormulaCatalog（本物・Resources.LoadAll 実装）の SO 由来検証。
//
// 【前提】
// - SO アセット生成メニュー実行済で
//   Assets/Resources/Data/MetaReward/meta_reward_formula_vsproto_standard.asset
//   が存在すること。未生成の場合は Catalog.GetAll().Count == 0 で fail する。
//
// 【検証観点】
// - SO ロードが Resources.LoadAll 経由で機能するか
// - SO の FormulaId が Registry で解決可能か（SO ↔ Registry の整合性）
// - VSプロト標準パラメタ値が SO に正しく入っているか
// - SO → Domain MetaRewardFormula 変換経路の end-to-end 計算
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Echolos.Data;
using Echolos.Domain.Catalog;
using Echolos.Domain.Formula;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MetaRewardFormulaCatalogTests
    {
        private IMetaRewardFormulaCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = new MetaRewardFormulaCatalog();
        }

        // ══════════════════════════════════════════════
        // SO ロード
        // ══════════════════════════════════════════════

        [Test]
        public void GetAll_SOアセットから1件以上ロードされる()
        {
            var formulas = _catalog.GetAll();
            Assert.Greater(formulas.Count, 0,
                "Resources/Data/MetaReward/*.asset が見つからない。" +
                "Editor で Echolos/Data/SO アセットを生成 を実行してください。");
        }

        [Test]
        public void GetAll_vsproto_standard_v1が含まれる()
        {
            var formulas = _catalog.GetAll();
            Assert.IsTrue(formulas.Any(f => f.Id == "vsproto_standard_v1"),
                "vsproto_standard_v1 が SO 由来で取得できない");
        }

        [Test]
        public void Get_vsproto_standard_v1_Formulaが返る()
        {
            var formula = _catalog.Get("vsproto_standard_v1");
            Assert.IsNotNull(formula);
            Assert.AreEqual("vsproto_standard_v1", formula.Id);
            Assert.AreEqual("vsproto_standard_v1", formula.FormulaId);
        }

        [Test]
        public void Get_未登録ID_例外()
        {
            Assert.Throws<KeyNotFoundException>(() => _catalog.Get("unknown_id_xyz"));
        }

        [Test]
        public void IsRegistered_vsproto_standard_v1_true()
        {
            Assert.IsTrue(_catalog.IsRegistered("vsproto_standard_v1"));
        }

        [Test]
        public void IsRegistered_未登録_false()
        {
            Assert.IsFalse(_catalog.IsRegistered("unknown_id_xyz"));
        }

        // ══════════════════════════════════════════════
        // SO ↔ Registry 整合性
        // ══════════════════════════════════════════════

        [Test]
        public void 全SOのFormulaIdがRegistry登録済()
        {
            foreach (var formula in _catalog.GetAll())
                Assert.IsTrue(MetaRewardFormulaRegistry.IsRegistered(formula.FormulaId),
                    $"SO {formula.Id} の FormulaId='{formula.FormulaId}' が Registry 未登録");
        }

        // ══════════════════════════════════════════════
        // パラメタ値（仕様準拠）
        // ══════════════════════════════════════════════

        [Test]
        public void Params_全5キーが揃っている()
        {
            var formula = _catalog.Get("vsproto_standard_v1");
            CollectionAssert.AreEquivalent(
                new[] { "perRound", "reachedBoss", "bridgetRescue", "bossDefeated", "trueEnd" },
                formula.Params.Keys);
        }

        [Test]
        public void Params_仕様準拠の値()
        {
            var formula = _catalog.Get("vsproto_standard_v1");
            Assert.AreEqual(10f, formula.Params["perRound"]);
            Assert.AreEqual(50f, formula.Params["reachedBoss"]);
            Assert.AreEqual(100f, formula.Params["bridgetRescue"]);
            Assert.AreEqual(200f, formula.Params["bossDefeated"]);
            Assert.AreEqual(150f, formula.Params["trueEnd"]);
        }

        // ══════════════════════════════════════════════
        // SO → Domain Formula 経路での end-to-end 計算
        // ══════════════════════════════════════════════

        [Test]
        public void End2End_SO経路でトゥルーエンド570()
        {
            var formula = _catalog.Get("vsproto_standard_v1");
            var ctx = new MetaRewardContext(7, true, true, true, true);
            Assert.AreEqual(570, formula.Calculate(ctx));
        }

        [Test]
        public void End2End_SO経路で1周目R3敗北30()
        {
            var formula = _catalog.Get("vsproto_standard_v1");
            var ctx = new MetaRewardContext(3, false, false, false, false);
            Assert.AreEqual(30, formula.Calculate(ctx));
        }
    }
}
