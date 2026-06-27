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
    public class HitMissAndCriticalTests
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

        private static RuntimeUnit Make(string id, int atk, int def, AttackKind kind, int hp = 100)
        {
            var u = new Unit(id, id)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = kind,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        // ── 命中/回避 ──

        [Test]
        public void 遠隔攻撃_target回避なら_HitOutcomeはWasEvaded()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Ranged);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 100f)); // 確定回避
            var ctx = new StubContext
            {
                Actor = attacker,
                Targets = new List<RuntimeUnit> { target },
                Random0To99 = () => 0,
            };

            new AttackEffect().Apply(ctx);

            Assert.AreEqual(1, ctx.Outcomes.Count);
            Assert.IsTrue(ctx.Outcomes[0].WasEvaded);
            Assert.AreEqual(0, ctx.Outcomes[0].Damage);
            Assert.AreEqual(100, target.CurrentHP);
        }

        [Test]
        public void 近接攻撃は_targetに_EvasionUpがあっても回避ロールしない()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Melee);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 100f));
            var ctx = new StubContext
            {
                Actor = attacker,
                Targets = new List<RuntimeUnit> { target },
                Random0To99 = () => 0,
            };

            new AttackEffect().Apply(ctx);

            Assert.IsFalse(ctx.Outcomes[0].WasEvaded);
            Assert.Greater(ctx.Outcomes[0].Damage, 0);
        }

        [Test]
        public void 遠隔攻撃_IsSureHitなら回避ロールスキップ()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Ranged);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 100f));
            var ctx = new StubContext
            {
                Actor = attacker,
                Targets = new List<RuntimeUnit> { target },
                Random0To99 = () => 0,
            };

            new AttackEffect(isSureHit: true).Apply(ctx);

            Assert.IsFalse(ctx.Outcomes[0].WasEvaded);
            Assert.Greater(ctx.Outcomes[0].Damage, 0);
        }

        [Test]
        public void 回避率10_乱数9なら回避_乱数10なら命中()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Ranged);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 10f));

            var ctxEvade = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target }, Random0To99 = () => 9 };
            new AttackEffect().Apply(ctxEvade);
            Assert.IsTrue(ctxEvade.Outcomes[0].WasEvaded);

            target.BaseUnit.CurrentHP = 100;
            var ctxHit = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target }, Random0To99 = () => 10 };
            new AttackEffect().Apply(ctxHit);
            Assert.IsFalse(ctxHit.Outcomes[0].WasEvaded);
        }

        [Test]
        public void EvasionUpキャップ50_合計100でも50で頭打ち_乱数49なら回避_乱数50なら命中()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Ranged);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 50f));
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 50f));

            var ctx49 = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target }, Random0To99 = () => 49 };
            new AttackEffect().Apply(ctx49);
            Assert.IsTrue(ctx49.Outcomes[0].WasEvaded);

            target.BaseUnit.CurrentHP = 100;
            var ctx50 = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target }, Random0To99 = () => 50 };
            new AttackEffect().Apply(ctx50);
            Assert.IsFalse(ctx50.Outcomes[0].WasEvaded);
        }

        [Test]
        public void 回避ヒット_Riderは発動しない()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Ranged);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            target.AddEffect(TestEff.Eff(EffectKind.EvasionUp, magnitude: 100f));
            var rider = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
            });
            var attack = new AttackEffect(onHitRiders: new List<IActionEffect> { rider });
            var ctx = new StubContext
            {
                Actor = attacker,
                Targets = new List<RuntimeUnit> { target },
                Random0To99 = () => 0,
            };

            attack.Apply(ctx);

            Assert.IsTrue(ctx.Outcomes[0].WasEvaded);
            Assert.IsNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
        }

        // ── クリティカル ──

        [Test]
        public void クリ率なし_非クリで素通し()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Melee);
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.IsFalse(ctx.Outcomes[0].IsCritical);
        }

        [Test]
        public void クリ率100_常にクリでダメージ1_5倍()
        {
            var attackerBase = Make("ab", atk: 50, def: 0, kind: AttackKind.Melee);
            var tBase = Make("tb", atk: 0, def: 0, kind: AttackKind.Melee);
            new AttackEffect().Apply(new StubContext { Actor = attackerBase, Targets = new List<RuntimeUnit> { tBase } });
            int baseDmg = 100 - tBase.CurrentHP;

            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Melee);
            attacker.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 100f));
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            var ctx = new StubContext { Actor = attacker, Targets = new List<RuntimeUnit> { target } };

            new AttackEffect().Apply(ctx);

            Assert.IsTrue(ctx.Outcomes[0].IsCritical);
            Assert.AreEqual((int)Math.Round(baseDmg * 1.5), ctx.Outcomes[0].Damage);
        }

        [Test]
        public void クリ率10_乱数9でクリ_乱数10で非クリ()
        {
            var attacker1 = Make("a1", atk: 50, def: 0, kind: AttackKind.Melee);
            attacker1.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 10f));
            var t1 = Make("t1", atk: 0, def: 0, kind: AttackKind.Melee);
            var ctx9 = new StubContext { Actor = attacker1, Targets = new List<RuntimeUnit> { t1 }, Random0To99 = () => 9 };
            new AttackEffect().Apply(ctx9);
            Assert.IsTrue(ctx9.Outcomes[0].IsCritical);

            var attacker2 = Make("a2", atk: 50, def: 0, kind: AttackKind.Melee);
            attacker2.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 10f));
            var t2 = Make("t2", atk: 0, def: 0, kind: AttackKind.Melee);
            var ctx10 = new StubContext { Actor = attacker2, Targets = new List<RuntimeUnit> { t2 }, Random0To99 = () => 10 };
            new AttackEffect().Apply(ctx10);
            Assert.IsFalse(ctx10.Outcomes[0].IsCritical);
        }

        [Test]
        public void クリ率_複数CriticalRateUp_は加算スタック()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Melee);
            attacker.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 10f));
            attacker.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 15f));
            Assert.AreEqual(25, DamageModifier.GetCriticalRatePercent(attacker));
        }

        [Test]
        public void ApplyCritical_純関数_クリ率100で1_5倍を返す()
        {
            var attacker = Make("a", atk: 0, def: 0, kind: AttackKind.Melee);
            attacker.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 100f));
            var (dmg, crit) = DamageModifier.ApplyCritical(100, attacker, () => 0);
            Assert.AreEqual(150, dmg);
            Assert.IsTrue(crit);
        }

        [Test]
        public void ApplyCritical_純関数_クリ率0で素通し()
        {
            var attacker = Make("a", atk: 0, def: 0, kind: AttackKind.Melee);
            var (dmg, crit) = DamageModifier.ApplyCritical(100, attacker, () => 0);
            Assert.AreEqual(100, dmg);
            Assert.IsFalse(crit);
        }

        [Test]
        public void ApplyCritical_純関数_生ダメ0は素通し()
        {
            var attacker = Make("a", atk: 0, def: 0, kind: AttackKind.Melee);
            attacker.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 100f));
            var (dmg, crit) = DamageModifier.ApplyCritical(0, attacker, () => 0);
            Assert.AreEqual(0, dmg);
            Assert.IsFalse(crit);
        }

        // ── 命中＋クリ複合 ──

        [Test]
        public void クリヒット時もRiderは発動する()
        {
            var attacker = Make("a", atk: 50, def: 0, kind: AttackKind.Melee);
            attacker.AddEffect(TestEff.Eff(EffectKind.CriticalRateUp, magnitude: 100f));
            var target = Make("t", atk: 0, def: 0, kind: AttackKind.Melee);
            var rider = new ApplyStatusEffectEffect(new List<IEffect>
            {
                TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
            });
            var attack = new AttackEffect(onHitRiders: new List<IActionEffect> { rider });
            var ctx = new StubContext
            {
                Actor = attacker,
                Targets = new List<RuntimeUnit> { target },
                Random0To99 = () => 0,
            };

            attack.Apply(ctx);

            Assert.IsTrue(ctx.Outcomes[0].IsCritical);
            Assert.IsNotNull(target.FindEffect(e => e.Kind == EffectKind.Burn));
        }
    }
}
