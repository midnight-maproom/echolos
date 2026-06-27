// BattleLogFormatter の純関数群に対する単体テスト。
// 戦闘ロジックを動かさず、HitOutcome / RuntimeUnit / StatusEffect / BattleResult を
// 直接組み立てて整形結果を検証する。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class BattleLogFormatterTests
    {
        private static Unit MakeUnit(string name, int hp = 100)
        {
            var u = new Unit("id_" + name, name)
            {
                MaxHP = hp,
                CurrentHP = hp,
                State = UnitState.Active,
            };
            return u;
        }

        private static RuntimeUnit MakeRu(string name, int slot, int hp = 100)
            => new RuntimeUnit(MakeUnit(name, hp), slot);

        private static HitOutcome Damage(RuntimeUnit target, int dmg, int hpAfter,
            bool died = false, params EffectKind[] effects)
            => new HitOutcome(target, damage: dmg, targetHPAfter: hpAfter,
                resultedInDeath: died, appliedEffects: ToChanges(effects));

        private static HitOutcome Evaded(RuntimeUnit target)
            => new HitOutcome(target, wasEvaded: true, targetHPAfter: target.CurrentHP);

        private static HitOutcome Heal(RuntimeUnit target, int amount, int hpAfter)
            => new HitOutcome(target, healAmount: amount, targetHPAfter: hpAfter);

        private static HitOutcome EffectOnly(RuntimeUnit target, params EffectKind[] effects)
            => new HitOutcome(target, targetHPAfter: target.CurrentHP, appliedEffects: ToChanges(effects));

        // テストデータ簡略化のため EffectKind 列挙だけから EffectChange リストを作るヘルパ。
        // Stacks=1 / RemainingTurns=-1 / Lifetime=Permanent の暫定値で埋める（表示テストでは値検証なし）。
        private static System.Collections.Generic.IReadOnlyList<EffectChange> ToChanges(EffectKind[] kinds)
        {
            if (kinds == null) return null;
            var list = new System.Collections.Generic.List<EffectChange>(kinds.Length);
            foreach (var k in kinds)
                list.Add(new EffectChange(k, 1, -1, Lifetime.Permanent));
            return list;
        }

        // ── CreateNameResolver ──

        [Test]
        public void NameResolverは味方には味プレフィックスを付ける()
        {
            var ally = MakeRu("剣士", 0);
            var enemy = MakeRu("ゴブリン", 1);
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { ally });

            Assert.AreEqual("味剣士#0", nameOf(ally));
            Assert.AreEqual("敵ゴブリン#1", nameOf(enemy));
        }

        // ── FormatSingleOutcome ──

        [Test]
        public void 通常ヒットは名前ダメージ残HPを並べる()
        {
            var target = MakeRu("敵", 1);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcome = Damage(target, 30, 70);

            Assert.AreEqual("敵敵#1に30ダメージ(残HP 70)", BattleLogFormatter.FormatSingleOutcome(outcome, nameOf));
        }

        [Test]
        public void 回避は対象名は回避と返す()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());

            Assert.AreEqual("敵敵#0は回避", BattleLogFormatter.FormatSingleOutcome(Evaded(target), nameOf));
        }

        [Test]
        public void 回復は対象名を回復量回復残HPで返す()
        {
            var target = MakeRu("味方", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { target });

            Assert.AreEqual("味味方#0を25回復(残HP 80)",
                BattleLogFormatter.FormatSingleOutcome(Heal(target, 25, 80), nameOf));
        }

        [Test]
        public void 戦闘不能ヒットは戦闘不能タグを付ける()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcome = Damage(target, 100, 0, died: true);

            Assert.AreEqual("敵敵#0に100ダメージ(残HP 0・戦闘不能)",
                BattleLogFormatter.FormatSingleOutcome(outcome, nameOf));
        }

        [Test]
        public void 付帯効果付きヒットは効果タグを末尾に付ける()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcome = Damage(target, 30, 70, false, EffectKind.Burn, EffectKind.DefenseDown);

            Assert.AreEqual("敵敵#0に30ダメージ(残HP 70) +Burn/+DefenseDown",
                BattleLogFormatter.FormatSingleOutcome(outcome, nameOf));
        }

        [Test]
        public void ダメージなし効果付与のみは付与表記()
        {
            var target = MakeRu("味方", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { target });

            Assert.AreEqual("味味方#0に+AttackUp付与",
                BattleLogFormatter.FormatSingleOutcome(EffectOnly(target, EffectKind.AttackUp), nameOf));
        }

        [Test]
        public void 何もないOutcomeはnull()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcome = new HitOutcome(target, targetHPAfter: target.CurrentHP);

            Assert.IsNull(BattleLogFormatter.FormatSingleOutcome(outcome, nameOf));
        }

        // ── FormatGroup ──

        [Test]
        public void 単一Outcomeのグループはそのまま単一フォーマットに委譲()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var single = Damage(target, 20, 80);

            string expected = BattleLogFormatter.FormatSingleOutcome(single, nameOf);
            string actual = BattleLogFormatter.FormatGroup(new[] { single }, nameOf);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void 多段全命中は合計表記とヒット比()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                Damage(target, 30, 70),
                Damage(target, 30, 40),
            };

            Assert.AreEqual("敵敵#0に2/2回ヒット 合計60ダメージ(残HP 40)",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 多段一部命中は合計を付けず命中分のみで集計()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                Damage(target, 30, 70),
                Evaded(target),
            };

            Assert.AreEqual("敵敵#0に1/2回ヒット 30ダメージ(残HP 100)",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 多段全回避はN回回避表記()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[] { Evaded(target), Evaded(target) };

            Assert.AreEqual("敵敵#0は2回回避", BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 多段の戦闘不能タグは1回でも発生したら付く()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                Damage(target, 50, 50),
                Damage(target, 50, 0, died: true),
            };

            Assert.AreEqual("敵敵#0に2/2回ヒット 合計100ダメージ(残HP 0・戦闘不能)",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 多段の付帯効果は重複除去して並べる()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                Damage(target, 10, 90, false, EffectKind.Burn),
                Damage(target, 10, 80, false, EffectKind.Burn, EffectKind.DefenseDown),
            };

            Assert.AreEqual("敵敵#0に2/2回ヒット 合計20ダメージ(残HP 80) +Burn/+DefenseDown",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 単発攻撃と付帯付与が別Outcomeでも1回ヒット表記を出さず合算表示()
        {
            // 火矢パターン：AttackEffect の攻撃 Outcome 1 件 + onHitRiders の
            // ApplyStatusEffectEffect 由来付与 Outcome 1 件が同 target に並ぶ。
            // 攻撃 hit カウントは付与専用 Outcome を除外して 1 件のみ＝「1/1 回ヒット」
            // 表記を省略して FormatSingleOutcome と同形にする。
            // 実プレイでは付与 Outcome の TargetHPAfter は攻撃 hit 後の HP（70）と同値
            // （ApplyStatusEffectEffect.Apply が target.BaseUnit.CurrentHP を読むため）。
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                Damage(target, 30, 70),
                new HitOutcome(target, targetHPAfter: 70,
                    appliedEffects: ToChanges(new[] { EffectKind.Burn })),
            };

            Assert.AreEqual("敵敵#0に30ダメージ(残HP 70) +Burn",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 同ターゲットへの付与のみ複数Outcomeはダメージ表記なしで集約()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                EffectOnly(target, EffectKind.AttackUp),
                EffectOnly(target, EffectKind.DefenseUp),
            };

            Assert.AreEqual("敵敵#0に+AttackUp/+DefenseUp付与",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        // ── FormatOutcomesLogLine ──

        [Test]
        public void RemovedEffectsのみのOutcomeは解除表記()
        {
            // Dispel/Cleanse 系 Effect のパターン：RemovedEffects のみ＋ Damage 0。
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var removed = new HitOutcome(target, targetHPAfter: 100,
                removedEffects: ToChanges(new[] { EffectKind.AttackUp }));
            Assert.AreEqual("敵敵#0から-AttackUp解除",
                BattleLogFormatter.FormatSingleOutcome(removed, nameOf));
        }

        [Test]
        public void Shield吸収Outcomeはシールドが防いだ表記()
        {
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var shielded = new HitOutcome(target, damage: 0, targetHPAfter: 100, wasShielded: true);
            Assert.AreEqual("敵敵#0のシールドが防いだ",
                BattleLogFormatter.FormatSingleOutcome(shielded, nameOf));
        }

        [Test]
        public void Shield吸収はFormatGroup単発でもシールドが防いだ表記()
        {
            // 火矢パターンで Shield 吸収＝攻撃 Outcome（WasShielded=true）のみ 1 件。
            // 付与専用 Outcome はそもそも積まれない（AttackEffect が Rider をスキップする）。
            var target = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var group = new[]
            {
                new HitOutcome(target, damage: 0, targetHPAfter: 100, wasShielded: true),
                new HitOutcome(target, targetHPAfter: 100), // ダミー追加で group.Count>=2
            };

            Assert.AreEqual("敵敵#0のシールドが防いだ",
                BattleLogFormatter.FormatGroup(group, nameOf));
        }

        [Test]
        public void 反撃Outcomeは攻撃グループから分離して反撃プレフィックス付きで連結される()
        {
            // 双連撃パターン：[攻撃1 → 反撃 → 攻撃2] の順で Outcomes が並ぶが、
            // 攻撃側 2 hit を 1 group に集約＋反撃を別 group として表示する。
            // 旧仕様では反撃 Outcome が同 target 連続を分断し 3 fragment に分割されていた。
            var enemy = MakeRu("敵", 0);
            var ally = MakeRu("味", 1);
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { ally });
            var outcomes = new[]
            {
                Damage(enemy, 47, 53),
                new HitOutcome(ally, damage: 17, targetHPAfter: 83, isCounterAttack: true),
                Damage(enemy, 26, 27),
            };

            string line = BattleLogFormatter.FormatOutcomesLogLine(outcomes, nameOf);

            Assert.AreEqual(
                "    敵敵#0に2/2回ヒット 合計73ダメージ(残HP 27) | （反撃）味味#1に17ダメージ(残HP 83)",
                line);
        }

        [Test]
        public void 複数ターゲットのアクションは縦棒区切りで連結する()
        {
            var t1 = MakeRu("敵A", 0);
            var t2 = MakeRu("敵B", 1);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcomes = new[] { Damage(t1, 20, 80), Damage(t2, 25, 75) };

            string line = BattleLogFormatter.FormatOutcomesLogLine(outcomes, nameOf);

            Assert.AreEqual("    敵敵A#0に20ダメージ(残HP 80) | 敵敵B#1に25ダメージ(残HP 75)", line);
        }

        [Test]
        public void 同一ターゲット連続はグルーピングされる()
        {
            var t1 = MakeRu("敵A", 0);
            var t2 = MakeRu("敵B", 1);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcomes = new[]
            {
                Damage(t1, 10, 90),
                Damage(t1, 10, 80),
                Damage(t2, 20, 80),
            };

            string line = BattleLogFormatter.FormatOutcomesLogLine(outcomes, nameOf);

            Assert.AreEqual("    敵敵A#0に2/2回ヒット 合計20ダメージ(残HP 80) | 敵敵B#1に20ダメージ(残HP 80)", line);
        }

        [Test]
        public void 空Outcomesはnull()
        {
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());

            Assert.IsNull(BattleLogFormatter.FormatOutcomesLogLine(Array.Empty<HitOutcome>(), nameOf));
            Assert.IsNull(BattleLogFormatter.FormatOutcomesLogLine(null, nameOf));
        }

        [Test]
        public void 何も表示すべきものがない時はnull()
        {
            var t = MakeRu("敵", 0);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());
            var outcomes = new[] { new HitOutcome(t, targetHPAfter: t.CurrentHP) };

            Assert.IsNull(BattleLogFormatter.FormatOutcomesLogLine(outcomes, nameOf));
        }

        // ── FormatHealOverTimePhaseLine ──

        private static StatusEffectProcessor.HealOverTimeTick Tick(
            RuntimeUnit u, int healed, int hpAfter, string source = null)
            => new StatusEffectProcessor.HealOverTimeTick(u, healed, hpAfter, source);

        [Test]
        public void HealOverTimePhase_味方全員が対象なら味方全体表記()
        {
            var u1 = MakeRu("剣士", 0); var u2 = MakeRu("タンク", 1);
            var ctx = new BattleContext(10) { AllyUnits = { u1, u2 } };
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { u1, u2 });
            var ticks = new[]
            {
                Tick(u1, 5, 55, "光の共鳴 Lv2"),
                Tick(u2, 5, 65, "光の共鳴 Lv2"),
            };

            string line = BattleLogFormatter.FormatHealOverTimePhaseLine(ticks, ctx, nameOf);

            Assert.AreEqual("    ✚ 味方全体に 光の共鳴 Lv2：継続回復 +5/+5", line);
        }

        [Test]
        public void HealOverTimePhase_部分対象なら味方N体表記()
        {
            var u1 = MakeRu("剣士", 0);
            var u2 = MakeRu("タンク", 1);
            var u3 = MakeRu("射手", 2);
            var ctx = new BattleContext(10) { AllyUnits = { u1, u2, u3 } };
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { u1, u2, u3 });
            var ticks = new[]
            {
                Tick(u1, 5, 55, "光の共鳴 Lv2"),
                Tick(u2, 4, 64, "光の共鳴 Lv2"),
            };

            string line = BattleLogFormatter.FormatHealOverTimePhaseLine(ticks, ctx, nameOf);

            Assert.AreEqual("    ✚ 味方2体に 光の共鳴 Lv2：継続回復 +5/+4", line);
        }

        [Test]
        public void HealOverTimePhase_SourceAbilityNameなしならソース部分省略()
        {
            var u1 = MakeRu("剣士", 0);
            var ctx = new BattleContext(10) { AllyUnits = { u1 } };
            var nameOf = BattleLogFormatter.CreateNameResolver(new[] { u1 });
            var ticks = new[] { Tick(u1, 5, 55, null) };

            string line = BattleLogFormatter.FormatHealOverTimePhaseLine(ticks, ctx, nameOf);

            Assert.AreEqual("    ✚ 味方全体に 継続回復 +5", line);
        }

        [Test]
        public void HealOverTimePhase_空ticksはnull()
        {
            var ctx = new BattleContext(10);
            var nameOf = BattleLogFormatter.CreateNameResolver(Array.Empty<RuntimeUnit>());

            Assert.IsNull(BattleLogFormatter.FormatHealOverTimePhaseLine(
                Array.Empty<StatusEffectProcessor.HealOverTimeTick>(), ctx, nameOf));
            Assert.IsNull(BattleLogFormatter.FormatHealOverTimePhaseLine(null, ctx, nameOf));
        }

        // ── SurvivorSummary / LineupSummary ──

        [Test]
        public void 全員死亡時は全滅表記()
        {
            var u = MakeRu("敵", 0);
            u.BaseUnit.CurrentHP = 0;
            u.BaseUnit.State = UnitState.Dead;

            Assert.AreEqual("全滅", BattleLogFormatter.SurvivorSummary(new[] { u }));
        }

        [Test]
        public void 一部生存時は名前と残HPを並べる()
        {
            var alive = MakeRu("剣士", 0, 100);
            alive.BaseUnit.CurrentHP = 60;
            var dead = MakeRu("弓兵", 1, 100);
            dead.BaseUnit.CurrentHP = 0;
            dead.BaseUnit.State = UnitState.Dead;

            Assert.AreEqual("剣士(60/100)", BattleLogFormatter.SurvivorSummary(new[] { alive, dead }));
        }

        [Test]
        public void Lineupはslot番号と名前とHPを並べる()
        {
            var back = MakeRu("弓兵", 3, 80);
            var front = MakeRu("剣士", 0, 100);

            Assert.AreEqual("slot0:剣士(100/100), slot3:弓兵(80/80)",
                BattleLogFormatter.LineupSummary(new[] { back, front }));
        }

        // ── ResultLabel ──

        [Test]
        public void ResultLabelは全分岐を網羅する()
        {
            Assert.AreEqual("完勝", BattleLogFormatter.ResultLabel(BattleResult.PerfectVictory));
            Assert.AreEqual("辛勝", BattleLogFormatter.ResultLabel(BattleResult.AdvantageousVictory));
            Assert.AreEqual("惜敗", BattleLogFormatter.ResultLabel(BattleResult.MarginalDefeat));
            Assert.AreEqual("完敗", BattleLogFormatter.ResultLabel(BattleResult.CrushingDefeat));
            Assert.AreEqual("未決着", BattleLogFormatter.ResultLabel(BattleResult.None));
        }

        // ── FormatEffectValue ──

        [Test]
        public void FormatEffectValue_Magnitude付きは符号付きで表示()
        {
            var eff = TestEff.Eff(EffectKind.AttackUp, magnitude: 10f);
            Assert.AreEqual("AttackUp +10", BattleLogFormatter.FormatEffectValue(eff));
        }

        [Test]
        public void FormatEffectValue_Shieldは残数Stacksで表示()
        {
            var eff = TestEff.Eff(EffectKind.Shield, stacks: 3);
            Assert.AreEqual("Shield 3", BattleLogFormatter.FormatEffectValue(eff));
        }

        [Test]
        public void FormatEffectValue_Magnitude0はEffectType名のみ()
        {
            var eff = TestEff.Eff(EffectKind.SilencedCounter);
            Assert.AreEqual("SilencedCounter", BattleLogFormatter.FormatEffectValue(eff));
        }

        // ── FormatEffectWithSource ──

        [Test]
        public void SourceAbilityNameがあればコロン付きで値表記を表示()
        {
            var eff = TestEff.Persistent(EffectKind.AttackUp, 5, "王家の加護");
            Assert.AreEqual("王家の加護：AttackUp +5", BattleLogFormatter.FormatEffectWithSource(eff));
        }

        [Test]
        public void SourceAbilityNameがなければ値表記のみ()
        {
            var eff = TestEff.Eff(EffectKind.Burn);
            Assert.AreEqual("Burn", BattleLogFormatter.FormatEffectWithSource(eff));
        }
    }
}
