using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // 戦闘中の Waza インスタンス。CD・使用回数を保持する。
    // 不変テンプレ（BaseWaza）と戦闘中状態（CurrentCooldown / CurrentUses）を分離する。
    // BaseWaza の不変フィールドはプロパティ転送（呼び出し側で BaseWaza.X と書かなくて済む）。
    public sealed class RuntimeWaza
    {
        public Waza BaseWaza { get; }

        /// <summary>現在のクールダウン残りターン数（戦闘中可変）</summary>
        public int CurrentCooldown { get; set; }

        /// <summary>バトル内使用回数カウント（戦闘中可変）</summary>
        public int CurrentUses { get; set; }

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
        public int TargetCount => BaseWaza.TargetCount;
        public TargetSelection TargetSelection => BaseWaza.TargetSelection;
        public Func<RuntimeUnit, bool> TargetingCondition => BaseWaza.TargetingCondition;
        public IList<IActionEffect> Effects => BaseWaza.Effects;

        public RuntimeWaza(Waza baseWaza)
        {
            BaseWaza = baseWaza ?? throw new ArgumentNullException(nameof(baseWaza));
            CurrentCooldown = baseWaza.InitialCooldown;
            CurrentUses = 0;
        }
    }
}
