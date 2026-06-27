using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 反撃発動判定の純関数。
    // 発動条件: 攻撃側＆被弾側ともに AttackKind=Melee で、攻撃／被弾を経た時点で双方生存。
    // シールド吸収は HP が減らないため defender.IsAlive で自動成立（攻撃された事実で反撃する）。
    // 多段攻撃の各 hit ごと判定と、反撃で攻撃側が死亡したら以降の hit 打ち止めは
    // 呼び出し側ループの責務（攻撃側 IsAlive を hit ループで確認）。
    public static class CounterAttackResolver
    {
        public static bool CanCounterAttack(RuntimeUnit attacker, RuntimeUnit defender)
        {
            if (attacker == null || defender == null) return false;
            if (!attacker.IsAlive || !defender.IsAlive) return false;
            if (attacker.BaseUnit.AttackKind != AttackKind.Melee) return false;
            if (defender.BaseUnit.AttackKind != AttackKind.Melee) return false;
            if (HasSilencedCounter(defender)) return false;
            // 攻撃側に IgnoreCounter（ブリジット「王家のペンダント」等）があれば、被弾側からの反撃を発動させない。
            if (HasIgnoreCounter(attacker)) return false;
            return true;
        }

        // 被弾側に SilencedCounter パッシブ（水の大盾兵の「専守」等）があれば反撃しない。
        // 反撃禁止フラグなので Magnitude / Stacks は参照せず、効果の存在のみで判定する。
        private static bool HasSilencedCounter(RuntimeUnit unit)
        {
            foreach (var e in unit.ActiveEffects)
            {
                if (e is SilencedCounterFlag) return true;
            }
            return false;
        }

        // 攻撃側に IgnoreCounter パッシブ（ブリジット「王家のペンダント」）があれば、相手の反撃を発動させない。
        private static bool HasIgnoreCounter(RuntimeUnit unit)
        {
            foreach (var e in unit.ActiveEffects)
            {
                if (e is IgnoreCounterFlag) return true;
            }
            return false;
        }
    }
}
