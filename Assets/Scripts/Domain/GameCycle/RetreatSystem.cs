using System;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;

namespace Echolos.Domain.GameCycle
{
    /// <summary>
    /// 戦闘中の撤退処理（しんがりシステム）を担うクラス。
    ///
    /// 仕様：
    ///  - PhaseState.InterventionStandby 中にプレイヤーが「撤退」を選択した場合に呼び出す
    ///  - 現在出撃中（AllyUnits内）かつ生存しているRuntimeUnitを「しんがり」として1体指定する
    ///  - しんがりは即座に完全ロスト（装備返還を含む）となる
    ///  - 撤退後は BattleResult.CrushingDefeat（完敗）として扱い、BattleContext.Result に設定する
    ///  - CommanderData.AccumulatedFailures を +1 する
    ///
    /// バトルループの強制終了（Break）は呼び出し元（BattleManagerを動かすループ）の責務。
    /// BattleContext.Result が CrushingDefeat になった後、呼び出し元がループを抜けることで実現する。
    /// </summary>
    public class RetreatSystem
    {
        private readonly LostProcessor _lostProcessor;

        /// <param name="lostProcessor">完全ロスト処理（装備返還・State変更）を担うインスタンス</param>
        public RetreatSystem(LostProcessor lostProcessor)
        {
            _lostProcessor = lostProcessor
                ?? throw new ArgumentNullException(nameof(lostProcessor));
        }

        /// <summary>
        /// 撤退処理を実行する。
        ///
        /// 処理順序：
        ///  1. しんがり指定の妥当性を検証する（出撃中 & 生存 のみ可）
        ///  2. しんがりユニットを完全ロスト（装備返還を含む）させる
        ///  3. BattleContext.Result を CrushingDefeat（完敗）に設定する
        ///  4. CommanderData.AccumulatedFailures を +1 する
        ///
        /// バトルループの強制終了は呼び出し元が BattleContext.Result を確認して行うこと。
        /// </summary>
        /// <param name="rearguard">しんがりに指定するRuntimeUnit（出撃中かつ生存していること）</param>
        /// <param name="context">現在のバトルコンテキスト（AllyUnitsとResult管理）</param>
        /// <param name="commander">敗北カウント加算と装備返還先の指揮官データ</param>
        /// <exception cref="ArgumentNullException">いずれかの引数がnullの場合</exception>
        /// <exception cref="InvalidOperationException">
        ///  しんがり指定が不正な場合：
        ///   - 死亡済みのユニットを指定した
        ///   - 出撃中（AllyUnits）ではないユニット（控えや敵）を指定した
        /// </exception>
        public void ExecuteRetreat(RuntimeUnit rearguard, BattleContext context, CommanderData commander)
        {
            if (rearguard == null) throw new ArgumentNullException(nameof(rearguard));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (commander == null) throw new ArgumentNullException(nameof(commander));

            // しんがりが生存しているか確認
            if (!rearguard.IsAlive)
                throw new InvalidOperationException(
                    $"死亡済みのユニット '{rearguard.BaseUnit.Name}' はしんがりに指定できません。");

            // しんがりが出撃中（AllyUnits内）であるか確認
            if (!context.AllyUnits.Contains(rearguard))
                throw new InvalidOperationException(
                    $"出撃中ではないユニット '{rearguard.BaseUnit.Name}' はしんがりに指定できません。" +
                    " 控え（Reserve）や敵ユニットは対象外です。");

            // しんがりを完全ロスト（装備返還 + State=Dead + HP=0）
            _lostProcessor.ProcessPermanentLost(rearguard.BaseUnit, commander);

            // バトルを完敗扱いにする（撤退＝完敗）
            context.Result = BattleResult.CrushingDefeat;

            // 累計敗北・撤退カウントを加算（3回でゲームオーバー）
            commander.AccumulatedFailures++;
        }

        /// <summary>
        /// しんがりとして指定可能かどうかを検証する（実行なし）。
        /// UIでの選択可否表示などに使用できる。
        /// </summary>
        /// <returns>指定可能なら true、不可能なら false</returns>
        public static bool CanDesignateAsRearguard(RuntimeUnit rearguard, BattleContext context)
        {
            if (rearguard == null) return false;
            if (context == null) return false;
            return rearguard.IsAlive && context.AllyUnits.Contains(rearguard);
        }
    }
}
