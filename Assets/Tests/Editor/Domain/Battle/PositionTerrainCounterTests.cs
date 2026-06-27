using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class PositionTerrainCounterTests
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

        private static RuntimeUnit Make(string id, int slot, int atk, int def, AttackKind kind,
            Element element = Element.None, int hp = 100)
        {
            var u = new Unit(id, id, element)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = kind,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        // ── 配置 ATK 補正の動的計算 ──

        [Test]
        public void 近接_最前_slot0は100パーセント補正()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee);
            var ally1 = Make("a1", 1, atk: 0, def: 0, kind: AttackKind.Melee);
            var enemy = Make("e", 0, atk: 0, def: 0, kind: AttackKind.Melee);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker, ally1 },
                EnemyUnits = new List<RuntimeUnit> { enemy },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { enemy } };

            new AttackEffect().Apply(ctx);

            int frontDmg = ctx.Outcomes[0].Damage;
            Assert.Greater(frontDmg, 0);
        }

        [Test]
        public void 近接_slot2と最前で補正差_slot2のほうが低ダメ()
        {
            // 6 体編成・近接・slot 0 = 100% / slot 2 = 95%
            var slot0Attacker = Make("a0", 0, atk: 100, def: 0, kind: AttackKind.Melee);
            var slot2Attacker = Make("a2", 2, atk: 100, def: 0, kind: AttackKind.Melee);
            var fillers = new List<RuntimeUnit>();
            for (int i = 0; i < 6; i++) fillers.Add(Make($"f{i}", i, 0, 0, AttackKind.Melee));
            fillers[0] = slot0Attacker;
            fillers[2] = slot2Attacker;
            var enemy = Make("e", 0, atk: 0, def: 0, kind: AttackKind.Melee, hp: 9999);
            var battle = new BattleContext(10)
            {
                AllyUnits = fillers,
                EnemyUnits = new List<RuntimeUnit> { enemy },
            };

            var ctx0 = new StubContext { Battle = battle, Actor = slot0Attacker, Targets = new List<RuntimeUnit> { enemy } };
            new AttackEffect().Apply(ctx0);
            int dmg0 = ctx0.Outcomes[0].Damage;

            int hpAfter0 = enemy.CurrentHP;
            var ctx2 = new StubContext { Battle = battle, Actor = slot2Attacker, Targets = new List<RuntimeUnit> { enemy } };
            new AttackEffect().Apply(ctx2);
            int dmg2 = ctx2.Outcomes[0].Damage;

            Assert.Greater(dmg0, dmg2);
        }

        [Test]
        public void 遠隔_最後尾から距離0は100パーセント補正()
        {
            var attacker = Make("a", 2, atk: 50, def: 0, kind: AttackKind.Ranged);
            var ally0 = Make("a0", 0, atk: 0, def: 0, kind: AttackKind.Melee);
            var ally1 = Make("a1", 1, atk: 0, def: 0, kind: AttackKind.Melee);
            var enemy = Make("e", 0, atk: 0, def: 0, kind: AttackKind.Melee);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { ally0, ally1, attacker },
                EnemyUnits = new List<RuntimeUnit> { enemy },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { enemy } };

            new AttackEffect().Apply(ctx);

            Assert.Greater(ctx.Outcomes[0].Damage, 0);
        }

        [Test]
        public void BattleContext無し_補正1で素通し()
        {
            var attacker = Make("a", 5, atk: 50, def: 0, kind: AttackKind.Melee);
            var target = Make("t", 0, atk: 0, def: 0, kind: AttackKind.Melee);
            var ctx = new StubContext { Battle = null, Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.Greater(ctx.Outcomes[0].Damage, 0);
        }

        // ── 地形補正の動的計算 ──

        [Test]
        public void 自属性地形_火属性ターゲット_火有利でDEF加算_被ダメ減()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee);
            var fireTarget = Make("ft", 0, atk: 0, def: 10, kind: AttackKind.Melee, element: Element.Fire, hp: 9999);
            var battleNeutral = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { fireTarget },
                Terrain = TerrainKind.Neutral,
                TerrainStrength = TerrainStrength.Light,
            };
            var ctxN = new StubContext { Battle = battleNeutral, Actor = attacker, Targets = new List<RuntimeUnit> { fireTarget } };
            new AttackEffect().Apply(ctxN);
            int dmgN = ctxN.Outcomes[0].Damage;

            fireTarget.BaseUnit.CurrentHP = 9999;
            var battleFireAdv = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { fireTarget },
                Terrain = TerrainKind.FireAdvantage,
                TerrainStrength = TerrainStrength.Light,
            };
            var ctxF = new StubContext { Battle = battleFireAdv, Actor = attacker, Targets = new List<RuntimeUnit> { fireTarget } };
            new AttackEffect().Apply(ctxF);
            int dmgF = ctxF.Outcomes[0].Damage;

            Assert.Less(dmgF, dmgN);
        }

        [Test]
        public void 逆属性地形_水属性ターゲット_火有利でDEF減算_被ダメ増()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee);
            var waterTarget = Make("wt", 0, atk: 0, def: 10, kind: AttackKind.Melee, element: Element.Water, hp: 9999);
            var battleNeutral = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { waterTarget },
            };
            var ctxN = new StubContext { Battle = battleNeutral, Actor = attacker, Targets = new List<RuntimeUnit> { waterTarget } };
            new AttackEffect().Apply(ctxN);
            int dmgN = ctxN.Outcomes[0].Damage;

            waterTarget.BaseUnit.CurrentHP = 9999;
            var battleFireAdv = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { waterTarget },
                Terrain = TerrainKind.FireAdvantage,
                TerrainStrength = TerrainStrength.Heavy,
            };
            var ctxF = new StubContext { Battle = battleFireAdv, Actor = attacker, Targets = new List<RuntimeUnit> { waterTarget } };
            new AttackEffect().Apply(ctxF);
            int dmgF = ctxF.Outcomes[0].Damage;

            Assert.Greater(dmgF, dmgN);
        }

        // ── 反撃発動 ──

        [Test]
        public void 近接対近接_反撃発動()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var defender = Make("d", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { defender } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(2, ctx.Outcomes.Count);
            Assert.AreSame(defender, ctx.Outcomes[0].Target);
            Assert.AreSame(attacker, ctx.Outcomes[1].Target);
            Assert.Less(attacker.CurrentHP, 200);
        }

        [Test]
        public void 遠隔対近接_反撃なし()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Ranged, hp: 200);
            var defender = Make("d", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { defender } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreEqual(200, attacker.CurrentHP);
        }

        [Test]
        public void 近接対遠隔_反撃なし()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var defender = Make("d", 0, atk: 50, def: 0, kind: AttackKind.Ranged, hp: 200);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { defender } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreEqual(200, attacker.CurrentHP);
        }

        [Test]
        public void 反撃の反撃なし_outcomesは2件のみ()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var defender = Make("d", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { defender } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(2, ctx.Outcomes.Count);
        }

        [Test]
        public void 反撃で攻撃で対象死亡_反撃発動しない()
        {
            var attacker = Make("a", 0, atk: 9999, def: 0, kind: AttackKind.Melee, hp: 200);
            var defender = Make("d", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 10);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defender },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { defender } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.IsTrue(ctx.Outcomes[0].ResultedInDeath);
            Assert.AreEqual(200, attacker.CurrentHP);
        }

        // ── CounterDamageUp ──

        [Test]
        public void CounterDamageUp_反撃時のみ乗算()
        {
            // 通常攻撃時は CounterDamageUp が乗らないことを確認
            var atkWithCdup = Make("ac", 0, atk: 50, def: 0, kind: AttackKind.Melee);
            atkWithCdup.AddEffect(TestEff.Eff(EffectKind.CounterDamageUp, magnitude: 100f));
            var enemy = Make("e", 0, atk: 0, def: 0, kind: AttackKind.Melee, hp: 9999);
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { atkWithCdup },
                EnemyUnits = new List<RuntimeUnit> { enemy },
            };
            var ctx = new StubContext { Battle = battle, Actor = atkWithCdup, Targets = new List<RuntimeUnit> { enemy } };

            new AttackEffect().Apply(ctx);

            int dmgWithCdup = ctx.Outcomes[0].Damage;

            // CounterDamageUp なしと比較
            var atkPlain = Make("ap", 0, atk: 50, def: 0, kind: AttackKind.Melee);
            var enemy2 = Make("e2", 0, atk: 0, def: 0, kind: AttackKind.Melee, hp: 9999);
            var battle2 = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { atkPlain },
                EnemyUnits = new List<RuntimeUnit> { enemy2 },
            };
            var ctx2 = new StubContext { Battle = battle2, Actor = atkPlain, Targets = new List<RuntimeUnit> { enemy2 } };
            new AttackEffect().Apply(ctx2);

            Assert.AreEqual(ctx2.Outcomes[0].Damage, dmgWithCdup);
        }

        [Test]
        public void CounterDamageUp_反撃時に1_5倍ダメ_50パーセント()
        {
            var attacker = Make("a", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);

            // 反撃時に CounterDamageUp 50% が乗ることを確認
            var defenderWithCdup = Make("dc", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            defenderWithCdup.AddEffect(TestEff.Eff(EffectKind.CounterDamageUp, magnitude: 50f));
            var battle = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker },
                EnemyUnits = new List<RuntimeUnit> { defenderWithCdup },
            };
            var ctx = new StubContext { Battle = battle, Actor = attacker, Targets = new List<RuntimeUnit> { defenderWithCdup } };
            new AttackEffect().Apply(ctx);
            int counterDmgCdup = ctx.Outcomes[1].Damage;

            // CounterDamageUp なしの反撃と比較
            var attacker2 = Make("a2", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var defenderPlain = Make("dp", 0, atk: 50, def: 0, kind: AttackKind.Melee, hp: 200);
            var battle2 = new BattleContext(10)
            {
                AllyUnits = new List<RuntimeUnit> { attacker2 },
                EnemyUnits = new List<RuntimeUnit> { defenderPlain },
            };
            var ctx2 = new StubContext { Battle = battle2, Actor = attacker2, Targets = new List<RuntimeUnit> { defenderPlain } };
            new AttackEffect().Apply(ctx2);
            int counterDmgPlain = ctx2.Outcomes[1].Damage;

            Assert.AreEqual((int)Math.Round(counterDmgPlain * 1.5, MidpointRounding.AwayFromZero), counterDmgCdup);
        }

        [Test]
        public void ApplyCounterMultiplier_純関数_50パーセントで1_5倍()
        {
            var attacker = Make("a", 0, atk: 0, def: 0, kind: AttackKind.Melee);
            attacker.AddEffect(TestEff.Eff(EffectKind.CounterDamageUp, magnitude: 50f));
            Assert.AreEqual(150, DamageModifier.ApplyCounterMultiplier(100, attacker));
        }

        [Test]
        public void ApplyCounterMultiplier_純関数_効果なしで素通し()
        {
            var attacker = Make("a", 0, atk: 0, def: 0, kind: AttackKind.Melee);
            Assert.AreEqual(100, DamageModifier.ApplyCounterMultiplier(100, attacker));
        }
    }
}
