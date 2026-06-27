// DraftPoolCatalog の整合性テスト（IUnitCatalog と組み合わせ）。
//
// 検証方針：
// - 本物 SO + Resources.LoadAll で動く統合テスト。
// - プール内 NormalUnitIds / RareUnitIds がすべて IUnitCatalog で解決可能。
// - 固有 2 体（princess / bridget）がプールに含まれないことを確認。
using System.Linq;
using NUnit.Framework;
using Echolos.Data;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DraftPoolCatalogTests
    {
        private IDraftPoolCatalog _poolCatalog;
        private IUnitCatalog _unitCatalog;

        [SetUp]
        public void SetUp()
        {
            var wazaCatalog = new WazaCatalog();
            _unitCatalog = new UnitCatalog(wazaCatalog, new UnitUpgradeCatalog());
            _poolCatalog = new DraftPoolCatalog();
        }

        [Test]
        public void プール_少なくとも1件_ロード可能()
        {
            int count = _poolCatalog.GetAll().Count;
            Assert.GreaterOrEqual(count, 1, "VSプロト標準プールが少なくとも 1 件登録されている");
        }

        [Test]
        public void プール_VSプロト標準_Normal10件_Rare5件()
        {
            var pool = _poolCatalog.Get("vsproto_standard_pool");
            Assert.IsNotNull(pool);
            Assert.AreEqual(10, pool.NormalUnitIds.Count);
            Assert.AreEqual(5, pool.RareUnitIds.Count);
        }

        [Test]
        public void プール_全UnitIdsがIUnitCatalogで解決可能()
        {
            var pool = _poolCatalog.Get("vsproto_standard_pool");
            foreach (var id in pool.NormalUnitIds)
                Assert.IsTrue(_unitCatalog.IsRegistered(id), $"Normal {id} が UnitCatalog で未登録");
            foreach (var id in pool.RareUnitIds)
                Assert.IsTrue(_unitCatalog.IsRegistered(id), $"Rare {id} が UnitCatalog で未登録");
        }

        [Test]
        public void プール_固有ユニット2体は含まれない()
        {
            var pool = _poolCatalog.Get("vsproto_standard_pool");
            var all = pool.NormalUnitIds.Concat(pool.RareUnitIds).ToList();
            CollectionAssert.DoesNotContain(all, UniqueUnitIds.Bridget);
            CollectionAssert.DoesNotContain(all, UniqueUnitIds.Princess);
        }

        [Test]
        public void プール_NormalとRareでId重複なし()
        {
            var pool = _poolCatalog.Get("vsproto_standard_pool");
            var dup = pool.NormalUnitIds.Intersect(pool.RareUnitIds).ToList();
            Assert.AreEqual(0, dup.Count, $"重複: {string.Join(",", dup)}");
        }

        [Test]
        public void プール_CandidatesPerOffer_3()
        {
            var pool = _poolCatalog.Get("vsproto_standard_pool");
            Assert.AreEqual(3, pool.CandidatesPerOffer);
        }
    }
}
