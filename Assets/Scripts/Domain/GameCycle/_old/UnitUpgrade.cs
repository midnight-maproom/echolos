using System;

namespace Echolos.Domain.Models
{
    /// <summary>
    /// ユニットのレベルアップ時に選択できる強化オプション。
    /// 各ユニットは固有の4択を持ち、Lv2〜4で1つずつ選択して適用する。
    /// Lv5到達時はマスターボーナス（IsMasteryBonus=true）も自動適用される。
    /// </summary>
    public class UnitUpgrade
    {
        public string UpgradeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>強化の種別（ステータス上昇・スキル追加・技強化・マスターボーナス）</summary>
        public UpgradeType UpgradeType { get; set; }

        /// <summary>
        /// Lv5到達時に自動適用されるマスターボーナスかどうか。
        /// trueのものは通常の選択肢には出ず、Lv5で強制付与される。
        /// </summary>
        public bool IsMasteryBonus { get; set; }

        /// <summary>
        /// 強化を適用する処理。
        /// 対象のUnitに対してステータス上昇・スキル追加などを直接実行する。
        /// </summary>
        public Action<Unit> ApplyEffect { get; set; }

        public UnitUpgrade(
            string upgradeId,
            string name,
            string description,
            UpgradeType upgradeType,
            bool isMasteryBonus = false)
        {
            UpgradeId = upgradeId;
            Name = name;
            Description = description;
            UpgradeType = upgradeType;
            IsMasteryBonus = isMasteryBonus;
        }
    }
}
