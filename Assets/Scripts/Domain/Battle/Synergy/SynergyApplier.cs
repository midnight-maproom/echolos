using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Synergy
{
    // シナジーを戦闘開始時に 1 回だけ味方陣営に適用する静的純関数群。
    // 生存体数をカウントし、段階に応じた SynergyBuff を Persistent で付与する。
    // 戦闘中の動的再評価は行わない（戦闘開始時に確定・戦闘終了まで永続・解除されない）。
    //
    // 敵側は対象外。敵編成はランダム抽選で組まれるため、シナジー発動の偏りを排除し
    // ATK/HP/DEF の数値だけで強度を調整できるようにする。
    public static class SynergyApplier
    {
        public static void ApplyAll(BattleContext context, IEnumerable<SynergyDefinition> definitions)
        {
            if (context == null || definitions == null) return;
            foreach (var def in definitions)
            {
                if (def == null) continue;
                ApplySide(context.AllyUnits, def);
            }
        }

        public static void ApplySide(IList<RuntimeUnit> side, SynergyDefinition def)
        {
            if (side == null || def == null || def.Tiers == null || def.Tiers.Count == 0) return;

            int count = CountAliveByElement(side, def.TriggerElement);
            int idx = count >= def.Tiers.Count ? def.Tiers.Count - 1 : count;

            var tier = def.Tiers[idx];
            if (tier == null || tier.Buffs == null || tier.Buffs.Count == 0) return;

            // 発動段階を SourceAbilityName に埋め込む（例「水の共鳴 Lv6」）。Recorder の
            // 戦闘開始集約ログがそのまま表示するため、観戦ビュー側は変更不要。
            string sourceLabel = $"{def.SourceAbilityName} Lv{count}";

            var targets = SelectTargets(side, tier.TargetCount, tier.SortBy);
            foreach (var t in targets)
            {
                foreach (var b in tier.Buffs)
                {
                    var eff = EffectFactory.CreateByKind(b.Kind, b.Magnitude);
                    eff.Lifetime = Lifetime.Permanent;
                    eff.RemainingTurns = -1;
                    eff.IsUndispellable = true;
                    eff.MaxStacks = Math.Max(1, b.InitialStacks);
                    if (b.InitialStacks > 1) eff.Stacks = b.InitialStacks;
                    eff.SourceAbilityName = sourceLabel;
                    t.AddEffect(eff);
                }
            }
        }

        private static int CountAliveByElement(IList<RuntimeUnit> side, Element element)
        {
            int n = 0;
            for (int i = 0; i < side.Count; i++)
            {
                var u = side[i];
                if (u == null || !u.IsAlive || u.BaseUnit == null) continue;
                if (u.BaseUnit.UnitElement == element) n++;
            }
            return n;
        }

        private static IEnumerable<RuntimeUnit> SelectTargets(
            IList<RuntimeUnit> side, int targetCount, TargetSelection sortBy)
        {
            var alive = side.Where(u => u != null && u.IsAlive);

            if (targetCount < 0) return alive.ToList();

            IOrderedEnumerable<RuntimeUnit> sorted;
            switch (sortBy)
            {
                case TargetSelection.HighestAtk:
                    sorted = alive.OrderByDescending(u => u.BaseUnit.BaseATK).ThenBy(u => u.SlotIndex);
                    break;
                case TargetSelection.HighestDef:
                    sorted = alive.OrderByDescending(u => u.BaseUnit.DEF).ThenBy(u => u.SlotIndex);
                    break;
                case TargetSelection.LowestHpRatio:
                    sorted = alive.OrderBy(u => HpRatio(u)).ThenBy(u => u.SlotIndex);
                    break;
                default:
                    sorted = alive.OrderBy(u => u.SlotIndex);
                    break;
            }
            return sorted.Take(targetCount).ToList();
        }

        private static double HpRatio(RuntimeUnit u)
        {
            int max = u.MaxHP > 0 ? u.MaxHP : 1;
            return (double)u.CurrentHP / max;
        }
    }
}
