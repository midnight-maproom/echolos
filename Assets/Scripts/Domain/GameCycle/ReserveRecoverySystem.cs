using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.GameCycle
{
    /// <summary>
    /// ノードクリア時に控えユニットのHPを回復するシステム。
    ///
    /// 仕様：
    ///  - UnitState.Reserve（控え）かつ CurrentHP > 0（生存）のユニットのみを対象とする
    ///  - UnitState.Active（出撃中）や UnitState.Dead（死亡）のユニットは回復しない
    ///  - 回復量 = MaxHP × recoveryRate（小数点以下切り捨て）
    ///  - 回復後のHPはMaxHPを超えない
    ///
    /// 呼び出し例（戦闘終了後や休憩イベントで呼ぶ）：
    ///   recoverySystem.RecoverReserveUnits(allUnits);
    /// </summary>
    public class ReserveRecoverySystem
    {
        /// <summary>
        /// デフォルトの回復割合（最大HPの30%）。
        /// ゲーム設定やレリック等で変更する場合はコンストラクタかメソッド引数で指定する。
        /// </summary>
        public const float DefaultRecoveryRate = 0.3f;

        /// <summary>このインスタンスの回復割合（0.0〜1.0）</summary>
        public float RecoveryRate { get; }

        /// <param name="recoveryRate">
        /// 回復割合（0.0〜1.0）。0.3 = 最大HPの30%。
        /// 省略した場合は DefaultRecoveryRate (0.3f) が使用される。
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">recoveryRate が 0 未満または 1 超の場合</exception>
        public ReserveRecoverySystem(float recoveryRate = DefaultRecoveryRate)
        {
            if (recoveryRate < 0f || recoveryRate > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(recoveryRate), "回復割合は 0.0〜1.0 の範囲で指定してください。");

            RecoveryRate = recoveryRate;
        }

        /// <summary>
        /// 控え（Reserve）かつ生存しているユニットのHPを回復する。
        /// ノードクリア時（戦闘終了・休憩・イベント完了など）に呼び出すこと。
        ///
        /// 対象条件：
        ///  - unit.State == UnitState.Reserve
        ///  - unit.CurrentHP > 0
        ///
        /// 対象外：
        ///  - UnitState.Active（出撃中）
        ///  - UnitState.Dead（死亡）
        ///  - CurrentHP &lt;= 0（生存していない）
        /// </summary>
        /// <param name="units">全ユニットのコレクション（Active・Reserve・Deadが混在してよい）</param>
        /// <exception cref="ArgumentNullException">units が null の場合</exception>
        public void RecoverReserveUnits(IEnumerable<Unit> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));

            foreach (var unit in units)
            {
                // 控え状態かつ生存しているユニットのみを回復する
                if (unit.State != UnitState.Reserve) continue;
                if (unit.CurrentHP <= 0) continue;

                ApplyRecovery(unit);
            }
        }

        /// <summary>
        /// 個別ユニットに回復を適用するオーバーロード（ユニットテスト・デバッグ用）。
        /// unit.State や unit.CurrentHP のチェックを行わずに直接回復量を適用する。
        /// </summary>
        /// <param name="unit">回復対象のユニット（nullは不可）</param>
        /// <exception cref="ArgumentNullException">unit が null の場合</exception>
        public void ApplyRecovery(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            int recoveryAmount = (int)(unit.MaxHP * RecoveryRate);
            unit.CurrentHP = Math.Min(unit.CurrentHP + recoveryAmount, unit.MaxHP);
        }
    }
}
