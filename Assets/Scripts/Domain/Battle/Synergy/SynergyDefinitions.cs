using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Synergy
{
    // プロト 3 属性（火 / 水 / 光）のシナジー定義集約。
    // 数値は暫定で、バランス調整フェーズで再調整する。
    public static class SynergyDefinitions
    {
        public static readonly SynergyDefinition Fire = new SynergyDefinition(
            Element.Fire,
            "炎の共鳴",
            new[]
            {
                SynergyTier.Empty,                                  // 0 体
                SynergyTier.Empty,                                  // 1 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.OutgoingDamageUp, 5) },  1, TargetSelection.HighestAtk), // 2 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.OutgoingDamageUp, 10) }, 1, TargetSelection.HighestAtk), // 3 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.OutgoingDamageUp, 20) }, 2, TargetSelection.HighestAtk), // 4 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.OutgoingDamageUp, 45) }, 2, TargetSelection.HighestAtk), // 5 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.OutgoingDamageUp, 70) }, 2, TargetSelection.HighestAtk), // 6 体
            });

        public static readonly SynergyDefinition Water = new SynergyDefinition(
            Element.Water,
            "水の共鳴",
            new[]
            {
                SynergyTier.Empty,                                  // 0 体
                SynergyTier.Empty,                                  // 1 体
                new SynergyTier(new[]
                {
                    new SynergyBuff(EffectKind.DefenseUp, 10),
                }, -1, TargetSelection.Default),                    // 2 体
                new SynergyTier(new[]
                {
                    new SynergyBuff(EffectKind.DefenseUp, 15),
                }, -1, TargetSelection.Default),                    // 3 体
                new SynergyTier(new[]
                {
                    new SynergyBuff(EffectKind.DefenseUp, 20),
                    new SynergyBuff(EffectKind.Shield, 0, initialStacks: 1),
                }, -1, TargetSelection.Default),                    // 4 体
                new SynergyTier(new[]
                {
                    new SynergyBuff(EffectKind.DefenseUp, 25),
                    new SynergyBuff(EffectKind.Shield, 0, initialStacks: 2),
                }, -1, TargetSelection.Default),                    // 5 体
                new SynergyTier(new[]
                {
                    new SynergyBuff(EffectKind.DefenseUp, 30),
                    new SynergyBuff(EffectKind.Shield, 0, initialStacks: 3),
                }, -1, TargetSelection.Default),                    // 6 体
            });

        public static readonly SynergyDefinition Light = new SynergyDefinition(
            Element.Light,
            "光の共鳴",
            new[]
            {
                SynergyTier.Empty,                                  // 0 体
                SynergyTier.Empty,                                  // 1 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.HealOverTime, 3) }, -1, TargetSelection.Default), // 2 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.HealOverTime, 5) }, -1, TargetSelection.Default), // 3 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.HealOverTime, 8) }, -1, TargetSelection.Default), // 4 体
                new SynergyTier(new[] { new SynergyBuff(EffectKind.HealOverTime, 8) }, -1, TargetSelection.Default), // 5 体（プロト範囲外・保険）
                new SynergyTier(new[] { new SynergyBuff(EffectKind.HealOverTime, 8) }, -1, TargetSelection.Default), // 6 体（プロト範囲外・保険）
            });

        public static readonly IReadOnlyList<SynergyDefinition> All =
            new[] { Fire, Water, Light };
    }
}
