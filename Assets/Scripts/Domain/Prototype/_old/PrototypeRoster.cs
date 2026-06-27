// Assets/Scripts/Core/Prototype/PrototypeRoster.cs
// プロト Inc6: 戦闘スライス用の6役ロスター定義（純C#・MonoBehaviour非依存）
//
// 役割が割れて「配置の読み」が成立する最小セット（仕様 210_prototype_spec.md §7.1）。
// バランス数値はすべて暫定であり、プロトのプレイテストで調整する前提。
using System.Collections.Generic;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>
    /// 戦闘スライスのプリセット編成を生成するファクトリ。
    /// 味方6役（前衛3／後衛3）と敵編成のRuntimeUnitを構築する。
    ///
    /// 【配置の読みの土台＝射程（§10.2）】
    /// 旧「保護の原則」（列単位の後衛保護）は撤去し、射程（近接/中射程/遠隔）で
    /// 「どこに置くと誰に届くか」を決める。本スライスは暫定で前衛=近接・後衛=中射程とし、
    /// 「敵前列が崩れるまで敵後列に届かない」挙動を再現する（射程の最小デモ）。
    /// 遠隔（後列を直接狙撃）や横列かばうは段階3の戦闘土台（§10.4）で導入する。
    /// Coverタグ→永続Cover付与の仕組み（ApplyCoverIfTagged）は将来用に残すが、
    /// 本スライスではどのユニットにもタグを付けない。
    /// </summary>
    public static class PrototypeRoster
    {
        private const string CoverTag = "Cover";

        // ══════════════════════════════════════════════
        // 公開API
        // ══════════════════════════════════════════════

        /// <summary>味方6役（スロット0〜5、前衛=重装/機動/物理、後衛=魔法/回復/軍師）を生成する。</summary>
        public static List<RuntimeUnit> BuildAllyParty()
        {
            // 前衛（0,1,2）
            var vanguard = MakeUnit("ally_vanguard", "ヴァンガード（重装前衛）", Element.Earth,
                hp: 160, atk: 16, pdef: 14, mdef: 8, spd: 4);
            vanguard.BaseWazas.Add(PhysAttack("vg_bash", "盾打ち", spd: 4, cd: 0, mult: 1.0f));
            // 全肩代わり「かばう」は付与しない（クラスdoc参照）。横列かばうは§10.4で導入。
            // 重装前衛は近接で前列に居座り、「敵の射程の的」になって後衛（中射程）を守る壁として機能させる。

            var skirmisher = MakeUnit("ally_skirmisher", "韋駄天（機動）", Element.Wind,
                hp: 90, atk: 24, pdef: 6, mdef: 4, spd: 14);
            skirmisher.BaseWazas.Add(PhysAttack("sk_gale", "疾風斬り", spd: 14, cd: 0, mult: 1.1f));

            var striker = MakeUnit("ally_striker", "剣士（物理）", Element.None,
                hp: 110, atk: 30, pdef: 8, mdef: 4, spd: 8);
            striker.BaseWazas.Add(PhysAttack("st_heavy", "強斬撃", spd: 8, cd: 2, mult: 1.7f));

            // 後衛（3,4,5）：暫定で中射程（後列から敵前列を攻撃。敵前列が崩れるまで敵後列には届かない）
            var mage = MakeUnit("ally_mage", "魔導士（魔法）", Element.Fire,
                hp: 70, atk: 32, pdef: 3, mdef: 10, spd: 7, range: AttackRange.Mid);
            mage.BaseWazas.Add(MagicAttack("mg_fireball", "火球", Element.Fire, spd: 7, cd: 0, mult: 1.3f));

            var healer = MakeUnit("ally_healer", "癒し手（回復）", Element.Light,
                hp: 80, atk: 12, pdef: 4, mdef: 8, spd: 9, range: AttackRange.Mid);
            healer.BaseWazas.Add(HealWaza("hl_heal", "治癒の光", spd: 9, cd: 0, amount: 45));
            healer.BaseWazas.Add(PhysAttack("hl_staff", "杖打ち", spd: 9, cd: 0, mult: 0.8f));

            var tactician = MakeUnit("ally_tactician", "軍師（強化/弱体）", Element.Dark,
                hp: 80, atk: 14, pdef: 4, mdef: 8, spd: 8, range: AttackRange.Mid);
            tactician.BaseWazas.Add(DebuffWaza("tc_break", "破甲", spd: 8, cd: 2, StatusEffectType.DefenseDown, mag: 8, turns: 2));
            tactician.BaseWazas.Add(BuffWaza("tc_rally", "鼓舞", spd: 8, cd: 3, StatusEffectType.AttackUp, mag: 12, turns: 3));
            tactician.BaseWazas.Add(PhysAttack("tc_jab", "刺突", spd: 8, cd: 0, mult: 0.7f));

            return new List<RuntimeUnit>
            {
                Place(vanguard,   0, isLeader: true),
                Place(skirmisher, 1),
                Place(striker,    2),
                Place(mage,       3),
                Place(healer,     4),
                Place(tactician,  5),
            };
        }

        /// <summary>敵編成（6体）。前衛に高耐久の重騎士を置き、後衛に弓・術・隊長を配置する。</summary>
        public static List<RuntimeUnit> BuildEnemyParty()
        {
            var knight = MakeUnit("enemy_knight", "重騎士", Element.None,
                hp: 150, atk: 18, pdef: 12, mdef: 6, spd: 4);
            knight.BaseWazas.Add(PhysAttack("ek_smash", "兜割り", spd: 4, cd: 0, mult: 1.0f));
            // 敵側も全肩代わり「かばう」は付与しない（味方側と対称）。

            var soldier = MakeUnit("enemy_soldier", "剣兵", Element.None,
                hp: 100, atk: 26, pdef: 8, mdef: 4, spd: 7);
            soldier.BaseWazas.Add(PhysAttack("es_slash", "斬撃", spd: 7, cd: 0, mult: 1.1f));

            var lancer = MakeUnit("enemy_lancer", "槍兵", Element.None,
                hp: 100, atk: 22, pdef: 7, mdef: 4, spd: 6);
            lancer.BaseWazas.Add(PhysAttack("el_thrust", "刺突", spd: 6, cd: 0, mult: 1.1f));

            // 後衛（3,4,5）：暫定で中射程（味方側と対称）
            var archer = MakeUnit("enemy_archer", "弓兵", Element.Wind,
                hp: 70, atk: 28, pdef: 3, mdef: 4, spd: 9, range: AttackRange.Mid);
            archer.BaseWazas.Add(PhysAttack("ea_shot", "射撃", spd: 9, cd: 0, mult: 1.2f));

            var sorcerer = MakeUnit("enemy_sorcerer", "術士", Element.Ice,
                hp: 65, atk: 30, pdef: 2, mdef: 6, spd: 7, range: AttackRange.Mid);
            sorcerer.BaseWazas.Add(MagicAttack("eso_frost", "氷弾", Element.Ice, spd: 7, cd: 0, mult: 1.3f));

            var captain = MakeUnit("enemy_captain", "隊長", Element.None,
                hp: 95, atk: 22, pdef: 6, mdef: 5, spd: 6, range: AttackRange.Mid);
            captain.BaseWazas.Add(PhysAttack("ec_cmd", "号令斬り", spd: 6, cd: 0, mult: 1.2f));

            return new List<RuntimeUnit>
            {
                Place(knight,   0),
                Place(soldier,  1),
                Place(lancer,   2),
                Place(archer,   3),
                Place(sorcerer, 4),
                Place(captain,  5),
            };
        }

        // ══════════════════════════════════════════════
        // 内部ビルダー
        // ══════════════════════════════════════════════

        /// <summary>ユニットを生成し、指定スロットのRuntimeUnitに変換する（Coverタグがあれば永続Coverを付与）。</summary>
        private static RuntimeUnit Place(Unit unit, int slot, bool isLeader = false)
        {
            var ru = new RuntimeUnit(unit, slot, isLeader);
            ApplyCoverIfTagged(ru);
            return ru;
        }

        /// <summary>Coverタグを持つユニットに全スロットをかばう永続Cover（InitializeBattleで消えない）を付与する。</summary>
        private static void ApplyCoverIfTagged(RuntimeUnit ru)
        {
            if (!ru.BaseUnit.Tags.Contains(CoverTag)) return;
            ru.AddEffect(new StatusEffect(StatusEffectType.Cover)
            {
                CoverTargetSlotIndex = -1,
                RemainingTurns = -1
            });
        }

        /// <summary>
        /// 再戦のためにユニットの戦闘状態を初期化する（HP全快・状態異常クリア・かばう再付与）。
        /// 配置（SlotIndex）は変更しないため、プレイヤーが組んだ配置は維持される。
        /// </summary>
        public static void ResetForBattle(RuntimeUnit ru)
        {
            ru.BaseUnit.CurrentHP = ru.BaseUnit.MaxHP;
            ru.BaseUnit.State = UnitState.Active;
            ru.CurrentShield = 0;
            ru.CanCarryOverShield = false;
            ru.HasActedThisTurn = false;
            ru.ParalysisIncapacitateCount = 0;
            ru.CurrentReviveCount = 0;
            ru.ClearAllEffects();
            ApplyCoverIfTagged(ru);
        }

        private static Unit MakeUnit(string id, string name, Element element,
            int hp, int atk, int pdef, int mdef, int spd,
            AttackRange range = AttackRange.Melee)
        {
            return new Unit(id, name, element)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                PDEF = pdef,
                MDEF = mdef,
                BaseSPD = spd,
                Range = range,
                State = UnitState.Active
            };
        }

        private static Waza PhysAttack(string id, string name, int spd, int cd, float mult)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Attack,
                IsPhysical = true,
                WazaElement = Element.None,
                SPD = spd,
                Cooldown = cd,
                TargetingType = TargetingType.SingleEnemy,
                CalculateBaseDamage = (a, t) => (int)(a.EffectiveATK * mult)
            };
        }

        private static Waza MagicAttack(string id, string name, Element element, int spd, int cd, float mult)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Attack,
                IsPhysical = false,
                WazaElement = element,
                SPD = spd,
                Cooldown = cd,
                TargetingType = TargetingType.SingleEnemy,
                CalculateBaseDamage = (a, t) => (int)(a.EffectiveATK * mult)
            };
        }

        private static Waza HealWaza(string id, string name, int spd, int cd, int amount)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Heal,
                SPD = spd,
                Cooldown = cd,
                TargetingType = TargetingType.SingleAlly,
                CalculateHealAmount = (a, t) => amount
            };
        }

        private static Waza BuffWaza(string id, string name, int spd, int cd,
            StatusEffectType effect, float mag, int turns)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Buff,
                SPD = spd,
                Cooldown = cd,
                TargetingType = TargetingType.SingleAlly,
                AppliedEffects = new List<StatusEffect>
                {
                    new StatusEffect(effect, stacks: 1, remainingTurns: turns) { Magnitude = mag }
                }
            };
        }

        private static Waza DebuffWaza(string id, string name, int spd, int cd,
            StatusEffectType effect, float mag, int turns)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Debuff,
                SPD = spd,
                Cooldown = cd,
                TargetingType = TargetingType.SingleEnemy,
                AppliedEffects = new List<StatusEffect>
                {
                    new StatusEffect(effect, stacks: 1, remainingTurns: turns) { Magnitude = mag }
                }
            };
        }
    }
}
