using System;
using Echolos.Domain.Models;

namespace Echolos.Domain.Items
{
    /// <summary>
    /// 装備品（エンチャント）。各ユニットに1つまで装備できる。
    /// 新しい技の追加・既存技の上書き・ステータス補正などの効果を持つ。
    /// ユニットがロスト（完全死亡）した場合、装備品はCommanderDataのインベントリに返還される。
    /// </summary>
    public class Equipment
    {
        public string EquipmentId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// 装備時にユニットへ適用する効果。
        /// 技の追加・ステータス補正などを対象Unitに直接適用する。
        /// </summary>
        public Action<Unit> OnEquip { get; set; }

        /// <summary>
        /// 取り外し時にユニットへの効果を取り消す処理。
        /// OnEquipで行った変更を元に戻す。
        /// </summary>
        public Action<Unit> OnUnequip { get; set; }

        public Equipment(string equipmentId, string name, string description = "")
        {
            EquipmentId = equipmentId;
            Name = name;
            Description = description;
        }
    }
}
