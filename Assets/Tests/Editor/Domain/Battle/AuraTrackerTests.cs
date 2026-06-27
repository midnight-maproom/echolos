using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Aura;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class AuraTrackerTests
    {
        private const string GuardianName = "王家の加護";
        private const string CovenantName = "連携";

        private static RuntimeUnit MakeUnit(string id, int slot, int hp = 100)
        {
            var u = new Unit(id, id, Element.None)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = 20,
                DEF = 0,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static BattleContext MakeContext(
            List<RuntimeUnit> allies = null, List<RuntimeUnit> enemies = null)
        {
            return new BattleContext(10)
            {
                AllyUnits = allies ?? new List<RuntimeUnit>(),
                EnemyUnits = enemies ?? new List<RuntimeUnit>(),
            };
        }

        private static AuraDefinition GuardianDef() => new AuraDefinition(
            UniqueUnitIds.Princess,
            GuardianName,
            new[] { new AuraBuff(EffectKind.DefenseUp, 3) },
            boostUpgradeKind: UpgradeKind.AuraBoost);

        private static AuraDefinition CovenantDef() => new AuraDefinition(
            UniqueUnitIds.Bridget,
            CovenantName,
            new[] { new AuraBuff(EffectKind.AttackUp, 3) },
            boostUpgradeKind: null,
            requiredPartnerUnitIds: new[] { UniqueUnitIds.Princess },
            targetMode: AuraTargetMode.SelfAndPartners);

        private static int CountByAuraSource(RuntimeUnit ru, string auraSourceId)
        {
            int n = 0;
            foreach (var e in ru.ActiveEffects)
                if (e != null && e.AuraSourceId == auraSourceId) n++;
            return n;
        }

        [Test]
        public void 王女死亡_王家の加護が陣営全員から剥がれる()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            var knight = MakeUnit("knight", 1);
            var archer = MakeUnit("archer", 2);
            var allies = new List<RuntimeUnit> { princess, knight, archer };
            var ctx = MakeContext(allies);
            var defs = new[] { GuardianDef() };

            AuraApplier.ApplyAll(ctx, defs);
            // 付与確認
            Assert.AreEqual(1, CountByAuraSource(princess, GuardianName));
            Assert.AreEqual(1, CountByAuraSource(knight, GuardianName));

            // 王女戦闘不能 → Tracker 通知
            princess.BaseUnit.CurrentHP = 0;
            var tracker = new AuraTracker(ctx, defs);
            tracker.HandleUnitDied(ctx, princess);
            // HandleUnitDied だけではキュー追加のみ＝剥奪されない
            Assert.AreEqual(1, CountByAuraSource(princess, GuardianName), "Flush 前は剥奪されない");
            tracker.FlushPendingDeaths(ctx);

            Assert.AreEqual(0, CountByAuraSource(princess, GuardianName));
            Assert.AreEqual(0, CountByAuraSource(knight, GuardianName));
            Assert.AreEqual(0, CountByAuraSource(archer, GuardianName));
        }

        [Test]
        public void ブリジット死亡_連携が両者から剥がれる()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            var allies = new List<RuntimeUnit> { bridget, princess };
            var ctx = MakeContext(allies);
            var defs = new[] { CovenantDef() };

            AuraApplier.ApplyAll(ctx, defs);
            Assert.AreEqual(1, CountByAuraSource(bridget, CovenantName));
            Assert.AreEqual(1, CountByAuraSource(princess, CovenantName));

            bridget.BaseUnit.CurrentHP = 0;
            var tracker = new AuraTracker(ctx, defs);
            tracker.HandleUnitDied(ctx, bridget);
            tracker.FlushPendingDeaths(ctx);

            Assert.AreEqual(0, CountByAuraSource(bridget, CovenantName));
            Assert.AreEqual(0, CountByAuraSource(princess, CovenantName));
        }

        [Test]
        public void 王女死亡_連携も剥がれる_パートナー死亡経路()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            var allies = new List<RuntimeUnit> { bridget, princess };
            var ctx = MakeContext(allies);
            var defs = new[] { GuardianDef(), CovenantDef() };

            AuraApplier.ApplyAll(ctx, defs);
            Assert.AreEqual(1, CountByAuraSource(bridget, CovenantName));
            Assert.AreEqual(1, CountByAuraSource(princess, CovenantName));

            princess.BaseUnit.CurrentHP = 0;
            var tracker = new AuraTracker(ctx, defs);
            tracker.HandleUnitDied(ctx, princess);
            tracker.FlushPendingDeaths(ctx);

            // 王家の加護も連携も両方剥がれる
            Assert.AreEqual(0, CountByAuraSource(bridget, GuardianName));
            Assert.AreEqual(0, CountByAuraSource(princess, GuardianName));
            Assert.AreEqual(0, CountByAuraSource(bridget, CovenantName));
            Assert.AreEqual(0, CountByAuraSource(princess, CovenantName));
        }

        [Test]
        public void 関係ないユニット死亡_何も起きない()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            var knight = MakeUnit("knight", 1);
            var allies = new List<RuntimeUnit> { princess, knight };
            var ctx = MakeContext(allies);
            var defs = new[] { GuardianDef() };

            AuraApplier.ApplyAll(ctx, defs);
            Assert.AreEqual(1, CountByAuraSource(princess, GuardianName));
            Assert.AreEqual(1, CountByAuraSource(knight, GuardianName));

            knight.BaseUnit.CurrentHP = 0;
            var tracker = new AuraTracker(ctx, defs);
            tracker.HandleUnitDied(ctx, knight);
            tracker.FlushPendingDeaths(ctx);

            // 王女は依存先ではないので王家の加護は剥がれない
            Assert.AreEqual(1, CountByAuraSource(princess, GuardianName));
            // knight も「自分が剥がす対象」ではないため王家の加護を持ったまま
            // （戦闘不能ユニットの effects 削除は別経路：UI 側で snapshot をクリア／実バトルでは
            //  影響なし＝行動しないので参照されない）
            Assert.AreEqual(1, CountByAuraSource(knight, GuardianName));
        }

        [Test]
        public void null安全_definition_unitとも()
        {
            var ctx = MakeContext();
            var tracker = new AuraTracker(ctx, null);
            Assert.DoesNotThrow(() => tracker.HandleUnitDied(ctx, null));
            Assert.DoesNotThrow(() => tracker.FlushPendingDeaths(ctx));
        }

        [Test]
        public void 同一unit重複追加は無視される()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            var knight = MakeUnit("knight", 1);
            var allies = new List<RuntimeUnit> { princess, knight };
            var ctx = MakeContext(allies);
            var defs = new[] { GuardianDef() };

            AuraApplier.ApplyAll(ctx, defs);

            princess.BaseUnit.CurrentHP = 0;
            var tracker = new AuraTracker(ctx, defs);
            // 同 unit を Executor.OnUnitDied と StatusProcessor.OnStatusEffectKill の
            // 両経路から登録する可能性を想定したシナリオ。
            tracker.HandleUnitDied(ctx, princess);
            tracker.HandleUnitDied(ctx, princess);

            Assert.DoesNotThrow(() => tracker.FlushPendingDeaths(ctx));
            Assert.AreEqual(0, CountByAuraSource(princess, GuardianName));
            Assert.AreEqual(0, CountByAuraSource(knight, GuardianName));
        }
    }
}
