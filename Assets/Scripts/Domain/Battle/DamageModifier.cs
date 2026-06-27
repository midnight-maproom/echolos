using System;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 最終ダメージ出力に対する割合補正を適用する純関数。
    // 適用順序：与ダメ% → 被ダメ%（キャップ 80%）→ クリティカル乗算。
    // クリティカルは Phase C で実装。
    public static class DamageModifier
    {
        // 被ダメ% カットの防御側キャップ（80% = 0.80）。どんなに積んでも 20% は通る。
        public const double IncomingDamageCapRate = 0.80;

        // クリティカル時の最終ダメージ倍率（全ユニット共通の基本倍率・仕様 320 §1.4.1）。
        public const double CriticalDamageMultiplier = 1.5;

        public static int ApplyOutgoingMultiplier(int rawDamage, RuntimeUnit attacker)
        {
            if (rawDamage <= 0) return 0;
            if (attacker == null) return rawDamage;
            double bonusRate = GetOutgoingBonusRate(attacker);
            double modified = rawDamage * (1.0 + bonusRate);
            if (modified < 0) modified = 0;
            return (int)Math.Round(modified, MidpointRounding.AwayFromZero);
        }

        // 攻撃側の OutgoingDamageUp 合計を 0〜の小数レートで返す（加算スタック）。
        // Magnitude（% 値）× Stacks の総和 / 100。
        public static double GetOutgoingBonusRate(RuntimeUnit attacker)
        {
            if (attacker == null) return 0.0;
            double total = 0.0;
            foreach (var e in attacker.ActiveEffects)
            {
                if (e is OutgoingDamageModifier mod)
                    total += (mod.IsBuff ? mod.Magnitude : -mod.Magnitude) * mod.Stacks;
            }
            return total / 100.0;
        }

        // 被ダメ% カットを適用する。Magnitude（% 値）× Stacks の総和を加算スタックで合計し、
        // キャップ 80% を上限とする。被弾者 null や効果なしは入力ダメージをそのまま返す。
        public static int ApplyIncomingMultiplier(int damage, RuntimeUnit defender)
        {
            if (damage <= 0) return 0;
            if (defender == null) return damage;
            double cutRate = GetIncomingCutRate(defender);
            double modified = damage * (1.0 - cutRate);
            if (modified < 0) modified = 0;
            return (int)Math.Round(modified, MidpointRounding.AwayFromZero);
        }

        // 被弾側の IncomingDamageDown 合計レートを 0〜キャップの小数で返す（加算スタック）。
        public static double GetIncomingCutRate(RuntimeUnit defender)
        {
            if (defender == null) return 0.0;
            double total = 0.0;
            foreach (var e in defender.ActiveEffects)
            {
                if (e is IncomingDamageModifier mod)
                    total += (mod.IsBuff ? mod.Magnitude : -mod.Magnitude) * mod.Stacks;
            }
            double rate = total / 100.0;
            if (rate < 0.0) rate = 0.0;
            if (rate > IncomingDamageCapRate) rate = IncomingDamageCapRate;
            return rate;
        }

        // クリティカル判定とダメージ適用。攻撃側の CriticalRateUp 合計 % を確率として Random0To99 と
        // 比較し、当選なら damage × CriticalDamageMultiplier を返す。返り値の bool が isCritical。
        // 基本クリ率は 0%（全ユニット共通）。CriticalRateUp が無ければ常に非クリで素通し。
        public static (int damage, bool isCritical) ApplyCritical(
            int damage, RuntimeUnit attacker, System.Func<int> random0To99)
        {
            if (damage <= 0) return (damage, false);
            if (attacker == null || random0To99 == null) return (damage, false);

            int critRate = GetCriticalRatePercent(attacker);
            if (critRate <= 0) return (damage, false);

            bool isCrit = critRate >= 100 || random0To99() < critRate;
            if (!isCrit) return (damage, false);

            double crit = damage * CriticalDamageMultiplier;
            if (crit < 0) crit = 0;
            int rounded = (int)Math.Round(crit, MidpointRounding.AwayFromZero);
            return (rounded, true);
        }

        // 攻撃側の CriticalRateUp 合計 %（Magnitude × Stacks の総和）。
        public static int GetCriticalRatePercent(RuntimeUnit attacker)
        {
            if (attacker == null) return 0;
            int total = 0;
            foreach (var e in attacker.ActiveEffects)
            {
                if (e is CriticalRateModifier mod)
                    total += (int)(mod.IsBuff ? mod.Magnitude : -mod.Magnitude) * mod.Stacks;
            }
            return total < 0 ? 0 : total;
        }

        // 反撃時のみ適用される最終出力割合補正（CounterDamageUp）。炎の大盾兵のパッシブ等。
        // 通常の与ダメ%（OutgoingDamageUp）とは独立に加算スタックされる。
        public static int ApplyCounterMultiplier(int rawDamage, RuntimeUnit attacker)
        {
            if (rawDamage <= 0) return 0;
            if (attacker == null) return rawDamage;
            double bonusRate = GetCounterBonusRate(attacker);
            double modified = rawDamage * (1.0 + bonusRate);
            if (modified < 0) modified = 0;
            return (int)Math.Round(modified, MidpointRounding.AwayFromZero);
        }

        public static double GetCounterBonusRate(RuntimeUnit attacker)
        {
            if (attacker == null) return 0.0;
            double total = 0.0;
            foreach (var e in attacker.ActiveEffects)
            {
                if (e is CounterDamageModifier mod)
                    total += (mod.IsBuff ? mod.Magnitude : -mod.Magnitude) * mod.Stacks;
            }
            return total / 100.0;
        }
    }
}
