using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Aura;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class AuraApplierTests
    {
        private const string GuardianName = "王家の加護";

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

        private static void GrantBoost(RuntimeUnit ru, int magnitude)
        {
            ru.BaseUnit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_aura_guard_plus_2",
                "王家の加護 +2",
                "テスト用",
                UpgradeKind.AuraBoost,
                magnitude));
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
            UpgradeKind.AuraBoost);

        private const string CovenantName = "連携";

        // ブリジット「連携（王女）」：王女が同時出撃しているときだけ、ブリジット＋王女に ATK +3。
        // BoostUpgradeKind=null＝メタ強化はオーラ量に影響しない。
        private static AuraDefinition CovenantDef() => new AuraDefinition(
            UniqueUnitIds.Bridget,
            CovenantName,
            new[] { new AuraBuff(EffectKind.AttackUp, 3) },
            boostUpgradeKind: null,
            requiredPartnerUnitIds: new[] { UniqueUnitIds.Princess },
            targetMode: AuraTargetMode.SelfAndPartners);

        private static int SumMagnitudeBy(RuntimeUnit ru, EffectKind kind, string sourceName)
        {
            int total = 0;
            foreach (var e in ru.ActiveEffects)
            {
                if (e == null) continue;
                if (e.Kind != kind) continue;
                if (e.SourceAbilityName != sourceName) continue;
                total += (int)TestEff.MagnitudeOf(e) * e.Stacks;
            }
            return total;
        }

        [Test]
        public void SourceUnit不在_効果なし()
        {
            var allies = new List<RuntimeUnit>
            {
                MakeUnit("knight", 0),
                MakeUnit("archer", 1),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void SourceUnit生存_陣営全員にDEF3付与()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            var allies = new List<RuntimeUnit>
            {
                princess,
                MakeUnit("knight", 1),
                MakeUnit("archer", 2),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            foreach (var u in allies)
                Assert.AreEqual(3, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void AuraBoost1個_DEF5付与()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            GrantBoost(princess, 2);
            var allies = new List<RuntimeUnit>
            {
                princess,
                MakeUnit("knight", 1),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            foreach (var u in allies)
                Assert.AreEqual(5, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void AuraBoost2個_DEF7付与()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            GrantBoost(princess, 2);
            GrantBoost(princess, 2);
            var allies = new List<RuntimeUnit>
            {
                princess,
                MakeUnit("knight", 1),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            foreach (var u in allies)
                Assert.AreEqual(7, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void 敵陣営にPrincessがいても味方には付与されない()
        {
            var allies = new List<RuntimeUnit>
            {
                MakeUnit("knight", 0),
                MakeUnit("archer", 1),
            };
            var enemies = new List<RuntimeUnit>
            {
                MakeUnit(UniqueUnitIds.Princess, 0),
            };
            var ctx = MakeContext(allies, enemies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
            foreach (var u in enemies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void SourceUnit戦闘不能_発動しない()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            princess.BaseUnit.CurrentHP = 0;
            var allies = new List<RuntimeUnit>
            {
                princess,
                MakeUnit("knight", 1),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void 戦闘不能ユニットには付与されない()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            var dead = MakeUnit("dead_knight", 1);
            dead.BaseUnit.CurrentHP = 0;
            var allies = new List<RuntimeUnit>
            {
                princess,
                dead,
                MakeUnit("alive_archer", 2),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            Assert.AreEqual(3, SumMagnitudeBy(princess, EffectKind.DefenseUp, GuardianName));
            Assert.AreEqual(0, SumMagnitudeBy(dead, EffectKind.DefenseUp, GuardianName));
            Assert.AreEqual(3, SumMagnitudeBy(allies[2], EffectKind.DefenseUp, GuardianName));
        }

        [Test]
        public void SourceAbilityName伝搬()
        {
            var princess = MakeUnit(UniqueUnitIds.Princess, 0);
            var allies = new List<RuntimeUnit> { princess };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { GuardianDef() });

            bool found = false;
            foreach (var e in princess.ActiveEffects)
                if (e != null && e.Kind == EffectKind.DefenseUp && e.SourceAbilityName == GuardianName)
                    found = true;
            Assert.IsTrue(found, "SourceAbilityName が '王家の加護' で付与されていること");
        }

        // ══════════════════════════════════════════════
        // 連携（王女）：RequiredPartnerUnitIds ＋ SelfAndPartners
        // ══════════════════════════════════════════════

        [Test]
        public void 連携_ブリジット単独_王女不在で不発()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            var allies = new List<RuntimeUnit>
            {
                bridget,
                MakeUnit("knight", 1),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { CovenantDef() });

            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.AttackUp, CovenantName));
        }

        [Test]
        public void 連携_ブリジットと王女同時出撃_両者にATK3付与()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            var allies = new List<RuntimeUnit>
            {
                bridget,
                princess,
                MakeUnit("knight", 2),
            };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { CovenantDef() });

            Assert.AreEqual(3, SumMagnitudeBy(bridget, EffectKind.AttackUp, CovenantName));
            Assert.AreEqual(3, SumMagnitudeBy(princess, EffectKind.AttackUp, CovenantName));
        }

        [Test]
        public void 連携_第三者には付与されない()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            var knight = MakeUnit("knight", 2);
            var allies = new List<RuntimeUnit> { bridget, princess, knight };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { CovenantDef() });

            Assert.AreEqual(0, SumMagnitudeBy(knight, EffectKind.AttackUp, CovenantName));
        }

        [Test]
        public void 連携_王女戦闘不能で不発()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            princess.BaseUnit.CurrentHP = 0;
            var allies = new List<RuntimeUnit> { bridget, princess };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { CovenantDef() });

            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.AttackUp, CovenantName));
        }

        [Test]
        public void 連携_ブリジット戦闘不能で不発()
        {
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            bridget.BaseUnit.CurrentHP = 0;
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            var allies = new List<RuntimeUnit> { bridget, princess };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { CovenantDef() });

            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.AttackUp, CovenantName));
        }

        [Test]
        public void 連携_BoostUpgradeKindなしでメタ強化はバフ量に乗らない()
        {
            // CovenantDef は boostUpgradeKind: null 指定。ブリジット側に AuraBoost を
            // 積んでも連携バフ量は固定 +3 で変わらない。
            var bridget = MakeUnit(UniqueUnitIds.Bridget, 0);
            GrantBoost(bridget, 2);
            GrantBoost(bridget, 2);
            var princess = MakeUnit(UniqueUnitIds.Princess, 1);
            var allies = new List<RuntimeUnit> { bridget, princess };
            var ctx = MakeContext(allies);

            AuraApplier.ApplyAll(ctx, new[] { CovenantDef() });

            Assert.AreEqual(3, SumMagnitudeBy(bridget, EffectKind.AttackUp, CovenantName));
            Assert.AreEqual(3, SumMagnitudeBy(princess, EffectKind.AttackUp, CovenantName));
        }
    }
}
