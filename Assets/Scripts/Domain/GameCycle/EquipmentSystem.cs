using System;
using Echolos.Domain.Items;
using Echolos.Domain.Models;

namespace Echolos.Domain.GameCycle
{
    /// <summary>
    /// 非戦闘時の装備品（エンチャント）着脱を管理するシステム。
    ///
    /// 仕様：
    ///  - 各ユニットが装備できるのは1つまで
    ///  - 既に装備している場合は先に外してインベントリへ戻してから新しい装備を付ける
    ///  - 装備時に Equipment.OnEquip(unit) を呼び出してステータス・技を反映する
    ///  - 取り外し時に Equipment.OnUnequip(unit) を呼び出してステータス・技を元に戻す
    ///  - 着脱は戦闘外でのみ行える前提（呼び出し元の責務で保証する）
    ///
    /// インベントリ上限への備え（処理順序の設計意図）：
    ///  Equip内では「新装備をインベントリから取り出す」を先に行い、
    ///  その後「旧装備をインベントリへ戻す」順序とする。
    ///  これによりインベントリに所持上限が設けられた場合でも、
    ///  先に枠を空けてから戻すことで一時的な上限超過が発生しない。
    /// </summary>
    public class EquipmentSystem
    {
        /// <summary>
        /// ユニットに装備品を装着する。
        ///
        /// 処理順序（インベントリ上限安全設計）：
        ///  1. 同一装備の再装備ガード（何もしない）
        ///  2. commander.EquipmentInventory から新装備を取り出す（枠を空ける）
        ///  3. 旧装備があれば OnUnequip を呼んでインベントリへ戻す
        ///  4. unit.EquippedGear に新装備を設定し、OnEquip を呼び出す
        ///
        /// 注意：equipment が EquipmentInventory に存在しない場合も装備できる
        ///       （ショップで直接渡すケースなど）。Remove は存在しない場合も安全に無視する。
        /// </summary>
        /// <param name="unit">装備を付けるユニット</param>
        /// <param name="equipment">装着する装備品</param>
        /// <param name="commander">装備インベントリの所持者（着脱時の返却先）</param>
        /// <exception cref="ArgumentNullException">いずれかの引数がnullの場合</exception>
        public void Equip(Unit unit, Equipment equipment, CommanderData commander)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (commander == null) throw new ArgumentNullException(nameof(commander));

            // 同じ装備を再装備しようとした場合は何もしない
            if (unit.EquippedGear == equipment) return;

            // ①新装備をインベントリから取り出す（枠を先に空ける）
            commander.EquipmentInventory.Remove(equipment);

            // ②旧装備を外してインベントリへ戻す
            if (unit.EquippedGear != null)
            {
                UnequipInternal(unit, commander);
            }

            // ③ユニットに装備してステータス・技を反映
            unit.EquippedGear = equipment;
            equipment.OnEquip?.Invoke(unit);
        }

        /// <summary>
        /// ユニットから装備品を取り外し、CommanderData.EquipmentInventoryへ返す。
        /// EquippedGearがnullの場合は何もしない。
        /// </summary>
        /// <param name="unit">装備を外すユニット</param>
        /// <param name="commander">装備の返却先（インベントリ所持者）</param>
        /// <exception cref="ArgumentNullException">いずれかの引数がnullの場合</exception>
        public void Unequip(Unit unit, CommanderData commander)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (commander == null) throw new ArgumentNullException(nameof(commander));

            if (unit.EquippedGear == null) return;

            UnequipInternal(unit, commander);
        }

        /// <summary>
        /// 実際の取り外し処理（null チェック済みの前提で呼ぶ）。
        /// OnUnequipを呼んでステータス・技を元に戻し、EquippedGearをnullにしてインベントリへ追加する。
        /// </summary>
        private static void UnequipInternal(Unit unit, CommanderData commander)
        {
            var gear = unit.EquippedGear;
            gear.OnUnequip?.Invoke(unit);
            unit.EquippedGear = null;
            commander.EquipmentInventory.Add(gear);
        }
    }
}
