using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // シールド消費判定の純関数。被弾時に対象の Shield Stacks が 1 以上あれば
    // 1 スタック消費してダメージを 0 に吸収する。Stacks=0 になった効果は剥奪。
    // 反撃発動は呼び出し元責務（Shield 吸収でも「攻撃された事実」は変わらない）。
    public static class ShieldConsumer
    {
        public readonly struct Result
        {
            public readonly int FinalDamage;
            public readonly bool ShieldConsumed;
            public Result(int finalDamage, bool shieldConsumed)
            {
                FinalDamage = finalDamage;
                ShieldConsumed = shieldConsumed;
            }
        }

        public static Result Consume(int incomingDamage, RuntimeUnit defender)
        {
            if (defender == null) return new Result(incomingDamage, false);
            if (incomingDamage <= 0) return new Result(0, false);
            if (defender.ShieldStacks <= 0) return new Result(incomingDamage, false);

            ShieldEffect target = null;
            foreach (var e in defender.ActiveEffects)
            {
                if (e is ShieldEffect shield && shield.Stacks > 0)
                {
                    target = shield;
                    break;
                }
            }
            if (target == null) return new Result(incomingDamage, false);

            target.Stacks -= 1;
            if (target.Stacks <= 0) defender.RemoveEffect(target);
            return new Result(0, true);
        }
    }
}
