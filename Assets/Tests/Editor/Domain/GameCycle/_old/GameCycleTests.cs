// 成長システム・装備着脱・撤退・ロスト・控え回復の単体テスト。
//
// テスト方針：
//  - GameCycle 配下の POCO クラスを直接呼び出してロジックを検証する
//  - BattleManager / BattleContext の進行フロー全体は経由しない
//  - 乱数・非同期・MonoBehaviour への依存は一切なし

using NUnit.Framework;
using System;
using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.GameCycle;
using Echolos.Domain.Items;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class GameCycleTests
    {
        // ════════════════════════════════════════════════════════
        // ヘルパーメソッド
        // ════════════════════════════════════════════════════════

        /// <summary>シンプルなユニットを生成する</summary>
        private static Unit CreateUnit(
            string id,
            int maxHp = 100,
            UnitState state = UnitState.Reserve)
        {
            return new Unit(id, id)
            {
                MaxHP = maxHp,
                CurrentHP = maxHp,
                State = state
            };
        }

        /// <summary>指揮官データを生成する</summary>
        private static CommanderData CreateCommander(int expItems = 10)
        {
            return new CommanderData("cmd1", "テスト指揮官")
            {
                TotalExpItems = expItems
            };
        }

        /// <summary>
        /// 固有の4強化肢（非マスター）+1マスターボーナスを持つユニットを生成する。
        /// ApplyEffectは渡したリストに強化IDを追記することで「適用済み」を検証可能にする。
        /// </summary>
        private static Unit CreateUnitWithUpgrades(
            string id,
            List<string> appliedLog = null)
        {
            var unit = CreateUnit(id);
            unit.CurrentLevel = 1;
            unit.CurrentExp = 0;

            var log = appliedLog ?? new List<string>();

            // 強化肢A〜D（非マスター）
            for (int i = 0; i < 4; i++)
            {
                var upgradeId = $"upgrade_{(char)('A' + i)}";
                unit.AvailableUpgrades.Add(new UnitUpgrade(
                    upgradeId,
                    $"強化{(char)('A' + i)}",
                    "",
                    UpgradeType.StatBoost,
                    isMasteryBonus: false)
                {
                    ApplyEffect = u => log.Add(upgradeId)
                });
            }

            // マスターボーナス
            unit.AvailableUpgrades.Add(new UnitUpgrade(
                "mastery",
                "マスターボーナス",
                "",
                UpgradeType.MasteryBonus,
                isMasteryBonus: true)
            {
                ApplyEffect = u => log.Add("mastery")
            });

            return unit;
        }

        /// <summary>装備品を生成する（ステータス変化をログに記録）</summary>
        private static Equipment CreateEquipment(
            string id,
            int atkBonus = 0,
            List<string> equipLog = null,
            List<string> unequipLog = null)
        {
            var log = equipLog ?? new List<string>();
            var uLog = unequipLog ?? new List<string>();

            return new Equipment(id, $"装備_{id}")
            {
                OnEquip = u =>
                {
                    u.BaseATK += atkBonus;
                    log.Add($"equip:{id}");
                },
                OnUnequip = u =>
                {
                    u.BaseATK -= atkBonus;
                    uLog.Add($"unequip:{id}");
                }
            };
        }

        /// <summary>BattleContextと対応するRuntimeUnitを生成する</summary>
        private static (BattleContext context, RuntimeUnit runtimeUnit) CreateBattleSetup(Unit unit)
        {
            var ctx = new BattleContext();
            var ru = new RuntimeUnit(unit, slotIndex: 0);
            ctx.AllyUnits.Add(ru);
            return (ctx, ru);
        }

        // ════════════════════════════════════════════════════════
        // ① GrowthSystem テスト
        // ════════════════════════════════════════════════════════

        [Test]
        public void AddExp_消費量がTotalExpItemsを超えない()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 0); // アイテムなし
            var growth = new GrowthSystem((u, opts) => opts[0]);

            growth.AddExp(unit, commander, 5);

            // アイテムが0個なのでEXPは加算されず、レベルも変わらない
            Assert.AreEqual(0, unit.CurrentExp);
            Assert.AreEqual(1, unit.CurrentLevel);
            Assert.AreEqual(0, commander.TotalExpItems);
        }

        [Test]
        public void AddExp_Lv1からLv2へ正しくレベルアップする()
        {
            var log = new List<string>();
            // Lv1→2に必要なEXP = 2個
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 2);
            var growth = new GrowthSystem((u, opts) => opts[0]); // 先頭を選択

            growth.AddExp(unit, commander, 2);

            Assert.AreEqual(2, unit.CurrentLevel);
            Assert.AreEqual(0, unit.CurrentExp); // ちょうど消費
            Assert.AreEqual(0, commander.TotalExpItems);
            Assert.AreEqual(1, log.Count, "強化が1つ適用されるべき");
            Assert.AreEqual("upgrade_A", log[0]);
        }

        [Test]
        public void AddExp_指定した強化オプションが選択されてAppliedUpgradesへ移動する()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 2);

            // Lv2でupgrade_Bを選択するセレクター
            var growth = new GrowthSystem((u, opts) =>
                opts.Find(up => up.UpgradeId == "upgrade_B"));

            growth.AddExp(unit, commander, 2); // Lv2

            Assert.AreEqual(2, unit.CurrentLevel);
            Assert.IsTrue(unit.AppliedUpgrades.Exists(up => up.UpgradeId == "upgrade_B"),
                "upgrade_BがAppliedUpgradesに移動しているべき");
            Assert.IsFalse(unit.AvailableUpgrades.Exists(up => up.UpgradeId == "upgrade_B"),
                "upgrade_BはAvailableUpgradesから消えているべき");
        }

        [Test]
        public void AddExp_Lv2から3を経てLv4まで各レベルで選択が1つずつ適用される()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 10);

            int selectCount = 0;
            // 選択するたびに先頭を返し、何回セレクターが呼ばれたか記録する
            var growth = new GrowthSystem((u, opts) =>
            {
                selectCount++;
                return opts[0];
            });

            // Lv4まで上げる（2+3+4 = 9個消費）
            growth.AddExp(unit, commander, 9);

            Assert.AreEqual(4, unit.CurrentLevel);
            Assert.AreEqual(3, selectCount, "Lv2・3・4でそれぞれ1回セレクターが呼ばれるべき");
            Assert.AreEqual(3, unit.AppliedUpgrades.Count, "非マスター強化が3つ適用済みのはず");
            // AvailableUpgradesには残り1つの非マスター + マスターの2つが残る
            Assert.AreEqual(2, unit.AvailableUpgrades.Count);
        }

        [Test]
        public void AddExp_Lv5到達時に残り全強化肢とマスターボーナスが自動適用される()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 20);

            // Lv2-4でA→B→Cと選択
            var selectionOrder = new Queue<string>(new[] { "upgrade_A", "upgrade_B", "upgrade_C" });
            var growth = new GrowthSystem((u, opts) =>
                opts.Find(up => up.UpgradeId == selectionOrder.Dequeue()));

            // Lv5まで上げる（2+3+4+5 = 14個消費）
            growth.AddExp(unit, commander, 14);

            Assert.AreEqual(5, unit.CurrentLevel, "Lv5に到達しているべき");
            Assert.AreEqual(0, unit.AvailableUpgrades.Count, "AvailableUpgradesが空になるべき");
            Assert.AreEqual(5, unit.AppliedUpgrades.Count, "全5強化肢がAppliedUpgradesにあるべき");
            Assert.IsTrue(log.Contains("mastery"), "マスターボーナスが適用されているべき");
            Assert.IsTrue(log.Contains("upgrade_D"), "残り1つの非マスター強化が自動適用されているべき");
        }

        [Test]
        public void AddExp_Lv5到達時はセレクターが呼ばれない_自動適用()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 20);

            bool selectorCalledAtLv5 = false;

            // Lv4→Lv5の遷移中にセレクターが呼ばれるかチェック
            int callCount = 0;
            var growth = new GrowthSystem((u, opts) =>
            {
                callCount++;
                if (u.CurrentLevel >= 5) selectorCalledAtLv5 = true;
                return opts[0];
            });

            growth.AddExp(unit, commander, 14);

            Assert.IsFalse(selectorCalledAtLv5, "Lv5到達時にセレクターは呼ばれないべき");
            Assert.AreEqual(3, callCount, "セレクターはLv2・3・4の3回のみ呼ばれるべき");
        }

        [Test]
        public void AddExp_すでにLv5のユニットにEXPを加算しても何も変わらない()
        {
            var unit = CreateUnit("u1");
            unit.CurrentLevel = 5;
            unit.CurrentExp = 0;
            var commander = CreateCommander(expItems: 5);
            var growth = new GrowthSystem();

            growth.AddExp(unit, commander, 5);

            // TotalExpItemsが消費されないことを確認
            Assert.AreEqual(5, commander.TotalExpItems, "Lv5済みユニットにEXPは消費されない");
            Assert.AreEqual(5, unit.CurrentLevel);
        }

        [Test]
        public void AddExp_余剰EXPが次のレベルへ繰り越される()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 5);

            // Lv1→2に2個、Lv2→3に3個 = 合計5個消費
            // 渡すのも5個なので余剰なし
            var growth = new GrowthSystem((u, opts) => opts[0]);

            growth.AddExp(unit, commander, 5);

            Assert.AreEqual(3, unit.CurrentLevel);
            Assert.AreEqual(0, unit.CurrentExp, "余剰EXPは0のはず");
        }

        [Test]
        public void AddExp_EXPに余剰がある場合は持ち越される()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            var commander = CreateCommander(expItems: 6);

            // Lv1→2に2個（残り4個）
            // Lv2→3に3個消費（残り1個）
            // Lv3→4に4個必要 → 1個足りずLv3で止まる
            var growth = new GrowthSystem((u, opts) => opts[0]);

            growth.AddExp(unit, commander, 6);

            Assert.AreEqual(3, unit.CurrentLevel);
            Assert.AreEqual(1, unit.CurrentExp, "余剰EXPが繰り越されているべき");
        }

        [Test]
        public void GetRequiredExpForNextLevel_各レベルで正しい必要EXPを返す()
        {
            var unit = CreateUnit("u1");

            unit.CurrentLevel = 1;
            Assert.AreEqual(2, GrowthSystem.GetRequiredExpForNextLevel(unit));

            unit.CurrentLevel = 2;
            Assert.AreEqual(3, GrowthSystem.GetRequiredExpForNextLevel(unit));

            unit.CurrentLevel = 3;
            Assert.AreEqual(4, GrowthSystem.GetRequiredExpForNextLevel(unit));

            unit.CurrentLevel = 4;
            Assert.AreEqual(5, GrowthSystem.GetRequiredExpForNextLevel(unit));

            unit.CurrentLevel = 5;
            Assert.AreEqual(0, GrowthSystem.GetRequiredExpForNextLevel(unit), "Lv5は必要EXP0");
        }

        // ════════════════════════════════════════════════════════
        // ② EquipmentSystem テスト
        // ════════════════════════════════════════════════════════

        [Test]
        public void Equip_装備するとOnEquipが呼ばれステータスが変化する()
        {
            var unit = CreateUnit("u1");
            unit.BaseATK = 10;
            var commander = CreateCommander();

            var equipLog = new List<string>();
            var gear = CreateEquipment("sword", atkBonus: 5, equipLog: equipLog);
            commander.EquipmentInventory.Add(gear);

            var equipSystem = new EquipmentSystem();
            equipSystem.Equip(unit, gear, commander);

            Assert.AreEqual(15, unit.BaseATK, "装備でATKが+5されるべき");
            Assert.AreEqual(gear, unit.EquippedGear);
            Assert.IsFalse(commander.EquipmentInventory.Contains(gear), "インベントリから消えるべき");
            Assert.AreEqual(1, equipLog.Count);
        }

        [Test]
        public void Unequip_装備を外すとOnUnequipが呼ばれステータスが元に戻る()
        {
            var unit = CreateUnit("u1");
            unit.BaseATK = 10;
            var commander = CreateCommander();

            var unequipLog = new List<string>();
            var gear = CreateEquipment("sword", atkBonus: 5, unequipLog: unequipLog);
            commander.EquipmentInventory.Add(gear);

            var equipSystem = new EquipmentSystem();
            equipSystem.Equip(unit, gear, commander);
            equipSystem.Unequip(unit, commander);

            Assert.AreEqual(10, unit.BaseATK, "取り外しでATKが元に戻るべき");
            Assert.IsNull(unit.EquippedGear);
            Assert.IsTrue(commander.EquipmentInventory.Contains(gear), "インベントリに戻るべき");
            Assert.AreEqual(1, unequipLog.Count);
        }

        [Test]
        public void Equip_既に別の装備をしている場合は先に取り外してからインベントリへ返す()
        {
            var unit = CreateUnit("u1");
            unit.BaseATK = 10;
            var commander = CreateCommander();

            var gearA = CreateEquipment("gearA", atkBonus: 5);
            var gearB = CreateEquipment("gearB", atkBonus: 3);
            commander.EquipmentInventory.Add(gearA);
            commander.EquipmentInventory.Add(gearB);

            var equipSystem = new EquipmentSystem();
            equipSystem.Equip(unit, gearA, commander); // gearA装備
            equipSystem.Equip(unit, gearB, commander); // gearBへ付け替え

            Assert.AreEqual(gearB, unit.EquippedGear, "gearBが装備されているべき");
            Assert.AreEqual(13, unit.BaseATK, "10 + gearBの3 = 13であるべき");
            Assert.IsTrue(commander.EquipmentInventory.Contains(gearA),
                "取り外したgearAはインベントリへ戻るべき");
            Assert.IsFalse(commander.EquipmentInventory.Contains(gearB),
                "現在装備中のgearBはインベントリにないべき");
        }

        [Test]
        public void Equip_同じ装備を再装備しようとしても何も変わらない()
        {
            var unit = CreateUnit("u1");
            var commander = CreateCommander();

            var equipLog = new List<string>();
            var gear = CreateEquipment("gear1", equipLog: equipLog);
            commander.EquipmentInventory.Add(gear);

            var equipSystem = new EquipmentSystem();
            equipSystem.Equip(unit, gear, commander);
            equipSystem.Equip(unit, gear, commander); // 再装備

            Assert.AreEqual(1, equipLog.Count, "OnEquipは最初の1回のみ呼ばれるべき");
            Assert.AreEqual(gear, unit.EquippedGear);
        }

        [Test]
        public void Unequip_装備していないユニットにUnequipを呼んでも例外が出ない()
        {
            var unit = CreateUnit("u1");
            var commander = CreateCommander();
            var equipSystem = new EquipmentSystem();

            Assert.DoesNotThrow(() => equipSystem.Unequip(unit, commander));
            Assert.IsEmpty(commander.EquipmentInventory);
        }

        // ════════════════════════════════════════════════════════
        // ③ LostProcessor テスト
        // ════════════════════════════════════════════════════════

        [Test]
        public void ProcessPermanentLost_装備なしのユニットが死亡状態になる()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Active;
            unit.EquippedGear = null;
            var commander = CreateCommander();
            var lostProcessor = new LostProcessor();

            lostProcessor.ProcessPermanentLost(unit, commander);

            Assert.AreEqual(UnitState.Dead, unit.State);
            Assert.AreEqual(0, unit.CurrentHP);
            Assert.IsEmpty(commander.EquipmentInventory, "装備なしなのでインベントリは空のまま");
        }

        [Test]
        public void ProcessPermanentLost_装備持ちのユニットが死亡時に装備がインベントリへ返還される()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Active;

            var gear = CreateEquipment("sword");
            unit.EquippedGear = gear;

            var commander = CreateCommander();
            var lostProcessor = new LostProcessor();

            lostProcessor.ProcessPermanentLost(unit, commander);

            Assert.AreEqual(UnitState.Dead, unit.State);
            Assert.AreEqual(0, unit.CurrentHP);
            Assert.IsNull(unit.EquippedGear, "装備がはずれているべき");
            Assert.IsTrue(commander.EquipmentInventory.Contains(gear),
                "装備がインベントリへ返還されているべき");
        }

        [Test]
        public void ProcessPermanentLost_二重呼び出しでも冪等に動作する()
        {
            var unit = CreateUnit("u1");
            var gear = CreateEquipment("sword");
            unit.EquippedGear = gear;
            var commander = CreateCommander();
            var lostProcessor = new LostProcessor();

            lostProcessor.ProcessPermanentLost(unit, commander);
            lostProcessor.ProcessPermanentLost(unit, commander); // 2回目

            // インベントリには1つだけ入っているべき（2回入らない）
            Assert.AreEqual(1, commander.EquipmentInventory.Count,
                "装備の返還は1回分だけのはず（2回目はEquippedGear=nullでスキップ）");
        }

        [Test]
        public void ReturnEquipmentToInventory_装備がnullなら何もしない()
        {
            var unit = CreateUnit("u1");
            unit.EquippedGear = null;
            var commander = CreateCommander();
            var lostProcessor = new LostProcessor();

            Assert.DoesNotThrow(() => lostProcessor.ReturnEquipmentToInventory(unit, commander));
            Assert.IsEmpty(commander.EquipmentInventory);
        }

        // ════════════════════════════════════════════════════════
        // ④ RetreatSystem テスト
        // ════════════════════════════════════════════════════════

        [Test]
        public void ExecuteRetreat_しんがりが完全ロストしAccumulatedFailuresが増加する()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Active;
            unit.CurrentHP = 80;

            var commander = CreateCommander();
            commander.AccumulatedFailures = 0;

            var (ctx, rearguard) = CreateBattleSetup(unit);

            var lostProcessor = new LostProcessor();
            var retreatSystem = new RetreatSystem(lostProcessor);

            retreatSystem.ExecuteRetreat(rearguard, ctx, commander);

            Assert.AreEqual(UnitState.Dead, unit.State, "しんがりがDeadになるべき");
            Assert.AreEqual(0, unit.CurrentHP);
            Assert.AreEqual(BattleResult.CrushingDefeat, ctx.Result, "バトルが完敗扱いになるべき");
            Assert.AreEqual(1, commander.AccumulatedFailures, "敗北カウントが+1されるべき");
        }

        [Test]
        public void ExecuteRetreat_しんがりが装備を持っていればインベントリへ返還される()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Active;
            var gear = CreateEquipment("rearguard_gear");
            unit.EquippedGear = gear;

            var commander = CreateCommander();
            var (ctx, rearguard) = CreateBattleSetup(unit);

            var lostProcessor = new LostProcessor();
            var retreatSystem = new RetreatSystem(lostProcessor);

            retreatSystem.ExecuteRetreat(rearguard, ctx, commander);

            Assert.IsNull(unit.EquippedGear, "装備が外れているべき");
            Assert.IsTrue(commander.EquipmentInventory.Contains(gear),
                "装備がインベントリへ返還されているべき");
        }

        [Test]
        public void ExecuteRetreat_死亡済みのユニットをしんがりに指定するとInvalidOperationExceptionがスローされる()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Dead;
            unit.CurrentHP = 0;

            var commander = CreateCommander();
            var ctx = new BattleContext();
            var deadRu = new RuntimeUnit(unit, slotIndex: 0);
            // AllyUnitsには追加しない（死亡済みなので出撃中ではない）

            var lostProcessor = new LostProcessor();
            var retreatSystem = new RetreatSystem(lostProcessor);

            Assert.Throws<InvalidOperationException>(() =>
                retreatSystem.ExecuteRetreat(deadRu, ctx, commander));
        }

        [Test]
        public void ExecuteRetreat_出撃中でない控えユニットをしんがりに指定するとInvalidOperationExceptionがスローされる()
        {
            // 控えユニットはBattleContext.AllyUnitsに含まれない
            var unit = CreateUnit("u1");
            unit.State = UnitState.Reserve;

            var commander = CreateCommander();
            var ctx = new BattleContext(); // AllyUnitsに追加しない

            // RuntimeUnitは作れるが、AllyUnitsには入れない
            var reserveRu = new RuntimeUnit(unit, slotIndex: 0);

            var lostProcessor = new LostProcessor();
            var retreatSystem = new RetreatSystem(lostProcessor);

            Assert.Throws<InvalidOperationException>(() =>
                retreatSystem.ExecuteRetreat(reserveRu, ctx, commander));
        }

        [Test]
        public void ExecuteRetreat_複数回の撤退でAccumulatedFailuresが正しく累積する()
        {
            var commander = CreateCommander();
            commander.AccumulatedFailures = 1; // 既に1回敗北済み

            var lostProcessor = new LostProcessor();
            var retreatSystem = new RetreatSystem(lostProcessor);

            // 1回目の撤退
            {
                var unit = CreateUnit("u1");
                unit.State = UnitState.Active;
                var (ctx, ru) = CreateBattleSetup(unit);
                retreatSystem.ExecuteRetreat(ru, ctx, commander);
            }

            Assert.AreEqual(2, commander.AccumulatedFailures);

            // 2回目の撤退（合計3回でゲームオーバー相当）
            {
                var unit2 = CreateUnit("u2");
                unit2.State = UnitState.Active;
                var (ctx2, ru2) = CreateBattleSetup(unit2);
                retreatSystem.ExecuteRetreat(ru2, ctx2, commander);
            }

            Assert.AreEqual(3, commander.AccumulatedFailures,
                "3回でゲームオーバー閾値に達するべき");
        }

        [Test]
        public void CanDesignateAsRearguard_生存かつAllyUnits内ならtrue()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Active;
            var (ctx, ru) = CreateBattleSetup(unit);

            Assert.IsTrue(RetreatSystem.CanDesignateAsRearguard(ru, ctx));
        }

        [Test]
        public void CanDesignateAsRearguard_死亡済みならfalse()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Dead;
            unit.CurrentHP = 0;

            var ctx = new BattleContext();
            var ru = new RuntimeUnit(unit, slotIndex: 0);
            ctx.AllyUnits.Add(ru);

            Assert.IsFalse(RetreatSystem.CanDesignateAsRearguard(ru, ctx));
        }

        [Test]
        public void CanDesignateAsRearguard_AllyUnits外ならfalse()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Reserve;
            var ctx = new BattleContext();
            var ru = new RuntimeUnit(unit, slotIndex: 0);
            // AllyUnitsには追加しない

            Assert.IsFalse(RetreatSystem.CanDesignateAsRearguard(ru, ctx));
        }

        // ════════════════════════════════════════════════════════
        // ⑤ ReserveRecoverySystem テスト
        // ════════════════════════════════════════════════════════

        [Test]
        public void RecoverReserveUnits_控えユニットのHPが回復する()
        {
            var reserveUnit = CreateUnit("u1");
            reserveUnit.State = UnitState.Reserve;
            reserveUnit.MaxHP = 100;
            reserveUnit.CurrentHP = 40;

            var recovery = new ReserveRecoverySystem(recoveryRate: 0.3f);
            recovery.RecoverReserveUnits(new[] { reserveUnit });

            // 40 + (100 * 0.3) = 40 + 30 = 70
            Assert.AreEqual(70, reserveUnit.CurrentHP);
        }

        [Test]
        public void RecoverReserveUnits_回復量がMaxHPを超えない()
        {
            var reserveUnit = CreateUnit("u1");
            reserveUnit.State = UnitState.Reserve;
            reserveUnit.MaxHP = 100;
            reserveUnit.CurrentHP = 95; // 95 + 30 = 125 → MaxHP(100)でクランプ

            var recovery = new ReserveRecoverySystem(recoveryRate: 0.3f);
            recovery.RecoverReserveUnits(new[] { reserveUnit });

            Assert.AreEqual(100, reserveUnit.CurrentHP, "MaxHPを超えないべき");
        }

        [Test]
        public void RecoverReserveUnits_出撃中のユニットは回復しない()
        {
            var activeUnit = CreateUnit("u1");
            activeUnit.State = UnitState.Active;
            activeUnit.MaxHP = 100;
            activeUnit.CurrentHP = 50;

            var recovery = new ReserveRecoverySystem(recoveryRate: 0.3f);
            recovery.RecoverReserveUnits(new[] { activeUnit });

            Assert.AreEqual(50, activeUnit.CurrentHP, "出撃中のユニットは回復しないべき");
        }

        [Test]
        public void RecoverReserveUnits_死亡済みのユニットは回復しない()
        {
            var deadUnit = CreateUnit("u1");
            deadUnit.State = UnitState.Dead;
            deadUnit.CurrentHP = 0;
            deadUnit.MaxHP = 100;

            var recovery = new ReserveRecoverySystem(recoveryRate: 0.3f);
            recovery.RecoverReserveUnits(new[] { deadUnit });

            Assert.AreEqual(0, deadUnit.CurrentHP, "死亡ユニットのHPは0のままであるべき");
        }

        [Test]
        public void RecoverReserveUnits_控えでもCurrentHP0は回復しない()
        {
            // 控えに居て現在HPが0というケースは通常発生しないが、
            // 状態が Reset されていない場合などのガードを確認する
            var unit = CreateUnit("u1");
            unit.State = UnitState.Reserve;
            unit.CurrentHP = 0;
            unit.MaxHP = 100;

            var recovery = new ReserveRecoverySystem(recoveryRate: 0.3f);
            recovery.RecoverReserveUnits(new[] { unit });

            Assert.AreEqual(0, unit.CurrentHP, "CurrentHP=0の控えは回復しないべき");
        }

        [Test]
        public void RecoverReserveUnits_混在リストで控え生存ユニットのみ回復する()
        {
            var active = CreateUnit("active");
            active.State = UnitState.Active;
            active.CurrentHP = 50;

            var reserve = CreateUnit("reserve");
            reserve.State = UnitState.Reserve;
            reserve.CurrentHP = 50;

            var dead = CreateUnit("dead");
            dead.State = UnitState.Dead;
            dead.CurrentHP = 0;

            var recovery = new ReserveRecoverySystem(recoveryRate: 0.3f);
            recovery.RecoverReserveUnits(new[] { active, reserve, dead });

            Assert.AreEqual(50, active.CurrentHP, "出撃中は変化なし");
            Assert.AreEqual(80, reserve.CurrentHP, "控えは50+30=80に回復");
            Assert.AreEqual(0, dead.CurrentHP, "死亡は変化なし");
        }

        [Test]
        public void ReserveRecoverySystem_回復割合0のとき回復量が0になる()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Reserve;
            unit.CurrentHP = 50;
            unit.MaxHP = 100;

            var recovery = new ReserveRecoverySystem(recoveryRate: 0f);
            recovery.RecoverReserveUnits(new[] { unit });

            Assert.AreEqual(50, unit.CurrentHP, "回復割合0%なら変化なし");
        }

        [Test]
        public void ReserveRecoverySystem_回復割合1のときMaxHP到達する()
        {
            var unit = CreateUnit("u1");
            unit.State = UnitState.Reserve;
            unit.CurrentHP = 1;
            unit.MaxHP = 100;

            var recovery = new ReserveRecoverySystem(recoveryRate: 1f);
            recovery.RecoverReserveUnits(new[] { unit });

            Assert.AreEqual(100, unit.CurrentHP, "回復割合100%でMaxHPになるべき");
        }

        [Test]
        public void ReserveRecoverySystem_回復割合が範囲外の場合は例外がスローされる()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ReserveRecoverySystem(recoveryRate: -0.1f));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ReserveRecoverySystem(recoveryRate: 1.1f));
        }

        // ════════════════════════════════════════════════════════
        // ① GrowthSystem 追加テスト（修正対応）
        // ════════════════════════════════════════════════════════

        [Test]
        public void AddExp_セレクターがnullを返した場合レベルが上がらずEXPも消費されない()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            // Lv1→2に1個消費できる分をセット
            var commander = CreateCommander(expItems: 5);
            commander.TotalExpItems = 5;

            // 常にnullを返すセレクター（キャンセル操作をシミュレート）
            var growth = new GrowthSystem((u, opts) => null);

            // EXP加算（2個あればLv2になれるはずだが、セレクターがnullを返すので保留）
            growth.AddExp(unit, commander, 5);

            // レベルが上がっていない
            Assert.AreEqual(1, unit.CurrentLevel, "セレクターnull時はレベルが上がらないべき");
            // EXPはunit.CurrentExpに加算されているが、消費はされていない
            // （AddExpでcommander→unitへの付け替えは行われるが、ProcessLevelUpsでの消費がない）
            Assert.AreEqual(5, unit.CurrentExp, "EXPはunitに加算済みだがLv2分(2個)は消費されていないべき");
            // 強化は一切適用されていない
            Assert.AreEqual(0, log.Count, "強化が適用されていないべき");
            Assert.AreEqual(0, unit.AppliedUpgrades.Count);
        }

        [Test]
        public void AddExp_Lv5到達時に余剰EXPが0にリセットされる()
        {
            var log = new List<string>();
            var unit = CreateUnitWithUpgrades("u1", log);
            // Lv5まで必要なEXP合計: 2+3+4+5=14個。余剰として1個多く渡す
            var commander = CreateCommander(expItems: 15);

            var selectionOrder = new Queue<string>(new[] { "upgrade_A", "upgrade_B", "upgrade_C" });
            var growth = new GrowthSystem((u, opts) =>
                opts.Find(up => up.UpgradeId == selectionOrder.Dequeue()));

            growth.AddExp(unit, commander, 15);

            Assert.AreEqual(5, unit.CurrentLevel, "Lv5に到達しているべき");
            Assert.AreEqual(0, unit.CurrentExp,
                "Lv5到達時に余剰EXPが0にリセットされるべき（1個の余剰が切り捨てられる）");
        }

        // ════════════════════════════════════════════════════════
        // ② EquipmentSystem 追加テスト（修正対応）
        // ════════════════════════════════════════════════════════

        [Test]
        public void Equip_付け替え後のインベントリ整合性が正しくアイテム数に矛盾がない()
        {
            var unit = CreateUnit("u1");
            var commander = CreateCommander();

            var gearA = CreateEquipment("gearA");
            var gearB = CreateEquipment("gearB");

            // 初期状態: gearA・gearB両方がインベントリにある
            commander.EquipmentInventory.Add(gearA);
            commander.EquipmentInventory.Add(gearB);

            var equipSystem = new EquipmentSystem();

            // gearAを装備
            equipSystem.Equip(unit, gearA, commander);

            // 装備後: gearBのみインベントリにある（gearAは装備中）
            Assert.AreEqual(1, commander.EquipmentInventory.Count,
                "gearA装備後はインベントリに1つ残るべき");
            Assert.IsTrue(commander.EquipmentInventory.Contains(gearB));

            // gearBに付け替え
            equipSystem.Equip(unit, gearB, commander);

            // 付け替え後: gearAがインベントリに戻り、gearBは装備中
            Assert.AreEqual(1, commander.EquipmentInventory.Count,
                "付け替え後もインベントリは1つのままであるべき（gearAが戻る）");
            Assert.IsTrue(commander.EquipmentInventory.Contains(gearA),
                "外したgearAがインベントリへ戻るべき");
            Assert.IsFalse(commander.EquipmentInventory.Contains(gearB),
                "現在装備中のgearBはインベントリにないべき");
            Assert.AreEqual(gearB, unit.EquippedGear, "gearBが装備されているべき");
        }

        // ════════════════════════════════════════════════════════
        // 統合シナリオ: 撤退→しんがり→装備返還→AccumulatedFailures
        // ════════════════════════════════════════════════════════

        [Test]
        public void IntegrationScenario_撤退実行後の全状態が正しく連動する()
        {
            // セットアップ
            var rearguardUnit = CreateUnit("rearguard");
            rearguardUnit.State = UnitState.Active;
            rearguardUnit.CurrentHP = 60;
            var gear = CreateEquipment("rearguard_sword");
            rearguardUnit.EquippedGear = gear;

            var aliveUnit = CreateUnit("alive");
            aliveUnit.State = UnitState.Active;
            aliveUnit.CurrentHP = 80;

            var commander = CreateCommander();
            commander.AccumulatedFailures = 0;

            var ctx = new BattleContext();
            var rearguardRu = new RuntimeUnit(rearguardUnit, slotIndex: 0);
            var aliveRu = new RuntimeUnit(aliveUnit, slotIndex: 1);
            ctx.AllyUnits.Add(rearguardRu);
            ctx.AllyUnits.Add(aliveRu);

            var lostProcessor = new LostProcessor();
            var retreatSystem = new RetreatSystem(lostProcessor);

            // 撤退実行（しんがり指定）
            retreatSystem.ExecuteRetreat(rearguardRu, ctx, commander);

            // 検証
            Assert.AreEqual(UnitState.Dead, rearguardUnit.State, "しんがりはDead");
            Assert.AreEqual(0, rearguardUnit.CurrentHP, "しんがりのHPは0");
            Assert.IsNull(rearguardUnit.EquippedGear, "装備が外れている");
            Assert.IsTrue(commander.EquipmentInventory.Contains(gear), "装備がインベントリへ返還");
            Assert.AreEqual(BattleResult.CrushingDefeat, ctx.Result, "バトル結果が完敗（CrushingDefeat）");
            Assert.AreEqual(1, commander.AccumulatedFailures, "敗北カウント+1");

            // 生き残ったユニットはそのまま維持される
            Assert.AreEqual(UnitState.Active, aliveUnit.State, "生存ユニットはそのまま");
            Assert.AreEqual(80, aliveUnit.CurrentHP, "生存ユニットのHPも維持");
        }
    }
}
