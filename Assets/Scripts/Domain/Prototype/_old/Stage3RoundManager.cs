// Assets/Scripts/Core/Prototype/Stage3RoundManager.cs
// 段階3：ラウンド進行・配置・戦闘解決・点数更新・勝敗判定（仕様 120 §9.3・§9.7・§10.7・§10.9）。
//
// ラウンドの大枠フロー：
//   StartRound  ：パターン抽選→敵編成適用→戦線リセット→HP全回復→行動力・履歴リセット
//   ResolveAllBattles ：R1-R6 全戦線 or R7 街ボス戦のみを順次解決→点数加算→勝敗判定
//   AdvanceToNextRound：次ラウンドへ進む
using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Models;

using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>1戦線の解決結果（点数加算前後の集計）。</summary>
    public sealed class Stage3FrontResolution
    {
        public BattlefrontKind Battlefront { get; set; }
        public BattleReport BattleReport { get; set; }  // null = 戦闘なし（敵なし or 双方なし）
        public bool AlliesAbandoned { get; set; }              // 敵あり・味方なし＝完全放置完敗
        public int PointsGained { get; set; }                  // 戦線累計に加算された点数
        public int InitialAllies { get; set; }
        public int InitialEnemies { get; set; }
        public int SurvivingAllies { get; set; }
        public int SurvivingEnemies { get; set; }
    }

    /// <summary>
    /// ラウンド進行マネージャ（純C#・MonoBehaviour非依存）。
    /// 状態は Stage3CampaignState に集約し、本クラスは進行関数群を提供する static 群。
    /// 戦闘解決は BattleResolver で差し替え可能（テスト時はモック実装を注入）。
    /// </summary>
    public static class Stage3RoundManager
    {
        /// <summary>戦闘ターン制限（仕様§1.2 既定 15ターン）。</summary>
        public const int DefaultMaxBattleTurns = 15;

        // ══════════════════════════════════════════════
        // ラウンド開始
        // ══════════════════════════════════════════════

        /// <summary>
        /// ラウンドの開始処理を行う：
        ///   1. 戦線状態をラウンド単位でリセット（敵編成・配置クリア・Lv3 は自動偵察）
        ///   2. 敵パターン抽選＋強度倍率適用＋戦線への割り当て
        ///   3. 負傷状態の遷移（§9.6：Resting→Active 休養完了、Injured→Resting 休養開始）
        ///   4. プレイヤー手駒のHP全回復（プロト暫定・§9.6・負傷状態とは独立にHPは回復）
        ///   5. 行動力リセット＋内政アクション履歴クリア
        /// </summary>
        public static void StartRound(Stage3CampaignState state, Func<int> rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // 1. 戦線リセット
            foreach (var f in state.Battlefronts) f.ResetForNewRound();

            // 2. 敵パターン抽選＋戦線割り当て
            var assignments = Stage3EnemyPatterns.AssignPatternsForRound(state.CurrentRound, rng);
            float scale = Stage3EnemyPatterns.GetStrengthScale(state.CurrentRound);

            foreach (var asg in assignments)
            {
                var front = state.GetBattlefront(asg.Battlefront);
                if (front == null) continue;
                front.PatternTier = asg.Pattern.Tier;
                front.PatternLabel = asg.Pattern.Label;
                front.EnemyComposition = asg.Pattern.CreateEnemies();
                Stage3EnemyPatterns.ApplyStrengthScale(front.EnemyComposition, scale);
            }

            // 3. 負傷状態の遷移（§9.6）
            //    Resting → Active：前ラウンドで休養したユニットが復帰
            //    Injured → Resting：前ラウンドで負傷したユニットがこのラウンド休養
            AdvanceInjuryStates(state);

            // 4. プロト暫定：ラウンド開始時にプレイヤー手駒のHPを全回復（§9.6）
            //    HPは負傷状態に関わらず回復する（負傷状態の解消は §9.6 通り時間経過のみ）
            HealAllRosterToFull(state);

            // 5. §10.10 姫騎士（固有キャラ）の拠点Lv連動強化を反映：
            //    全戦線の拠点 BaseLevel 合計 ÷ 2 を姫騎士の EnhancementLevel に代入する。
            //    戦闘開始時に AuraEffect.Magnitude が EnhancementMagnitudePerLevel × Lv ぶん加算される
            //    （BattleManager.ApplyTeamAuras 経路）。姫騎士がいないラウンドでは何もしない。
            ApplyPrincessEnhancementFromBaseLevels(state);

            // 6. 行動力・履歴リセット
            state.ActionPoints = state.Config.ActionPointsPerRound;
            state.ExecutedInteriorActions.Clear();
        }

        /// <summary>
        /// 姫騎士（s3_princess）が手駒にいれば、Stage3CampaignState の拠点Lv合計から算出される
        /// EnhancementLevel をその Unit インスタンスに代入する（§10.10）。
        /// 拠点を強化した次ラウンドから反映される。
        /// </summary>
        private static void ApplyPrincessEnhancementFromBaseLevels(Stage3CampaignState state)
        {
            int level = state.GetPrincessEnhancementLevel();
            foreach (var u in state.Roster)
                if (u.Id == Stage3Roster.PrincessId) u.EnhancementLevel = level;
        }

        /// <summary>
        /// 負傷状態を1ラウンド分進める（§9.6）。
        /// Resting → Active（休養完了）／Injured → Resting（休養開始）。
        /// Active のままのユニットには影響なし。
        /// </summary>
        public static void AdvanceInjuryStates(Stage3CampaignState state)
        {
            foreach (var u in state.Roster)
            {
                if (u.InjuryStatus == InjuryState.Resting)
                    u.InjuryStatus = InjuryState.Active;
                else if (u.InjuryStatus == InjuryState.Injured)
                    u.InjuryStatus = InjuryState.Resting;
            }
        }

        /// <summary>
        /// 手駒の全ユニットHPを実効最大HP（兵種強化込み）まで回復する（プロト暫定 §9.6）。
        /// 負傷状態の解消はこの関数では行わない（§9.6：1ラウンドの休養でのみ解消）。
        /// </summary>
        public static void HealAllRosterToFull(Stage3CampaignState state)
        {
            foreach (var u in state.Roster)
            {
                int effectiveMaxHp = u.MaxHP + u.EnhancementHPPerLevel * u.EnhancementLevel;
                u.CurrentHP = effectiveMaxHp;
                if (u.State == UnitState.Dead) u.State = UnitState.Active;
            }
        }

        // ══════════════════════════════════════════════
        // 配置
        // ══════════════════════════════════════════════

        /// <summary>
        /// プレイヤーが手駒から指定ユニットを戦線スロットに配置する。
        /// SlotIndex 0-5（前列3・後列3）。同じ Unit は1戦線あたり1スロットのみ。
        /// 既に他の戦線に配置されているユニットは配置不可。
        /// **負傷の休養中（InjuryStatus=Resting）は配置不可**（§9.6）。
        /// </summary>
        public static bool AssignUnit(Stage3CampaignState state, BattlefrontKind kind, Unit unit, int slotIndex)
        {
            if (state == null || unit == null) return false;
            if (slotIndex < 0 || slotIndex > 5) return false;
            if (!state.Roster.Contains(unit)) return false;
            if (unit.InjuryStatus == InjuryState.Resting) return false;

            // 全戦線で既に配置されていないかチェック
            foreach (var f in state.Battlefronts)
                if (f.AssignedAllies.Any(a => a.BaseUnit == unit)) return false;

            var front = state.GetBattlefront(kind);
            if (front == null) return false;
            if (front.AssignedAllies.Any(a => a.SlotIndex == slotIndex)) return false;

            front.AssignedAllies.Add(new RuntimeUnit(unit, slotIndex));
            return true;
        }

        /// <summary>指定戦線の配置をすべて解除する。</summary>
        public static void ClearAssignments(Battlefront front)
        {
            front?.AssignedAllies.Clear();
        }

        // ══════════════════════════════════════════════
        // 戦闘解決＋点数更新
        // ══════════════════════════════════════════════

        /// <summary>
        /// 全戦線の戦闘を解決する。R1-R6 は3戦線すべて、R7 は街のみ（ボス戦）を解決する。
        /// 解決結果に応じて累計点数を加算し、勝敗判定（state.Result）を更新する。
        /// 戻り値：1ラウンド分の戦線解決結果リスト（順序：平原→街→砦）。
        /// </summary>
        public static List<Stage3FrontResolution> ResolveAllBattles(
            Stage3CampaignState state, Func<int> battleRng,
            BattleResolver resolver = null, int maxBattleTurns = DefaultMaxBattleTurns)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            resolver = resolver ?? BattleRunner.Run;

            var resolutions = new List<Stage3FrontResolution>();
            bool isBossRound = state.CurrentRound >= state.Config.MaxRounds;

            foreach (var front in state.Battlefronts)
            {
                // R7 は街のみ戦闘解決（平原・砦は EnemyPatternTier.None で実質スキップ）
                var resolution = ResolveSingleFront(front, battleRng, resolver, maxBattleTurns);
                resolutions.Add(resolution);

                // ボス戦の結果は別途記録（state.Result 判定で使用）
                if (isBossRound && front.Kind == BattlefrontKind.City && resolution.BattleReport != null)
                    state.BossBattleResult = resolution.BattleReport.Result;
            }

            // 勝敗判定
            UpdateCampaignResult(state, isBossRound);
            return resolutions;
        }

        /// <summary>1戦線の戦闘を解決して Stage3FrontResolution を返す。点数加算も行う。</summary>
        public static Stage3FrontResolution ResolveSingleFront(
            Battlefront front, Func<int> battleRng,
            BattleResolver resolver, int maxBattleTurns)
        {
            var result = new Stage3FrontResolution
            {
                Battlefront = front.Kind,
                InitialAllies = front.AssignedAllies.Count,
                InitialEnemies = front.EnemyComposition.Count,
            };

            // ケース①：敵なし → 戦闘なし、点数0
            if (front.EnemyComposition.Count == 0)
            {
                result.SurvivingAllies = front.AssignedAllies.Count;
                return result;
            }

            // ケース②：敵あり・配置なし → 完全放置完敗（+2点）
            if (front.AssignedAllies.Count == 0)
            {
                result.AlliesAbandoned = true;
                result.SurvivingEnemies = front.EnemyComposition.Count;
                result.PointsGained = 2;
                front.CumulativePoints += 2;
                return result;
            }

            // ケース③：戦闘実行
            ApplyBaseFortificationEffects(front);
            result.BattleReport = resolver(
                front.AssignedAllies, front.EnemyComposition, maxBattleTurns, battleRng);

            result.SurvivingAllies = front.AssignedAllies.Count(u => u.IsAlive);
            result.SurvivingEnemies = front.EnemyComposition.Count(u => u.IsAlive);

            // 戦闘終了後、HP0 になった味方は負傷状態（§9.6）。
            // 既に Resting だった味方は休養続行扱いだが、配置不可なのでここに到達しない想定。
            // Active のまま戦闘した味方が HP0 になった場合のみ Injured マークする。
            foreach (var ru in front.AssignedAllies)
                if (!ru.IsAlive && ru.BaseUnit.InjuryStatus == InjuryState.Active)
                    ru.BaseUnit.InjuryStatus = InjuryState.Injured;

            result.PointsGained = ComputePointsFromBattle(
                result.InitialAllies, result.InitialEnemies,
                result.SurvivingAllies, result.SurvivingEnemies);
            front.CumulativePoints += result.PointsGained;

            return result;
        }

        /// <summary>
        /// 戦闘結果から戦線累計点数の加算値を算出する（§9.7）。
        /// 完勝（敵全滅）/辛勝（減数で味方有利）/引き分け（減数同数）：0点
        /// 完敗（味方全滅）：+2点／惜敗（減数で敵有利）：+1点
        /// </summary>
        public static int ComputePointsFromBattle(
            int initialAllies, int initialEnemies,
            int survivingAllies, int survivingEnemies)
        {
            if (survivingAllies == 0) return 2;                        // 完敗（味方全滅）
            if (survivingEnemies == 0) return 0;                       // 完勝
            int allyLoss = initialAllies - survivingAllies;
            int enemyLoss = initialEnemies - survivingEnemies;
            if (allyLoss < enemyLoss) return 0;                        // 辛勝
            if (allyLoss == enemyLoss) return 0;                       // 引き分け
            return 1;                                                  // 惜敗
        }

        /// <summary>
        /// 拠点強化レベルに応じた永続バフを味方に付与する（§10.9）。
        ///   Lv1+: DefenseUp Magnitude=3（PDEF+3 / MDEF+3）
        ///   Lv2+: AttackUp Magnitude=5（ATK+5）追加
        ///   Lv3 : 同じバフを重ねて適用＝PDEF/MDEF +6 / ATK +10（攻防両方の重バフ）
        ///        旧仕様の自動偵察は §10.8 偵察コマンド全戦線化で廃止。
        /// </summary>
        public static void ApplyBaseFortificationEffects(Battlefront front)
        {
            if (front == null || front.BaseLevel <= 0) return;

            int defMagnitude = front.BaseLevel >= 3 ? 6 : 3;          // Lv1/2 → 3、Lv3 → 6
            int atkMagnitude = front.BaseLevel >= 3 ? 10 : 5;          // Lv2 → 5、Lv3 → 10

            foreach (var ru in front.AssignedAllies)
            {
                ru.AddEffect(new StatusEffect(StatusEffectType.DefenseUp, stacks: 1, remainingTurns: -1)
                {
                    Magnitude = defMagnitude
                });
                if (front.BaseLevel >= 2)
                    ru.AddEffect(new StatusEffect(StatusEffectType.AttackUp, stacks: 1, remainingTurns: -1)
                    {
                        Magnitude = atkMagnitude
                    });
            }
        }

        /// <summary>
        /// ラウンド解決後の勝敗判定（§9.7）。
        ///   R1-R6：いずれかの戦線が累計上限到達 → FrontLost
        ///   R7  ：街のボス戦が完勝or辛勝でない → BossLost、勝てば Cleared
        /// 既に Result が None でない場合は上書きしない。
        /// </summary>
        public static void UpdateCampaignResult(Stage3CampaignState state, bool isBossRound)
        {
            if (state.Result != Stage3CampaignResult.None) return;

            // 戦線累計上限のチェックは全ラウンド共通
            foreach (var f in state.Battlefronts)
            {
                if (f.IsExhausted)
                {
                    state.Result = Stage3CampaignResult.FrontLost;
                    return;
                }
            }

            if (!isBossRound) return;

            // R7：ボス戦の勝敗で確定
            if (!state.BossBattleResult.HasValue)
            {
                // ボス戦が解決されていない（街が敵なしになる等の異常ケース）→ 念のため None維持
                return;
            }

            var bossResult = state.BossBattleResult.Value;
            bool bossWon = bossResult == BattleResult.PerfectVictory
                || bossResult == BattleResult.AdvantageousVictory;
            state.Result = bossWon ? Stage3CampaignResult.Cleared : Stage3CampaignResult.BossLost;
        }

        // ══════════════════════════════════════════════
        // 次ラウンドへ
        // ══════════════════════════════════════════════

        /// <summary>
        /// 次ラウンドへ進む。Result が None でない場合は進めない（ゲームオーバー or クリア後）。
        /// 行動力リセットは StartRound 側で実施する。
        /// </summary>
        public static bool AdvanceToNextRound(Stage3CampaignState state)
        {
            if (state == null) return false;
            if (state.Result != Stage3CampaignResult.None) return false;
            if (state.CurrentRound >= state.Config.MaxRounds) return false;

            state.CurrentRound++;
            return true;
        }
    }
}
