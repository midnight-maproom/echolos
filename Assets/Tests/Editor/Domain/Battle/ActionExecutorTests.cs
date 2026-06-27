using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class ActionExecutorTests
    {
        private static RuntimeUnit Make(string id, int slot, int hp = 100, int atk = 30, int def = 10)
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
            return new RuntimeUnit(u, slot);
        }

        private static RuntimeWaza AttackWaza(
            string id, int hitCount = 1, double mult = 1.0,
            TargetingType type = TargetingType.SingleEnemy,
            int cd = 0, int initialCd = 0)
        {
            var w = new Waza(id, id)
            {
                HitCount = hitCount,
                TargetingType = type,
                Cooldown = cd,
                InitialCooldown = initialCd,
                Effects = new List<IActionEffect> { new AttackEffect(wazaMultiplier: mult) },
            };
            return new RuntimeWaza(w);
        }

        private static ActionDeclaration Decl(
            RuntimeUnit actor, RuntimeWaza waza, params RuntimeUnit[] targets)
        {
            return new ActionDeclaration(
                actor: actor,
                declaredWaza: waza,
                targets: new List<RuntimeUnit>(targets),
                effectiveSPD: actor?.BaseUnit?.BaseSPD ?? 0,
                isWaiting: false);
        }

        // ── 単体・基本実行 ──

        [Test]
        public void 単体攻撃_1ヒット()
        {
            var actor = Make("a", 0, atk: 50);
            var target = Make("t", 0);
            var waza = AttackWaza("atk");
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, target), new BattleContext(10));
            Assert.AreEqual(1, outcomes.Count);
            Assert.Greater(outcomes[0].Damage, 0);
            Assert.Less(target.CurrentHP, 100);
        }

        // ── 多段攻撃 ──

        [Test]
        public void 多段攻撃_3ヒット_3つのOutcome()
        {
            var actor = Make("a", 0, atk: 50);
            var target = Make("t", 0, hp: 9999);
            var waza = AttackWaza("multi", hitCount: 3);
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, target), new BattleContext(10));
            Assert.AreEqual(3, outcomes.Count);
            foreach (var o in outcomes)
                Assert.AreSame(target, o.Target);
        }

        [Test]
        public void 多段攻撃_途中で対象死亡_残ヒットは不発()
        {
            var actor = Make("a", 0, atk: 9999);
            var target = Make("t", 0, hp: 10);
            var waza = AttackWaza("multi", hitCount: 5);
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, target), new BattleContext(10));
            Assert.AreEqual(1, outcomes.Count);
            Assert.IsTrue(outcomes[0].ResultedInDeath);
        }

        // ── 範囲攻撃 ──

        [Test]
        public void 範囲攻撃_全対象にOutcome()
        {
            var actor = Make("a", 0, atk: 50);
            var t1 = Make("t1", 0);
            var t2 = Make("t2", 1);
            var t3 = Make("t3", 2);
            var waza = AttackWaza("aoe", type: TargetingType.AllEnemies);
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, t1, t2, t3), new BattleContext(10));
            Assert.AreEqual(3, outcomes.Count);
        }

        [Test]
        public void 範囲攻撃_1体死亡しても他に継続()
        {
            var actor = Make("a", 0, atk: 50);
            var dead = Make("dead", 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make("alive", 1);
            var waza = AttackWaza("aoe", type: TargetingType.AllEnemies);
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, dead, alive), new BattleContext(10));
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(alive, outcomes[0].Target);
        }

        // ── 待機・行動者死亡 ──

        [Test]
        public void 待機_outcomes空()
        {
            var actor = Make("a", 0);
            var enemy = Make("e", 0);
            var waiting = new ActionDeclaration(
                actor: actor, declaredWaza: null,
                targets: new List<RuntimeUnit> { enemy },
                effectiveSPD: 30, isWaiting: true);
            var outcomes = new ActionExecutor().Execute(waiting, new BattleContext(10));
            Assert.AreEqual(0, outcomes.Count);
            Assert.AreEqual(100, enemy.CurrentHP);
        }

        [Test]
        public void 行動者死亡_outcomes空()
        {
            var actor = Make("a", 0);
            actor.BaseUnit.CurrentHP = 0;
            var enemy = Make("e", 0);
            var waza = AttackWaza("atk");
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, enemy), new BattleContext(10));
            Assert.AreEqual(0, outcomes.Count);
        }

        // ── CD / Uses 更新 ──

        [Test]
        public void 実行後_CDが設定される()
        {
            var actor = Make("a", 0, atk: 50);
            var enemy = Make("e", 0);
            var waza = AttackWaza("cd_skill", cd: 3, initialCd: 0);
            new ActionExecutor().Execute(Decl(actor, waza, enemy), new BattleContext(10));
            Assert.AreEqual(3, waza.CurrentCooldown);
        }

        [Test]
        public void 実行後_CurrentUses加算()
        {
            var actor = Make("a", 0, atk: 50);
            var enemy = Make("e", 0);
            var waza = AttackWaza("atk");
            new ActionExecutor().Execute(Decl(actor, waza, enemy), new BattleContext(10));
            new ActionExecutor().Execute(Decl(actor, waza, enemy), new BattleContext(10));
            Assert.AreEqual(2, waza.CurrentUses);
        }

        // ── 複数 Effects の順次適用 ──

        [Test]
        public void 攻撃Effect_と_状態異常付帯Effectが両方適用()
        {
            var actor = Make("a", 0, atk: 50);
            var enemy = Make("e", 0);
            var w = new Waza("burn_arrow", "炎矢")
            {
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect>
                {
                    new AttackEffect(),
                    new ApplyStatusEffectEffect(new List<IEffect>
                    {
                        TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1, remainingTurns: -1, maxStacks: 99),
                    }),
                },
            };
            var waza = new RuntimeWaza(w);
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, enemy), new BattleContext(10));
            Assert.AreEqual(2, outcomes.Count);
            Assert.Greater(outcomes[0].Damage, 0);
            Assert.AreEqual(EffectKind.Burn, outcomes[1].AppliedEffects[0].Kind);
            Assert.IsNotNull(enemy.FindEffect(e => e.Kind == EffectKind.Burn));
        }

        // ── Random 注入の決定論 ──

        [Test]
        public void Random注入_固定値で決定論的動作()
        {
            var actor = Make("a", 0, atk: 50);
            var enemy = Make("e", 0);
            var waza = AttackWaza("atk");
            var fixed42 = new ActionExecutor(random0To99: () => 42);
            var outcomes = fixed42.Execute(Decl(actor, waza, enemy), new BattleContext(10));
            Assert.AreEqual(1, outcomes.Count);
            Assert.Greater(outcomes[0].Damage, 0);
        }

        // ── null 安全 ──

        [Test]
        public void null_declaration_safe()
        {
            var outcomes = new ActionExecutor().Execute(null, new BattleContext(10));
            Assert.AreEqual(0, outcomes.Count);
        }

        [Test]
        public void HitCount0以下は1に矯正()
        {
            var actor = Make("a", 0, atk: 50);
            var enemy = Make("e", 0);
            var w = new Waza("zero_hit", "ゼロ")
            {
                HitCount = 0,
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect> { new AttackEffect() },
            };
            var waza = new RuntimeWaza(w);
            var outcomes = new ActionExecutor().Execute(Decl(actor, waza, enemy), new BattleContext(10));
            Assert.AreEqual(1, outcomes.Count);
        }
    }
}
