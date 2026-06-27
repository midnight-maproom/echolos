using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class BattleRunnerTests
    {
        private static RuntimeUnit Make(int slot, int hp = 100, int atk = 50, int def = 0,
            int spd = 10, Element el = Element.None, AttackKind kind = AttackKind.Ranged)
        {
            var u = new Unit($"u_{slot}", $"u_{slot}", el)
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

        private static RuntimeWaza Attack(double mult = 1.0)
        {
            var w = new Waza("a", "通常攻撃")
            {
                SPD = 10,
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect> { new AttackEffect(wazaMultiplier: mult, isSureHit: true) },
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

        // ───── 勝敗判定 ─────

        [Test]
        public void Run_完勝_敵全滅でPerfectVictory()
        {
            var ally = Make(0, atk: 9999);
            var enemy = Make(0, hp: 1);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy },
                maxTurns: 10,
                battleWazasByUnit: Wazas((ally, Attack()), (enemy, Attack())),
                isAttackingSide: true);

            Assert.AreEqual(BattleResult.PerfectVictory, report.Result);
        }

        [Test]
        public void Run_完敗_味方全滅でCrushingDefeat()
        {
            var ally = Make(0, hp: 1, atk: 1);
            var enemy = Make(0, hp: 100, atk: 9999);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy },
                maxTurns: 10,
                battleWazasByUnit: Wazas((ally, Attack()), (enemy, Attack())),
                isAttackingSide: false);

            Assert.AreEqual(BattleResult.CrushingDefeat, report.Result);
        }

        [Test]
        public void Run_時間切れ_攻め側1撃破でAdvantageousVictory()
        {
            // ally が enemy1 を 1 体だけ撃破 / 残り 1 体は生存で時間切れ
            var ally = Make(0, atk: 9999, hp: 1000, def: 9999);
            var enemy1 = Make(0, hp: 1, def: 0);
            var enemy2 = Make(1, hp: 99999, atk: 1, def: 9999);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy1, enemy2 },
                maxTurns: 1,
                battleWazasByUnit: Wazas(
                    (ally, Attack()), (enemy1, Attack()), (enemy2, Attack())),
                isAttackingSide: true);

            Assert.AreEqual(BattleResult.AdvantageousVictory, report.Result);
        }

        [Test]
        public void Run_時間切れ_攻め側0撃破でMarginalDefeat()
        {
            // 削り合うが撃破までいかずに時間切れ
            var ally = Make(0, hp: 1000, atk: 1, def: 9999);
            var enemy = Make(0, hp: 1000, atk: 1, def: 9999);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy },
                maxTurns: 1,
                battleWazasByUnit: Wazas((ally, Attack()), (enemy, Attack())),
                isAttackingSide: true);

            Assert.AreEqual(BattleResult.MarginalDefeat, report.Result);
        }

        [Test]
        public void Run_時間切れ_守り側0被撃破でAdvantageousVictory()
        {
            var ally = Make(0, hp: 1000, atk: 1, def: 9999);
            var enemy = Make(0, hp: 1000, atk: 1, def: 9999);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy },
                maxTurns: 1,
                battleWazasByUnit: Wazas((ally, Attack()), (enemy, Attack())),
                isAttackingSide: false);

            Assert.AreEqual(BattleResult.AdvantageousVictory, report.Result);
        }

        // ───── Report 内容 ─────

        [Test]
        public void Run_LineupとTurnsがBattleReportにセットされる()
        {
            var a1 = Make(0); var a2 = Make(1);
            var e1 = Make(0);

            var report = BattleRunner.Run(
                new[] { a1, a2 }, new[] { e1 },
                maxTurns: 1,
                battleWazasByUnit: Wazas((a1, Attack()), (a2, Attack()), (e1, Attack())));

            Assert.AreEqual(2, report.AllyLineup.Count);
            Assert.AreEqual(1, report.EnemyLineup.Count);
            Assert.GreaterOrEqual(report.Turns, 1);
        }

        [Test]
        public void Run_結果ログが追加される()
        {
            var ally = Make(0, atk: 9999);
            var enemy = Make(0, hp: 1);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy }, maxTurns: 10,
                battleWazasByUnit: Wazas((ally, Attack()), (enemy, Attack())),
                isAttackingSide: true);

            bool foundResult = false;
            foreach (var l in report.Log)
                if (l.Contains("結果：")) { foundResult = true; break; }
            Assert.IsTrue(foundResult);
        }

        [Test]
        public void Run_BattleEndイベントが記録される()
        {
            var ally = Make(0, atk: 9999);
            var enemy = Make(0, hp: 1);

            var report = BattleRunner.Run(
                new[] { ally }, new[] { enemy }, maxTurns: 10,
                battleWazasByUnit: Wazas((ally, Attack()), (enemy, Attack())),
                isAttackingSide: true);

            int endCount = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.BattleEnd) endCount++;
            Assert.AreEqual(1, endCount);
        }

        // ───── 統合：シナジー発動 ─────

        [Test]
        public void Run_戦闘開始シナジー付与は1行に集約される()
        {
            // 水 2 体染め：DefenseUp +10 を全員に付与（Shield は 4 体染めから）。
            // 旧仕様では unit × effect で個別 Log 行（味方 N 体 × 1 = N 行）。
            // 新仕様では SourceAbilityName ごとに 1 行に集約される。
            var w1 = Make(0, el: Element.Water);
            var w2 = Make(1, el: Element.Water);
            var enemy = Make(0, hp: 9999, def: 9999);

            var report = BattleRunner.Run(
                new[] { w1, w2 }, new[] { enemy }, maxTurns: 1,
                battleWazasByUnit: Wazas((w1, Attack()), (w2, Attack()), (enemy, Attack())));

            int synergyLineCount = 0;
            string synergyLine = null;
            foreach (var l in report.Log)
            {
                if (l.Contains("水の共鳴"))
                {
                    synergyLineCount++;
                    if (synergyLine == null) synergyLine = l;
                }
            }
            Assert.AreEqual(1, synergyLineCount,
                "水シナジー由来の付与ログは SourceAbilityName ごとに 1 行集約される");
            Assert.IsTrue(synergyLine.Contains("味方全体"),
                $"味方陣営生存全員＝「味方全体」表記: {synergyLine}");
            Assert.IsTrue(synergyLine.Contains("DefenseUp"),
                $"効果名 DefenseUp が含まれる: {synergyLine}");
        }

        [Test]
        public void Run_火2体染め編成でシナジー由来OutgoingDamageUpが付与される()
        {
            var f1 = Make(0, atk: 80, el: Element.Fire);
            var f2 = Make(1, atk: 60, el: Element.Fire);
            var enemy = Make(0, hp: 9999, def: 9999);

            var report = BattleRunner.Run(
                new[] { f1, f2 }, new[] { enemy }, maxTurns: 1,
                battleWazasByUnit: Wazas(
                    (f1, Attack()), (f2, Attack()), (enemy, Attack())));

            int fireApplied = 0;
            foreach (var e in report.Events)
                if (e.Kind == BattleEventKind.StatusEffectApplied
                    && e.EffectChange != null
                    && e.EffectChange.Kind == EffectKind.OutgoingDamageUp) fireApplied++;
            Assert.GreaterOrEqual(fireApplied, 1, "火シナジー由来 OutgoingDamageUp が StatusEffectApplied で記録される");
        }

        // ───── null 安全 ─────

        [Test]
        public void Run_alliesがnullで例外()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                BattleRunner.Run(null, new List<RuntimeUnit>(), 10));
        }

        [Test]
        public void Run_enemiesがnullで例外()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                BattleRunner.Run(new List<RuntimeUnit>(), null, 10));
        }
    }
}
