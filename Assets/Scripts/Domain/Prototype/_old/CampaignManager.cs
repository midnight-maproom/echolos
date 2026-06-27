// Assets/Scripts/Core/Prototype/CampaignManager.cs
// プロト 段階2: 多戦線ミニループ（H2）のオーケストレーション（純C#・MonoBehaviour非依存）
//
// 1ターンの流れ（仕様 210_prototype_spec.md §5.3）:
//   内政（行動力を配分）→ 師団を各戦線へ配置 → 全戦線が同時に戦闘解決 → 次ターン
// 勝敗（§6.6）: 規定ターン本拠地を守り切ればクリア／本拠地HP0で敗北。
//
// 戦闘解決は IFrontResolver に委譲し、戦略ロジックを戦闘実装から分離する。
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>
    /// 多戦線ミニループの進行を司る。状態（CampaignState）を外部から受け取り操作する
    /// ステートレスなマネージャ（テスト容易性のため状態を内部に持たない）。
    /// </summary>
    public sealed class CampaignManager
    {
        private readonly CampaignConfig _config;

        public CampaignManager(CampaignConfig config = null)
        {
            _config = config ?? new CampaignConfig();
        }

        public CampaignConfig Config => _config;

        // ══════════════════════════════════════════════
        // 開始・ターン進行
        // ══════════════════════════════════════════════

        /// <summary>キャンペーン開始時の初期化（行動力付与・全戦闘員の全快・配置クリア）。</summary>
        public void StartCampaign(CampaignState state)
        {
            state.ActionPoints = _config.ActionPointsPerTurn;
            state.RestingUnits.Clear();
            state.NewlyDownedUnits.Clear();
            RestoreAllCombatants(state);
            ClearAssignments(state);
        }

        /// <summary>
        /// 次ターンへ進める（継続中のみ）。
        /// 行動力回復・前ターン戦闘不能ぶんを休みに移行・手駒全快・配置/防衛強化リセット。
        /// </summary>
        public void AdvanceTurn(CampaignState state)
        {
            if (state.Result != CampaignResult.None) return;

            state.CurrentTurn++;
            state.ActionPoints = _config.ActionPointsPerTurn;

            // 前ターンに戦闘不能になったユニットを「このターン休み」に移行する
            state.RestingUnits.Clear();
            foreach (var u in state.NewlyDownedUnits) state.RestingUnits.Add(u);
            state.NewlyDownedUnits.Clear();

            // 全戦闘員を全快（HPは持ち越さない）。手駒の休みユニットも全快はするが割り当て不可のまま。
            // 敵編成も全快＝毎ターン新たな攻勢として扱う（仕様§5.1）。
            RestoreAllCombatants(state);

            ClearAssignments(state);
        }

        // ══════════════════════════════════════════════
        // 内政（行動力消費）
        // ══════════════════════════════════════════════

        /// <summary>偵察：対象戦線の敵編成を可視化する。行動力不足/範囲外なら何もせずfalse。</summary>
        public bool Scout(CampaignState state, int frontIndex)
        {
            if (!IsValidFront(state, frontIndex)) return false;
            if (state.ActionPoints < _config.ScoutCost) return false;

            state.ActionPoints -= _config.ScoutCost;
            state.Fronts[frontIndex].IsScouted = true;
            return true;
        }

        /// <summary>守備強化：対象ユニットのPDEF/MDEFを恒久的に上げる。手駒外なら何もせずfalse。</summary>
        public bool Fortify(CampaignState state, RuntimeUnit unit)
        {
            if (unit == null || !state.Roster.Contains(unit)) return false;
            if (state.ActionPoints < _config.FortifyCost) return false;

            state.ActionPoints -= _config.FortifyCost;
            unit.BaseUnit.PDEF += _config.FortifyDefBonus;
            unit.BaseUnit.MDEF += _config.FortifyDefBonus;
            return true;
        }

        /// <summary>攻撃強化：対象ユニットのATKを恒久的に上げる。手駒外なら何もせずfalse。</summary>
        public bool Reinforce(CampaignState state, RuntimeUnit unit)
        {
            if (unit == null || !state.Roster.Contains(unit)) return false;
            if (state.ActionPoints < _config.ReinforceCost) return false;

            state.ActionPoints -= _config.ReinforceCost;
            unit.BaseUnit.BaseATK += _config.ReinforceAtkBonus;
            return true;
        }

        // ══════════════════════════════════════════════
        // 配置（師団割り当て）
        // ══════════════════════════════════════════════

        /// <summary>
        /// ユニットを指定戦線へ割り当てる。
        /// 既に他戦線へ割り当て済みなら移動する。手駒外/休み/範囲外なら何もせずfalse。
        /// </summary>
        public bool AssignToFront(CampaignState state, RuntimeUnit unit, int frontIndex)
        {
            if (!IsValidFront(state, frontIndex)) return false;
            if (unit == null || !state.Roster.Contains(unit)) return false;
            if (state.RestingUnits.Contains(unit)) return false;

            Unassign(state, unit); // 既存の割り当てから外してから付け替える
            state.Fronts[frontIndex].AssignedAllies.Add(unit);
            return true;
        }

        /// <summary>ユニットを現在の割り当て戦線から外す（どこにも居なければ何もしない）。</summary>
        public void Unassign(CampaignState state, RuntimeUnit unit)
        {
            foreach (var front in state.Fronts)
                front.AssignedAllies.Remove(unit);
        }

        // ══════════════════════════════════════════════
        // ターン解決
        // ══════════════════════════════════════════════

        /// <summary>
        /// 全戦線を固定順で解決し、本拠地HP・負傷・勝敗を更新する。
        /// 各戦線のレポート（FrontResolution）のリストを返す。
        /// </summary>
        public List<FrontResolution> ResolveTurn(CampaignState state, IFrontResolver resolver)
        {
            var reports = new List<FrontResolution>();
            state.NewlyDownedUnits.Clear();

            foreach (var front in state.Fronts)
            {
                FrontResolution report;

                if (front.AssignedAllies.Count == 0)
                {
                    // 無防備で捨てた戦線：そのまま突破される
                    int dmg = front.BaseBreakthroughDamage;
                    report = new FrontResolution
                    {
                        FrontName = front.Name,
                        Defended = false,
                        Held = false,
                        BattleResult = BattleResult.None,
                        BreakthroughDamage = dmg,
                    };
                    state.HomeBaseHP -= dmg;
                }
                else
                {
                    var result = resolver.Resolve(front.AssignedAllies, front);
                    result.FrontName = front.Name;
                    result.Defended = true;

                    if (!result.Held)
                    {
                        int dmg = front.BaseBreakthroughDamage;
                        result.BreakthroughDamage = dmg;
                        state.HomeBaseHP -= dmg;
                    }
                    else
                    {
                        result.BreakthroughDamage = 0;
                    }

                    // 戦闘不能（HP0）になった味方を翌ターン休みに予約する
                    foreach (var ally in front.AssignedAllies)
                        if (ally.CurrentHP <= 0) state.NewlyDownedUnits.Add(ally);

                    report = result;
                }

                reports.Add(report);
            }

            if (state.HomeBaseHP < 0) state.HomeBaseHP = 0;
            state.Result = EvaluateResult(state);
            return reports;
        }

        /// <summary>現在の状態から勝敗を判定する。</summary>
        public CampaignResult EvaluateResult(CampaignState state)
        {
            if (state.HomeBaseHP <= 0) return CampaignResult.Defeat;
            if (state.CurrentTurn >= state.MaxTurns) return CampaignResult.Clear;
            return CampaignResult.None;
        }

        // ══════════════════════════════════════════════
        // 内部ユーティリティ
        // ══════════════════════════════════════════════

        private static bool IsValidFront(CampaignState state, int frontIndex) =>
            frontIndex >= 0 && frontIndex < state.Fronts.Count;

        private static void ClearAssignments(CampaignState state)
        {
            foreach (var front in state.Fronts)
                front.AssignedAllies.Clear();
        }

        /// <summary>手駒と全戦線の敵編成を全快状態に戻す（HP・状態異常・シールドをリセット）。</summary>
        private static void RestoreAllCombatants(CampaignState state)
        {
            foreach (var u in state.Roster) PrototypeRoster.ResetForBattle(u);
            foreach (var front in state.Fronts)
                foreach (var e in front.EnemyDivision) PrototypeRoster.ResetForBattle(e);
        }
    }
}
