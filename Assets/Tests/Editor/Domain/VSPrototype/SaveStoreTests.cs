// ISaveStore の振る舞い契約検証（StubSaveStore 経由）。
//
// 【検証観点】
// - 未保存キーの Load は空文字
// - Save した内容が Load で復元される
// - Save → Delete で消える
// - 上書き Save が前の値を消す
// - Has が Save/Delete に追従する
// - null 内容を渡しても例外にせず空文字相当として扱う（実装契約）
//
// 本物の PlayerPrefsSaveStore は Unity PlayerPrefs 依存のためここでは検証しない。
using NUnit.Framework;
using Echolos.Domain.Save;
using Echolos.Tests.Domain.Helpers;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class SaveStoreTests
    {
        private ISaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new StubSaveStore();
        }

        [Test]
        public void Load_未保存_空文字()
        {
            Assert.AreEqual(string.Empty, _store.Load("missing_key"));
        }

        [Test]
        public void Save_Load_往復()
        {
            _store.Save("k1", "hello");
            Assert.AreEqual("hello", _store.Load("k1"));
        }

        [Test]
        public void Save_上書き()
        {
            _store.Save("k1", "first");
            _store.Save("k1", "second");
            Assert.AreEqual("second", _store.Load("k1"));
        }

        [Test]
        public void Save_null_空文字相当()
        {
            _store.Save("k1", null);
            Assert.AreEqual(string.Empty, _store.Load("k1"));
        }

        [Test]
        public void Has_未保存_false()
        {
            Assert.IsFalse(_store.Has("missing_key"));
        }

        [Test]
        public void Has_Save後_true()
        {
            _store.Save("k1", "v");
            Assert.IsTrue(_store.Has("k1"));
        }

        [Test]
        public void Delete_Has_false_Load_空文字()
        {
            _store.Save("k1", "v");
            _store.Delete("k1");
            Assert.IsFalse(_store.Has("k1"));
            Assert.AreEqual(string.Empty, _store.Load("k1"));
        }

        [Test]
        public void Delete_未保存_例外なし()
        {
            Assert.DoesNotThrow(() => _store.Delete("missing_key"));
        }

        [Test]
        public void 複数キー_独立性()
        {
            _store.Save("a", "1");
            _store.Save("b", "2");
            _store.Save("c", "3");

            Assert.AreEqual("1", _store.Load("a"));
            Assert.AreEqual("2", _store.Load("b"));
            Assert.AreEqual("3", _store.Load("c"));

            _store.Delete("b");
            Assert.AreEqual("1", _store.Load("a"));
            Assert.AreEqual(string.Empty, _store.Load("b"));
            Assert.AreEqual("3", _store.Load("c"));
        }
    }
}
