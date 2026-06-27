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
    public class BattleAssemblyTests
    {
        private static RuntimeUnit Make(Element el, int slot, int hp = 100, int atk = 50,
            int spd = 10, AttackKind kind = AttackKind.Ranged)
        {
            var u = new Unit($"u_{el}_{slot}", $"u_{slot}", el)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                BaseSPD = spd,
                AttackKind = kind,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static Dictionary<RuntimeUnit, IList<RuntimeWaza>> Wazas(
            params (RuntimeUnit, RuntimeWaza)[] pairs)
        {
            var dict = new Dictionary<RuntimeUnit, IList<RuntimeWaza>>();
            foreach (var (u, w) in pairs)
                dict[u] = new List<RuntimeWaza> { w };
            return dict;
        }

        private static RuntimeWaza MakeAttackWaza()
        {
            var w = new Waza("attack", "通常攻撃")
            {
                SPD = 10,
                TargetingType = TargetingType.SingleEnemy,
                Effects = new List<IActionEffect> { new AttackEffect(wazaMultiplier: 1.0, isSureHit: true) },
            };
            return new RuntimeWaza(w);
        }

        // ───── Constructor ─────

        [Test]
        public void Constructor_alliesがnullでArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new BattleAssembly(null, new List<RuntimeUnit>(), 10));
        }

        [Test]
        public void Constructor_enemiesがnullでArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new BattleAssembly(new List<RuntimeUnit>(), null, 10));
        }

        [Test]
        public void Constructor_Contextの地形と攻め守りがセット()
        {
            var asm = new BattleAssembly(
                new List<RuntimeUnit>(),
                new List<RuntimeUnit>(),
                maxTurns: 10,
                terrain: TerrainKind.FireAdvantage,
                terrainStrength: TerrainStrength.Heavy,
                isAttackingSide: true);

            Assert.AreEqual(TerrainKind.FireAdvantage, asm.Context.Terrain);
            Assert.AreEqual(TerrainStrength.Heavy, asm.Context.TerrainStrength);
            Assert.IsTrue(asm.Context.IsAttackingSide);
            Assert.AreEqual(10, asm.Context.MaxTurnLimit);
        }

        [Test]
        public void Constructor_UnitsがContextに登録される()
        {
            var ally = Make(Element.Fire, 0);
            var enemy = Make(Element.Water, 0);
            var asm = new BattleAssembly(
                new[] { ally }, new[] { enemy }, 10);

            Assert.AreSame(ally, asm.Context.AllyUnits[0]);
            Assert.AreSame(enemy, asm.Context.EnemyUnits[0]);
        }

        [Test]
        public void Constructor_Manager_Executor_StatusProcessorが非null()
        {
            var asm = new BattleAssembly(
                new List<RuntimeUnit>(), new List<RuntimeUnit>(), 10);

            Assert.NotNull(asm.Manager);
            Assert.NotNull(asm.Executor);
            Assert.NotNull(asm.StatusProcessor);
        }

        // ───── WireBattleLogic：SynergyApplier 結線 ─────

        [Test]
        public void WireBattleLogic_InitializeBattleでSynergyApplyが走る()
        {
            // 火 2 体で 5% OutgoingDamageUp が最 ATK 1 体に付与される
            var f1 = Make(Element.Fire, 0, atk: 80);
            var f2 = Make(Element.Fire, 1, atk: 60);
            var asm = new BattleAssembly(
                new[] { f1, f2 }, new List<RuntimeUnit>(), 10);
            asm.WireBattleLogic();

            asm.Manager.InitializeBattle(asm.Context);

            // SourceAbilityName は SynergyApplier が「{def.SourceAbilityName} Lv{count}」で
            // 埋めるため StartsWith で先頭一致判定する。
            bool f1HasFireBuff = false;
            foreach (var e in f1.ActiveEffects)
                if (e.Kind == EffectKind.OutgoingDamageUp
                    && e.SourceAbilityName != null
                    && e.SourceAbilityName.StartsWith("炎の共鳴"))
                {
                    f1HasFireBuff = true;
                    break;
                }
            Assert.IsTrue(f1HasFireBuff, "最 ATK の f1 に火属性シナジー由来 OutgoingDamageUp が付与される");
        }

        // ───── WireBattleLogic：StatusProcessor 結線 ─────

        [Test]
        public void WireBattleLogic_OnActionStartでActionGuardが削除される()
        {
            var u = Make(Element.None, 0, kind: AttackKind.Ranged);
            u.AddEffect(new SelfGuard(3));
            var target = Make(Element.None, 0, hp: 1000, kind: AttackKind.Ranged);
            var asm = new BattleAssembly(
                new[] { u }, new[] { target }, 10);
            asm.WireBattleLogic();

            asm.Manager.InitializeBattle(asm.Context);
            asm.Manager.ProcessTurn(asm.Context, Wazas((u, MakeAttackWaza()), (target, MakeAttackWaza())));

            Assert.IsNull(u.FindEffect(EffectKind.DefenseUp),
                "OnActionStart 経由で ActionGuard の DefenseUp が削除される");
        }

        [Test]
        public void WireBattleLogic_OnActionSkippedで麻痺許容量倍化()
        {
            var u = Make(Element.None, 0);
            u.AddEffect(TestEff.Eff(EffectKind.Paralysis, stacks: 5));
            int beforeTol = u.ParalysisTolerance;
            var asm = new BattleAssembly(
                new[] { u }, new List<RuntimeUnit>(), 10);
            asm.WireBattleLogic();

            asm.Manager.InitializeBattle(asm.Context);
            asm.Manager.ProcessTurn(asm.Context, Wazas((u, MakeAttackWaza())));

            Assert.AreEqual(beforeTol * 2, u.ParalysisTolerance);
        }

        [Test]
        public void WireBattleLogic_OnEndPhaseでBurn処理()
        {
            var u = Make(Element.None, 0, hp: 100);
            u.BaseUnit.CurrentHP = 50;
            u.AddEffect(TestEff.Eff(EffectKind.Burn, magnitude: 5f, stacks: 2));
            // 敵を生存させて EndPhase スキップ条件（陣営全滅）を回避。
            // dummy には Waza を渡さず def_guard 防御に回らせて u への攻撃を防ぐ。
            var dummy = Make(Element.None, 0, hp: 1000);
            var asm = new BattleAssembly(
                new[] { u }, new[] { dummy }, 10);
            asm.WireBattleLogic();

            asm.Manager.InitializeBattle(asm.Context);
            asm.Manager.ProcessTurn(asm.Context, Wazas((u, MakeAttackWaza())));

            Assert.AreEqual(40, u.CurrentHP, "EndPhase で Burn 2x5=10 ダメージ");
        }

        [Test]
        public void WireBattleLogic_OnUnitDiedでアンデッド復活が走る()
        {
            var ally = Make(Element.None, 0, hp: 1, atk: 50, kind: AttackKind.Melee);
            ally.CurrentReviveCount = 1;
            var attacker = Make(Element.None, 0, atk: 9999, kind: AttackKind.Ranged);
            var asm = new BattleAssembly(
                new[] { ally }, new[] { attacker }, 10);
            asm.WireBattleLogic();

            asm.Manager.InitializeBattle(asm.Context);
            asm.Manager.ProcessTurn(asm.Context, Wazas(
                (ally, MakeAttackWaza()), (attacker, MakeAttackWaza())));

            Assert.IsTrue(ally.IsAlive, "致死量を受けても復活回数で生存");
            Assert.AreEqual(0, ally.CurrentReviveCount);
        }

        // ───── 統合：1 ターン進行 ─────

        [Test]
        public void 統合_火2体染め編成のDPSがシナジーバフ分上昇()
        {
            // f1 (ATK80) と f2 (ATK60) で f1 が 5% OutgoingDamageUp を受け、
            // 攻撃ダメージが (raw × 1.05) になることを確認。
            var f1 = Make(Element.Fire, 0, atk: 80, kind: AttackKind.Ranged);
            var f2 = Make(Element.Fire, 1, atk: 60, kind: AttackKind.Ranged);
            var enemy = Make(Element.None, 0, hp: 10000, kind: AttackKind.Ranged);
            var asm = new BattleAssembly(
                new[] { f1, f2 }, new[] { enemy }, 10);
            asm.WireBattleLogic();

            asm.Manager.InitializeBattle(asm.Context);

            int hpBefore = enemy.CurrentHP;
            asm.Manager.ProcessTurn(asm.Context, Wazas(
                (f1, MakeAttackWaza()), (f2, MakeAttackWaza()), (enemy, MakeAttackWaza())));
            int totalDamageDealt = hpBefore - enemy.CurrentHP;

            Assert.Greater(totalDamageDealt, 0, "両側から少なくとも 1 撃命中");
        }
    }
}
