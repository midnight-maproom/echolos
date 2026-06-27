using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Models;

namespace Echolos.Domain.GameCycle
{
    /// <summary>
    /// ユニットの成長システム（EXP消費・レベルアップ・強化オプション選択）。
    /// 非戦闘時に呼び出され、CommanderData.TotalExpItemsを消費してUnit.CurrentExpを加算する。
    ///
    /// レベルアップ仕様：
    ///  Lv1→2: EXP 1個、Lv2→3: 2個、Lv3→4: 3個、Lv4→5: 4個
    ///  Lv2〜4: 強化選択コールバックを先に呼び、nullを返した場合はそこでループを中断する
    ///          （EXP消費・レベルアップはコールバック確定後にのみ行う）
    ///  Lv5到達: 残り全選択肢とIsMasteryBonus=trueの強化を自動適用し、余剰EXPを0にクリアする
    /// </summary>
    public class GrowthSystem
    {
        /// <summary>
        /// 各レベルアップに必要なEXPアイテム数。
        /// インデックス0 = Lv1→2（1個）、1 = Lv2→3（2個）、2 = Lv3→4（3個）、3 = Lv4→5（4個）。
        /// </summary>
        public static readonly int[] RequiredExpPerLevel = { 2, 3, 4, 5 };

        /// <summary>ユニットの最大レベル</summary>
        public const int MaxLevel = 5;

        /// <summary>
        /// Lv2〜4のレベルアップ時に呼ばれる強化選択コールバック。
        /// 引数1: 強化対象のUnit
        /// 引数2: 選択可能な強化オプションのリスト（非マスターボーナスのみ）
        /// 戻り値: 選択されたUnitUpgrade。nullを返すとレベルアップ処理が中断される（保留/キャンセル）。
        /// </summary>
        private readonly Func<Unit, List<UnitUpgrade>, UnitUpgrade> _upgradeSelector;

        /// <param name="upgradeSelector">
        /// レベルアップ時の強化選択コールバック。
        /// nullを返すとレベルアップが保留（EXP消費もキャンセル）される。
        /// 引数をnullにした場合は先頭の選択肢を自動選択するデフォルト実装が使われる。
        /// </param>
        public GrowthSystem(Func<Unit, List<UnitUpgrade>, UnitUpgrade> upgradeSelector = null)
        {
            // nullの場合は先頭の選択肢を自動選択するデフォルト実装
            _upgradeSelector = upgradeSelector
                ?? ((unit, options) => options.Count > 0 ? options[0] : null);
        }

        /// <summary>
        /// EXPアイテムを消費してユニットのEXPを加算し、レベルアップを処理する。
        /// 消費は「commander.TotalExpItems」と「expItemsToConsume」の小さい方に留まる。
        ///
        /// 呼び出し例（10個消費してユニットを強化）：
        ///   growthSystem.AddExp(unit, commander, 10);
        /// </summary>
        /// <param name="unit">EXPを加算する対象ユニット</param>
        /// <param name="commander">EXPアイテムの供給元指揮官データ</param>
        /// <param name="expItemsToConsume">消費しようとするEXPアイテム数</param>
        /// <exception cref="ArgumentNullException">unit または commander が null の場合</exception>
        /// <exception cref="ArgumentOutOfRangeException">expItemsToConsume が負の場合</exception>
        public void AddExp(Unit unit, CommanderData commander, int expItemsToConsume)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (commander == null) throw new ArgumentNullException(nameof(commander));
            if (expItemsToConsume < 0) throw new ArgumentOutOfRangeException(
                nameof(expItemsToConsume), "消費EXP数は0以上でなければなりません。");

            // すでに最大レベルなら何もしない
            if (unit.CurrentLevel >= MaxLevel) return;

            // 所持数を超えては消費できない
            int actualConsumed = Math.Min(expItemsToConsume, commander.TotalExpItems);
            commander.TotalExpItems -= actualConsumed;
            unit.CurrentExp += actualConsumed;

            // EXPが溜まっている間レベルアップを繰り返す
            ProcessLevelUps(unit);
        }

        /// <summary>
        /// ユニットが次のレベルアップに必要なEXPアイテム数を返す。
        /// すでに最大レベルの場合は0を返す。
        /// </summary>
        public static int GetRequiredExpForNextLevel(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (unit.CurrentLevel >= MaxLevel) return 0;

            // インデックス: CurrentLevel - 1 (Lv1=0, Lv2=1, ...)
            return RequiredExpPerLevel[unit.CurrentLevel - 1];
        }

        /// <summary>
        /// CurrentExpが閾値を超えている間、レベルアップを繰り返す。
        ///
        /// Lv2〜4の場合は強化選択を先に行い、nullが返った時点でループを中断する。
        /// EXPの消費とレベルインクリメントは選択確定後にのみ実行する。
        ///
        /// Lv5（MaxLevel）到達時は全強化肢を自動適用し、余剰EXPを0にクリアしてループを終了する。
        /// </summary>
        private void ProcessLevelUps(Unit unit)
        {
            while (unit.CurrentLevel < MaxLevel)
            {
                int required = RequiredExpPerLevel[unit.CurrentLevel - 1];
                if (unit.CurrentExp < required) break;

                int nextLevel = unit.CurrentLevel + 1;

                if (nextLevel >= MaxLevel)
                {
                    // Lv5到達: 自動適用フロー
                    // 選択の余地なく確定なので、先にEXP消費・レベルアップして全強化肢を適用する。
                    unit.CurrentExp -= required;
                    unit.CurrentLevel++;
                    ApplyAllRemainingAndMastery(unit);

                    // 余剰EXPを切り捨て（Lv5で累積しても意味がないため）
                    unit.CurrentExp = 0;
                    break;
                }
                else
                {
                    // Lv2〜4: セレクター先行フロー
                    // EXP消費の前に選択を確定させる。nullならキャンセル扱いでループを中断する。
                    var choices = unit.AvailableUpgrades
                        .Where(u => !u.IsMasteryBonus)
                        .ToList();

                    var selected = _upgradeSelector(unit, choices);

                    // キャンセル: EXPもレベルも変化させずにループを抜ける
                    if (selected == null) break;

                    // 選択確定: EXP消費・レベルアップ・強化適用
                    unit.CurrentExp -= required;
                    unit.CurrentLevel++;
                    ApplyUpgrade(unit, selected);
                }
            }
        }

        /// <summary>
        /// Lv5到達時の処理。
        /// AvailableUpgradesに残っているすべての強化肢（非マスター・マスター含む）を自動適用する。
        /// </summary>
        private static void ApplyAllRemainingAndMastery(Unit unit)
        {
            // リストをコピーしてイテレーション中の変更を防ぐ
            var remaining = unit.AvailableUpgrades.ToList();
            foreach (var upgrade in remaining)
            {
                ApplyUpgrade(unit, upgrade);
            }
        }

        /// <summary>
        /// 単一の強化オプションを適用し、AvailableUpgrades→AppliedUpgradesへ移す。
        /// </summary>
        private static void ApplyUpgrade(Unit unit, UnitUpgrade upgrade)
        {
            // AvailableUpgradesに存在しないものは弾く（二重適用防止）
            if (!unit.AvailableUpgrades.Contains(upgrade)) return;

            upgrade.ApplyEffect?.Invoke(unit);
            unit.AvailableUpgrades.Remove(upgrade);
            unit.AppliedUpgrades.Add(upgrade);
        }
    }
}
