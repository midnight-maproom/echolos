using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class AttackEffectTests
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

        // AttackEffect の基本機能を Outcome 数で検証するため、反撃発動を無効化したい。
        // 攻撃側 AttackKind=Ranged は §3.2 で反撃が発生しない条件。
        private static RuntimeUnit Make(string id, int atk, int def, int hp = 100)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = AttackKind.Ranged,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        // ── 基本ダメージ ──

        [Test]
        public void 単体ターゲットに通常ダメージが適用される()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var target = Make("t", atk: 0, def: 0, hp: 100);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.Greater(ctx.Outcomes[0].Damage, 0);
            Assert.AreEqual(target, ctx.Outcomes[0].Target);
            Assert.AreEqual(100 - ctx.Outcomes[0].Damage, target.CurrentHP);
        }

        [Test]
        public void 複数ターゲット全員にダメージが適用される()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var t1 = Make("t1", atk: 0, def: 0);
            var t2 = Make("t2", atk: 0, def: 0);
            var t3 = Make("t3", atk: 0, def: 0);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { t1, t2, t3 } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(3, ctx.Outcomes.Count);
            Assert.Less(t1.CurrentHP, 100);
            Assert.Less(t2.CurrentHP, 100);
            Assert.Less(t3.CurrentHP, 100);
        }

        [Test]
        public void 与ダメ20パーセントバフが適用される()
        {
            var attackerBase = Make("ab", atk: 50, def: 0);
            var tBase = Make("tb", atk: 0, def: 0);
            new AttackEffect().Apply(new StubContext { Actor = attackerBase, Targets = new List<RuntimeUnit> { tBase } });
            int baseDmg = 100 - tBase.CurrentHP;

            var attacker = Make("a", atk: 50, def: 0);
            attacker.AddEffect(TestEff.Eff(EffectKind.OutgoingDamageUp, magnitude: 20f));
            var target = Make("t", atk: 0, def: 0);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual((int)Math.Round(baseDmg * 1.20), ctx.Outcomes[0].Damage);
        }

        [Test]
        public void 被ダメ50パーセントカットが適用される()
        {
            var attackerBase = Make("ab", atk: 50, def: 0);
            var tBase = Make("tb", atk: 0, def: 0);
            new AttackEffect().Apply(new StubContext { Actor = attackerBase, Targets = new List<RuntimeUnit> { tBase } });
            int baseDmg = 100 - tBase.CurrentHP;

            var attacker = Make("a", atk: 50, def: 0);
            var target = Make("t", atk: 0, def: 0);
            target.AddEffect(TestEff.Eff(EffectKind.IncomingDamageDown, magnitude: 50f));
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual((int)Math.Round(baseDmg * 0.50), ctx.Outcomes[0].Damage);
        }

        [Test]
        public void Shield吸収でダメージ0_HPは減らない()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var target = Make("t", atk: 0, def: 0, hp: 100);
            var shield = TestEff.Persistent(
                EffectKind.Shield, magnitude: 0f,
                sourceAbilityName: "テスト", maxStacks: 1);
            shield.Stacks = 1;
            target.AddEffect(shield);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(0, ctx.Outcomes[0].Damage);
            Assert.AreEqual(100, target.CurrentHP);
        }

        [Test]
        public void HPゼロまでクランプ_即死級ダメージでも負値にならない()
        {
            var attacker = Make("a", atk: 9999, def: 0);
            var target = Make("t", atk: 0, def: 0, hp: 10);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(0, target.CurrentHP);
            Assert.IsTrue(ctx.Outcomes[0].ResultedInDeath);
            Assert.AreEqual(0, ctx.Outcomes[0].TargetHPAfter);
        }

        [Test]
        public void 死亡済ターゲットはスキップ_Outcomeに追加されない()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var dead = Make("dead", atk: 0, def: 0, hp: 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("alive", atk: 0, def: 0);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { dead, alive } };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.AreEqual(alive, ctx.Outcomes[0].Target);
        }

        [Test]
        public void wazaMultiplier2倍は概ね2倍のダメージ()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var target = Make("t", atk: 0, def: 0);

            var ctxX1 = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };
            new AttackEffect(wazaMultiplier: 1.0).Apply(ctxX1);
            int dmgX1 = ctxX1.Outcomes[0].Damage;

            target.BaseUnit.CurrentHP = 100;
            var ctxX2 = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };
            new AttackEffect(wazaMultiplier: 2.0).Apply(ctxX2);
            int dmgX2 = ctxX2.Outcomes[0].Damage;

            Assert.AreEqual(dmgX1 * 2, dmgX2);
        }

        [Test]
        public void null_context_actor_targetsで例外なし()
        {
            Assert.DoesNotThrow(() => new AttackEffect().Apply(null));
            var ctxNullActor = new StubContext { Actor = null, Targets = new List<RuntimeUnit> { Make("t", 0, 0) } };
            Assert.DoesNotThrow(() => new AttackEffect().Apply(ctxNullActor));
            var ctxNullTargets = new StubContext { Actor = Make("a", 50, 0), Targets = null };
            Assert.DoesNotThrow(() => new AttackEffect().Apply(ctxNullTargets));
        }

        // ── Rider（命中時のみ付帯）──

        [Test]
        public void Rider_命中時に同ターゲットへ付帯状態異常が適用される()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var target = Make("t", atk: 0, def: 0);
            var rider = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
            });
            var attack = new AttackEffect(onHitRiders: new List<IActionEffect> { rider });
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            attack.Apply(ctx);

            Assert.Greater(ctx.Outcomes[0].Damage, 0);
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
        }

        [Test]
        public void Rider_範囲攻撃で各ターゲットそれぞれに独立付与()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var t1 = Make("t1", atk: 0, def: 0);
            var t2 = Make("t2", atk: 0, def: 0);
            var rider = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3),
            });
            var attack = new AttackEffect(onHitRiders: new List<IActionEffect> { rider });
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { t1, t2 } };

            attack.Apply(ctx);

            Assert.IsNotNull(t1.FindEffect(e => e.Kind == EffectKind.AttackDown));
            Assert.IsNotNull(t2.FindEffect(e => e.Kind == EffectKind.AttackDown));
        }

        [Test]
        public void Rider_複数指定_全て順次適用()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var target = Make("t", atk: 0, def: 0);
            var rider1 = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.AttackDown, magnitude: 10f, stacks: 1, remainingTurns: 3),
            });
            var rider2 = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.DefenseDown, magnitude: 5f, stacks: 1, remainingTurns: 3),
            });
            var attack = new AttackEffect(onHitRiders: new List<IActionEffect> { rider1, rider2 });
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            attack.Apply(ctx);

            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.AttackDown));
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.DefenseDown));
        }

        [Test]
        public void Rider_死亡ターゲットへは_Rider_も発動しない()
        {
            var attacker = Make("a", atk: 50, def: 0);
            var dead = Make("dead", atk: 0, def: 0, hp: 0);
            dead.BaseUnit.CurrentHP = 0;
            var rider = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
            });
            var attack = new AttackEffect(onHitRiders: new List<IActionEffect> { rider });
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { dead } };

            attack.Apply(ctx);

            Assert.AreEqual(0, ctx.Outcomes.Count);
            Assert.IsNull(dead.FindEffect(e => e.Kind == EffectKind.Burn));
        }
    }
}
