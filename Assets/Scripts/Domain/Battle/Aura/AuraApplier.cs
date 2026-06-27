using System;
using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Aura
{
    // オーラ（固有ユニットの陣営常時バフ）を戦闘開始時に 1 回だけ味方陣営に適用する静的純関数群。
    // SynergyApplier と同じ Manager.OnBattleStart タイミングで評価され、戦闘終了まで永続。
    //
    // 動作：
    //   1. AuraDefinition.SourceUnitId が陣営内に生存していれば候補発動
    //   2. RequiredPartnerUnitIds が指定されている場合は全員生存しているかチェック（1 体でも欠ければ不発）
    //   3. SourceUnit.AppliedUpgrades から BoostUpgradeKind の Magnitude 合計を読み取り
    //   4. TargetMode に応じた配布先（陣営全員 or SourceUnit+Partners）に Persistent 付与
    //
    // 敵側は対象外（シナジー方針と同じく、敵編成はランダム抽選で偏り排除のため）。
    public static class AuraApplier
    {
        public static void ApplyAll(BattleContext context, IEnumerable<AuraDefinition> definitions)
        {
            if (context == null || definitions == null) return;
            foreach (var def in definitions)
            {
                if (def == null) continue;
                ApplySide(context.AllyUnits, def);
            }
        }

        public static void ApplySide(IList<RuntimeUnit> side, AuraDefinition def)
        {
            if (side == null || def == null || def.Buffs == null || def.Buffs.Count == 0) return;
            if (string.IsNullOrEmpty(def.SourceUnitId)) return;

            var source = FindAliveSource(side, def.SourceUnitId);
            if (source == null) return;

            // パートナー全員生存チェック（連携系・1 体でも欠ければ不発）
            var partners = ResolveRequiredPartners(side, def.RequiredPartnerUnitIds);
            if (partners == null) return;

            int boost = def.BoostUpgradeKind.HasValue
                ? SumBoost(source, def.BoostUpgradeKind.Value)
                : 0;

            var targets = ResolveTargets(side, source, partners, def.TargetMode);
            foreach (var t in targets)
            {
                if (t == null || !t.IsAlive) continue;

                foreach (var b in def.Buffs)
                {
                    int finalMagnitude = b.BaseMagnitude + boost;
                    var eff = EffectFactory.CreateByKind(b.Kind, finalMagnitude);
                    eff.Lifetime = Lifetime.Permanent;
                    eff.RemainingTurns = -1;
                    eff.IsUndispellable = true;
                    eff.MaxStacks = Math.Max(1, b.InitialStacks);
                    if (b.InitialStacks > 1) eff.Stacks = b.InitialStacks;
                    eff.SourceAbilityName = def.SourceAbilityName;
                    // AuraTracker が「死亡したユニットに紐付くオーラ」を特定するための識別子。
                    // 同名のオーラが他に存在しない前提（AuraDefinitions の SourceAbilityName を一意に保つ）。
                    eff.AuraSourceId = def.SourceAbilityName;
                    t.AddEffect(eff);
                }
            }
        }

        // RequiredPartnerUnitIds 全員の生存 RuntimeUnit を返す。1 体でも欠ければ null（＝不発）。
        // 空リスト or null なら空配列を返す（無条件発動・既存王家の加護はこちら）。
        private static List<RuntimeUnit> ResolveRequiredPartners(
            IList<RuntimeUnit> side, IReadOnlyList<string> requiredIds)
        {
            var result = new List<RuntimeUnit>();
            if (requiredIds == null || requiredIds.Count == 0) return result;

            foreach (var id in requiredIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var partner = FindAliveSource(side, id);
                if (partner == null) return null;
                result.Add(partner);
            }
            return result;
        }

        private static IEnumerable<RuntimeUnit> ResolveTargets(
            IList<RuntimeUnit> side, RuntimeUnit source, List<RuntimeUnit> partners, AuraTargetMode mode)
        {
            switch (mode)
            {
                case AuraTargetMode.SelfAndPartners:
                    var list = new List<RuntimeUnit>(1 + partners.Count) { source };
                    list.AddRange(partners);
                    return list;
                case AuraTargetMode.AllAllies:
                default:
                    return side;
            }
        }

        private static RuntimeUnit FindAliveSource(IList<RuntimeUnit> side, string unitId)
        {
            for (int i = 0; i < side.Count; i++)
            {
                var u = side[i];
                if (u == null || !u.IsAlive || u.BaseUnit == null) continue;
                if (u.BaseUnit.Id == unitId) return u;
            }
            return null;
        }

        private static int SumBoost(RuntimeUnit source, UpgradeKind boostKind)
        {
            if (source.BaseUnit == null || source.BaseUnit.AppliedUpgrades == null) return 0;
            int sum = 0;
            foreach (var up in source.BaseUnit.AppliedUpgrades)
                if (up != null && up.Kind == boostKind) sum += up.Magnitude;
            return sum;
        }
    }
}
