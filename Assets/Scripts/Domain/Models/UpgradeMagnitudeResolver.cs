using Echolos.Domain.Effects;

namespace Echolos.Domain.Models
{
    /// <summary>
    /// Unit.AppliedUpgrades から特定の Upgrade 合計値を取り出す純関数群。
    /// HealEffect / ApplyStatusEffectEffect / Bootstrap.PrepareForBattle から呼ばれる。
    /// </summary>
    public static class UpgradeMagnitudeResolver
    {
        /// <summary>
        /// WazaPowerBoost のうち、TargetWazaId が指定 wazaId と一致するものの Magnitude 合計。
        /// wazaId が null/空のときは 0 を返す（適用条件なしと扱う）。
        /// </summary>
        public static int SumWazaPowerBoost(Unit unit, string wazaId)
        {
            if (unit?.AppliedUpgrades == null) return 0;
            if (string.IsNullOrEmpty(wazaId)) return 0;
            int sum = 0;
            foreach (var up in unit.AppliedUpgrades)
            {
                if (up == null) continue;
                if (up.Kind != UpgradeKind.WazaPowerBoost) continue;
                if (up.TargetWazaId != wazaId) continue;
                sum += up.Magnitude;
            }
            return sum;
        }

        /// <summary>
        /// PersistentEffectBoost のうち、TargetEffectKind ＋ TargetSourceAbilityName が一致するものの Magnitude 合計。
        /// SourceAbilityName が null/空でも一致判定する（null 同士／空文字同士は同一視）。
        /// </summary>
        public static int SumPersistentEffectBoost(Unit unit, EffectKind kind, string sourceAbilityName)
        {
            if (unit?.AppliedUpgrades == null) return 0;
            int sum = 0;
            foreach (var up in unit.AppliedUpgrades)
            {
                if (up == null) continue;
                if (up.Kind != UpgradeKind.PersistentEffectBoost) continue;
                if (up.TargetEffectKind != kind) continue;
                if (!IsSameSource(up.TargetSourceAbilityName, sourceAbilityName)) continue;
                sum += up.Magnitude;
            }
            return sum;
        }

        private static bool IsSameSource(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;
            return a == b;
        }
    }
}
