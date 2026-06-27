// VSPrototypeDefeatSceneResolver の 3 分岐検証。
using NUnit.Framework;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class DefeatSceneResolverTests
    {
        [Test]
        public void Resolve_1周目敗北_EndingDefeatFirst()
        {
            // RunCount=0 のラン中はどんな状態でも First（最優先）。
            var id = VSPrototypeDefeatSceneResolver.Resolve(
                runCount: 0,
                hadFirstReachedBossAtRunStart: false,
                currentHasFirstReachedBoss: false);
            Assert.AreEqual(VSPrototypeStorySceneIds.EndingDefeatFirst, id);
        }

        [Test]
        public void Resolve_1周目で本ラン中に7R到達敗北しても_First優先()
        {
            // RunCount=0 が最優先。本ラン中に HasFirstReachedBoss が新規に立っても First のまま。
            var id = VSPrototypeDefeatSceneResolver.Resolve(
                runCount: 0,
                hadFirstReachedBossAtRunStart: false,
                currentHasFirstReachedBoss: true);
            Assert.AreEqual(VSPrototypeStorySceneIds.EndingDefeatFirst, id);
        }

        [Test]
        public void Resolve_2周目以降で本ラン中に初到達_EndingDefeatNormalClear()
        {
            // 6R 防衛達成→7R 到達→7R 敗北の流れ。スナップ false → 現在 true で振り分け。
            var id = VSPrototypeDefeatSceneResolver.Resolve(
                runCount: 1,
                hadFirstReachedBossAtRunStart: false,
                currentHasFirstReachedBoss: true);
            Assert.AreEqual(VSPrototypeStorySceneIds.EndingDefeatNormalClear, id);
        }

        [Test]
        public void Resolve_2周目以降_既到達済_本ラン6R以下敗北_Repeated()
        {
            // 過去ランで R7 到達済（スナップ true）。本ランは 6R 以下で敗北（current は不変＝true）。
            var id = VSPrototypeDefeatSceneResolver.Resolve(
                runCount: 2,
                hadFirstReachedBossAtRunStart: true,
                currentHasFirstReachedBoss: true);
            Assert.AreEqual(VSPrototypeStorySceneIds.EndingDefeatRepeated, id);
        }

        [Test]
        public void Resolve_2周目以降_未到達のまま6R以下敗北_Repeated()
        {
            // 過去ランで R7 未到達。本ランも 6R 以下で敗北（current は false のまま）。
            var id = VSPrototypeDefeatSceneResolver.Resolve(
                runCount: 3,
                hadFirstReachedBossAtRunStart: false,
                currentHasFirstReachedBoss: false);
            Assert.AreEqual(VSPrototypeStorySceneIds.EndingDefeatRepeated, id);
        }

        [Test]
        public void Resolve_2周目以降_既到達済_本ランで再度7R敗北_Repeated()
        {
            // 過去ランで R7 到達済（スナップ true）。本ランも R7 到達＋敗北（current=true）。
            // NormalClear はあくまで「本ラン中に新たに HasFirstReachedBoss が立つ」のが条件。
            var id = VSPrototypeDefeatSceneResolver.Resolve(
                runCount: 4,
                hadFirstReachedBossAtRunStart: true,
                currentHasFirstReachedBoss: true);
            Assert.AreEqual(VSPrototypeStorySceneIds.EndingDefeatRepeated, id);
        }
    }
}
