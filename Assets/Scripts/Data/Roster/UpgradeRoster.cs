// Lv 強化選択肢の UnitUpgradeDefinition ファクトリ集（VSプロト）。
//
// 役割：
// - 強化定義の SSoT。SO アセット生成ツールがこのリストから Resources/Data/Upgrades/{id}.asset を生成。
// - 各 UnitDefinition.AvailableUpgradeIds はここで定義した ID を 3 件指定する。
//
// 仮置きの 4 種類：
// - up_atk_plus_5  ATK +5
// - up_def_plus_3  DEF +3
// - up_hp_plus_20  HP +20
// - up_eva_plus_5  EVA +5
//
// バランス調整段階で兵種ごと固有の Upgrade（WazaPowerBoost 含む）を追加する余地は残す。
using System.Collections.Generic;
using Echolos.Data.Definitions;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Data.Roster
{
    /// <summary>Lv 強化選択肢の UnitUpgradeDefinition ファクトリ集。</summary>
    public static class UpgradeRoster
    {
        public const string AtkPlus5Id = "up_atk_plus_5";
        public const string DefPlus3Id = "up_def_plus_3";
        public const string HpPlus20Id = "up_hp_plus_20";
        public const string AuraGuardPlus2Id = "up_aura_guard_plus_2";

        // 新 5 種（PersistentEffectBoost / WazaPowerBoost 機構を活用した固有スキル強化）
        public const string WaterShieldGuardPlus10Id = "up_water_shield_guard_plus_10";
        public const string BattleCryPlus5Id = "up_battle_cry_plus_5";
        public const string AegisPlus3Id = "up_aegis_plus_3";
        public const string PurifyHealPlus1Id = "up_purify_heal_plus_1";
        public const string LesserHealPlus1Id = "up_lesser_heal_plus_1";

        public static UnitUpgradeDefinition AtkPlus5() => new UnitUpgradeDefinition
        {
            Id = AtkPlus5Id,
            Name = "攻撃 +5",
            Description = "基礎 ATK を +5 する。",
            Kind = UpgradeKind.AtkBoost,
            Magnitude = 5,
        };

        public static UnitUpgradeDefinition DefPlus3() => new UnitUpgradeDefinition
        {
            Id = DefPlus3Id,
            Name = "防御 +3",
            Description = "DEF を +3 する。",
            Kind = UpgradeKind.DefBoost,
            Magnitude = 3,
        };

        public static UnitUpgradeDefinition HpPlus20() => new UnitUpgradeDefinition
        {
            Id = HpPlus20Id,
            Name = "体力 +20",
            Description = "MaxHP を +20 する。",
            Kind = UpgradeKind.HpBoost,
            Magnitude = 20,
        };

        /// <summary>王女専用：味方全体に付与される「王家の加護」DEF バフ量を +2 する。AuraApplier が SourceUnit の AppliedUpgrades から合計を読み取って基本値に加算。</summary>
        public static UnitUpgradeDefinition AuraGuardPlus2() => new UnitUpgradeDefinition
        {
            Id = AuraGuardPlus2Id,
            Name = "王家の加護 +2",
            Description = "味方全体に付与される DEF バフ量を +2 する。",
            Kind = UpgradeKind.AuraBoost,
            Magnitude = 2,
        };

        /// <summary>水の大盾兵専用：パッシブ「専守」の被ダメ -% 効果を +10% する（30%→40%）。</summary>
        public static UnitUpgradeDefinition WaterShieldGuardPlus10() => new UnitUpgradeDefinition
        {
            Id = WaterShieldGuardPlus10Id,
            Name = "専守 +10%",
            Description = "パッシブ「専守」の被ダメージ軽減を +10% する。",
            Kind = UpgradeKind.PersistentEffectBoost,
            Magnitude = 10,
            TargetSourceAbilityName = "専守",
            TargetEffectKind = EffectKind.IncomingDamageDown,
        };

        /// <summary>炎の鼓舞師専用：「鼓舞」の ATK バフ量を +5 する（10→15）。</summary>
        public static UnitUpgradeDefinition BattleCryPlus5() => new UnitUpgradeDefinition
        {
            Id = BattleCryPlus5Id,
            Name = "鼓舞 +5",
            Description = "「鼓舞」の ATK バフ量を +5 する。",
            Kind = UpgradeKind.WazaPowerBoost,
            Magnitude = 5,
            TargetWazaId = "waza_battle_cry",
        };

        /// <summary>水の護術師専用：「庇護」の DEF バフ量を +3 する（6→9）。</summary>
        public static UnitUpgradeDefinition AegisPlus3() => new UnitUpgradeDefinition
        {
            Id = AegisPlus3Id,
            Name = "庇護 +3",
            Description = "「庇護」の DEF バフ量を +3 する。",
            Kind = UpgradeKind.WazaPowerBoost,
            Magnitude = 3,
            TargetWazaId = "waza_aegis",
        };

        /// <summary>癒水の巫女専用：「浄化の癒し」の wazaPower を +1 する（1.2→2.2）。</summary>
        public static UnitUpgradeDefinition PurifyHealPlus1() => new UnitUpgradeDefinition
        {
            Id = PurifyHealPlus1Id,
            Name = "浄化の癒し +1",
            Description = "「浄化の癒し」の回復量倍率を +1 する。",
            Kind = UpgradeKind.WazaPowerBoost,
            Magnitude = 1,
            TargetWazaId = "waza_purify_heal",
        };

        /// <summary>光の司祭専用：「中回復」の wazaPower を +1 する（3→4）。</summary>
        public static UnitUpgradeDefinition LesserHealPlus1() => new UnitUpgradeDefinition
        {
            Id = LesserHealPlus1Id,
            Name = "中回復 +1",
            Description = "「中回復」の回復量倍率を +1 する。",
            Kind = UpgradeKind.WazaPowerBoost,
            Magnitude = 1,
            TargetWazaId = "waza_lesser_heal",
        };

        /// <summary>全 Upgrade 定義を列挙（SoAssetGenerator 用）。</summary>
        public static IEnumerable<UnitUpgradeDefinition> AllUpgrades()
        {
            yield return AtkPlus5();
            yield return DefPlus3();
            yield return HpPlus20();
            yield return AuraGuardPlus2();
            yield return WaterShieldGuardPlus10();
            yield return BattleCryPlus5();
            yield return AegisPlus3();
            yield return PurifyHealPlus1();
            yield return LesserHealPlus1();
        }

        // ──────────────────────────────────────────────
        // 兵種ごとの 3 択プリセット
        //
        // 順序ルール（敵 Lv 強化は AvailableUpgrades 先頭から順に適用＝Lv2 で 0、Lv3 で 0+1）：
        // - 固有スキル持ち Preset：固有スキル → 第 2 ステ → HP の順（敵 Lv2 で固有適用、Lv3 で +ステ）
        // - 固有スキルなし AttackerTankPreset：ATK → DEF → HP の順
        // - HP +20 を順序 2 に置く＝敵に乗らない設計（プレイヤーの選択枠としてのみ機能）
        // ──────────────────────────────────────────────

        /// <summary>アタッカー／タンク共通 3 択（王女・水の大盾兵以外）：ATK +5 / DEF +3 / HP +20</summary>
        public static List<string> AttackerTankPreset() => new List<string>
        {
            AtkPlus5Id, DefPlus3Id, HpPlus20Id,
        };

        /// <summary>水の大盾兵専用：専守 +10% / DEF +3 / HP +20（攻撃 0 のため ATK 強化なし）</summary>
        public static List<string> WaterShieldPreset() => new List<string>
        {
            WaterShieldGuardPlus10Id, DefPlus3Id, HpPlus20Id,
        };

        /// <summary>癒水の巫女専用：浄化の癒し +1 / DEF +3 / HP +20</summary>
        public static List<string> HealerWaterPreset() => new List<string>
        {
            PurifyHealPlus1Id, DefPlus3Id, HpPlus20Id,
        };

        /// <summary>光の司祭専用：中回復 +1 / DEF +3 / HP +20</summary>
        public static List<string> HealerLightPreset() => new List<string>
        {
            LesserHealPlus1Id, DefPlus3Id, HpPlus20Id,
        };

        /// <summary>炎の鼓舞師専用：鼓舞 +5 / DEF +3 / HP +20</summary>
        public static List<string> BufferFirePreset() => new List<string>
        {
            BattleCryPlus5Id, DefPlus3Id, HpPlus20Id,
        };

        /// <summary>水の護術師専用：庇護 +3 / DEF +3 / HP +20</summary>
        public static List<string> BufferWaterPreset() => new List<string>
        {
            AegisPlus3Id, DefPlus3Id, HpPlus20Id,
        };

        /// <summary>王女専用：王家の加護 +2 / ATK +5 / HP +20（敵側に王女はいないので順序適用なし）</summary>
        public static List<string> PrincessPreset() => new List<string>
        {
            AuraGuardPlus2Id, AtkPlus5Id, HpPlus20Id,
        };
    }
}
