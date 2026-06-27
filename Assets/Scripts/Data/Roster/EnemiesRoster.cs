// 帝国軍 11 体の UnitDefinition ファクトリ集（VSプロト）。
//
// 役割：
// - 通常プール用 10 体（味方コピーだが Name は共用化＋ Element=None）。
//   同名異 ID で「同じ見た目のユニットが行動だけ違って見える」（火大盾兵は反撃強化／水大盾兵は専守 等）。
// - 皇太子戦専用 1 体（imperial_fire_mage = 帝国大魔導士）。
//
// 命名規則：
// - Name：「帝国剣士／帝国弓兵／帝国大盾兵／帝国補助兵／帝国騎士／帝国司祭」の 6 種で 10 体をカバー。
//   皇太子戦の魔導士のみ「帝国大魔導士」で別名（味方の焦熱の大魔導士／暁光の大魔導士のレア相当に揃える）。
// - Element：VSプロトの地形効果はすべて Neutral 固定で属性差別なし、シナジーも味方陣営限定発動なので、
//   敵側は一律 Element.None。能力差（反撃強化／専守等）は PersistentEffect で残る。
using System;
using System.Collections.Generic;
using Echolos.Data.Definitions;
using Echolos.Domain.Models;

namespace Echolos.Data.Roster
{
    /// <summary>帝国軍 11 体の UnitDefinition ファクトリ集（味方コピー＋ Name/Element 上書き）。</summary>
    public static class EnemiesRoster
    {
        private const string ImperialIdPrefix = "imperial_";

        // 通常プール用 10 体（同名異 ID で行動だけ違って見える設計）

        public static UnitDefinition FireSwordsman()    => AsImperial(AlliesRoster.FireSwordsman(),  "帝国剣士");
        public static UnitDefinition WaterSwordsman()   => AsImperial(AlliesRoster.WaterSwordsman(), "帝国剣士");
        public static UnitDefinition FireArcher()       => AsImperial(AlliesRoster.FireArcher(),     "帝国弓兵");
        public static UnitDefinition WaterArcher()      => AsImperial(AlliesRoster.WaterArcher(),    "帝国弓兵");
        public static UnitDefinition FireTank()         => AsImperial(AlliesRoster.FireTank(),       "帝国大盾兵");
        public static UnitDefinition WaterTank()        => AsImperial(AlliesRoster.WaterTank(),      "帝国大盾兵");
        public static UnitDefinition FireBuffer()       => AsImperial(AlliesRoster.FireBuffer(),     "帝国補助兵");
        public static UnitDefinition WaterBuffer()      => AsImperial(AlliesRoster.WaterBuffer(),    "帝国補助兵");
        public static UnitDefinition LightPaladin()     => AsImperial(AlliesRoster.LightPaladin(),   "帝国騎士");
        public static UnitDefinition LightPriest()      => AsImperial(AlliesRoster.LightPriest(),    "帝国司祭");

        // 皇太子戦専用 1 体（通常プール非使用・レア相当）

        public static UnitDefinition FireMage()         => AsImperial(AlliesRoster.FireMage(),       "帝国大魔導士");

        /// <summary>全 11 体の UnitDefinition を列挙（SoAssetGenerator 用）。</summary>
        public static IEnumerable<UnitDefinition> AllUnits()
        {
            yield return FireSwordsman();
            yield return WaterSwordsman();
            yield return FireArcher();
            yield return WaterArcher();
            yield return FireTank();
            yield return WaterTank();
            yield return FireBuffer();
            yield return WaterBuffer();
            yield return LightPaladin();
            yield return LightPriest();
            yield return FireMage();
        }

        // 味方 UnitDefinition を浅くコピーして Id にプレフィックスを付け、Name/Element を上書きする。
        // PersistentEffects は Clone で深いコピー（List/参照共有破壊を防ぐ）。
        private static UnitDefinition AsImperial(UnitDefinition allySrc, string nameOverride)
        {
            if (allySrc == null) throw new ArgumentNullException(nameof(allySrc));
            var dst = new UnitDefinition
            {
                Id = ImperialIdPrefix + allySrc.Id,
                Name = nameOverride,
                Description = allySrc.Description,
                UnitElement = Element.None,
                AttackKind = allySrc.AttackKind,
                TargetingDirection = allySrc.TargetingDirection,
                PlacementHint = allySrc.PlacementHint,
                MaxHP = allySrc.MaxHP,
                BaseATK = allySrc.BaseATK,
                DEF = allySrc.DEF,
                BaseSPD = allySrc.BaseSPD,
                BaseParalysisTolerance = allySrc.BaseParalysisTolerance,
                SortOrder = allySrc.SortOrder,
            };
            if (allySrc.CombatRoles != null) dst.CombatRoles.AddRange(allySrc.CombatRoles);
            if (allySrc.AbilityLabels != null) dst.AbilityLabels.AddRange(allySrc.AbilityLabels);
            if (allySrc.Tags != null) dst.Tags.AddRange(allySrc.Tags);
            if (allySrc.WazaIds != null) dst.WazaIds.AddRange(allySrc.WazaIds);
            if (allySrc.PersistentEffects != null)
                foreach (var e in allySrc.PersistentEffects)
                    if (e != null) dst.PersistentEffects.Add(e.Clone());
            if (allySrc.ImmunityKinds != null) dst.ImmunityKinds.AddRange(allySrc.ImmunityKinds);
            if (allySrc.AvailableUpgradeIds != null) dst.AvailableUpgradeIds.AddRange(allySrc.AvailableUpgradeIds);
            return dst;
        }
    }
}
