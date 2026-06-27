using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class ActionExecutorEventTests
    {
        private static RuntimeUnit Make(int slot, int hp = 100, int atk = 50, int def = 0,
            AttackKind kind = AttackKind.Melee)
        {
            var u = new Unit($"u_{slot}", $"u_{slot}", Element.None)
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

        private static BattleContext MakeContext(
            List<RuntimeUnit> allies = null, List<RuntimeUnit> enemies = null)
        {
            return new BattleContext(10)
            {
                AllyUnits = allies ?? new List<RuntimeUnit>(),
                EnemyUnits = enemies ?? new List<RuntimeUnit>(),
            };
        }

        private static RuntimeWaza MakeAttackWaza(double mult = 1.0, int hitCount = 1)
        {
            var w = new Waza("test_attack", "テスト攻撃")
            {
                HitCount = hitCount,
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect> { new AttackEffect(wazaMultiplier: mult, isSureHit: true) },
            };
            return new RuntimeWaza(w);
        }

        private static RuntimeWaza MakeHealWaza(double power = 5.0)
        {
            var w = new Waza("test_heal", "テスト回復")
            {
                TargetingType = TargetingType.SingleAlly,
                Effects = new List<IActionEffect> { new HealEffect(wazaPower: power) },
            };
            return new RuntimeWaza(w);
        }

        // ───── OnActionResolved ─────

        [Test]
        public void OnActionResolved_攻撃で発火_Outcomeを束ねる()
        {
            var actor = Make(0, atk: 50, kind: AttackKind.Ranged);
            var target = Make(0, hp: 100, def: 0, kind: AttackKind.Ranged);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            var decl = new ActionDeclaration(actor, MakeAttackWaza(), new List<RuntimeUnit> { target }, 10, false);

            int fired = 0;
            IReadOnlyList<HitOutcome> seenOutcomes = null;
            var exec = new ActionExecutor();
            exec.OnActionResolved += (c, d, outs) => { fired++; seenOutcomes = outs; };

            exec.Execute(decl, ctx);

            Assert.AreEqual(1, fired);
            Assert.IsNotNull(seenOutcomes);
            Assert.AreEqual(1, seenOutcomes.Count);
        }

        [Test]
        public void OnActionResolved_回復で発火()
        {
            var actor = Make(0, hp: 100);
            var ally = Make(1, hp: 50);
            var ctx = MakeContext(new List<RuntimeUnit> { actor, ally });
            var decl = new ActionDeclaration(actor, MakeHealWaza(), new List<RuntimeUnit> { ally }, 10, false);

            int fired = 0;
            var exec = new ActionExecutor();
            exec.OnActionResolved += (c, d, outs) => fired++;

            exec.Execute(decl, ctx);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void OnActionResolved_待機時は発火しない()
        {
            var actor = Make(0);
            var ctx = MakeContext(new List<RuntimeUnit> { actor });
            var decl = new ActionDeclaration(actor, null, new List<RuntimeUnit>(), 10, isWaiting: true);

            int fired = 0;
            var exec = new ActionExecutor();
            exec.OnActionResolved += (c, d, outs) => fired++;

            exec.Execute(decl, ctx);

            Assert.AreEqual(0, fired);
        }

        [Test]
        public void OnActionResolved_Wazaなしは発火しない()
        {
            var actor = Make(0);
            var ctx = MakeContext(new List<RuntimeUnit> { actor });
            var decl = new ActionDeclaration(actor, null, new List<RuntimeUnit>(), 10, false);

            int fired = 0;
            var exec = new ActionExecutor();
            exec.OnActionResolved += (c, d, outs) => fired++;

            exec.Execute(decl, ctx);

            Assert.AreEqual(0, fired);
        }

        [Test]
        public void OnActionResolved_Actor死亡時は発火しない()
        {
            var actor = Make(0);
            actor.BaseUnit.CurrentHP = 0;
            var target = Make(0, kind: AttackKind.Ranged);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            var decl = new ActionDeclaration(actor, MakeAttackWaza(), new List<RuntimeUnit> { target }, 10, false);

            int fired = 0;
            var exec = new ActionExecutor();
            exec.OnActionResolved += (c, d, outs) => fired++;

            exec.Execute(decl, ctx);

            Assert.AreEqual(0, fired);
        }

        // ───── OnUnitDied ─────

        [Test]
        public void OnUnitDied_攻撃で死亡したターゲットを通知()
        {
            var actor = Make(0, atk: 9999, kind: AttackKind.Ranged);
            var target = Make(0, hp: 10, def: 0, kind: AttackKind.Ranged);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            var decl = new ActionDeclaration(actor, MakeAttackWaza(), new List<RuntimeUnit> { target }, 10, false);

            var dead = new List<RuntimeUnit>();
            var exec = new ActionExecutor();
            exec.OnUnitDied += (c, u) => dead.Add(u);

            exec.Execute(decl, ctx);

            Assert.AreEqual(1, dead.Count);
            Assert.AreSame(target, dead[0]);
        }

        [Test]
        public void OnUnitDied_死亡なしなら発火しない()
        {
            var actor = Make(0, atk: 10, kind: AttackKind.Ranged);
            var target = Make(0, hp: 1000, def: 0, kind: AttackKind.Ranged);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            var decl = new ActionDeclaration(actor, MakeAttackWaza(), new List<RuntimeUnit> { target }, 10, false);

            int fired = 0;
            var exec = new ActionExecutor();
            exec.OnUnitDied += (c, u) => fired++;

            exec.Execute(decl, ctx);

            Assert.AreEqual(0, fired);
        }

        [Test]
        public void OnUnitDied_多段攻撃で同ターゲット死亡は1回のみ通知()
        {
            var actor = Make(0, atk: 50, kind: AttackKind.Ranged);
            var target = Make(0, hp: 10, def: 0, kind: AttackKind.Ranged);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            // 多段でも生存判定で 2 段目はスキップ。Outcome は 1 件のみだが、念のため重複防止を検証。
            var decl = new ActionDeclaration(
                actor, MakeAttackWaza(hitCount: 3), new List<RuntimeUnit> { target }, 10, false);

            int fired = 0;
            var exec = new ActionExecutor();
            exec.OnUnitDied += (c, u) => { if (u == target) fired++; };

            exec.Execute(decl, ctx);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void OnUnitDied_反撃で死亡した攻撃者も通知()
        {
            // 攻撃側 Melee × 被弾側 Melee で反撃発動。攻撃者が虚弱なら反撃で死ぬ。
            var actor = Make(0, hp: 1, atk: 50, def: 0, kind: AttackKind.Melee);
            var target = Make(0, hp: 100, atk: 50, def: 0, kind: AttackKind.Melee);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            var decl = new ActionDeclaration(actor, MakeAttackWaza(), new List<RuntimeUnit> { target }, 10, false);

            var deaths = new List<RuntimeUnit>();
            var exec = new ActionExecutor();
            exec.OnUnitDied += (c, u) => deaths.Add(u);

            exec.Execute(decl, ctx);

            // attacker は反撃で死ぬはず（target 生存・attacker 死亡）
            CollectionAssert.Contains(deaths, actor);
        }

        // ───── 例外なし ─────

        [Test]
        public void イベント未購読でも例外を投げない()
        {
            var actor = Make(0, atk: 9999, kind: AttackKind.Ranged);
            var target = Make(0, hp: 10, def: 0, kind: AttackKind.Ranged);
            var ctx = MakeContext(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { target });
            var decl = new ActionDeclaration(actor, MakeAttackWaza(), new List<RuntimeUnit> { target }, 10, false);

            var exec = new ActionExecutor();
            Assert.DoesNotThrow(() => exec.Execute(decl, ctx));
        }
    }
}
