// メタ通貨「王国の記憶」獲得式へ渡すラン結果コンテキスト。
// MetaRewardFormulaRegistry の入力 struct として共有する。
using System;

namespace Echolos.Domain.Formula
{
    /// <summary>メタ通貨獲得量の計算に必要なラン結果コンテキスト。</summary>
    public readonly struct MetaRewardContext
    {
        /// <summary>このランで完了したラウンド数（1〜7）。</summary>
        public int RoundsCompleted { get; }

        /// <summary>R7 ボス戦に到達したか（R7 開始時点で true）。</summary>
        public bool ReachedBossRound { get; }

        /// <summary>このランでブリジット（バルドゥイン拠点）を救出したか。</summary>
        public bool BridgetRescued { get; }

        /// <summary>R7 ボス戦に勝利したか（ビターエンド／トゥルーエンドのいずれか）。</summary>
        public bool BossDefeated { get; }

        /// <summary>確定エンディングがトゥルーエンドか。</summary>
        public bool TrueEnd { get; }

        public MetaRewardContext(
            int roundsCompleted,
            bool reachedBossRound,
            bool bridgetRescued,
            bool bossDefeated,
            bool trueEnd)
        {
            if (roundsCompleted < 0)
                throw new ArgumentOutOfRangeException(nameof(roundsCompleted));
            RoundsCompleted = roundsCompleted;
            ReachedBossRound = reachedBossRound;
            BridgetRescued = bridgetRescued;
            BossDefeated = bossDefeated;
            TrueEnd = trueEnd;
        }
    }
}
