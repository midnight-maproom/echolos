using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Models;

namespace Echolos.Domain.Skills
{
    /// <summary>
    /// 戦闘中の Waza インスタンス（CD・使用回数・Magnitude 強化済テンプレを保持）。
    /// 不変テンプレ（BaseWaza）と戦闘中状態（CurrentCooldown / CurrentUses）を分離する。
    /// Unit ／ RuntimeUnit の関係と同じパターン。
    ///
    /// 設計：
    /// - BaseWaza の不変フィールドはプロパティ転送（呼び出し側で BaseWaza.X と書かなくて済む）
    /// - AppliedEffects は兵種強化の Magnitude 加算を吸収するため deep copy
    /// - CD は BaseWaza.InitialCooldown で初期化（チャージ用）
    /// </summary>
    public sealed class RuntimeWaza
    {
        public Waza BaseWaza { get; }

        /// <summary>現在のクールダウン残りターン数（戦闘中可変）</summary>
        public int CurrentCooldown { get; set; }

        /// <summary>バトル内使用回数カウント（戦闘中可変）</summary>
        public int CurrentUses { get; set; }

        /// <summary>
        /// 兵種強化の Magnitude 強化を吸収した AppliedEffects の deep copy。
        /// 元の BaseWaza.AppliedEffects（共有参照テンプレ）は変更しないため、
        /// 敵側で複数体同居しても個別強化が利く。
        /// </summary>
        public List<StatusEffect> AppliedEffects { get; set; }

        // BaseWaza の不変フィールドへの転送プロパティ
        public string Id => BaseWaza.Id;
        public string Name => BaseWaza.Name;
        public int SPD => BaseWaza.SPD;
        public int Cooldown => BaseWaza.Cooldown;
        public int InitialCooldown => BaseWaza.InitialCooldown;
        public bool IsForcedWhenReady => BaseWaza.IsForcedWhenReady;
        public int HitCount => BaseWaza.HitCount;
        public int MaxUsesPerBattle => BaseWaza.MaxUsesPerBattle;
        public TargetingType TargetingType => BaseWaza.TargetingType;
        public WazaCategory Category => BaseWaza.Category;
        public bool IsSureHit => BaseWaza.IsSureHit;
        public bool CleansesStatusAilments => BaseWaza.CleansesStatusAilments;

        public Func<RuntimeUnit, RuntimeUnit, int> CalculateBaseDamage => BaseWaza.CalculateBaseDamage;
        public Func<RuntimeUnit, RuntimeUnit, int> CalculateHealAmount => BaseWaza.CalculateHealAmount;
        public Func<RuntimeUnit, bool> TargetingCondition => BaseWaza.TargetingCondition;

        /// <summary>
        /// BaseWaza から戦闘用インスタンスを構築する。
        /// CD は InitialCooldown で初期化、使用回数は 0 リセット、
        /// AppliedEffects は magnitudeBonus 分を加算した deep copy にする。
        /// </summary>
        public RuntimeWaza(Waza baseWaza, int magnitudeBonus = 0)
        {
            BaseWaza = baseWaza ?? throw new ArgumentNullException(nameof(baseWaza));
            CurrentCooldown = baseWaza.InitialCooldown;
            CurrentUses = 0;

            var src = baseWaza.AppliedEffects;
            if (src == null || src.Count == 0)
            {
                AppliedEffects = new List<StatusEffect>();
            }
            else if (magnitudeBonus > 0)
            {
                AppliedEffects = src
                    .Select(e => { var c = e.Clone(); c.Magnitude += magnitudeBonus; return c; })
                    .ToList();
            }
            else
            {
                AppliedEffects = src.Select(e => e.Clone()).ToList();
            }
        }
    }
}
