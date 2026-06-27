using System.Collections.Generic;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Battle
{
    /// <summary>
    /// メインフェーズで1ユニットが宣言した行動内容。
    /// TargetEvaluator.DeclareActionが生成し、BattleManagerがSPDソートと実行に使用する。
    ///
    /// 「評価フェーズ」と「実行フェーズ」の分離を体現するクラス。
    /// 評価フェーズでターゲット・技を決定し、実行フェーズで実際にダメージを適用する。
    /// </summary>
    public class ActionDeclaration
    {
        /// <summary>この行動を行うユニット</summary>
        public RuntimeUnit Actor { get; }

        /// <summary>
        /// 宣言した技（RuntimeWaza・戦闘中状態込み）。
        /// IsWaiting = true の場合は null。
        /// </summary>
        public RuntimeWaza DeclaredWaza { get; }

        /// <summary>
        /// この行動のターゲットリスト。
        /// 単体技の場合は要素1つ。AoE技の場合は複数。待機の場合は空。
        /// </summary>
        public List<RuntimeUnit> Targets { get; }

        /// <summary>
        /// 凍結などの状態異常を考慮した実効SPD。
        /// BattleManagerがメインフェーズの行動順（降順）ソートに使用する。
        /// </summary>
        public int EffectiveSPD { get; }

        /// <summary>
        /// 待機かどうか。
        /// 麻痺・凍結による行動不能の場合に true。行動はスキップされるが、行動権は消費する。
        /// </summary>
        public bool IsWaiting { get; }

        public ActionDeclaration(
            RuntimeUnit actor,
            RuntimeWaza declaredWaza,
            List<RuntimeUnit> targets,
            int effectiveSPD,
            bool isWaiting)
        {
            Actor = actor;
            DeclaredWaza = declaredWaza;
            Targets = targets ?? new List<RuntimeUnit>();
            EffectiveSPD = effectiveSPD;
            IsWaiting = isWaiting;
        }
    }
}
