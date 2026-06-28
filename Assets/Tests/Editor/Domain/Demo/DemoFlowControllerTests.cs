// DemoFlowController / NullDemoFlowController の最小動作テスト。
// 試遊版は R4 開始・通常進行で救出戦に挑む構成に縮約（Save2 単独・進行ルール介入なし）。
using NUnit.Framework;
using Echolos.UseCase.Demo;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DemoFlowControllerTests
    {
        [Test]
        public void NullImpl_IsActive_False()
        {
            var ctl = new NullDemoFlowController();
            Assert.IsFalse(ctl.IsActive);
            Assert.IsNull(ctl.CurrentSave);
        }

        [Test]
        public void DemoImpl_IsActive_True()
        {
            var ctl = new DemoFlowController();
            Assert.IsTrue(ctl.IsActive);
        }

        [Test]
        public void LoadSave_Save2_セーブが連動()
        {
            var ctl = new DemoFlowController();
            ctl.LoadSave(DemoSaveCatalog.Save2Id);
            Assert.IsNotNull(ctl.CurrentSave);
            Assert.AreEqual(DemoSaveCatalog.Save2Id, ctl.CurrentSave.Id);
        }

        [Test]
        public void LoadSave_未知のID_例外()
        {
            var ctl = new DemoFlowController();
            Assert.Throws<System.ArgumentException>(() => ctl.LoadSave("unknown_save_id"));
        }
    }
}
