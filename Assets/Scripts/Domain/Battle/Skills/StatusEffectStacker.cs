using System;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // IEffect テンプレを対象へ付与する純関数（スタック処理＋ ImmunityKinds チェック）。
    // 同 Kind かつ同 SourceAbilityName の既存効果があれば MaxStacks までスタック加算＋強度・残ターンを
    // リフレッシュ。SourceAbilityName が異なる場合は別効果として共存（連携 +3 と鼓舞 +6 を両方乗せる等）。
    // なければテンプレを Clone して MaxStacks でクランプした初期 Stacks で追加。
    // Unit.ImmunityKinds に template.Kind が含まれていれば付与を弾く。
    public static class StatusEffectStacker
    {
        // 付与結果：true なら適用された（スタック追加 or 新規）／false ならスキップされた（無効耐性等）
        public static bool ApplyWithStacking(RuntimeUnit target, IEffect template)
        {
            if (target == null || template == null) return false;

            // ImmunityKinds による弾き
            if (target.BaseUnit.ImmunityKinds != null
                && target.BaseUnit.ImmunityKinds.Contains(template.Kind))
            {
                return false;
            }

            int cap = template.MaxStacks > 0 ? template.MaxStacks : 1;

            // 同 Source なら統合（重ね掛けで強化）、別 Source なら別効果として共存。
            // 「連携 +3 / 永続」と「鼓舞 +6 / 残 3T」を両方乗せたい等のシナリオに対応。
            var existing = target.FindEffect(e =>
                e.Kind == template.Kind
                && SameSource(e.SourceAbilityName, template.SourceAbilityName));
            if (existing != null)
            {
                existing.Stacks = Math.Min(existing.Stacks + template.Stacks, cap);
                existing.RemainingTurns = template.RemainingTurns;
                existing.MaxStacks = template.MaxStacks;
                RefreshTypeSpecificFields(existing, template);
                return true;
            }

            var clone = template.Clone();
            if (clone.Stacks > cap) clone.Stacks = cap;
            target.AddEffect(clone);
            return true;
        }

        // SourceAbilityName の同値判定。null と空文字は同一視（無名 Source 同士はマージする）。
        private static bool SameSource(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;
            return a == b;
        }

        // Magnitude 等の派生クラス固有フィールドを既存効果へリフレッシュする。
        // 共通 IEffect には Magnitude プロパティがないため、pattern match で個別に処理する。
        private static void RefreshTypeSpecificFields(IEffect existing, IEffect template)
        {
            switch (existing)
            {
                case AbilityModifier ex when template is AbilityModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case EvasionModifier ex when template is EvasionModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case OutgoingDamageModifier ex when template is OutgoingDamageModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case IncomingDamageModifier ex when template is IncomingDamageModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case CriticalRateModifier ex when template is CriticalRateModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case CounterDamageModifier ex when template is CounterDamageModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case HealReceivedModifier ex when template is HealReceivedModifier tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case ContinuousDot ex when template is ContinuousDot tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case ContinuousHot ex when template is ContinuousHot tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                case SelfGuard ex when template is SelfGuard tp:
                    ex.Magnitude = tp.Magnitude;
                    break;
                // Freeze/Paralysis/Curse/Shield/Flag 系は Magnitude を持たない
            }
        }
    }
}
