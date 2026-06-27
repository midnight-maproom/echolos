// MetaUpgradeCatalog（本物・Resources.LoadAll 実装）の SO 由来検証。
//
// 【前提】
// - SO アセット生成メニュー実行済で
//   Assets/Resources/Data/MetaUpgrades/meta_upgrade_*.asset 4 個が存在すること。
//   未生成の場合は GetAll().Count == 0 で fail する。
//
// 【検証観点】
// - SO ロードが Resources.LoadAll 経由で機能するか（3 件登録）
// - 各 SO の Id が MetaUpgradeIds の const string と一致
// - 仕様準拠のコスト・上限
// - SO → Domain MetaUpgrade 変換経路
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Echolos.Data;
using Echolos.Domain.Catalog;
using Echolos.Domain.Meta;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MetaUpgradeCatalogTests
    {
        private IMetaUpgradeCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = new MetaUpgradeCatalog();
        }

        // ══════════════════════════════════════════════
        // SO ロード
        // ══════════════════════════════════════════════

        [Test]
        public void GetAll_SOアセットから4件ロードされる()
        {
            var upgrades = _catalog.GetAll();
            Assert.AreEqual(4, upgrades.Count,
                "Resources/Data/MetaUpgrades/*.asset の 4 件が見つからない。" +
                "Editor で Echolos/Data/SO アセットを生成 を実行してください。");
        }

        [Test]
        public void GetAll_4項目すべて含まれる()
        {
            var ids = _catalog.GetAll().Select(u => u.Id).ToList();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    MetaUpgradeIds.PrincessLevel,
                    MetaUpgradeIds.BridgetLevel,
                    MetaUpgradeIds.ActionPoints,
                    MetaUpgradeIds.InitialUnit,
                },
                ids);
        }

        [Test]
        public void Get_未登録ID_例外()
        {
            Assert.Throws<KeyNotFoundException>(() => _catalog.Get("unknown_id_xyz"));
        }

        [Test]
        public void IsRegistered_4項目すべてtrue()
        {
            Assert.IsTrue(_catalog.IsRegistered(MetaUpgradeIds.PrincessLevel));
            Assert.IsTrue(_catalog.IsRegistered(MetaUpgradeIds.BridgetLevel));
            Assert.IsTrue(_catalog.IsRegistered(MetaUpgradeIds.ActionPoints));
            Assert.IsTrue(_catalog.IsRegistered(MetaUpgradeIds.InitialUnit));
        }

        [Test]
        public void IsRegistered_未登録_false()
        {
            Assert.IsFalse(_catalog.IsRegistered("unknown_id_xyz"));
        }

        // ══════════════════════════════════════════════
        // 仕様準拠（コスト・上限）
        // ══════════════════════════════════════════════

        [Test]
        public void PrincessLevel_段階別コスト30_60_上限2()
        {
            var u = _catalog.Get(MetaUpgradeIds.PrincessLevel);
            Assert.AreEqual(2, u.Cap);
            Assert.AreEqual(2, u.Costs.Count);
            Assert.AreEqual(30, u.Costs[0]);
            Assert.AreEqual(60, u.Costs[1]);
            Assert.AreEqual(30, u.GetCostForNextLevel(0));
            Assert.AreEqual(60, u.GetCostForNextLevel(1));
            Assert.IsNotEmpty(u.DisplayName);
            Assert.IsNotEmpty(u.EffectText);
        }

        [Test]
        public void BridgetLevel_段階別コスト30_60_上限2()
        {
            var u = _catalog.Get(MetaUpgradeIds.BridgetLevel);
            Assert.AreEqual(2, u.Cap);
            Assert.AreEqual(2, u.Costs.Count);
            Assert.AreEqual(30, u.Costs[0]);
            Assert.AreEqual(60, u.Costs[1]);
            Assert.IsNotEmpty(u.DisplayName);
            Assert.IsNotEmpty(u.EffectText);
        }

        [Test]
        public void ActionPoints_コスト100_上限1()
        {
            var u = _catalog.Get(MetaUpgradeIds.ActionPoints);
            Assert.AreEqual(1, u.Cap);
            Assert.AreEqual(1, u.Costs.Count);
            Assert.AreEqual(100, u.Costs[0]);
            Assert.IsNotEmpty(u.DisplayName);
            Assert.IsNotEmpty(u.EffectText);
        }

        [Test]
        public void InitialUnit_段階別コスト50_80_100_上限3()
        {
            var u = _catalog.Get(MetaUpgradeIds.InitialUnit);
            Assert.AreEqual(3, u.Cap);
            Assert.AreEqual(3, u.Costs.Count);
            Assert.AreEqual(50, u.Costs[0]);
            Assert.AreEqual(80, u.Costs[1]);
            Assert.AreEqual(100, u.Costs[2]);
            Assert.IsNotEmpty(u.DisplayName);
            Assert.IsNotEmpty(u.EffectText);
        }

        // ══════════════════════════════════════════════
        // SO → MetaProgressState 経路の end-to-end 検証
        // ══════════════════════════════════════════════

        [Test]
        public void End2End_SO経路で初期ユニット強化を3回適用_4回目はfalse()
        {
            var u = _catalog.Get(MetaUpgradeIds.InitialUnit);
            var meta = new MetaProgressState();
            for (int i = 1; i <= u.Cap; i++)
                Assert.IsTrue(meta.ApplyUpgrade(u.Id, u.Cap), $"{i} 回目は成功すべき");
            Assert.IsFalse(meta.ApplyUpgrade(u.Id, u.Cap),
                "Cap (3) を超えた 4 回目は false");
            Assert.AreEqual(u.Cap, meta.GetUpgradeLevel(u.Id));
        }
    }
}
