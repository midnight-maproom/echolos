// DemoFlowController / NullDemoFlowController の最小動作テスト。
// 試遊版は救出戦のみに縮約（Scenario2 単独）。Save1/SaveRetry/Scenario1/ScenarioRetry は廃止。
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
            Assert.IsNull(ctl.CurrentScenario);
        }

        [Test]
        public void NullImpl_全問い合わせ_介入しない()
        {
            var ctl = new NullDemoFlowController();
            Assert.IsFalse(ctl.ShouldSkipInteriorPhase(1));
            Assert.IsFalse(ctl.ShouldAutoResolveBattle(1, 0, 1));
        }

        [Test]
        public void DemoImpl_IsActive_True()
        {
            var ctl = new DemoFlowController();
            Assert.IsTrue(ctl.IsActive);
        }

        [Test]
        public void DemoImpl_未ロード_全問い合わせ_介入しない()
        {
            var ctl = new DemoFlowController();
            Assert.IsFalse(ctl.ShouldSkipInteriorPhase(1));
            Assert.IsFalse(ctl.ShouldAutoResolveBattle(1, 0, 1));
        }

        [Test]
        public void LoadScenario_Scenario2_StartingSaveが連動()
        {
            var ctl = new DemoFlowController();
            ctl.LoadScenario(DemoScenarioCatalog.Scenario2Id);
            Assert.AreEqual(DemoScenarioCatalog.Scenario2Id, ctl.CurrentScenario.Id);
            Assert.AreEqual(DemoSaveCatalog.Save2Id, ctl.CurrentSave.Id);
        }

        [Test]
        public void NullImpl_GetObjectiveText_null返す()
        {
            var ctl = new NullDemoFlowController();
            Assert.IsNull(ctl.GetObjectiveText(1));
        }

        [Test]
        public void DemoImpl_Scenario2_R4_ObjectiveText取得()
        {
            var ctl = new DemoFlowController();
            ctl.LoadScenario(DemoScenarioCatalog.Scenario2Id);
            Assert.IsNotNull(ctl.GetObjectiveText(4));
            StringAssert.Contains("救援", ctl.GetObjectiveText(4));
        }

        [Test]
        public void DemoImpl_RoundRuleなし_null返す()
        {
            var ctl = new DemoFlowController();
            ctl.LoadScenario(DemoScenarioCatalog.Scenario2Id);
            Assert.IsNull(ctl.GetObjectiveText(99));
        }

        // 動画撮影用シナリオ（Rec_*）は RoundRules 空＝通常進行。
        // ロード成功＋ ObjectiveText 全ラウンド null だけ確認する。

        [Test]
        public void RecScenarios_全件ロード可能()
        {
            var ids = new[]
            {
                DemoScenarioCatalog.RecR5BB2Id,
                DemoScenarioCatalog.RecR7AC1Id,
                DemoScenarioCatalog.RecR6RescueId,
                DemoScenarioCatalog.RecR7TrueId,
            };
            foreach (var id in ids)
            {
                var ctl = new DemoFlowController();
                ctl.LoadScenario(id);
                Assert.AreEqual(id, ctl.CurrentScenario.Id, $"シナリオ {id} がロードできること");
                Assert.IsNotNull(ctl.CurrentSave, $"シナリオ {id} の連動セーブがロードされること");
            }
        }

        [Test]
        public void RecScenarios_RoundRule空_ObjectiveText常にnull()
        {
            var ctl = new DemoFlowController();
            ctl.LoadScenario(DemoScenarioCatalog.RecR6RescueId);
            // 全ラウンドで null（通常進行＝目的バー表示なし）
            for (int r = 1; r <= 7; r++)
                Assert.IsNull(ctl.GetObjectiveText(r), $"R{r} の ObjectiveText は null");
        }
    }
}
