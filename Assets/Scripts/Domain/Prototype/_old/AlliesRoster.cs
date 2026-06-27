using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Conditional;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Prototype
{
    /// <summary>王国軍（味方）ユニットのファクトリ。固有 2 体＋通常 15 体。</summary>
    public static class AlliesRoster
    {
        // 固有キャラ（固定加入・ドラフト対象外）

        /// <summary>王女の Unit.Id。「兵種強化対象外＆固定加入」の判定に使う公開定数。</summary>
        public const string PrincessId = "princess";

        /// <summary>ブリジットの Unit.Id。</summary>
        public const string BridgetId = "bridget";

        /// <summary>王女：物語の主人公（仮）。初期手駒に固定加入する前衛。
        /// 騎士よりやや脆い（HP -20 / ATK -2 / MDEF -2）が、置物オーラ DefenseUp Mag 5 を持つ。
        /// 兵種強化は対象外で、代わりに全戦線の拠点強化Lv合計に応じて動的に強化される。
        /// 軍師（AttackUp 全軍オーラ）と棲み分けて両方使う動機を作る守護バフ役。</summary>
        public static Unit Princess()
        {
            var u = RosterHelpers.Make(PrincessId, "王女", Element.Light,
                hp: 170, atk: 28, pdef: 12, mdef: 14, spd: 8, range: AttackRange.Melee);
            u.EnhancementMagnitudePerLevel = 2;
            u.BaseWazas.Add(RosterHelpers.Phys("princess_slash", "聖剣の一閃", spd: 8, mult: 1.0f));
            u.AuraEffect = StatusEffect.CreatePersistent(
                StatusEffectType.DefenseUp, magnitude: 5, sourceAbilityName: "王家の加護");
            return u;
        }

        /// <summary>ブリジット：救出後に王女陣営に加わる前衛固有キャラ。
        /// 王女とのシナジー前衛アタッカー：
        /// - 王家のペンダント（PendantOwnerTag → PendantConditionalProcessor）：王女と同時出撃時、自＋王女に PDEF +3
        /// - 攻防一体（OffenseDefenseLinkTag → OffenseDefenseLinkConditionalProcessor）：自分の PDEF バフ合計分 ATK が上昇
        /// 拠点Lv連動強化は持たない（VSプロト仕様）。</summary>
        public static Unit Bridget()
        {
            var u = RosterHelpers.Make(BridgetId, "ブリジット", Element.Light,
                hp: 150, atk: 26, pdef: 10, mdef: 12, spd: 9, range: AttackRange.Melee,
                tags: new[]
                {
                    PendantConditionalProcessor.PendantOwnerTag,
                    OffenseDefenseLinkConditionalProcessor.OffenseDefenseLinkTag,
                });
            u.BaseWazas.Add(RosterHelpers.Phys("bridget_great_strike", "大剣の一撃", spd: 9, mult: 1.0f));
            return u;
        }

        // 前衛

        /// <summary>一般タンク：横列かばう・攻撃不可・物理防御がやや高い。</summary>
        public static Unit GeneralTank()
        {
            var u = RosterHelpers.Make("tank_def", "重装兵", Element.Earth,
                hp: 240, atk: 0, pdef: 12, mdef: 8, spd: 5, range: AttackRange.Melee,
                tags: new[] { BattleManager.RowCoverTag });
            u.EnhancementPDEFPerLevel = 3;
            return u;
        }

        /// <summary>騎士：かばう持ちの殴れるサブタンク。重装兵より硬くないが攻撃もできる「殴れるかばう役」のポジション。</summary>
        public static Unit Paladin()
        {
            var u = RosterHelpers.Make("paladin", "騎士", Element.Light,
                hp: 190, atk: 26, pdef: 10, mdef: 14, spd: 7, range: AttackRange.Melee,
                tags: new[] { BattleManager.RowCoverTag });
            u.EnhancementPDEFPerLevel = 2;
            u.EnhancementMDEFPerLevel = 2;

            u.BaseWazas.Add(RosterHelpers.Phys("pal_slash", "剣撃", spd: 7, mult: 1.0f));
            return u;
        }

        /// <summary>アタッカー1：2回攻撃。バフ/デバフとのシナジー重視。</summary>
        public static Unit Attacker1()
        {
            var u = RosterHelpers.Make("atk_multi", "双剣士", Element.None,
                hp: 115, atk: 36, pdef: 4, mdef: 4, spd: 9, range: AttackRange.Melee);
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("atk1_dual", "連撃", spd: 9, mult: 0.75f, hits: 2));
            return u;
        }

        /// <summary>サムライ：近接・同列スプラッシュアタッカー。
        /// 毎ターン「薙ぎ払い」を発動：単体ターゲット解決＋かばう移動を経たメインターゲットに mult 1.0、
        /// 同列の他の生存敵に 0.8 倍の巻き込み（SameRowSplashMultiplier=0.8）。
        /// タンクを叩く→同列の脆い敵もろとも巻き込む戦術が刺さる。</summary>
        public static Unit Samurai()
        {
            var u = RosterHelpers.Make("samurai", "サムライ", Element.None,
                hp: 130, atk: 36, pdef: 4, mdef: 4, spd: 7, range: AttackRange.Melee);
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("samurai_sweep", "薙ぎ払い", spd: 7, mult: 0.9f,
                rowSplash: 0.8f));
            return u;
        }

        /// <summary>大槌兵：攻撃対象に物理防御デバフ（弱め・長め・高スタック上限）。蓄積でタンク突破。</summary>
        public static Unit Debuffer()
        {
            var u = RosterHelpers.Make("debuffer", "大槌兵", Element.None,
                hp: 130, atk: 32, pdef: 5, mdef: 4, spd: 8, range: AttackRange.Melee);
            u.EnhancementATKPerLevel = 3;
            u.EnhancementPDEFPerLevel = 2;
            u.BaseWazas.Add(RosterHelpers.Phys("deb_break", "鎧砕き", spd: 8, mult: 1.0f,
                riders: new List<StatusEffect>
                {
                    new StatusEffect(StatusEffectType.DefenseDown, stacks: 1, remainingTurns: 5)
                    { Magnitude = 7, MaxStacks = 6 }
                }));
            return u;
        }

        /// <summary>傭兵：大剣を振りかざす前衛物理アタッカー。パッシブ「孤高の戦士」(<see cref="LonerWolfConditionalProcessor.LonerTag"/>)。
        /// 陣営生存数が 3 以下のとき AttackUp / DefenseUp が IsUndispellable=true で付与される
        /// （3 体→+10／2 体→+20／1 体→+30・実体は <see cref="LonerWolfConditionalProcessor"/>）。
        /// 編成時点で 3 体以下でも発動し、人数が減るたびに強度が再計算される。
        /// 味方の少ない序盤ほど活躍するユニットとしてデザイン。</summary>
        public static Unit Mercenary()
        {
            var u = RosterHelpers.Make("mercenary", "傭兵", Element.None,
                hp: 130, atk: 34, pdef: 5, mdef: 4, spd: 8, range: AttackRange.Melee,
                tags: new[] { LonerWolfConditionalProcessor.LonerTag });
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("merc_cleave", "大剣の薙ぎ", spd: 8, mult: 1.0f));
            return u;
        }

        // 後衛

        /// <summary>弓兵：遠隔物理・必中。ニンジャ（回避）の明確なカウンター／後衛狙撃。</summary>
        public static Unit Archer()
        {
            var u = RosterHelpers.Make("archer", "弓兵", Element.Wind,
                hp: 100, atk: 34, pdef: 3, mdef: 3, spd: 10, range: AttackRange.Ranged);
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("arc_snipe", "狙撃", spd: 10, mult: 1.0f, sureHit: true));
            return u;
        }

        /// <summary>忍者：潜入暗殺者コンセプト。
        /// 「敵陣に潜って削れる相手を狩る」キャラ性を、3 つの直交した仕掛けで表現：
        /// - <see cref="BattleManager.MageHunterTag"/>：魔導士に対し基礎ダメージ ×2
        /// - <see cref="BattleManager.InfiltratorTag"/>：自身が味方タンクのかばう対象外（潜入中は守られない）
        /// - 疾風刃 (`nin_slash`) の <c>IgnoresCover</c> + <c>IgnoresFrontRowGuard</c>：
        ///   かばう貫通＋列単位保護貫通で、敵後列の魔導士に直接届く
        /// 通常の TargetEvaluator スコア式（damage/HP）が「とどめを刺せる相手 &gt; 魔導士特効 &gt; その他」を
        /// 自然に選ぶため、専用ターゲット選定ロジック・潜入状態管理は不要。
        /// カウンタープレイ：忍者は前列扱いだがタンクに守られないため、敵から直接狙われやすい。
        /// 「初撃のみ強い特殊技」のテンプレとして <see cref="CreateLegacyAssassinateTemplate"/> を温存
        ///（u.BaseWazas には追加しない）。</summary>
        public static Unit Ninja()
        {
            var u = RosterHelpers.Make("ninja", "忍者", Element.Dark,
                hp: 105, atk: 42, pdef: 3, mdef: 3, spd: 16, range: AttackRange.Melee, eva: 5,
                tags: new[] { BattleManager.MageHunterTag, BattleManager.InfiltratorTag });
            u.EnhancementATKPerLevel = 5;
            u.BaseWazas.Add(RosterHelpers.Phys("nin_slash", "疾風刃", spd: 16, mult: 1.0f,
                ignoresCover: true, ignoresFrontRowGuard: true));
            return u;
        }

        /// <summary>戦闘開始 1 ターン目のみ後衛直撃する特殊単体技のテンプレ（未使用）。
        /// 「初撃のみ強い特殊技」のパターンを別ユニットで再利用するための温存。
        /// 仕様：mult 1.2／<see cref="TargetingType.BackRowEnemies"/>／優先発動／1 戦 1 回／かばう貫通。
        /// 復元は本メソッドを呼んで <c>u.BaseWazas.Add()</c> するだけで良い。</summary>
#pragma warning disable IDE0051 // 未使用の private メンバーを削除する
        private static Waza CreateLegacyAssassinateTemplate()
        {
            var leap = RosterHelpers.Phys("nin_assassinate", "暗殺", spd: 16, mult: 1.2f,
                targeting: TargetingType.BackRowEnemies, ignoresCover: true);
            leap.IsForcedWhenReady = true;
            leap.MaxUsesPerBattle = 1;
            return leap;
        }
#pragma warning restore IDE0051

        /// <summary>炎魔導士：中射程・単体魔法・前列のみ攻撃。命中時に炎上付与。物理タンクキラー（魔防を抜く）。</summary>
        public static Unit FireMage()
        {
            var u = RosterHelpers.Make("firemage", "炎魔導士", Element.Fire,
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

        /// <summary>全体魔法：遠隔・全体攻撃・チャージ（初期CD＋優先発動で数ターンごと）。
        /// チャージ中は「詠唱」で見た目を表現（実害なし・防御に流れない）。</summary>
        public static Unit AoeMage()
        {
            var u = RosterHelpers.Make("aoemage", "雷魔導士", Element.None,
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

        /// <summary>回復役1：遠隔・最もHP割合の低い味方1体を回復。攻撃手段なし。
        /// 回復対象（HP&lt;MaxHP の味方）が居なければ TargetEvaluator の自己防御フォールバックで防御する。</summary>
        public static Unit Healer1()
        {
            var u = RosterHelpers.Make("healer", "司祭", Element.Light,
                hp: 50, atk: 0, pdef: 3, mdef: 6, spd: 9, range: AttackRange.Ranged);
            u.EnhancementHealPerLevel = 10;
            u.BaseWazas.Add(RosterHelpers.HealW("heal_cure", "治癒の光", spd: 9, amount: 35));
            return u;
        }

        /// <summary>巫女：遠隔・全体に小回復を撒く補助役。司祭との差別化として全体回復に特化。
        /// 通常攻撃は行わない。毎ターン発動で挙動を単純化し、1 回あたりの数値を半減して
        /// 合計回復量は維持する設計。</summary>
        public static Unit Healer2()
        {
            var u = RosterHelpers.Make("medic", "巫女", Element.Light,
                hp: 50, atk: 0, pdef: 3, mdef: 6, spd: 9, range: AttackRange.Ranged);
            u.EnhancementHealPerLevel = 2;

            var aoeHeal = RosterHelpers.HealW("heal_small_aoe", "祈り", spd: 9, amount: 12,
                targeting: TargetingType.AllAllies, cooldown: 1);
            aoeHeal.IsForcedWhenReady = true;
            u.BaseWazas.Add(aoeHeal);
            return u;
        }

        /// <summary>バッファー：遠隔・味方単体（最も ATK の高い味方）の攻撃力バフ（上限3・3ターン）。攻撃手段なし。
        /// 攻撃手段を持つ味方が居なければ TargetEvaluator の自己防御フォールバックで防御する。</summary>
        public static Unit Buffer()
        {
            var u = RosterHelpers.Make("buffer", "踊り子", Element.None,
                hp: 85, atk: 0, pdef: 3, mdef: 5, spd: 8, range: AttackRange.Ranged);
            u.EnhancementMagnitudePerLevel = 3;
            u.BaseWazas.Add(RosterHelpers.BuffW("buf_rally", "鼓舞", spd: 8,
                riders: new List<StatusEffect>
                {
                    new StatusEffect(StatusEffectType.AttackUp, stacks: 1, remainingTurns: 3)
                    { Magnitude = 10, MaxStacks = 3 }
                }));
            return u;
        }

        /// <summary>軍師：遠隔・全軍を微バフ（置物・開始付与/死亡で剥奪）。
        /// 2 つの解除技を CD 2＋初期 CD 0/1 で互い違いに発動：
        /// - 奇数ターン（T1, T3, ...）：敵バフ解除 `tac_purge`（全敵対象・DispelsBuffs）
        /// - 偶数ターン（T2, T4, ...）：味方デバフ解除 `tac_dispel`（全味方対象・DispelsDebuffs）
        /// 対象不在時は技を発動せず防御（TargetEvaluator の purge/dispel 専用ガードで吸収）。</summary>
        public static Unit Tactician()
        {
            var u = RosterHelpers.Make("tactician", "軍師", Element.Dark,
                hp: 80, atk: 0, pdef: 4, mdef: 6, spd: 7, range: AttackRange.Ranged);
            u.EnhancementMagnitudePerLevel = 2;

            u.AuraEffect = StatusEffect.CreatePersistent(
                StatusEffectType.AttackUp, magnitude: 5, sourceAbilityName: "戦術隊形");

            var purge = new Waza("tac_purge", "看破")
            {
                Category = WazaCategory.Debuff,
                TargetingType = TargetingType.AllEnemies,
                SPD = 7,
                Cooldown = 2,
                InitialCooldown = 0,
                IsForcedWhenReady = true,
                DispelsBuffs = true
            };
            u.BaseWazas.Add(purge);

            var dispel = new Waza("tac_dispel", "戦線整理")
            {
                Category = WazaCategory.Buff,
                TargetingType = TargetingType.AllAllies,
                SPD = 7,
                Cooldown = 2,
                InitialCooldown = 1,
                IsForcedWhenReady = true,
                DispelsDebuffs = true
            };
            u.BaseWazas.Add(dispel);
            return u;
        }
    }
}
