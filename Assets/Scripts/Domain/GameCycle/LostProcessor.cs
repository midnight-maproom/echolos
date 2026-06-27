using System;
using Echolos.Domain.Models;

namespace Echolos.Domain.GameCycle
{
    /// <summary>
    /// ユニットの「完全ロスト（永続的な死亡）」処理を担うクラス。
    ///
    /// 完全ロストが発生するケース：
    ///  1. 通常戦闘でHPが0になった場合（復活スキルがない・使い切った場合）
    ///  2. 撤退時に「しんがり」として選出された場合
    ///
    /// 処理内容：
    ///  - 対象ユニットの State を Dead に設定し、CurrentHP を0にする
    ///  - EquippedGear が存在する場合は CommanderData.EquipmentInventory へ即座に返還する
    ///
    /// ActionExecutor.OnUnitDied イベントへ登録して使用することもできる。
    /// 例：
    ///   var lostProcessor = new LostProcessor();
    ///   actionExecutor.OnUnitDied += (ctx, runtimeUnit) =>
    ///   {
    ///       // アンデッド復活などの処理が済んだ後で呼び出す
    ///       if (!runtimeUnit.IsAlive)
    ///           lostProcessor.ProcessPermanentLost(runtimeUnit.BaseUnit, commander);
    ///   };
    /// </summary>
    public class LostProcessor
    {
        /// <summary>
        /// ユニットの完全ロスト処理を実行する。
        ///
        /// 処理順序：
        ///  1. EquippedGear があれば CommanderData.EquipmentInventory へ返還する
        ///  2. unit.State を UnitState.Dead に設定する
        ///  3. unit.CurrentHP を 0 に設定する（二重呼び出し時も冪等に動作する）
        /// </summary>
        /// <param name="unit">完全ロスト処理の対象ユニット（永続データ）</param>
        /// <param name="commander">装備品の返還先</param>
        /// <exception cref="ArgumentNullException">いずれかの引数がnullの場合</exception>
        public void ProcessPermanentLost(Unit unit, CommanderData commander)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (commander == null) throw new ArgumentNullException(nameof(commander));

            // 装備品をインベントリへ返還（装備が外れた状態になる）
            ReturnEquipmentToInventory(unit, commander);

            // 死亡状態に設定
            unit.State = UnitState.Dead;
            unit.CurrentHP = 0;
        }

        /// <summary>
        /// 装備品のみをインベントリへ返還する（State変更は行わない）。
        /// EquippedGear が null の場合は何もしない。
        /// </summary>
        /// <param name="unit">対象ユニット</param>
        /// <param name="commander">装備品の返還先インベントリ所持者</param>
        /// <exception cref="ArgumentNullException">いずれかの引数がnullの場合</exception>
        public void ReturnEquipmentToInventory(Unit unit, CommanderData commander)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (commander == null) throw new ArgumentNullException(nameof(commander));

            if (unit.EquippedGear == null) return;

            commander.EquipmentInventory.Add(unit.EquippedGear);
            unit.EquippedGear = null;
        }
    }
}
