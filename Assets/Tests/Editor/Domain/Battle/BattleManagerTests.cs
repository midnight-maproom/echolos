using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class BattleManagerTests
    {
        private static RuntimeUnit Make(int slot, int hp = 100, int atk = 50, int def = 0,
            int spd = 10, AttackKind kind = AttackKind.Ranged)
        {
            var u = new Unit($"u_{slot}", $"u_{slot}", Element.None)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                BaseSPD = spd,
                AttackKind = kind,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static BattleContext Ctx(
            List<RuntimeUnit> allies = null,
            List<RuntimeUnit> enemies = null,
            bool isAttacking = false)
        {
            return new BattleContext(10)
            {
                AllyUnits = allies ?? new List<RuntimeUnit>(),
                EnemyUnits = enemies ?? new List<RuntimeUnit>(),
                IsAttackingSide = isAttacking,
            };
        }

        private static RuntimeWaza MakeAttackWaza(int spd = 10, int cd = 0, int hitCount = 1)
        {
            var w = new Waza("attack", "通常攻撃")
            {
                SPD = spd,
                Cooldown = cd,
                HitCount = hitCount,
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect> { new AttackEffect(wazaMultiplier: 1.0, isSureHit: true) },
            };
            return new RuntimeWaza(w);
        }

        private static Dictionary<RuntimeUnit, IList<RuntimeWaza>> Wazas(
            params (RuntimeUnit, RuntimeWaza)[] pairs)
        {
            var dict = new Dictionary<RuntimeUnit, IList<RuntimeWaza>>();
            foreach (var (u, w) in pairs)
                dict[u] = new List<RuntimeWaza> { w };
            return dict;
        }

        // ───── InitializeBattle ─────

        [Test]
        public void InitializeBattle_InitialCountをセット()
        {
            var ctx = Ctx(
                new List<RuntimeUnit> { Make(0), Make(1) },
                new List<RuntimeUnit> { Make(0), Make(1), Make(2) });
            var mgr = new BattleManager(new ActionExecutor());

            mgr.InitializeBattle(ctx);

            Assert.AreEqual(2, ctx.InitialAllyCount);
            Assert.AreEqual(3, ctx.InitialEnemyCount);
        }

        [Test]
        public void InitializeBattle_OnBattleStart発火()
        {
            var ctx = Ctx();
            var mgr = new BattleManager(new ActionExecutor());
            int fired = 0;
            mgr.OnBattleStart += _ => fired++;

            mgr.InitializeBattle(ctx);

            Assert.AreEqual(1, fired);
        }

        // ───── ProcessTurn フェーズ遷移とイベント ─────

        [Test]
        public void ProcessTurn_イベントが正しい順で発火()
        {
            // actor と enemy が両方行動する 1 ターンの順序：
            // Start → (ActionStart → ActionEnd) × 2 → End
            var actor = Make(0, atk: 1);
            var enemy = Make(0, hp: 1000);
            var ctx = Ctx(new List<RuntimeUnit> { actor }, new List<RuntimeUnit> { enemy });
            var mgr = new BattleManager(new ActionExecutor());

            var log = new List<string>();
            mgr.OnStartPhase += _ => log.Add("Start");
            mgr.OnActionStart += (_, _u) => log.Add("ActionStart");
            mgr.OnActionEnd += (_, _u) => log.Add("ActionEnd");
            mgr.OnEndPhase += _ => log.Add("End");

            mgr.ProcessTurn(ctx, Wazas((actor, MakeAttackWaza()), (enemy, MakeAttackWaza())));

            CollectionAssert.AreEqual(
                new[] { "Start", "ActionStart", "ActionEnd", "ActionStart", "ActionEnd", "End" },
                log);
        }

        [Test]
        public void ProcessTurn_CurrentTurnがインクリメント()
        {
            var ctx = Ctx();
            ctx.CurrentTurn = 5;
            var mgr = new BattleManager(new ActionExecutor());

            mgr.ProcessTurn(ctx, null);

            Assert.AreEqual(6, ctx.CurrentTurn);
        }

        [Test]
        public void ProcessTurn_CurrentPhaseが最終的にEnd()
        {
            var ctx = Ctx(
                new List<RuntimeUnit> { Make(0) },
                new List<RuntimeUnit> { Make(0) });
            var mgr = new BattleManager(new ActionExecutor());

            mgr.ProcessTurn(ctx, null);

            Assert.AreEqual(PhaseState.End, ctx.CurrentPhase);
        }

        // ───── HasActedThisTurn リセット ─────

        [Test]
        public void ProcessTurn_入口でHasActedThisTurnをリセット()
        {
            var u = Make(0);
            u.HasActedThisTurn = true;
            var ctx = Ctx(new List<RuntimeUnit> { u });
            var mgr = new BattleManager(new ActionExecutor());

            int actionStartFired = 0;
            mgr.OnActionStart += (_, _u) => actionStartFired++;
            mgr.ProcessTurn(ctx, Wazas((u, MakeAttackWaza())));

            Assert.AreEqual(1, actionStartFired, "リセット後に行動が走る");
        }

        // ───── 行動順 ─────

        [Test]
        public void ProcessTurn_SPD降順で行動()
        {
            var fast = Make(0, spd: 20);
            var slow = Make(1, spd: 5);
            var enemy = Make(0, hp: 1000);
            var ctx = Ctx(new List<RuntimeUnit> { fast, slow }, new List<RuntimeUnit> { enemy });
            var mgr = new BattleManager(new ActionExecutor());

            var order = new List<string>();
            mgr.OnActionStart += (_, u) => order.Add(u.BaseUnit.Id);
            mgr.ProcessTurn(ctx,
                Wazas((fast, MakeAttackWaza()), (slow, MakeAttackWaza()), (enemy, MakeAttackWaza())));

            int fastIdx = order.IndexOf("u_0");
            int slowIdx = order.LastIndexOf("u_1");
            Assert.Less(fastIdx, slowIdx, "fast(SPD20) は slow(SPD5) より先に行動");
        }

        // ───── 待機・スキップ ─────

        [Test]
        public void ProcessTurn_麻痺で行動スキップ_OnActionSkipped発火()
        {
            var u = Make(0);
            u.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 5));
            var target = Make(0, hp: 1000);
            var ctx = Ctx(new List<RuntimeUnit> { u }, new List<RuntimeUnit> { target });
            var mgr = new BattleManager(new ActionExecutor());

            int skipped = 0;
            int resolved = 0;
            var exec = new ActionExecutor();
            exec.OnActionResolved += (_, _d, _o) => resolved++;
            mgr = new BattleManager(exec);
            mgr.OnActionSkipped += (_, _u) => { if (_u == u) skipped++; };

            mgr.ProcessTurn(ctx, Wazas((u, MakeAttackWaza()), (target, MakeAttackWaza())));

            Assert.AreEqual(1, skipped);
            Assert.AreEqual(1, resolved, "麻痺ユニットは Execute されない・target は行動する");
        }

        // ───── OnActionStart で死亡 → 行動スキップ ─────

        [Test]
        public void ProcessTurn_OnActionStartで死亡したらExecute発動しない()
        {
            var u = Make(0, hp: 10);
            var target = Make(0, hp: 100);
            var ctx = Ctx(new List<RuntimeUnit> { u }, new List<RuntimeUnit> { target });

            var exec = new ActionExecutor();
            int resolved = 0;
            exec.OnActionResolved += (_, _d, _o) => { if (_d.Actor == u) resolved++; };

            var mgr = new BattleManager(exec);
            mgr.OnActionStart += (_, _u) => { if (_u == u) _u.BaseUnit.CurrentHP = 0; };

            mgr.ProcessTurn(ctx, Wazas((u, MakeAttackWaza()), (target, MakeAttackWaza())));

            Assert.AreEqual(0, resolved);
        }

        // ───── CD 減算 ─────

        [Test]
        public void ProcessTurn_EndPhase末尾でCDを1減算()
        {
            var u = Make(0);
            var enemy = Make(0, hp: 1000);
            var waza = MakeAttackWaza(cd: 3);
            waza.CurrentCooldown = 3;
            var ctx = Ctx(new List<RuntimeUnit> { u }, new List<RuntimeUnit> { enemy });
            var mgr = new BattleManager(new ActionExecutor());

            mgr.ProcessTurn(ctx, Wazas((u, waza)));

            Assert.AreEqual(2, waza.CurrentCooldown);
        }

        [Test]
        public void ProcessTurn_CD0は減算してマイナスにならない()
        {
            var u = Make(0);
            var enemy = Make(0, hp: 1000);
            var waza = MakeAttackWaza(cd: 0);
            waza.CurrentCooldown = 0;
            var ctx = Ctx(new List<RuntimeUnit> { u }, new List<RuntimeUnit> { enemy });
            var mgr = new BattleManager(new ActionExecutor());

            mgr.ProcessTurn(ctx, Wazas((u, waza)));

            Assert.AreEqual(0, waza.CurrentCooldown);
        }

        // ───── 敵視点のターゲット選定 ─────

        [Test]
        public void ProcessTurn_敵ユニットは味方を攻撃する_自陣を攻撃しない()
        {
            var ally = Make(0, hp: 100, def: 0, kind: AttackKind.Ranged);
            var enemy = Make(0, hp: 100, atk: 9999, kind: AttackKind.Ranged);
            var ctx = Ctx(new List<RuntimeUnit> { ally }, new List<RuntimeUnit> { enemy });
            var mgr = new BattleManager(new ActionExecutor());

            mgr.ProcessTurn(ctx, Wazas((ally, MakeAttackWaza()), (enemy, MakeAttackWaza())));

            Assert.AreEqual(0, ally.CurrentHP, "敵 ATK=9999 で ally HP=100 が 0 に");
        }

        // ───── 動的順序方式：実行直前ターゲット再選定 ─────

        [Test]
        public void ProcessTurn_先行ユニットがターゲットを倒した場合_後続は別ターゲットを選ぶ()
        {
            // 動的順序方式の検証：同 SPD・slot 0/1 の味方 2 体（一撃必殺の ATK）が、
            // FromFront で生存敵の最前を狙う。a が enemy0 を倒した後、b は実行直前の
            // DeclareAction で生存敵を再選定し、enemy1 を狙うことを検証。
            // 旧仕様（ターン開始時に行動順とターゲットを一斉確定）では b は enemy0 を
            // 宣言したまま不発になっていた。
            var a = Make(0, atk: 9999, spd: 10);
            var b = Make(1, atk: 9999, spd: 10);
            var enemy0 = Make(0, hp: 100);
            var enemy1 = Make(1, hp: 100);
            var ctx = Ctx(new List<RuntimeUnit> { a, b }, new List<RuntimeUnit> { enemy0, enemy1 });
            var mgr = new BattleManager(new ActionExecutor());

            mgr.ProcessTurn(ctx,
                Wazas((a, MakeAttackWaza()), (b, MakeAttackWaza()),
                      (enemy0, MakeAttackWaza()), (enemy1, MakeAttackWaza())));

            Assert.IsFalse(enemy0.IsAlive, "敵 0 は a の攻撃で死亡");
            Assert.IsFalse(enemy1.IsAlive,
                "敵 1 は b の実行直前再選定で攻撃されて死亡（旧仕様では enemy0 を狙ったまま不発）");
        }

        // ───── ターン中の陣営全滅で残り未行動ユニットは行動しない ─────

        [Test]
        public void ProcessTurn_ターン中に敵全滅したら残り味方は行動しない()
        {
            // SPD: a(10) > b(5)。a が一撃で敵を倒した時点で残り味方 b の行動を抑止する。
            // 観察された「敵全滅後の防御フォールバック発動」が出ないことを保証。
            var a = Make(0, atk: 9999, spd: 10);
            var b = Make(1, atk: 10, spd: 5);
            var enemy = Make(0, hp: 100);
            var ctx = Ctx(new List<RuntimeUnit> { a, b }, new List<RuntimeUnit> { enemy });

            var actorsStarted = new List<RuntimeUnit>();
            var mgr = new BattleManager(new ActionExecutor());
            mgr.OnActionStart += (_, u) => actorsStarted.Add(u);

            mgr.ProcessTurn(ctx, Wazas(
                (a, MakeAttackWaza(spd: 10)), (b, MakeAttackWaza(spd: 5)),
                (enemy, MakeAttackWaza())));

            Assert.IsFalse(enemy.IsAlive, "a の一撃で敵が死亡");
            CollectionAssert.Contains(actorsStarted, a);
            CollectionAssert.DoesNotContain(actorsStarted, b);
            Assert.IsFalse(b.HasActedThisTurn, "敵全滅後、b は未行動のまま");
        }

        [Test]
        public void ProcessTurn_ターン中に味方全滅したら残り敵は行動しない()
        {
            var attacker = Make(0, atk: 9999, spd: 10);
            var enemy2 = Make(1, atk: 10, spd: 5);
            var victim = Make(0, hp: 100);
            var ctx = Ctx(new List<RuntimeUnit> { victim }, new List<RuntimeUnit> { attacker, enemy2 });

            var actorsStarted = new List<RuntimeUnit>();
            var mgr = new BattleManager(new ActionExecutor());
            mgr.OnActionStart += (_, u) => actorsStarted.Add(u);

            mgr.ProcessTurn(ctx, Wazas(
                (attacker, MakeAttackWaza(spd: 10)), (enemy2, MakeAttackWaza(spd: 5)),
                (victim, MakeAttackWaza())));

            Assert.IsFalse(victim.IsAlive, "attacker の一撃で味方が死亡");
            CollectionAssert.Contains(actorsStarted, attacker);
            CollectionAssert.DoesNotContain(actorsStarted, enemy2);
        }

        [Test]
        public void ProcessTurn_ターン中の陣営全滅でEndPhaseはスキップ()
        {
            // 敵全滅で MainPhase 早期離脱＋ EndPhase も丸ごとスキップして即決着。
            // HOT 回復・Burn・SearingWound 等のターン末処理は走らない。
            var ally = Make(0, atk: 9999, spd: 10);
            var enemy = Make(0, hp: 100);
            var ctx = Ctx(new List<RuntimeUnit> { ally }, new List<RuntimeUnit> { enemy });

            int endPhaseCount = 0;
            var mgr = new BattleManager(new ActionExecutor());
            mgr.OnEndPhase += _ => endPhaseCount++;

            mgr.ProcessTurn(ctx, Wazas((ally, MakeAttackWaza()), (enemy, MakeAttackWaza())));

            Assert.IsFalse(enemy.IsAlive, "ally の一撃で enemy が死亡");
            Assert.AreEqual(0, endPhaseCount, "敵全滅で OnEndPhase は発火しない");
        }

        [Test]
        public void ProcessTurn_両陣営同時生存ならEndPhaseは通常通り走る()
        {
            var ally = Make(0, atk: 1, spd: 10);
            var enemy = Make(0, hp: 1000);
            var ctx = Ctx(new List<RuntimeUnit> { ally }, new List<RuntimeUnit> { enemy });

            int endPhaseCount = 0;
            var mgr = new BattleManager(new ActionExecutor());
            mgr.OnEndPhase += _ => endPhaseCount++;

            mgr.ProcessTurn(ctx, Wazas((ally, MakeAttackWaza()), (enemy, MakeAttackWaza())));

            Assert.IsTrue(enemy.IsAlive);
            Assert.IsTrue(ally.IsAlive);
            Assert.AreEqual(1, endPhaseCount, "全滅していなければ EndPhase は通常通り 1 回発火");
        }

        // ───── 死亡ユニットスキップ ─────

        [Test]
        public void ProcessTurn_死亡ユニットは行動しない()
        {
            var dead = Make(0, hp: 0);
            dead.BaseUnit.CurrentHP = 0;
            var alive = Make(1);
            var ctx = Ctx(new List<RuntimeUnit> { dead, alive });
            var mgr = new BattleManager(new ActionExecutor());

            var order = new List<RuntimeUnit>();
            mgr.OnActionStart += (_, u) => order.Add(u);

            mgr.ProcessTurn(ctx, Wazas((dead, MakeAttackWaza()), (alive, MakeAttackWaza())));

            CollectionAssert.DoesNotContain(order, dead);
            CollectionAssert.Contains(order, alive);
        }

        // ───── イベント結線（SynergyApplier 想定） ─────

        [Test]
        public void OnBattleStart_購読でApply的処理ができる()
        {
            var ctx = Ctx(new List<RuntimeUnit> { Make(0), Make(1) });
            var mgr = new BattleManager(new ActionExecutor());
            int sideACount = -1;
            mgr.OnBattleStart += c => sideACount = c.AllyUnits.Count;

            mgr.InitializeBattle(ctx);

            Assert.AreEqual(2, sideACount);
        }

        // ───── null 安全 ─────

        [Test]
        public void Constructor_ActionExecutorがnullで例外()
        {
            Assert.Throws<System.ArgumentNullException>(() => new BattleManager(null));
        }

        [Test]
        public void ProcessTurn_battleWazas_nullでも例外なし()
        {
            var ctx = Ctx(new List<RuntimeUnit> { Make(0) });
            var mgr = new BattleManager(new ActionExecutor());
            Assert.DoesNotThrow(() => mgr.ProcessTurn(ctx, null));
        }
    }
}
