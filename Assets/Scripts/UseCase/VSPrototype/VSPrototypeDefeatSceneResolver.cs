// Defeat エンディング 3 分割の振り分けロジック（純関数）。
//
// 【3 分岐の決定ロジック】
//   if  Meta.RunCount == 0                                   → ending_defeat_first
//   elif !hadFirstReachedBossAtRunStart && HasFirstReachedBoss → ending_defeat_normal_clear
//   else                                                     → ending_defeat_repeated
//
// 【スナップショット引数 hadFirstReachedBossAtRunStart】
//   ラン開始時点の Meta.HasFirstReachedBoss を Bootstrap が保持し、ラン終了時に渡す。
//   「本ラン中に新たに HasFirstReachedBoss が立ったか」を判定するため。
//   Meta.MarkFirstReachedBoss() は R7 敗北を確定した瞬間に呼ぶ前提（Bootstrap.EnterEndingEvent）。
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>Defeat エンディング 3 分割の振り分け純関数。</summary>
    public static class VSPrototypeDefeatSceneResolver
    {
        /// <summary>
        /// Defeat 演出のシーン ID を決定する。
        /// </summary>
        /// <param name="runCount">ラン開始時点（=演出時点でまだ FinishRun していない）の Meta.RunCount。</param>
        /// <param name="hadFirstReachedBossAtRunStart">本ラン開始時の Meta.HasFirstReachedBoss スナップショット。</param>
        /// <param name="currentHasFirstReachedBoss">現在の Meta.HasFirstReachedBoss（R7 敗北で立った直後の値）。</param>
        public static string Resolve(
            int runCount,
            bool hadFirstReachedBossAtRunStart,
            bool currentHasFirstReachedBoss)
        {
            if (runCount == 0) return VSPrototypeStorySceneIds.EndingDefeatFirst;
            if (!hadFirstReachedBossAtRunStart && currentHasFirstReachedBoss)
                return VSPrototypeStorySceneIds.EndingDefeatNormalClear;
            return VSPrototypeStorySceneIds.EndingDefeatRepeated;
        }
    }
}
