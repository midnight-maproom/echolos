using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Conditional;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Prototype
{
    /// <summary>中ボス／ラスボス（敵側）のファクトリ。</summary>
    public static class BossesRoster
    {
        // 中ボス（6R 中ボス枠）

        /// <summary>どくどく男爵：後列/遠隔・毒（燃焼流用＝蓄積型固定ダメージ）付与の全体攻撃。
        /// パッシブ AntiHealPassive 持ち：生存中、味方（プレイヤー）が受ける回復を1/3に減衰する耐久つぶしのギミックボス。</summary>
        public static Unit PoisonBaron()
        {
            var u = RosterHelpers.Make("boss_baron", "どくどく男爵", Element.Dark,
                hp: 200, atk: 24, pdef: 6, mdef: 8, spd: 6, range: AttackRange.Ranged,
                tags: new[] { BattleManager.AntiHealPassiveTag });

            var poison = RosterHelpers.Magic("baron_miasma", "毒霧", Element.Dark, spd: 6, mult: 0.4f,
                targeting: TargetingType.AllEnemies,
                riders: new List<StatusEffect>
                {
                    StatusEffect.CreateStatusAilment(StatusEffectType.Burn,
                        stacks: 1, maxStacks: 5, magnitude: 2),
                });
            poison.Cooldown = 1;
            poison.IsForcedWhenReady = true;
            u.BaseWazas.Add(poison);

            u.BaseWazas.Add(RosterHelpers.Magic("baron_bolt", "瘴気弾", Element.Dark, spd: 6, mult: 0.5f));
            return u;
        }

        /// <summary>隻眼のサムライ：前列/近接・高火力3回攻撃・3の倍数ターンで薙ぎ払い（前列全体＋物防デバフ上限1・優先発動）・防御割合無視・状態異常無効。</summary>
        public static Unit OneEyedSamurai()
        {
            var u = RosterHelpers.Make("boss_one_eyed_samurai", "隻眼のサムライ", Element.None,
                hp: 240, atk: 34, pdef: 10, mdef: 6, spd: 8, range: AttackRange.Melee,
                immune: true);

            u.BaseWazas.Add(RosterHelpers.Phys("sam_triple", "三段斬り", spd: 8, mult: 0.7f, hits: 3, defIgnore: 0.5f));

            var sweep = RosterHelpers.Phys("sam_true_sweep", "真・薙ぎ払い", spd: 8, mult: 1.0f, defIgnore: 0.5f,
                targeting: TargetingType.FrontRowEnemies);
            sweep.Cooldown = 3;
            sweep.InitialCooldown = 2;
            sweep.IsForcedWhenReady = true;
            u.BaseWazas.Add(sweep);
            return u;
        }

        // 中ボス想定編成

        /// <summary>どくどく男爵の想定編成：前列 DEFタンク×2／大槌兵、後列 DEFタンク／男爵／巫女。</summary>
        public static List<RuntimeUnit> PoisonBaronParty()
        {
            return new List<RuntimeUnit>
            {
                new RuntimeUnit(AlliesRoster.GeneralTank(), 0),
                new RuntimeUnit(AlliesRoster.GeneralTank(), 1),
                new RuntimeUnit(AlliesRoster.Debuffer(),    2),
                new RuntimeUnit(AlliesRoster.GeneralTank(), 3),
                new RuntimeUnit(PoisonBaron(),              4),
                new RuntimeUnit(AlliesRoster.Healer2(),     5),
            };
        }

        /// <summary>隻眼のサムライの想定編成：前列 DEFタンク／隻眼サムライ／サムライ、後列 帝国の影×3。</summary>
        public static List<RuntimeUnit> SamuraiParty()
        {
            return new List<RuntimeUnit>
            {
                new RuntimeUnit(AlliesRoster.GeneralTank(),     0),
                new RuntimeUnit(OneEyedSamurai(),               1),
                new RuntimeUnit(AlliesRoster.Samurai(),         2),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(), 3),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(), 4),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(), 5),
            };
        }

        // ラスボス（皇太子・R7）

        /// <summary>皇太子（闇）：必敗形態。漆黒の愛馬にまたがりランスを構える、闇に染まった皇太子。
        /// 物理／魔法防御ともに 999（実質無敵）・状態異常無効・通常攻撃なし。
        /// 毎ターン「闇槍の薙ぎ」で全体物理攻撃を撃ちつつ、3T 経過ごとに自己 AttackUp が +15 スタックする
        /// （実体は <see cref="PrinceDarkAuraConditionalProcessor"/>・<see cref="PrinceDarkAuraConditionalProcessor.DarkAuraTag"/> で識別・青天井）。
        /// バフのスタックが 3 になる頃には全滅する想定で必敗を演出する。</summary>
        public static Unit PrinceDark()
        {
            var u = RosterHelpers.Make("boss_prince_dark", "皇太子", Element.Dark,
                hp: 9999, atk: 30, pdef: 999, mdef: 999, spd: 10, range: AttackRange.Melee,
                immune: true,
                tags: new[] { PrinceDarkAuraConditionalProcessor.DarkAuraTag });

            // 全体物理攻撃（毎ターン・CD なし）
            var sweep = RosterHelpers.Phys("prince_dark_sweep", "闇槍の薙ぎ", spd: 10, mult: 1.0f,
                targeting: TargetingType.AllEnemies);
            u.BaseWazas.Add(sweep);

            // 「闇のオーラ」は Waza ではなく PrinceDarkAuraConditionalProcessor で
            // ターン経過連動の動的 AttackUp として管理する。
            return u;
        }

        /// <summary>皇太子（浄化）：最強形態。闇が祓われ正気を取り戻した皇太子。
        /// 漆黒の愛馬にまたがりランスを構え、正々堂々と王国軍を迎え撃つ。
        /// 全ボス中最強性能。3 行動サイクル（CD=3 互い違い）：
        ///   T1：「鼓舞」味方全体 AttackUp +20（3T 持続）
        ///   T2：「破邪の一撃」敵全体物理（mult 0.7）＋ DispelsBuffs ＋ DefenseDown +30（2T）
        ///   T3：「審判」敵全体物理（mult 1.2）
        /// 状態異常無効。通常攻撃なし（3 行動でターン埋まる）。</summary>
        public static Unit PrinceLight()
        {
            var u = RosterHelpers.Make("boss_prince_light", "皇太子", Element.Light,
                hp: 400, atk: 40, pdef: 12, mdef: 12, spd: 10, range: AttackRange.Melee,
                immune: true);

            // T1：味方全体 AttackUp（CD3・初期 CD0）
            var rally = new Waza("prince_rally", "鼓舞")
            {
                Category = WazaCategory.Buff,
                TargetingType = TargetingType.AllAllies,
                SPD = 10,
                Cooldown = 3,
                InitialCooldown = 0,
                IsForcedWhenReady = true,
                AppliedEffects = new List<StatusEffect>
                {
                    new StatusEffect(StatusEffectType.AttackUp, stacks: 1, remainingTurns: 3)
                    { Magnitude = 20, MaxStacks = 1 }
                }
            };
            u.BaseWazas.Add(rally);

            // T2：敵全体バフ解除＋防御デバフ大の通常攻撃（CD3・初期 CD1）
            var purgeStrike = new Waza("prince_purge_strike", "破邪の一撃")
            {
                Category = WazaCategory.Attack,
                IsPhysical = true,
                WazaElement = Element.Light,
                SPD = 10,
                Cooldown = 3,
                InitialCooldown = 1,
                IsForcedWhenReady = true,
                TargetingType = TargetingType.AllEnemies,
                DispelsBuffs = true,
                CalculateBaseDamage = (a, t) => (int)(a.EffectiveATK * 0.7f),
                AppliedEffects = new List<StatusEffect>
                {
                    new StatusEffect(StatusEffectType.DefenseDown, stacks: 1, remainingTurns: 2)
                    { Magnitude = 30, MaxStacks = 1 }
                }
            };
            u.BaseWazas.Add(purgeStrike);

            // T3：敵全体物理大ダメージ（CD3・初期 CD2）
            var judgement = RosterHelpers.Phys("prince_judgement", "審判", spd: 10, mult: 1.2f,
                targeting: TargetingType.AllEnemies);
            judgement.Cooldown = 3;
            judgement.InitialCooldown = 2;
            judgement.IsForcedWhenReady = true;
            u.BaseWazas.Add(judgement);

            return u;
        }

        /// <summary>皇太子（闇）の想定編成：必敗版。中央スロット 1 に皇太子本体、
        /// 周囲は前衛タンク 1＋サムライ 1＋帝国の影 3 を配置。
        /// 皇太子本体の超絶バフで 3 ターン以内に全滅する想定。</summary>
        public static List<RuntimeUnit> PrinceDarkParty()
        {
            return new List<RuntimeUnit>
            {
                new RuntimeUnit(EnemiesRoster.ImperialTankDef(),  0),
                new RuntimeUnit(PrinceDark(),                     1),
                new RuntimeUnit(EnemiesRoster.ImperialSamurai(),  2),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(),   3),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(),   4),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(),   5),
            };
        }

        /// <summary>皇太子（浄化）の想定編成：戦える版。中央スロット 1 に皇太子本体、
        /// 周囲 5 体は帝国軍精鋭編成（重装兵／傭兵／帝国の影×2／弓兵）。</summary>
        public static List<RuntimeUnit> PrinceLightParty()
        {
            return new List<RuntimeUnit>
            {
                new RuntimeUnit(EnemiesRoster.ImperialTankDef(),  0),
                new RuntimeUnit(PrinceLight(),                    1),
                new RuntimeUnit(EnemiesRoster.ImperialSamurai(),  2),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(),   3),
                new RuntimeUnit(EnemiesRoster.ImperialArcher(),   4),
                new RuntimeUnit(EnemiesRoster.ImperialShadow(),   5),
            };
        }
    }
}
