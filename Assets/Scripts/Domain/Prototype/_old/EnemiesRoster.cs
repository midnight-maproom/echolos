using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Prototype
{
    /// <summary>敵雑魚（帝国軍）ユニットのファクトリ。散兵＋帝国軍 10 体。</summary>
    public static class EnemiesRoster
    {
        // 既存敵

        /// <summary>帝国偵察兵：序盤の弱パターン向け敵専用雑魚キャラ。中射程・「短槍突き」のみで攻撃。
        /// 数の有利不利を直感的に体感させるための基準ユニット（偵察兵2人 vs 双剣士1人 → 双剣士勝ち／偵察兵3人 vs 双剣士1人 → 双剣士負け）。</summary>
        public static Unit Skirmisher()
        {
            var u = RosterHelpers.Make("imperial_scout", "帝国偵察兵", Element.None,
                hp: 50, atk: 20, pdef: 3, mdef: 3, spd: 7, range: AttackRange.Mid);
            u.BaseWazas.Add(RosterHelpers.Phys("imperial_scout_strike", "短槍突き", spd: 7, mult: 1.0f));
            return u;
        }

        // 新規敵専用（帝国軍 10 体・味方コピー）
        // 性能は対応する味方と完全同一。アイコン・表示名・Id のみ差別化。

        /// <summary>帝国重装兵：横列かばう・攻撃不可の壁役。
        /// 純アタッカー 2 体で 1 体を 4T 程度で抜けるよう HP/PDEF をやや低めに設定し、
        /// 15T 戦闘内でサポート役の活躍機会を確保している。</summary>
        public static Unit ImperialTankDef()
        {
            var u = RosterHelpers.Make("imperial_tank_def", "帝国重装兵", Element.Earth,
                hp: 220, atk: 0, pdef: 10, mdef: 8, spd: 5, range: AttackRange.Melee,
                tags: new[] { BattleManager.RowCoverTag });
            u.EnhancementPDEFPerLevel = 3;
            return u;
        }

        /// <summary>帝国騎士：味方 Paladin の完全コピー（かばう付き殴れる前衛）。</summary>
        public static Unit ImperialPaladin()
        {
            var u = RosterHelpers.Make("imperial_paladin", "帝国騎士", Element.Light,
                hp: 190, atk: 26, pdef: 10, mdef: 14, spd: 7, range: AttackRange.Melee,
                tags: new[] { BattleManager.RowCoverTag });
            u.EnhancementPDEFPerLevel = 2;
            u.EnhancementMDEFPerLevel = 2;
            u.BaseWazas.Add(RosterHelpers.Phys("pal_slash", "剣撃", spd: 7, mult: 1.0f));
            return u;
        }

        /// <summary>帝国双剣士：味方 Attacker1 のコピー。2 回攻撃。</summary>
        public static Unit ImperialAtkMulti()
        {
            var u = RosterHelpers.Make("imperial_atk_multi", "帝国双剣士", Element.None,
                hp: 115, atk: 36, pdef: 4, mdef: 4, spd: 9, range: AttackRange.Melee);
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("atk1_dual", "連撃", spd: 9, mult: 0.75f, hits: 2));
            return u;
        }

        /// <summary>帝国傭兵：味方 Samurai のコピー（薙ぎ払い：mult 0.9 + 同列スプラッシュ 0.8）。</summary>
        public static Unit ImperialSamurai()
        {
            var u = RosterHelpers.Make("imperial_samurai", "帝国傭兵", Element.None,
                hp: 130, atk: 36, pdef: 4, mdef: 4, spd: 7, range: AttackRange.Melee);
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("imperial_samurai_sweep", "薙ぎ払い", spd: 7, mult: 0.9f,
                rowSplash: 0.8f));
            return u;
        }

        /// <summary>帝国暗殺者：味方 Ninja のコピー（潜入＋疾風刃のかばう／列保護貫通）。</summary>
        public static Unit ImperialAssassin()
        {
            var u = RosterHelpers.Make("imperial_assassin", "帝国暗殺者", Element.Dark,
                hp: 105, atk: 42, pdef: 3, mdef: 3, spd: 16, range: AttackRange.Melee, eva: 5,
                tags: new[] { BattleManager.MageHunterTag, BattleManager.InfiltratorTag });
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("nin_slash", "疾風刃", spd: 16, mult: 1.0f,
                ignoresCover: true, ignoresFrontRowGuard: true));
            return u;
        }

        /// <summary>帝国弓兵：味方 Archer のコピー。遠隔物理・必中。</summary>
        public static Unit ImperialArcher()
        {
            var u = RosterHelpers.Make("imperial_archer", "帝国弓兵", Element.Wind,
                hp: 100, atk: 34, pdef: 3, mdef: 3, spd: 10, range: AttackRange.Ranged);
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("arc_snipe", "狙撃", spd: 10, mult: 1.0f, sureHit: true));
            return u;
        }

        /// <summary>帝国炎魔導士：味方 FireMage のコピー。中射程・単体魔法・前列のみ攻撃・Burn 付与。</summary>
        public static Unit ImperialFireMage()
        {
            var u = RosterHelpers.Make("imperial_firemage", "帝国炎魔導士", Element.Fire,
                hp: 80, atk: 36, pdef: 2, mdef: 8, spd: 8, range: AttackRange.Mid,
                roles: new[] { BattleManager.MageRole });
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Magic("fm_flame", "火炎弾", Element.Fire, spd: 8, mult: 1.0f,
                riders: new List<StatusEffect>
                {
                    StatusEffect.CreateStatusAilment(StatusEffectType.Burn, stacks: 2, maxStacks: 12),
                }));
            return u;
        }

        /// <summary>帝国大魔導士：味方 AoeMage のコピー。全体魔法・チャージ。
        /// チャージ中は「詠唱」（実害なし）。</summary>
        public static Unit ImperialAoeMage()
        {
            var u = RosterHelpers.Make("imperial_aoemage", "帝国大魔導士", Element.None,
                hp: 78, atk: 36, pdef: 2, mdef: 6, spd: 6, range: AttackRange.Ranged,
                roles: new[] { BattleManager.MageRole });
            u.EnhancementATKPerLevel = 5;

            var thunder = RosterHelpers.Magic("aoe_meteor", "サンダー", Element.Lightning, spd: 6, mult: 0.8f,
                targeting: TargetingType.AllEnemies);
            thunder.Cooldown = 3;
            thunder.InitialCooldown = 2;
            thunder.IsForcedWhenReady = true;
            u.BaseWazas.Add(thunder);

            u.BaseWazas.Add(RosterHelpers.Idle("mage_chant", "詠唱", spd: 6));
            return u;
        }

        /// <summary>帝国の影：中射程・前衛麻痺・小回避。
        /// 麻痺の MaxStacks は事実上撤廃（99）：麻痺仕様では行動不能発動時に
        /// スタック全消去＋許容量倍化なので、付与時の上限制約は不要。複数体の影が同ターンに
        /// 当てるとスタックが合算され、許容量超過で確実に行動不能化する設計。</summary>
        public static Unit ImperialShadow()
        {
            var u = RosterHelpers.Make("imperial_shadow", "帝国の影", Element.None,
                hp: 60, atk: 22, pdef: 3, mdef: 3, spd: 14, range: AttackRange.Mid, eva: 15);
            u.EnhancementEvasionPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("nin_strike", "当て身", spd: 14, mult: 0.6f,
                riders: new List<StatusEffect>
                {
                    StatusEffect.CreateStatusAilment(StatusEffectType.Paralysis, stacks: 1, maxStacks: 99),
                }));
            return u;
        }

        /// <summary>帝国司祭：味方 Healer1 のコピー。遠隔・最 HP 割合低い味方を回復。攻撃手段なし。
        /// 回復対象不在時は TargetEvaluator の自己防御フォールバックで防御する。</summary>
        public static Unit ImperialHealer()
        {
            var u = RosterHelpers.Make("imperial_healer", "帝国司祭", Element.Light,
                hp: 50, atk: 0, pdef: 3, mdef: 6, spd: 9, range: AttackRange.Ranged);
            u.EnhancementHealPerLevel = 10;
            u.BaseWazas.Add(RosterHelpers.HealW("heal_cure", "治癒の光", spd: 9, amount: 35));
            return u;
        }
    }
}
