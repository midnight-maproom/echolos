using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Synergy
{
    // 属性体数 1 段階の効果定義。
    // Buffs を TargetCount 体に付与する。TargetCount=-1 は全員（SortBy 不問）。
    // SortBy は TargetCount > 0 の時のみ意味を持ち、対象を絞る基準を表す。
    public sealed class SynergyTier
    {
        public IReadOnlyList<SynergyBuff> Buffs { get; }
        public int TargetCount { get; }
        public TargetSelection SortBy { get; }

        public SynergyTier(IReadOnlyList<SynergyBuff> buffs, int targetCount, TargetSelection sortBy)
        {
            Buffs = buffs ?? Array.Empty<SynergyBuff>();
            TargetCount = targetCount;
            SortBy = sortBy;
        }

        // 効果なし段階（0/1 体段階用）。
        public static readonly SynergyTier Empty =
            new SynergyTier(Array.Empty<SynergyBuff>(), 0, TargetSelection.Default);
    }
}
