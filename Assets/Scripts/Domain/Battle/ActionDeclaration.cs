using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // メインフェーズで 1 ユニットが宣言した行動内容。
    // 評価フェーズ（TargetEvaluator が生成）と実行フェーズ（ActionExecutor が
    // 実際にダメージ・回復・付与等を適用）の分離を体現するクラス。
    public sealed class ActionDeclaration
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
        /// 単体技の場合は要素 1 つ。範囲・全体技の場合は複数。待機の場合は空。
        /// </summary>
        public List<RuntimeUnit> Targets { get; }

        /// <summary>
        /// 凍結等の状態異常を考慮した実効 SPD。
        /// BattleManager がメインフェーズの行動順（降順）ソートに使用する。
        /// </summary>
        public int EffectiveSPD { get; }

        /// <summary>
        /// 待機かどうか。麻痺・凍結による行動不能の場合に true。
        /// 行動はスキップされるが、行動権は消費する。
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
