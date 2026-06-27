// 味方陣営 17 体の UnitDefinition ファクトリ集（VSプロト）。
//
// 役割：
// - ユニット数値の SSoT。SO アセット生成ツールがこのリストから SO アセットを生成。
// - 初期値・新規追加は本ファイル経由、バランス調整は Editor で SO 側を編集。
//
// 数値はすべて暫定（320 §4.8.1 仕様準拠）。
//
// SortOrder：敵編成抽選後の並び順優先度（小さいほど前列）。1=メインタンク／2=サブタンク
// ／3=近接アタッカー／4=後衛支援／5=遠隔。味方陣営では手動配置なので参照されない。
using System.Collections.Generic;
using Echolos.Data.Definitions;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Data.Roster
{
    /// <summary>味方陣営 17 体の UnitDefinition ファクトリ集。</summary>
    public static class AlliesRoster
    {
        // ═══════════════════════════════════════════════
        // 火属性（7 体）
        // ═══════════════════════════════════════════════

        public static UnitDefinition Bridget() => new UnitDefinition
        {
            Id = UniqueUnitIds.Bridget, Name = "ブリジット",
            Description = "祖父から譲り受けた大剣を振るう赤髪の少女。連携（王女）：王女と同時に出撃すると両者の ATK が上昇。王家のペンダント：反撃を受けない。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            AbilityLabels = { "連携（王女）", "王家のペンダント" },
            MaxHP = 110, BaseATK = 50, DEF = 3, BaseSPD = 12,
            WazaIds = { "waza_attack_basic_melee" },
            PersistentEffects =
            {
                EffectDefinition.CreatePersistent(
                    EffectKind.IgnoreCounter, magnitude: 0f,
                    sourceAbilityName: "王家のペンダント"),
            },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition FireSwordsman() => new UnitDefinition
        {
            Id = "fire_swordsman", Name = "炎の双剣士",
            Description = "素早く 2 連撃を放つ前衛アタッカー。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            AbilityLabels = { "多段攻撃" },
            MaxHP = 100, BaseATK = 40, DEF = 3, BaseSPD = 10,
            WazaIds = { "waza_double_strike" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition FireMage() => new UnitDefinition
        {
            Id = "fire_mage", Name = "焦熱の大魔導士",
            Description = "前から 3 体の敵に火炎魔法を放ち、状態異常：熱傷（受ける回復効果を低下）を付与する遠隔アタッカー。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.Ranged, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 5,
            AbilityLabels = { "範囲攻撃", "回復低下付与" },
            MaxHP = 90, BaseATK = 40, DEF = 2, BaseSPD = 11,
            WazaIds = { "waza_pyro_blast" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition FireArcher() => new UnitDefinition
        {
            Id = "fire_archer", Name = "炎の弓兵",
            Description = "後ろの敵を攻撃する遠隔アタッカー。状態異常：燃焼（ターン終了時に継続ダメージ）を付与する。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.Ranged, TargetingDirection = TargetingDirection.FromBack,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 5,
            AbilityLabels = { "炎上付与" },
            MaxHP = 80, BaseATK = 30, DEF = 2, BaseSPD = 12,
            WazaIds = { "waza_fire_arrow" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition FireAssassin() => new UnitDefinition
        {
            Id = "fire_assassin", Name = "焔影の暗殺者",
            Description = "後ろの敵を攻撃する特殊な近接アタッカー。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromBack,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            AbilityLabels = { "暗殺", "クリ率+" },
            MaxHP = 85, BaseATK = 38, DEF = 2, BaseSPD = 14,
            WazaIds = { "waza_shadow_strike" },
            PersistentEffects =
            {
                EffectDefinition.CreatePersistent(
                    EffectKind.CriticalRateUp, magnitude: 10f,
                    sourceAbilityName: "影の業"),
            },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition FireTank() => new UnitDefinition
        {
            Id = "fire_tank", Name = "炎の大盾兵",
            Description = "防御に優れたタンク。反撃ダメージ 1.5 倍。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Tank },
            SortOrder = 1,
            AbilityLabels = { "反撃強化" },
            MaxHP = 110, BaseATK = 12, DEF = 6, BaseSPD = 6,
            WazaIds = { /* 防御フォールバック */ },
            PersistentEffects =
            {
                EffectDefinition.CreatePersistent(
                    EffectKind.CounterDamageUp, magnitude: 50f,
                    sourceAbilityName: "灼熱の反撃"),
            },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition FireBuffer() => new UnitDefinition
        {
            Id = "fire_buffer", Name = "炎の鼓舞師",
            Description = "味方に ATK バフを付与する補助ユニット。",
            UnitElement = Element.Fire,
            AttackKind = AttackKind.None, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Support },
            SortOrder = 4,
            AbilityLabels = { "ATK+ バフ" },
            MaxHP = 90, BaseATK = 0, DEF = 3, BaseSPD = 15,
            WazaIds = { "waza_battle_cry" },
            AvailableUpgradeIds = UpgradeRoster.BufferFirePreset(),
        };

        // ═══════════════════════════════════════════════
        // 水属性（6 体）
        // ═══════════════════════════════════════════════

        public static UnitDefinition WaterSwordsman() => new UnitDefinition
        {
            Id = "water_swordsman", Name = "水の剣士",
            Description = "攻撃対象の ATK を低下させる効果を持つ近接アタッカー。",
            UnitElement = Element.Water,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            AbilityLabels = { "ATK 低下付与" },
            MaxHP = 100, BaseATK = 32, DEF = 3, BaseSPD = 10,
            WazaIds = { "waza_mist_blade" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition WaterArcher() => new UnitDefinition
        {
            Id = "water_archer", Name = "水の弓兵",
            Description = "後ろの敵を攻撃する遠隔アタッカー。攻撃対象の DEF を低下させる。",
            UnitElement = Element.Water,
            AttackKind = AttackKind.Ranged, TargetingDirection = TargetingDirection.FromBack,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 5,
            AbilityLabels = { "DEF 低下付与" },
            MaxHP = 80, BaseATK = 30, DEF = 2, BaseSPD = 12,
            WazaIds = { "waza_water_pierce" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition WaterTank() => new UnitDefinition
        {
            Id = "water_tank", Name = "水の大盾兵",
            Description = "防御に優れたタンク。専守：被ダメ -30% ただし反撃不可。",
            UnitElement = Element.Water,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Tank },
            SortOrder = 1,
            AbilityLabels = { "専守" },
            MaxHP = 140, BaseATK = 0, DEF = 8, BaseSPD = 5,
            WazaIds = { /* 防御フォールバック */ },
            PersistentEffects =
            {
                EffectDefinition.CreatePersistent(
                    EffectKind.SilencedCounter, magnitude: 0f,
                    sourceAbilityName: "専守"),
                EffectDefinition.CreatePersistent(
                    EffectKind.IncomingDamageDown, magnitude: 30f,
                    sourceAbilityName: "専守"),
            },
            AvailableUpgradeIds = UpgradeRoster.WaterShieldPreset(),
        };

        public static UnitDefinition WaterDispelTank() => new UnitDefinition
        {
            Id = "water_dispel_tank", Name = "水鏡の幻盾兵",
            Description = "水の力で守られたタンク。敵のバフを解除する補助スキルを持つ。",
            UnitElement = Element.Water,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Tank, UnitRole.Support },
            SortOrder = 1,
            AbilityLabels = { "敵バフ解除", "遠隔回避" },
            MaxHP = 90, BaseATK = 12, DEF = 8, BaseSPD = 7,
            WazaIds = { "waza_dispel_aura" },
            PersistentEffects =
            {
                EffectDefinition.CreatePersistent(
                    EffectKind.EvasionUp, magnitude: 10f,
                    sourceAbilityName: "幻惑の盾"),
            },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition WaterBuffer() => new UnitDefinition
        {
            Id = "water_buffer", Name = "水の護術師",
            Description = "味方に DEF バフを付与する補助ユニット。",
            UnitElement = Element.Water,
            AttackKind = AttackKind.None, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Support },
            SortOrder = 4,
            AbilityLabels = { "DEF+ バフ" },
            MaxHP = 90, BaseATK = 0, DEF = 4, BaseSPD = 15,
            WazaIds = { "waza_aegis" },
            AvailableUpgradeIds = UpgradeRoster.BufferWaterPreset(),
        };

        public static UnitDefinition WaterHealer() => new UnitDefinition
        {
            Id = "water_healer", Name = "癒水の巫女",
            Description = "味方全員を回復しつつ能力デバフを解除するレア回復役。",
            UnitElement = Element.Water,
            AttackKind = AttackKind.None, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Healer },
            SortOrder = 4,
            AbilityLabels = { "全体回復", "デバフ解除" },
            MaxHP = 95, BaseATK = 0, DEF = 3, BaseSPD = 10,
            WazaIds = { "waza_purify_heal" },
            AvailableUpgradeIds = UpgradeRoster.HealerWaterPreset(),
        };

        // ═══════════════════════════════════════════════
        // 光属性（4 体）
        // ═══════════════════════════════════════════════

        public static UnitDefinition Princess() => new UnitDefinition
        {
            Id = UniqueUnitIds.Princess, Name = "王女",
            Description = "亡国の王女。前衛アタッカー。王家の加護：すべての味方に常時 DEF +3 を付与する。",
            UnitElement = Element.Light,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            AbilityLabels = { "王家の加護" },
            MaxHP = 110, BaseATK = 44, DEF = 3, BaseSPD = 11,
            WazaIds = { "waza_attack_basic_melee" },
            AvailableUpgradeIds = UpgradeRoster.PrincessPreset(),
        };

        public static UnitDefinition LightPaladin() => new UnitDefinition
        {
            Id = "light_paladin", Name = "光の騎士",
            Description = "サブタンク兼アタッカー。攻撃も耐久もこなす万能型。",
            UnitElement = Element.Light,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Tank, UnitRole.Attacker },
            SortOrder = 2,
            MaxHP = 120, BaseATK = 30, DEF = 6, BaseSPD = 8,
            WazaIds = { "waza_holy_strike" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        public static UnitDefinition LightPriest() => new UnitDefinition
        {
            Id = "light_priest", Name = "光の司祭",
            Description = "味方単体を回復する標準的なヒーラー。",
            UnitElement = Element.Light,
            AttackKind = AttackKind.None, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Healer },
            SortOrder = 4,
            AbilityLabels = { "単体回復" },
            MaxHP = 60, BaseATK = 0, DEF = 1, BaseSPD = 10,
            WazaIds = { "waza_lesser_heal" },
            AvailableUpgradeIds = UpgradeRoster.HealerLightPreset(),
        };

        public static UnitDefinition LightMage() => new UnitDefinition
        {
            Id = "light_mage", Name = "暁光の大魔導士",
            Description = "敵全体に光魔法を放つ範囲アタッカー。",
            UnitElement = Element.Light,
            AttackKind = AttackKind.Ranged, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Back,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 5,
            AbilityLabels = { "全体魔法" },
            MaxHP = 85, BaseATK = 35, DEF = 2, BaseSPD = 11,
            WazaIds = { "waza_radiant_judgment" },
            AvailableUpgradeIds = UpgradeRoster.AttackerTankPreset(),
        };

        // ═══════════════════════════════════════════════
        // 列挙ヘルパ
        // ═══════════════════════════════════════════════

        /// <summary>全 17 体の UnitDefinition を列挙（SoAssetGenerator 用）。</summary>
        public static IEnumerable<UnitDefinition> AllUnits()
        {
            yield return Bridget();
            yield return FireSwordsman();
            yield return FireMage();
            yield return FireArcher();
            yield return FireAssassin();
            yield return FireTank();
            yield return FireBuffer();
            yield return WaterSwordsman();
            yield return WaterArcher();
            yield return WaterTank();
            yield return WaterDispelTank();
            yield return WaterBuffer();
            yield return WaterHealer();
            yield return Princess();
            yield return LightPaladin();
            yield return LightPriest();
            yield return LightMage();
        }
    }
}
