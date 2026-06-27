// MetaProgressStore（ISaveStore 注入版）のロード／セーブ／削除検証。
//
// 【検証観点】
// - 未保存からの Load は初期状態（Memories=0 / RunCount=0 等）の新規 State
// - Save → Load 往復で全フィールド復元
// - Save 後 DeleteAll で Load が初期状態に戻る
// - 不正 JSON が ISaveStore 内にあっても Load は初期状態の新規 State を返す
// - PrefsKey は "vsproto_meta_progress" 固定
using System;
using NUnit.Framework;
using Echolos.Domain.Meta;
using Echolos.Tests.Domain.Helpers;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MetaProgressStoreTests
    {
        private StubSaveStore _saveStore;
        private MetaProgressStore _metaStore;

        [SetUp]
        public void SetUp()
        {
            _saveStore = new StubSaveStore();
            _metaStore = new MetaProgressStore(_saveStore);
        }

        // ══════════════════════════════════════════════
        // コンストラクタ
        // ══════════════════════════════════════════════

        [Test]
        public void コンストラクタ_null_例外()
        {
            Assert.Throws<ArgumentNullException>(() => new MetaProgressStore(null));
        }

        [Test]
        public void PrefsKey_仕様準拠()
        {
            Assert.AreEqual("vsproto_meta_progress", MetaProgressStore.PrefsKey);
        }

        // ══════════════════════════════════════════════
        // 未保存からの Load
        // ══════════════════════════════════════════════

        [Test]
        public void Load_未保存_初期状態の新規State()
        {
            var state = _metaStore.Load();
            Assert.IsNotNull(state);
            Assert.AreEqual(0, state.Memories);
            Assert.AreEqual(0, state.RunCount);
            Assert.IsFalse(state.HasReachedTrueEnd);
            Assert.AreEqual(0, state.UnlockedUnits.Count);
            Assert.AreEqual(0, state.AppliedUpgrades.Count);
        }

        // ══════════════════════════════════════════════
        // Save → Load 往復
        // ══════════════════════════════════════════════

        [Test]
        public void Save_Load_全フィールド復元()
        {
            var src = new MetaProgressState();
            src.EarnMemories(370);
            src.IncrementRunCount();
            src.IncrementRunCount();
            src.MarkTrueEndReached();
            src.UnlockUnit(MetaUnitIds.Bridget);
            src.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, 2);
            src.ApplyUpgrade(MetaUpgradeIds.InitialUnit, 3);
            src.ApplyUpgrade(MetaUpgradeIds.InitialUnit, 3);

            _metaStore.Save(src);
            var restored = _metaStore.Load();

            Assert.AreEqual(370, restored.Memories);
            Assert.AreEqual(2, restored.RunCount);
            Assert.IsTrue(restored.HasReachedTrueEnd);
            Assert.IsTrue(restored.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, restored.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
            Assert.AreEqual(2, restored.GetUpgradeLevel(MetaUpgradeIds.InitialUnit));
        }

        [Test]
        public void Save_PrefsKeyに保存される()
        {
            var src = new MetaProgressState();
            src.EarnMemories(100);

            _metaStore.Save(src);

            Assert.IsTrue(_saveStore.Has(MetaProgressStore.PrefsKey));
            Assert.IsNotEmpty(_saveStore.Load(MetaProgressStore.PrefsKey));
        }

        [Test]
        public void Save_null_何もしない()
        {
            Assert.DoesNotThrow(() => _metaStore.Save(null));
            Assert.IsFalse(_saveStore.Has(MetaProgressStore.PrefsKey));
        }

        // ══════════════════════════════════════════════
        // DeleteAll
        // ══════════════════════════════════════════════

        [Test]
        public void DeleteAll_Save後にDeleteAll_Loadは初期状態()
        {
            var src = new MetaProgressState();
            src.EarnMemories(500);
            _metaStore.Save(src);

            _metaStore.DeleteAll();

            Assert.IsFalse(_saveStore.Has(MetaProgressStore.PrefsKey));
            var restored = _metaStore.Load();
            Assert.AreEqual(0, restored.Memories);
        }

        // ══════════════════════════════════════════════
        // 破損データのフォールバック
        // ══════════════════════════════════════════════

        [Test]
        public void Load_不正JSON_初期状態の新規State()
        {
            // SaveStore に不正な JSON を直接書き込む（過去バージョン互換問題のシミュレート）
            _saveStore.Save(MetaProgressStore.PrefsKey, "this is not a valid json{");

            var state = _metaStore.Load();

            Assert.IsNotNull(state);
            Assert.AreEqual(0, state.Memories,
                "不正 JSON はパース失敗で State に何も書き込まれない＝初期状態");
        }
    }
}
