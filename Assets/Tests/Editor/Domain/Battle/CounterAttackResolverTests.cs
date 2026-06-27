// 反撃発動判定の単体テスト。
//
// 発動条件：攻撃側＆被弾側ともに AttackKind=Melee で双方生存
// シールド吸収でも発動（HP 減らず生存判定で生きている＝反撃する）
// 被弾側が攻撃で死亡したら反撃なし／攻撃側が他要因で死んでいたら反撃なし
// 多段攻撃の各 hit 反撃と「死亡したら以降の hit 打ち止め」は呼び出し側責務
// 被弾側に SilencedCounter（水の大盾兵の「専守」等）パッシブがあれば反撃なし
using NUnit.Framework;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class CounterAttackResolverTests
    {
        private static RuntimeUnit MakeRu(AttackKind kind, int hp = 100)
        {
            var u = new Unit("u_" + kind, kind.ToString())
            {
                MaxHP = hp,
                CurrentHP = hp,
                AttackKind = kind,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, 0);
        }

        private static void Kill(RuntimeUnit ru) { ru.BaseUnit.CurrentHP = 0; }

        // ── 発動条件（攻撃側＆被弾側両方 Melee） ──

        [Test]
        public void 攻撃側Melee_被弾側Melee_双方生存で反撃発動()
        {
            var atk = MakeRu(AttackKind.Melee);
            var def = MakeRu(AttackKind.Melee);
            Assert.IsTrue(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        [Test]
        public void 攻撃側Ranged_被弾側Meleeでも反撃しない_遠隔は反撃を受けない設計()
        {
            var atk = MakeRu(AttackKind.Ranged);
            var def = MakeRu(AttackKind.Melee);
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        [Test]
        public void 攻撃側Melee_被弾側Rangedは反撃しない_発動者条件()
        {
            var atk = MakeRu(AttackKind.Melee);
            var def = MakeRu(AttackKind.Ranged);
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        [Test]
        public void 攻撃側Ranged_被弾側Rangedも反撃しない()
        {
            var atk = MakeRu(AttackKind.Ranged);
            var def = MakeRu(AttackKind.Ranged);
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        // ── 死亡判定 ──

        [Test]
        public void 被弾側が攻撃で死亡したら反撃しない()
        {
            var atk = MakeRu(AttackKind.Melee);
            var def = MakeRu(AttackKind.Melee);
            Kill(def);
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        [Test]
        public void 攻撃側が他要因で死んでいたら反撃しない()
        {
            var atk = MakeRu(AttackKind.Melee);
            var def = MakeRu(AttackKind.Melee);
            Kill(atk);
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        // ── 攻撃側 IgnoreCounter（ブリジット「王家のペンダント」） ──

        [Test]
        public void 攻撃側にIgnoreCounter_反撃しない()
        {
            var atk = MakeRu(AttackKind.Melee);
            var def = MakeRu(AttackKind.Melee);
            atk.AddEffect(new IgnoreCounterFlag());
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        [Test]
        public void 被弾側にIgnoreCounterがあっても無関係_攻撃側基準()
        {
            // IgnoreCounter は攻撃側基準フラグ。被弾側に乗っていても発動には影響しない。
            var atk = MakeRu(AttackKind.Melee);
            var def = MakeRu(AttackKind.Melee);
            def.AddEffect(new IgnoreCounterFlag());
            Assert.IsTrue(CounterAttackResolver.CanCounterAttack(atk, def));
        }

        // ── null 安全 ──

        [Test]
        public void null入力は反撃発動しない()
        {
            var ru = MakeRu(AttackKind.Melee);
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(null, ru));
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(ru, null));
            Assert.IsFalse(CounterAttackResolver.CanCounterAttack(null, null));
        }
    }
}
