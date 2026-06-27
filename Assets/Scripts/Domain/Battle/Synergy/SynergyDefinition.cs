using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Synergy
{
    // シナジー 1 個（1 属性）の完全定義。
    // Tiers のインデックス = 自陣の発動属性体数（0..N）。索引超過は最後尾段階で頭打ち。
    public sealed class SynergyDefinition
    {
        public Element TriggerElement { get; }
        public string SourceAbilityName { get; }
        public IReadOnlyList<SynergyTier> Tiers { get; }

        public SynergyDefinition(
            Element triggerElement,
            string sourceAbilityName,
            IReadOnlyList<SynergyTier> tiers)
        {
            TriggerElement = triggerElement;
            SourceAbilityName = sourceAbilityName;
            Tiers = tiers ?? Array.Empty<SynergyTier>();
        }
    }
}
