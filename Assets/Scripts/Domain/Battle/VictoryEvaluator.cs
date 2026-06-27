namespace Echolos.Domain.Battle
{
    // ターン制限到達時の優勢勝利条件を判定する純関数。
    // 攻め側：1 体でも敵を撃破していれば優勢勝利（時間切れの持久戦逃げを構造防止）。
    // 守り側：1 体も味方を撃破されていなければ優勢勝利（防衛の価値を明確化）。
    public static class VictoryEvaluator
    {
        public static bool IsAdvantageousVictory(int allyKillCount, int allyDeathCount, bool isAttackingSide)
        {
            if (isAttackingSide) return allyKillCount > 0;
            return allyDeathCount == 0;
        }
    }
}
