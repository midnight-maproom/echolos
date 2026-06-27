// 全 Waza の WazaDefinition ファクトリ集（VSプロト 17 体が使う Waza）。
//
// 役割：
// - Waza 数値の SSoT。SO アセット生成ツールがこのリストから SO アセットを生成。
// - 初期値・新規追加は本ファイル経由、バランス調整は Editor で SO 側を編集。
//
// 数値はすべて暫定（320 §4.8.2 仕様準拠）。
using System.Collections.Generic;
using Echolos.Data.Definitions;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Data.Roster
{
    /// <summary>全 Waza の WazaDefinition ファクトリ集。</summary>
    public static class WazaRoster
    {
        // 火属性

        public static WazaDefinition AttackBasicMelee() => new WazaDefinition
        {
            Id = "waza_attack_basic_melee",
            Name = "剣撃",
            SPD = 10,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.Attack,
            WazaMultiplier = 1.0,
        };

        public static WazaDefinition DoubleStrike() => new WazaDefinition
        {
            Id = "waza_double_strike",
            Name = "双連撃",
            SPD = 10,
            HitCount = 2,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.Attack,
            WazaMultiplier = 1.2,
        };

        public static WazaDefinition PyroBlast() => new WazaDefinition
        {
            Id = "waza_pyro_blast",
            Name = "焦熱波",
            SPD = 11,
            TargetingType = TargetingType.DirectionalEnemies,
            TargetCount = 3,
            Pattern = WazaPattern.AttackWithStatusRider,
            WazaMultiplier = 0.4,
            // 熱傷：1 スタック 10% 回復低下・最大 9 スタックで -90%。Cleanse で解除可能。
            RiderEffect = EffectDefinition.CreateCleansable(
                EffectKind.SearingWound, magnitude: 10f, stacks: 1, maxStacks: 9),
        };

        public static WazaDefinition FireArrow() => new WazaDefinition
        {
            Id = "waza_fire_arrow",
            Name = "火矢",
            SPD = 12,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.AttackWithStatusRider,
            WazaMultiplier = 1.0,
            // Burn は永続蓄積モデル（仕様 §1.4.4・RemainingTurns=-1・MaxStacks 大）。
            RiderEffect = EffectDefinition.CreateCleansable(
                EffectKind.Burn, magnitude: 5f, stacks: 1, maxStacks: 99),
        };

        public static WazaDefinition ShadowStrike() => new WazaDefinition
        {
            Id = "waza_shadow_strike",
            Name = "影刺",
            SPD = 14,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.Attack,
            WazaMultiplier = 1.0,
        };

        public static WazaDefinition BattleCry() => new WazaDefinition
        {
            Id = "waza_battle_cry",
            Name = "鼓舞",
            SPD = 11,
            TargetingType = TargetingType.SingleAlly,
            TargetSelection = TargetSelection.HighestAtk,
            Pattern = WazaPattern.ApplyStatusEffect,
            // MaxStacks=3 で重ね掛け累積（最大 +30）。サポート役が時間をかけて主力を強化する設計。
            RiderEffect = EffectDefinition.CreateTriggered(
                EffectKind.AttackUp, magnitude: 10f, remainingTurns: 3, maxStacks: 3),
        };

        // 水属性

        public static WazaDefinition MistBlade() => new WazaDefinition
        {
            Id = "waza_mist_blade",
            Name = "霧刃",
            SPD = 10,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.AttackWithStatusRider,
            WazaMultiplier = 1.0,
            // MaxStacks=2 で同じ敵に集中攻撃でデバフ累積（最大 -16）。試遊で様子見。
            RiderEffect = EffectDefinition.CreateTriggered(
                EffectKind.AttackDown, magnitude: 8f, remainingTurns: 3, maxStacks: 2),
        };

        public static WazaDefinition WaterPierce() => new WazaDefinition
        {
            Id = "waza_water_pierce",
            Name = "水穿矢",
            SPD = 12,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.AttackWithStatusRider,
            WazaMultiplier = 1.0,
            // MaxStacks=2 で同じ敵に集中攻撃でデバフ累積（最大 -8）。試遊で様子見。
            RiderEffect = EffectDefinition.CreateTriggered(
                EffectKind.DefenseDown, magnitude: 4f, remainingTurns: 3, maxStacks: 2),
        };

        public static WazaDefinition DispelAura() => new WazaDefinition
        {
            Id = "waza_dispel_aura",
            Name = "看破",
            SPD = 7,
            TargetingType = TargetingType.AllEnemies,
            Pattern = WazaPattern.DispelEnemyBuffs,
        };

        public static WazaDefinition Aegis() => new WazaDefinition
        {
            Id = "waza_aegis",
            Name = "庇護",
            SPD = 11,
            TargetingType = TargetingType.SingleAlly,
            // HighestDef ＝タンクをさらに固くして盾性能を伸ばす設計意図。
            // 旧 LowestHpRatio は LesserHeal（回復）と対象選定が被るため整理（2026-06-20）。
            TargetSelection = TargetSelection.HighestDef,
            Pattern = WazaPattern.ApplyStatusEffect,
            // MaxStacks=3 で重ね掛け累積（最大 +18）。BattleCry と対称的に MaxStacks=3 で揃える。
            RiderEffect = EffectDefinition.CreateTriggered(
                EffectKind.DefenseUp, magnitude: 6f, remainingTurns: 3, maxStacks: 3),
        };

        public static WazaDefinition PurifyHeal() => new WazaDefinition
        {
            Id = "waza_purify_heal",
            Name = "浄化の癒し",
            SPD = 10,
            TargetingType = TargetingType.AllAllies,
            Pattern = WazaPattern.HealAndDispelDebuffs,
            WazaPower = 1.2,
        };

        // 光属性

        public static WazaDefinition HolyStrike() => new WazaDefinition
        {
            Id = "waza_holy_strike",
            Name = "聖打",
            SPD = 8,
            TargetingType = TargetingType.SingleEnemy,
            Pattern = WazaPattern.Attack,
            WazaMultiplier = 1.0,
        };

        public static WazaDefinition LesserHeal() => new WazaDefinition
        {
            Id = "waza_lesser_heal",
            Name = "中回復",
            SPD = 10,
            TargetingType = TargetingType.SingleAlly,
            TargetSelection = TargetSelection.LowestHpRatio,
            Pattern = WazaPattern.Heal,
            WazaPower = 3,
        };

        public static WazaDefinition RadiantJudgment() => new WazaDefinition
        {
            Id = "waza_radiant_judgment",
            Name = "聖光裁き",
            SPD = 11,
            TargetingType = TargetingType.AllEnemies,
            Pattern = WazaPattern.Attack,
            WazaMultiplier = 0.35,
        };

        // ラスボス（皇太子）専用 Waza。CD=2 ＋ InitialCD ずらしで「T1=闇剣、T2=闇波、T3=闇剣...」の交互発動を実現。

        public static WazaDefinition DarkBlade() => new WazaDefinition
        {
            Id = "waza_dark_blade",
            Name = "闇剣",
            SPD = 12,
            TargetingType = TargetingType.SingleEnemy,
            // TargetSelection は Default（最前優先・既定値）。
            // 旧戦闘エンジン時代に HighestAtk を指定していたが、新エンジンで通常近接攻撃と
            // 統一するため Default 化（2026-06-20）。
            Pattern = WazaPattern.AttackWithStatusRider,
            WazaMultiplier = 1.5,
            Cooldown = 2,
            InitialCooldown = 0,
            // MaxStacks=1 据え置き（デフォルト）：Magnitude=10 が他デバフより強めで、
            // 累積前提でない効果値設計。重ね掛けは延長のみ。
            RiderEffect = EffectDefinition.CreateTriggered(
                EffectKind.DefenseDown, magnitude: 10f, remainingTurns: 3),
        };

        public static WazaDefinition DarkWave() => new WazaDefinition
        {
            Id = "waza_dark_wave",
            Name = "闇波",
            SPD = 12,
            TargetingType = TargetingType.AllEnemies,
            Pattern = WazaPattern.Attack,
            WazaMultiplier = 0.7,
            Cooldown = 2,
            InitialCooldown = 1,
        };

        /// <summary>闇皇太子（A-c1 必敗形態）専用：全体物理＋自己 AttackUp 永続スタック。
        /// CD=0 で毎ターン発動し、ATK を青天井で積み上げる（T4 で全滅級ダメージ＝必敗演出）。
        /// 「闇のオーラ」は IsUndispellable=true なので Dispel で解除不能。</summary>
        public static WazaDefinition DarkSweep() => new WazaDefinition
        {
            Id = "waza_dark_sweep",
            Name = "闇槍の薙ぎ",
            SPD = 12,
            TargetingType = TargetingType.AllEnemies,
            Pattern = WazaPattern.AttackWithSelfStatusRider,
            WazaMultiplier = 1.0,
            Cooldown = 0,
            InitialCooldown = 0,
            IsForcedWhenReady = true,
            RiderEffect = EffectDefinition.CreatePersistent(
                EffectKind.AttackUp, magnitude: 15f,
                sourceAbilityName: "闇のオーラ", maxStacks: 999),
        };

        /// <summary>全 Waza 定義を列挙（SoAssetGenerator 用）。</summary>
        public static IEnumerable<WazaDefinition> AllWazas()
        {
            yield return AttackBasicMelee();
            yield return DoubleStrike();
            yield return PyroBlast();
            yield return FireArrow();
            yield return ShadowStrike();
            yield return BattleCry();
            yield return MistBlade();
            yield return WaterPierce();
            yield return DispelAura();
            yield return Aegis();
            yield return PurifyHeal();
            yield return HolyStrike();
            yield return LesserHeal();
            yield return RadiantJudgment();
            yield return DarkBlade();
            yield return DarkWave();
            yield return DarkSweep();
        }
    }
}
