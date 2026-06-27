using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class StatusEffectProcessorTests
    {
        private static RuntimeUnit Make(int slot = 0, int maxHp = 100, int currentHp = -1)
        {
            var u = new Unit($"u_{slot}", $"u_{slot}", Element.None)
            {
                MaxHP = maxHp,
                CurrentHP = currentHp < 0 ? maxHp : currentHp,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static BattleContext Ctx(params RuntimeUnit[] allies)
        {
            var ctx = new BattleContext(10);
            foreach (var u in allies) ctx.AllyUnits.Add(u);
            return ctx;
        }

        // ───── HandleActionStart ─────

        [Test]
        public void HandleActionStart_ActionGuardを削除()
        {
            var u = Make();
            u.AddEffect(new SelfGuard(3));
            new StatusEffectProcessor().HandleActionStart(Ctx(u), u);
            Assert.AreEqual(0, u.ActiveEffects.Count);
        }

        [Test]
        public void HandleActionStart_呪い致死量で即死()
        {
            var u = Make(currentHp: 40);
            u.AddEffect(TestEff.Eff(EffectKind.Curse, stacks: 5));
            new StatusEffectProcessor().HandleActionStart(Ctx(u), u);
            Assert.IsFalse(u.IsAlive);
            Assert.AreEqual(0, u.CurrentHP);
        }

        [Test]
        public void HandleActionStart_呪い致死量未満は生存()
        {
            var u = Make(currentHp: 60);
            u.AddEffect(TestEff.Eff(EffectKind.Curse, stacks: 5));
            new StatusEffectProcessor().HandleActionStart(Ctx(u), u);
            Assert.IsTrue(u.IsAlive);
            Assert.AreEqual(60, u.CurrentHP);
        }

        [Test]
        public void HandleActionStart_死亡ユニットは何もしない()
        {
            var u = Make(currentHp: 0);
            u.BaseUnit.CurrentHP = 0;
            u.AddEffect(new SelfGuard(3));
            new StatusEffectProcessor().HandleActionStart(Ctx(u), u);
            Assert.AreEqual(1, u.ActiveEffects.Count);
        }

        // ───── HandleActionSkipped ─────

        [Test]
        public void HandleActionSkipped_麻痺発動でスタック削除と許容量倍化()
        {
            var u = Make();
            u.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 2));
            int beforeTol = u.ParalysisTolerance;
            new StatusEffectProcessor().HandleActionSkipped(Ctx(u), u);
            Assert.IsNull(u.FindEffect(EffectKind.Paralysis));
            Assert.AreEqual(beforeTol * 2, u.ParalysisTolerance);
        }

        [Test]
        public void HandleActionSkipped_凍結スキップは麻痺処理しない()
        {
            var u = Make();
            u.AddEffect(TestEff.Eff(EffectKind.Freeze, stacks: 10));
            int beforeTol = u.ParalysisTolerance;
            new StatusEffectProcessor().HandleActionSkipped(Ctx(u), u);
            Assert.AreEqual(beforeTol, u.ParalysisTolerance);
            Assert.IsNotNull(u.FindEffect(EffectKind.Freeze));
        }

        // ───── Burn（DOT は Shield 貫通） ─────

        [Test]
        public void HandleEndPhase_Burn_Shield貫通でHP直撃()
        {
            var u = Make(maxHp: 100, currentHp: 50);
            var shield = TestEff.Persistent(EffectKind.Shield, 0f, "test", maxStacks: 3);
            shield.Stacks = 3;
            u.AddEffect(shield);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(40, u.CurrentHP, "2x5=10 ダメージが Shield 貫通で HP に直撃");
            Assert.AreEqual(3, u.ShieldStacks, "Shield は消費されない");
        }

        [Test]
        public void HandleEndPhase_Burn致死量で死亡()
        {
            var u = Make(maxHp: 100, currentHp: 10);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 3));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(0, u.CurrentHP);
            Assert.IsFalse(u.IsAlive);
        }

        [Test]
        public void HandleEndPhase_Burn致死後はHealOverTime発動しない()
        {
            var u = Make(maxHp: 100, currentHp: 5);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            u.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 10f));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(0, u.CurrentHP);
            Assert.IsFalse(u.IsAlive);
        }

        [Test]
        public void HandleEndPhase_Burn_Magnitude未設定は最低1ダメージ毎スタック()
        {
            var u = Make(currentHp: 50);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, stacks: 4));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(46, u.CurrentHP, "Magnitude=0 → max(1, 0)=1 / 1*4=4 ダメージ");
        }

        // ───── HealOverTime ─────

        [Test]
        public void HandleEndPhase_HealOverTime_MaxHP割合で回復()
        {
            var u = Make(maxHp: 100, currentHp: 50);
            u.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(55, u.CurrentHP, "100 × 5% = 5 回復");
        }

        [Test]
        public void HandleEndPhase_HealOverTime_最大HPでクランプ()
        {
            var u = Make(maxHp: 100, currentHp: 98);
            u.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(100, u.CurrentHP);
        }

        [Test]
        public void HandleEndPhase_HealOverTime_満タンなら何もしない()
        {
            var u = Make(maxHp: 100, currentHp: 100);
            u.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(100, u.CurrentHP);
        }

        [Test]
        public void HandleEndPhase_BurnとHealOverTime_先にBurn後にHeal()
        {
            var u = Make(maxHp: 100, currentHp: 50);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            u.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            // Burn -10 → 40 → HealOverTime +5 → 45
            Assert.AreEqual(45, u.CurrentHP);
        }

        // ───── 時限・凍結減算 ─────

        [Test]
        public void HandleEndPhase_凍結スタック1減算_0で消滅()
        {
            var u = Make();
            u.AddEffect(TestEff.Eff(EffectKind.Freeze, stacks: 1));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.IsNull(u.FindEffect(EffectKind.Freeze));
        }

        [Test]
        public void HandleEndPhase_時限効果残ターン減算_0で消滅()
        {
            var u = Make();
            u.AddEffect(TestEff.Triggered(EffectKind.AttackUp, 10, remainingTurns: 1));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(0, u.ActiveEffects.Count);
        }

        [Test]
        public void HandleEndPhase_永続効果は時限減算対象外()
        {
            var u = Make();
            u.AddEffect(TestEff.Persistent(EffectKind.DefenseUp, 5, "test"));

            new StatusEffectProcessor().HandleEndPhase(Ctx(u));

            Assert.AreEqual(1, u.ActiveEffects.Count);
            Assert.AreEqual(-1, u.ActiveEffects[0].RemainingTurns);
        }

        [Test]
        public void HandleEndPhase_死亡ユニットはスキップ()
        {
            var alive = Make(slot: 0, currentHp: 50);
            var dead = Make(slot: 1, currentHp: 0);
            dead.BaseUnit.CurrentHP = 0;
            dead.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 5));

            new StatusEffectProcessor().HandleEndPhase(Ctx(alive, dead));

            Assert.AreEqual(50, alive.CurrentHP);
            Assert.AreEqual(0, dead.CurrentHP);
            Assert.AreEqual(1, dead.ActiveEffects.Count, "死亡ユニットの Burn は消化されない");
        }

        // ───── アンデッド復活 ─────

        [Test]
        public void HandleUnitDied_復活回数あり_HP半分で復活()
        {
            var u = Make(maxHp: 100);
            u.CurrentReviveCount = 1;
            u.BaseUnit.CurrentHP = 0;
            u.BaseUnit.State = UnitState.Dead;

            new StatusEffectProcessor().HandleUnitDied(Ctx(u), u);

            Assert.IsTrue(u.IsAlive);
            Assert.AreEqual(50, u.CurrentHP);
            Assert.AreEqual(0, u.CurrentReviveCount);
            Assert.IsTrue(u.HasActedThisTurn);
        }

        [Test]
        public void HandleUnitDied_復活回数0_復活しない()
        {
            var u = Make(maxHp: 100);
            u.CurrentReviveCount = 0;
            u.BaseUnit.CurrentHP = 0;
            u.BaseUnit.State = UnitState.Dead;

            new StatusEffectProcessor().HandleUnitDied(Ctx(u), u);

            Assert.IsFalse(u.IsAlive);
        }

        [Test]
        public void HandleUnitDied_復活無効化デバフあり_復活しない()
        {
            var u = Make(maxHp: 100);
            u.CurrentReviveCount = 1;
            u.AddEffect(TestEff.Eff(EffectKind.ReviveInvalid));
            u.BaseUnit.CurrentHP = 0;
            u.BaseUnit.State = UnitState.Dead;

            new StatusEffectProcessor().HandleUnitDied(Ctx(u), u);

            Assert.IsFalse(u.IsAlive);
            Assert.AreEqual(1, u.CurrentReviveCount);
        }

        // ───── イベント発火 ─────

        [Test]
        public void OnBurnTickDamage_イベント発火()
        {
            var u = Make(currentHp: 50);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            int received = 0;
            var p = new StatusEffectProcessor();
            p.OnBurnTickDamage += (_, _u, dmg) => { if (_u == u) received = dmg; };

            p.HandleEndPhase(Ctx(u));

            Assert.AreEqual(10, received);
        }

        [Test]
        public void OnHealOverTimePhase_イベント発火_実回復量を通知()
        {
            var u = Make(maxHp: 100, currentHp: 98);
            u.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));
            int received = 0;
            var p = new StatusEffectProcessor();
            p.OnHealOverTimePhase += (_, ticks) =>
            {
                foreach (var t in ticks)
                    if (t.Unit == u) received = t.Healed;
            };

            p.HandleEndPhase(Ctx(u));

            Assert.AreEqual(2, received, "クランプ後の実回復量");
        }

        [Test]
        public void OnHealOverTimePhase_複数unitで1回だけ集約発火()
        {
            var u1 = Make(slot: 0, maxHp: 100, currentHp: 50);
            var u2 = Make(slot: 1, maxHp: 100, currentHp: 70);
            u1.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));
            u2.AddEffect(TestEff.Eff(EffectKind.HealOverTime, magnitude: 5f));
            int phaseFireCount = 0;
            int ticksCount = 0;
            var p = new StatusEffectProcessor();
            p.OnHealOverTimePhase += (_, ticks) =>
            {
                phaseFireCount++;
                ticksCount = ticks.Count;
            };

            p.HandleEndPhase(Ctx(u1, u2));

            Assert.AreEqual(1, phaseFireCount, "陣営の人数によらず Phase は 1 回だけ発火");
            Assert.AreEqual(2, ticksCount, "tick は対象 unit 数ぶん含まれる");
        }

        [Test]
        public void OnHealOverTimePhase_SourceAbilityNameを伝搬()
        {
            var u = Make(maxHp: 100, currentHp: 50);
            u.AddEffect(TestEff.Persistent(
                EffectKind.HealOverTime, 5f, "光の共鳴 Lv4"));
            string receivedSource = null;
            var p = new StatusEffectProcessor();
            p.OnHealOverTimePhase += (_, ticks) =>
            {
                foreach (var t in ticks)
                    if (t.Unit == u) receivedSource = t.SourceLabel;
            };

            p.HandleEndPhase(Ctx(u));

            Assert.AreEqual("光の共鳴 Lv4", receivedSource);
        }

        [Test]
        public void OnStatusEffectKill_Burn致死で発火()
        {
            var u = Make(currentHp: 5);
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            int killed = 0;
            var p = new StatusEffectProcessor();
            p.OnStatusEffectKill += (_, _u) => { if (_u == u) killed++; };

            p.HandleEndPhase(Ctx(u));

            Assert.AreEqual(1, killed);
        }
    }
}
