// 内政コマンド（召集・ユニット個別 Lv 強化）の実行サービス。
//
// 【設計方針】
// - 純Core・MonoBehaviour非依存。
// - Roster（実際の王国軍リスト）と DraftOffer は呼び出し側（Bootstrap）が保持し、
//   本サービスは「Can / Execute」判定と「State / Unit の更新」だけを担う。
// - ユニット強化は個別ユニット単位（旧「兵種＝同種全部に効く」から変更）。
//   選択された UnitUpgrade を AppliedUpgrades に追加し Unit.Level を +1 する。
//   固有ユニット（王女・ブリジット）は内政画面からは強化できない（メタ強化画面に動線）。
using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロト 内政コマンド実行サービス。</summary>
    public sealed class VSPrototypeInteriorService
    {
        // 召集（Conscript）

        /// <summary>召集を実行可能か（行動力残＋同一未実行）。</summary>
        public bool CanConscript(VSPrototypeInteriorState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.CanExecuteAction(VSPrototypeInteriorAction.Conscript);
        }

        /// <summary>
        /// 召集を実行する。引数 picked は DraftService.AcceptPick で取得したユニット。
        /// roster へ追加し、行動力消費＋同一実行マークを行う。
        /// </summary>
        public bool ExecuteConscript(VSPrototypeInteriorState state, IList<Unit> roster, Unit picked)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (picked == null) return false;
            if (!CanConscript(state)) return false;

            if (!state.MarkActionExecuted(VSPrototypeInteriorAction.Conscript)) return false;
            roster.Add(picked);
            return true;
        }

        // ユニット個別 Lv 強化（UpgradeUnit）

        /// <summary>
        /// 指定ユニットを Lv 強化可能か（行動力残＋Lv 上限未満＋AvailableUpgrades が残っている＋固有ユニットでない）。
        /// 召集と違い、同一ラウンド複数回の実行を許容する（行動力の続く限り）。
        /// 固有ユニットはメタ強化画面で扱うため、内政画面からは強化不可。
        /// </summary>
        public bool CanUpgradeUnit(VSPrototypeInteriorState state, Unit unit)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (unit == null) return false;
            if (IsUniqueUnit(unit.Id)) return false;
            if (state.ActionPoints < VSPrototypeInteriorState.ActionCost) return false;
            if (unit.Level >= VSPrototypeInteriorState.MaxUnitLevel) return false;
            if (unit.AvailableUpgrades == null || unit.AvailableUpgrades.Count == 0) return false;
            return true;
        }

        /// <summary>固有ユニット（兵種強化対象外）判定。GUI 側でもフィルタに使用可。</summary>
        public static bool IsUniqueUnit(string unitTypeId)
        {
            return unitTypeId == UniqueUnitIds.Princess
                || unitTypeId == UniqueUnitIds.Bridget;
        }

        /// <summary>
        /// 個別ユニットに選択された強化を適用する。
        /// AvailableUpgrades から該当 Upgrade を取り除き AppliedUpgrades に移し、Unit.Level を +1。
        /// </summary>
        public bool ExecuteUpgradeUnit(VSPrototypeInteriorState state, Unit unit, UnitUpgrade selectedUpgrade)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (unit == null || selectedUpgrade == null) return false;
            if (!CanUpgradeUnit(state, unit)) return false;
            if (!unit.AvailableUpgrades.Contains(selectedUpgrade)) return false;

            if (!state.TryConsumeActionPoint()) return false;
            unit.AvailableUpgrades.Remove(selectedUpgrade);
            unit.AppliedUpgrades.Add(selectedUpgrade);
            unit.Level++;
            return true;
        }
    }
}
