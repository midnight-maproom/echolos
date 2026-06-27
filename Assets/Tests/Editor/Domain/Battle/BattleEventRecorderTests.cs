using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class BattleEventRecorderTests
    {
        private static RuntimeUnit Make(int slot, int hp = 100, int atk = 50,
            AttackKind kind = AttackKind.Ranged)
        {
            var u = new Unit($"u_{slot}", $"u_{slot}", Element.None)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                AttackKind = kind,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static (BattleReport report, BattleEventRecorder rec) Setup()
        {
            var report = new BattleReport();
            var rec = new BattleEventRecorder(report, u => u.BaseUnit.Name);
            return (report, rec);
        }

        private static BattleContext MakeContext(int turn = 1) => new BattleContext(10) { CurrentTurn = turn };

        // ───── Constructor ─────

        [Test]
        public void Constructor_reportがnullで例外()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new BattleEventRecorder(null, u => u.BaseUnit.Name));
        }

        [Test]
        public void Constructor_nameOfがnullで例外()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new BattleEventRecorder(new BattleReport(), null));
        }

        // ───── AttachToManager ─────

        [Test]
        public void AttachToManager_OnStartPhaseでTurnStartイベントとログ()
        {
            var (report, rec) = Setup();
            var mgr = new BattleManager(new ActionExecutor());
            rec.AttachToManager(mgr);
            var ctx = MakeContext(turn: 3);

            mgr.InitializeBattle(ctx);
            mgr.ProcessTurn(ctx, null);

            bool foundTurnLog = false;
            foreach (var l in report.Log)
                if (l == "── ターン3 ──") { foundTurnLog = true; break; }
            Assert.IsTrue(foundTurnLog, "ターン3 開始ログが含まれる");
            int turnStartCount = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.TurnStart && e.Turn == 3) turnStartCount++;
            Assert.AreEqual(1, turnStartCount);
        }

        [Test]
        public void AttachToManager_OnActionSkippedでログとイベント()
        {
            var (report, rec) = Setup();
            var mgr = new BattleManager(new ActionExecutor());
            rec.AttachToManager(mgr);
            var u = Make(0);
            u.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 5));
            var ctx = MakeContext();
            ctx.AllyUnits.Add(u);

            mgr.InitializeBattle(ctx);
            mgr.ProcessTurn(ctx,
                new Dictionary<RuntimeUnit, IList<RuntimeWaza>>
                {
                    { u, new List<RuntimeWaza>() }
                });

            int skippedCount = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.ActionSkipped && e.Actor == u) skippedCount++;
            Assert.AreEqual(1, skippedCount);

            bool foundLog = false;
            foreach (var l in report.Log)
                if (l.Contains("行動できない")) { foundLog = true; break; }
            Assert.IsTrue(foundLog);
        }

        // ───── AttachToExecutor ─────

        [Test]
        public void AttachToExecutor_OnActionResolvedで宣言とOutcomeが1行に集約される()
        {
            var (report, rec) = Setup();
            var exec = new ActionExecutor();
            rec.AttachToExecutor(exec);

            var actor = Make(0, atk: 50);
            var target = Make(0, hp: 100);
            var ctx = new BattleContext(10) { AllyUnits = { actor }, EnemyUnits = { target } };

            var waza = new Waza("a", "テスト技")
            {
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect> { new AttackEffect(isSureHit: true) },
            };
            var decl = new ActionDeclaration(
                actor, new RuntimeWaza(waza),
                new List<RuntimeUnit> { target }, 10, false);

            int logBefore = report.Log.Count;
            exec.Execute(decl, ctx);
            int logAdded = report.Log.Count - logBefore;

            // Log と Events を 1:1 に揃えるため、宣言行と Outcome 集約行は 1 行統合される。
            Assert.AreEqual(1, logAdded, "ActionResolved 1 件あたり Log 1 行のみ追加される");
            string combinedLine = report.Log[logBefore];
            Assert.IsTrue(combinedLine.Contains("テスト技"),
                $"統合行に Waza 名「テスト技」が含まれる: {combinedLine}");
            Assert.IsTrue(combinedLine.Contains("ダメージ"),
                $"統合行に Outcome の「ダメージ」が含まれる: {combinedLine}");

            int resolved = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.ActionResolved) resolved++;
            Assert.AreEqual(1, resolved);
        }

        // ───── AttachToStatusProcessor ─────

        [Test]
        public void AttachToStatusProcessor_OnBurnTickDamageでログとイベント()
        {
            var (report, rec) = Setup();
            var sp = new StatusEffectProcessor();
            rec.AttachToStatusProcessor(sp);

            var u = Make(0, hp: 100);
            u.BaseUnit.CurrentHP = 50;
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            var ctx = new BattleContext(10) { AllyUnits = { u }, CurrentTurn = 2 };

            sp.HandleEndPhase(ctx);

            bool foundLog = false;
            foreach (var l in report.Log)
                if (l.Contains("毒/燃焼 10")) { foundLog = true; break; }
            Assert.IsTrue(foundLog);

            int burnCount = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.BurnTick && e.Target == u && e.Damage == 10) burnCount++;
            Assert.AreEqual(1, burnCount);
        }

        [Test]
        public void AttachToStatusProcessor_BurnTickのTargetHPAfterは適用後のHP()
        {
            // ApplyBurnDamage は先に CurrentHP を減算してから OnBurnTickDamage を発火する。
            // Recorder の TargetHPAfter = unit.CurrentHP は Burn 適用後の値になる。
            var (report, rec) = Setup();
            var sp = new StatusEffectProcessor();
            rec.AttachToStatusProcessor(sp);

            var u = Make(0, hp: 100);
            u.BaseUnit.CurrentHP = 50;
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            var ctx = new BattleContext(10) { AllyUnits = { u }, CurrentTurn = 2 };

            sp.HandleEndPhase(ctx);

            BattleEvent burnEv = null;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.BurnTick) { burnEv = e; break; }
            Assert.IsNotNull(burnEv);
            Assert.AreEqual(40, burnEv.TargetHPAfter, "Burn 適用後の HP (50 - 10) が記録される");
        }

        [Test]
        public void AttachToStatusProcessor_OnStatusEffectKillでDiedイベントとログ()
        {
            // Burn 致死量で OnStatusEffectKill 経由の Died イベントが発火される。
            var (report, rec) = Setup();
            var sp = new StatusEffectProcessor();
            rec.AttachToStatusProcessor(sp);

            var u = Make(0, hp: 10);
            u.BaseUnit.CurrentHP = 10;
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3));
            var ctx = new BattleContext(10) { AllyUnits = { u }, CurrentTurn = 2 };

            sp.HandleEndPhase(ctx);

            int diedCount = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.Died && e.Target == u) diedCount++;
            Assert.AreEqual(1, diedCount);

            bool foundLog = false;
            foreach (var l in report.Log)
                if (l.Contains("戦闘不能")) { foundLog = true; break; }
            Assert.IsTrue(foundLog, "戦闘不能ログが出る");
        }

        [Test]
        public void AttachToStatusProcessor_OnHealOverTimePhaseで1Event集約()
        {
            var (report, rec) = Setup();
            var sp = new StatusEffectProcessor();
            rec.AttachToStatusProcessor(sp);

            var u1 = Make(0, hp: 100); u1.BaseUnit.CurrentHP = 50;
            var u2 = Make(1, hp: 100); u2.BaseUnit.CurrentHP = 60;
            u1.AddEffect(TestEff.Persistent(EffectKind.HealOverTime, 5f, "光の共鳴 Lv2"));
            u2.AddEffect(TestEff.Persistent(EffectKind.HealOverTime, 5f, "光の共鳴 Lv2"));
            var ctx = new BattleContext(10) { AllyUnits = { u1, u2 } };

            sp.HandleEndPhase(ctx);

            // 1 Event = 1 ログの設計：HealOverTimePhase Event が 1 件、それに LogLine と HealTicks が乗る
            int phaseEventCount = 0;
            BattleEvent phaseEvent = null;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.HealOverTimePhase)
                {
                    phaseEventCount++;
                    phaseEvent = e;
                }
            Assert.AreEqual(1, phaseEventCount, "HealOverTimePhase Event は 1 件");
            Assert.IsNotNull(phaseEvent.HealTicks);
            Assert.AreEqual(2, phaseEvent.HealTicks.Count, "HealTicks に全 unit ぶん含まれる");
            Assert.IsNotNull(phaseEvent.LogLine);
            Assert.IsTrue(phaseEvent.LogLine.Contains("味方全体")
                       && phaseEvent.LogLine.Contains("光の共鳴 Lv2")
                       && phaseEvent.LogLine.Contains("継続回復 +5/+5"),
                "集約 LogLine が陣営単位 1 行形式（味方全体に 光の共鳴 Lv2：継続回復 +5/+5）");

            // per-unit Healed Event は出ない（1 Event 集約原則）
            int perUnitHealCount = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.Healed) perUnitHealCount++;
            Assert.AreEqual(0, perUnitHealCount, "per-unit Healed Event は作られない（集約 1 件のみ）");
        }

        // ───── SubscribeUnitEffects ─────

        [Test]
        public void SubscribeUnitEffects_Triggered付与でログとイベント()
        {
            var (report, rec) = Setup();
            var u = Make(0);
            var ctx = MakeContext();
            rec.SubscribeUnitEffects(u, ctx);

            u.AddEffect(TestEff.Triggered(EffectKind.AttackUp, 10, remainingTurns: 3));

            bool foundLog = false;
            foreach (var l in report.Log)
                if (l.Contains("付与")) { foundLog = true; break; }
            Assert.IsTrue(foundLog);

            int applied = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.StatusEffectApplied && e.Target == u) applied++;
            Assert.AreEqual(1, applied);
        }

        [Test]
        public void SubscribeUnitEffects_ActionGuard付与はログイベント抑制()
        {
            var (report, rec) = Setup();
            var u = Make(0);
            var ctx = MakeContext();
            rec.SubscribeUnitEffects(u, ctx);

            u.AddEffect(new SelfGuard(3));

            Assert.AreEqual(0, report.Log.Count);
            Assert.AreEqual(0, report.Events.Count);
        }

        [Test]
        public void SubscribeUnitEffects_Waza由来の付与は二重表示防止のため抑制される()
        {
            var (report, rec) = Setup();
            var exec = new ActionExecutor();
            var mgr = new BattleManager(exec);
            rec.AttachToManager(mgr);
            rec.AttachToExecutor(exec);

            var actor = Make(0, atk: 50);
            var target = Make(0, hp: 100);
            var ctx = new BattleContext(10) { AllyUnits = { actor }, EnemyUnits = { target } };
            rec.SubscribeUnitEffects(target, ctx);

            var burnTemplate = TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 1);
            var waza = new Waza("a", "炎攻撃")
            {
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect>
                {
                    new AttackEffect(isSureHit: true, onHitRiders: new List<IActionEffect>
                    {
                        new ApplyStatusEffectEffect(new List<IEffect> { burnTemplate }),
                    }),
                },
            };

            mgr.InitializeBattle(ctx);
            mgr.ProcessTurn(ctx, new Dictionary<RuntimeUnit, IList<RuntimeWaza>>
            {
                { actor, new List<RuntimeWaza> { new RuntimeWaza(waza) } },
            });

            // 単独「+ ... 付与」ログは出ない（ActionResolved 集約行に「+Burn」として含まれる）
            int standaloneAppliedLog = 0;
            foreach (var l in report.Log)
                if (l.StartsWith("    + ") && l.Contains("付与")) standaloneAppliedLog++;
            Assert.AreEqual(0, standaloneAppliedLog,
                "Waza 由来付与は単独「+ 付与」ログを出さない");

            // 単独 StatusEffectApplied イベントもなし（Log と Events の 1:1 を保つ）
            int applied = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.StatusEffectApplied) applied++;
            Assert.AreEqual(0, applied,
                "Waza 由来付与は単独 StatusEffectApplied イベントを発火しない");
        }

        [Test]
        public void SubscribeUnitEffects_アクション外の付与は引き続き記録される()
        {
            var (report, rec) = Setup();
            var mgr = new BattleManager(new ActionExecutor());
            rec.AttachToManager(mgr);

            var u = Make(0);
            var ctx = MakeContext();
            rec.SubscribeUnitEffects(u, ctx);

            // BattleManager 経由でアクションを実行していない＝_inActionResolution は false のまま
            u.AddEffect(TestEff.Triggered(EffectKind.AttackUp, 10, remainingTurns: 3));

            bool foundLog = false;
            foreach (var l in report.Log)
                if (l.Contains("AttackUp") && l.Contains("付与")) { foundLog = true; break; }
            Assert.IsTrue(foundLog, "アクション外の付与（シナジー等）はログ記録される");

            int applied = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.StatusEffectApplied) applied++;
            Assert.AreEqual(1, applied, "アクション外の付与はイベント記録される");
        }

        [Test]
        public void SubscribeUnitEffects_Persistent剥奪はログ抑制イベント残す()
        {
            var (report, rec) = Setup();
            var u = Make(0);
            var ctx = MakeContext();
            var eff = TestEff.Persistent(EffectKind.DefenseUp, 5, "test");
            u.AddEffect(eff);
            rec.SubscribeUnitEffects(u, ctx);
            int logBefore = report.Log.Count;

            u.RemoveEffect(eff);

            // 剥奪ログはなし
            Assert.AreEqual(logBefore, report.Log.Count);
            // イベントは残る
            int expired = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.StatusEffectExpired) expired++;
            Assert.AreEqual(1, expired);
        }

        // ───── RecordBattleEnd ─────

        [Test]
        public void RecordBattleEnd_LogとEventを1対1で追加_結果ラベルのみ()
        {
            var (report, rec) = Setup();
            rec.RecordBattleEnd(BattleResult.PerfectVictory, turn: 5);

            // Log と Events を 1:1 に保つため、両方に 1 件ずつ追加される。
            Assert.AreEqual(1, report.Log.Count);
            Assert.IsTrue(report.Log[0].StartsWith("結果："), $"結果行: {report.Log[0]}");
            Assert.AreEqual(1, report.Events.Count);
            Assert.AreEqual(BattleEventKind.BattleEnd, report.Events[0].Kind);
            Assert.AreEqual(BattleResult.PerfectVictory, report.Events[0].Result);
            Assert.AreEqual(5, report.Events[0].Turn);
        }

        [Test]
        public void RecordBattleEnd_生存サマリ付きでも1行に集約()
        {
            var (report, rec) = Setup();
            rec.RecordBattleEnd(BattleResult.AdvantageousVictory, turn: 3,
                allySurvivors: "剣士(50/100)", enemySurvivors: "全滅");

            Assert.AreEqual(1, report.Log.Count);
            Assert.IsTrue(report.Log[0].Contains("味方：剣士(50/100)"));
            Assert.IsTrue(report.Log[0].Contains("敵：全滅"));
            Assert.AreEqual(1, report.Events.Count);
        }
    }
}
