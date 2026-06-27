using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class BattleContextTests
    {
        private static RuntimeUnit MakeAlive(int slot)
        {
            var u = new Unit($"u_{slot}", $"u_{slot}", Element.None)
            {
                MaxHP = 100,
                CurrentHP = 100,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static BattleContext MakeContext(int allyCount, int enemyCount, bool isAttacking)
        {
            var ctx = new BattleContext(10)
            {
                InitialAllyCount = allyCount,
                InitialEnemyCount = enemyCount,
                IsAttackingSide = isAttacking,
            };
            for (int i = 0; i < allyCount; i++) ctx.AllyUnits.Add(MakeAlive(i));
            for (int i = 0; i < enemyCount; i++) ctx.EnemyUnits.Add(MakeAlive(i));
            return ctx;
        }

        // ───── プロパティのデフォルト値 ─────

        [Test]
        public void IsAttackingSide_既定はfalse_守り側()
        {
            var ctx = new BattleContext(10);
            Assert.IsFalse(ctx.IsAttackingSide);
        }

        [Test]
        public void Terrain_既定はNeutral()
        {
            var ctx = new BattleContext(10);
            Assert.AreEqual(TerrainKind.Neutral, ctx.Terrain);
        }

        [Test]
        public void TerrainStrength_既定はLight()
        {
            var ctx = new BattleContext(10);
            Assert.AreEqual(TerrainStrength.Light, ctx.TerrainStrength);
        }

        // ───── 攻め側評価（攻め=1撃破で勝利） ─────

        [Test]
        public void 攻め側_1体撃破で優勢勝利()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: true);
            ctx.EnemyUnits[0].BaseUnit.CurrentHP = 0;
            Assert.IsTrue(ctx.IsAdvantageousVictoryCondition);
        }

        [Test]
        public void 攻め側_0体撃破は負け_被撃破有無問わず()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: true);
            Assert.IsFalse(ctx.IsAdvantageousVictoryCondition, "0/0 で負け");

            ctx.AllyUnits[0].BaseUnit.CurrentHP = 0;
            Assert.IsFalse(ctx.IsAdvantageousVictoryCondition, "被撃破ありでも 0 撃破は負け");
        }

        [Test]
        public void 攻め側_複数撃破で勝利()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: true);
            ctx.EnemyUnits[0].BaseUnit.CurrentHP = 0;
            ctx.EnemyUnits[1].BaseUnit.CurrentHP = 0;
            ctx.AllyUnits[0].BaseUnit.CurrentHP = 0;
            ctx.AllyUnits[1].BaseUnit.CurrentHP = 0;
            Assert.IsTrue(ctx.IsAdvantageousVictoryCondition, "2撃破/2被撃破でも攻め側勝利");
        }

        // ───── 守り側評価（守り=0被撃破で勝利） ─────

        [Test]
        public void 守り側_0体被撃破で優勢勝利()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: false);
            Assert.IsTrue(ctx.IsAdvantageousVictoryCondition);
        }

        [Test]
        public void 守り側_撃破ありでも0被撃破なら勝利()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: false);
            ctx.EnemyUnits[0].BaseUnit.CurrentHP = 0;
            ctx.EnemyUnits[1].BaseUnit.CurrentHP = 0;
            Assert.IsTrue(ctx.IsAdvantageousVictoryCondition);
        }

        [Test]
        public void 守り側_1体被撃破で負け()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: false);
            ctx.AllyUnits[0].BaseUnit.CurrentHP = 0;
            Assert.IsFalse(ctx.IsAdvantageousVictoryCondition);
        }

        // ───── 集計の整合 ─────

        [Test]
        public void AllyKillCount_AllyDeathCount_集計と整合()
        {
            var ctx = MakeContext(allyCount: 3, enemyCount: 3, isAttacking: true);
            ctx.EnemyUnits[0].BaseUnit.CurrentHP = 0;
            ctx.AllyUnits[0].BaseUnit.CurrentHP = 0;
            ctx.AllyUnits[1].BaseUnit.CurrentHP = 0;
            Assert.AreEqual(1, ctx.AllyKillCount);
            Assert.AreEqual(2, ctx.AllyDeathCount);
        }
    }
}
