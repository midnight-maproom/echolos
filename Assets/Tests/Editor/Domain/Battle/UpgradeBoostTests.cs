using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    /// <summary>
    /// WazaPowerBoost / PersistentEffectBoost Upgrade の動作検証。
    /// HealEffect / ApplyStatusEffectEffect から Actor.AppliedUpgrades が読まれて
    /// wazaPower / Magnitude に加算されることを確認する。
    /// PersistentEffectBoost は UpgradeMagnitudeResolver 単独テストで担保
    /// （Bootstrap.PrepareForBattle は Presentation 層のためロジック単体に限定）。
    /// </summary>
    [TestFixture]
    public class UpgradeBoostTests
    {
        private sealed class StubContext : IActionContext
        {
            public BattleContext Battle { get; set; }
            public RuntimeUnit Actor { get; set; }
            public IList<RuntimeUnit> Targets { get; set; } = new List<RuntimeUnit>();
            public Func<int> Random0To99 { get; set; } = () => 50;
            public IList<HitOutcome> Outcomes { get; set; } = new List<HitOutcome>();
            public string CurrentWazaId { get; set; }
        }

        private static RuntimeUnit Make(string id, int hp = 100, int atk = 30, int def = 10)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        // ── WazaPowerBoost × HealEffect ──

        [Test]
        public void WazaPowerBoost_HealEffectのwazaPowerに加算される()
        {
            var actor = Make("a", hp: 100);
            actor.BaseUnit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.WazaPowerBoost, magnitude: 2,
                targetWazaId: "waza_test_heal"));

            var target = Make("t", hp: 100);
            target.BaseUnit.CurrentHP = 30;
            var ctx = new StubContext
            {
                Actor = actor,
                Targets = new List<RuntimeUnit> { target },
                CurrentWazaId = "waza_test_heal",
            };

            // wazaPower=3 + boost=2 → 実効 wazaPower=5 → √100 × 5 = 50 回復
            new HealEffect(wazaPower: 3.0).Apply(ctx);

            Assert.AreEqual(80, target.BaseUnit.CurrentHP,
                "WazaPowerBoost が wazaPower に加算され回復量が増える");
        }

        [Test]
        public void WazaPowerBoost_TargetWazaId不一致は加算されない()
        {
            var actor = Make("a", hp: 100);
            actor.BaseUnit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.WazaPowerBoost, magnitude: 5,
                targetWazaId: "waza_別の技"));

            var target = Make("t", hp: 100);
            target.BaseUnit.CurrentHP = 30;
            var ctx = new StubContext
            {
                Actor = actor,
                Targets = new List<RuntimeUnit> { target },
                CurrentWazaId = "waza_test_heal",
            };

            // boost 加算されない＝√100 × 3 = 30 回復
            new HealEffect(wazaPower: 3.0).Apply(ctx);

            Assert.AreEqual(60, target.BaseUnit.CurrentHP,
                "TargetWazaId 不一致の WazaPowerBoost は加算されない");
        }

        [Test]
        public void WazaPowerBoost_CurrentWazaIdがnullなら加算されない()
        {
            var actor = Make("a", hp: 100);
            actor.BaseUnit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.WazaPowerBoost, magnitude: 5,
                targetWazaId: "waza_test_heal"));

            var target = Make("t", hp: 100);
            target.BaseUnit.CurrentHP = 30;
            var ctx = new StubContext
            {
                Actor = actor,
                Targets = new List<RuntimeUnit> { target },
                CurrentWazaId = null,
            };

            new HealEffect(wazaPower: 3.0).Apply(ctx);

            Assert.AreEqual(60, target.BaseUnit.CurrentHP,
                "CurrentWazaId=null では WazaPowerBoost は適用されない");
        }

        // ── WazaPowerBoost × ApplyStatusEffectEffect ──

        [Test]
        public void WazaPowerBoost_ApplyStatusEffectのMagnitudeに加算される()
        {
            var actor = Make("a");
            actor.BaseUnit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.WazaPowerBoost, magnitude: 5,
                targetWazaId: "waza_test_buff"));

            var target = Make("t");
            var ctx = new StubContext
            {
                Actor = actor,
                Targets = new List<RuntimeUnit> { target },
                CurrentWazaId = "waza_test_buff",
            };

            var template = new AbilityModifier(EffectKind.AttackUp, magnitude: 10f)
            {
                Lifetime = Lifetime.Triggered,
                RemainingTurns = 3,
            };
            new ApplyStatusEffectEffect(new IEffect[] { template }).Apply(ctx);

            // template Magnitude=10 + boost=5 → 15 が乗る
            var applied = target.FindEffect(e => e.Kind == EffectKind.AttackUp) as AbilityModifier;
            Assert.IsNotNull(applied, "AttackUp が付与されている");
            Assert.AreEqual(15f, applied.Magnitude,
                "WazaPowerBoost が template Magnitude に加算される");

            // テンプレ自体は破壊されない（次回適用時も元値）
            Assert.AreEqual(10f, template.Magnitude,
                "テンプレ自体の Magnitude は破壊されない");
        }

        [Test]
        public void WazaPowerBoost_適用Upgradeなしならテンプレそのまま()
        {
            var actor = Make("a");
            var target = Make("t");
            var ctx = new StubContext
            {
                Actor = actor,
                Targets = new List<RuntimeUnit> { target },
                CurrentWazaId = "waza_test_buff",
            };

            var template = new AbilityModifier(EffectKind.AttackUp, magnitude: 10f)
            {
                Lifetime = Lifetime.Triggered,
                RemainingTurns = 3,
            };
            new ApplyStatusEffectEffect(new IEffect[] { template }).Apply(ctx);

            var applied = target.FindEffect(e => e.Kind == EffectKind.AttackUp) as AbilityModifier;
            Assert.IsNotNull(applied);
            Assert.AreEqual(10f, applied.Magnitude, "AppliedUpgrades なしならテンプレ値そのまま");
        }

        // ── UpgradeMagnitudeResolver 単独テスト（PersistentEffectBoost を含む） ──

        [Test]
        public void Resolver_PersistentEffectBoost_KindとSourceAbilityName一致で加算()
        {
            var unit = new Unit("u", "u");
            unit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_専守強化", "テスト", "テスト",
                UpgradeKind.PersistentEffectBoost, magnitude: 10,
                targetSourceAbilityName: "専守",
                targetEffectKind: EffectKind.IncomingDamageDown));

            int sum = UpgradeMagnitudeResolver.SumPersistentEffectBoost(
                unit, EffectKind.IncomingDamageDown, "専守");
            Assert.AreEqual(10, sum);
        }

        [Test]
        public void Resolver_PersistentEffectBoost_Kind不一致は加算しない()
        {
            var unit = new Unit("u", "u");
            unit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.PersistentEffectBoost, magnitude: 10,
                targetSourceAbilityName: "専守",
                targetEffectKind: EffectKind.IncomingDamageDown));

            // 別 Kind で照会＝合計 0
            int sum = UpgradeMagnitudeResolver.SumPersistentEffectBoost(
                unit, EffectKind.AttackUp, "専守");
            Assert.AreEqual(0, sum);
        }

        [Test]
        public void Resolver_PersistentEffectBoost_SourceAbilityName不一致は加算しない()
        {
            var unit = new Unit("u", "u");
            unit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.PersistentEffectBoost, magnitude: 10,
                targetSourceAbilityName: "専守",
                targetEffectKind: EffectKind.IncomingDamageDown));

            int sum = UpgradeMagnitudeResolver.SumPersistentEffectBoost(
                unit, EffectKind.IncomingDamageDown, "別の能力");
            Assert.AreEqual(0, sum);
        }

        [Test]
        public void Resolver_WazaPowerBoost_TargetWazaId一致で加算()
        {
            var unit = new Unit("u", "u");
            unit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test", "テスト", "テスト",
                UpgradeKind.WazaPowerBoost, magnitude: 3,
                targetWazaId: "waza_test"));
            unit.AppliedUpgrades.Add(new UnitUpgrade(
                "up_test2", "テスト2", "テスト",
                UpgradeKind.WazaPowerBoost, magnitude: 2,
                targetWazaId: "waza_test"));

            int sum = UpgradeMagnitudeResolver.SumWazaPowerBoost(unit, "waza_test");
            Assert.AreEqual(5, sum, "同 TargetWazaId 複数は合算される");
        }

        // ── EffectMagnitudeAccumulator ──

        [Test]
        public void Accumulator_AbilityModifierのMagnitudeを加算する()
        {
            var mod = new AbilityModifier(EffectKind.AttackUp, magnitude: 10f);
            EffectMagnitudeAccumulator.Add(mod, 5f);
            Assert.AreEqual(15f, mod.Magnitude);
        }

        [Test]
        public void Accumulator_FlagはMagnitudeを持たないので無視()
        {
            var flag = new IgnoreCounterFlag();
            // 例外を投げず、何もしない
            Assert.DoesNotThrow(() => EffectMagnitudeAccumulator.Add(flag, 10f));
        }
    }
}
