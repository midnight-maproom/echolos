using NUnit.Framework;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class VictoryEvaluatorTests
    {
        // ── 攻め側 ──

        [Test]
        public void 攻め側_1体撃破で優勢勝利()
        {
            Assert.IsTrue(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 1, allyDeathCount: 0, isAttackingSide: true));
        }

        [Test]
        public void 攻め側_3体撃破1体被撃破でも優勢勝利()
        {
            Assert.IsTrue(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 3, allyDeathCount: 1, isAttackingSide: true));
        }

        [Test]
        public void 攻め側_0撃破は時間切れ負け()
        {
            Assert.IsFalse(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 0, allyDeathCount: 0, isAttackingSide: true));
        }

        [Test]
        public void 攻め側_0撃破1被撃破も負け()
        {
            Assert.IsFalse(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 0, allyDeathCount: 1, isAttackingSide: true));
        }

        // ── 守り側 ──

        [Test]
        public void 守り側_0被撃破で優勢勝利()
        {
            Assert.IsTrue(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 0, allyDeathCount: 0, isAttackingSide: false));
        }

        [Test]
        public void 守り側_2撃破0被撃破でも優勢勝利()
        {
            Assert.IsTrue(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 2, allyDeathCount: 0, isAttackingSide: false));
        }

        [Test]
        public void 守り側_1体でも被撃破されたら負け()
        {
            Assert.IsFalse(VictoryEvaluator.IsAdvantageousVictory(
                allyKillCount: 5, allyDeathCount: 1, isAttackingSide: false));
        }
    }
}
