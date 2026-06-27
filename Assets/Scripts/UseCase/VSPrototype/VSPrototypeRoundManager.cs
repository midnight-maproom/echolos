// 領地マップの判定ロジック関数群＋ラン進行ロジック。
// 戦闘解決は呼び出し側が BattleResolver を注入する（resolver 必須）。
using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Catalog;
using Echolos.Domain.Formula;
using Echolos.Domain.Meta;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロト ラウンド・配置・救出に関する判定ロジック関数群。</summary>
    public sealed class VSPrototypeRoundManager
    {
        private readonly VSPrototypeEndingResolver _endingResolver;
        private readonly VSPrototypeEnemyPatterns _enemyPatterns;
        private readonly IMetaRewardFormulaCatalog _rewardFormulaCatalog;
        private readonly IMetaProgressView _metaProgress;

        public VSPrototypeRoundManager(
            VSPrototypeEndingResolver endingResolver,
            VSPrototypeEnemyPatterns enemyPatterns,
            IMetaRewardFormulaCatalog rewardFormulaCatalog,
            IMetaProgressView metaProgress)
        {
            _endingResolver = endingResolver ?? throw new ArgumentNullException(nameof(endingResolver));
            _enemyPatterns = enemyPatterns ?? throw new ArgumentNullException(nameof(enemyPatterns));
            _rewardFormulaCatalog = rewardFormulaCatalog ?? throw new ArgumentNullException(nameof(rewardFormulaCatalog));
            _metaProgress = metaProgress ?? throw new ArgumentNullException(nameof(metaProgress));
        }

        /// <summary>最終ラウンド（本拠地ボス戦）。</summary>
        public const int BossRound = 7;

        /// <summary>総ラウンド数。</summary>
        public const int MaxRounds = 7;

        /// <summary>ブリジット救出デッドライン（このラウンドまでに左列敵拠点を制圧）。</summary>
        public const int BridgetRescueDeadline = 5;

        /// <summary>1マスの配置スロット最大数（前列3＋後列3）。<see cref="MapNode.MaxAlliedSlots"/> を参照。</summary>
        public const int SlotsPerNode = MapNode.MaxAlliedSlots;

        // 自領陥落・本拠地連続防衛

        /// <summary>陥落済の自領数を数える（0〜3）。本拠地連続防衛の発生数に等しい。</summary>
        public int CountFallenFronts(VSPrototypeMapState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int count = 0;
            foreach (var f in state.FriendlyNodes())
                if (f.IsFallen) count++;
            return count;
        }

        // 配置可否判定

        /// <summary>
        /// 指定マスにユニットを配置可能か。
        /// マスが「敵拠点」の場合は同列の敵領が制圧済であることを要求する（攻略順序依存）。
        /// R7 ボス戦時は本拠地以外への配置を一切禁止（本拠地のみ配置必須）。
        /// </summary>
        public bool CanAssign(VSPrototypeMapState state, MapNode node, int round)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));

            // R7 ボス戦：本拠地以外への配置を禁止（既存ユニットは StartRound 末尾の
            // DischargeUnreachableAssignments で自動回収される）。
            if (round == BossRound && node.Kind != MapNodeKind.Home)
                return false;

            switch (node.Kind)
            {
                case MapNodeKind.Friendly:
                    // 自領は常時配置可：健在なら防衛戦／陥落なら取り戻し戦の進攻配置
                    return true;
                case MapNodeKind.EnemyTerritory:
                    // 敵領は戦線（BattleMode != None）のときのみ配置可。
                    // 戦線外（自領陥落で隣接性喪失等）への新規配置は地理的整合性で不可。
                    // 戦線変動で配置不可になったマスの既存ユニットは StartRound 末尾の
                    // 自動回収（DischargeUnreachableAssignments）で手持ちに戻る。
                    return node.BattleMode != MapNodeBattleMode.None;
                case MapNodeKind.EnemyStronghold:
                    // 敵拠点は同列の敵領が制圧済であること（攻略順序依存）。
                    if (!state.GetNode(node.Col, VSPrototypeMapState.LayerEnemyTerritory).IsCaptured)
                        return false;
                    // 戦線外（列戦線終了・自領陥落で隣接性喪失等）への新規配置は不可。
                    return node.BattleMode != MapNodeBattleMode.None;
                case MapNodeKind.Home:
                    // 本拠地は常に配置可。R7 ボス戦の主舞台であり、R1-R6 でも自領陥落時の
                    // 本拠地連続防衛戦に備えてプレイヤーが保険配置できる必要がある
                    // （ResolveHomeBattle は Home.AssignedAllies が空なら即敗北扱い）。
                    return true;
                default:
                    return false;
            }
        }

        // ブリジット救出判定

        /// <summary>
        /// バルドゥイン拠点が制圧された場合、ブリジット救出フラグを立てる。
        /// 冪等：救出済の状態で再度呼んでも問題ない。
        /// </summary>
        public void TryMarkBridgetRescued(VSPrototypeMapState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.BalduinStronghold.IsCaptured)
                state.MarkBridgetRescued();
        }

        /// <summary>
        /// 指定ラウンド終了時点で、ブリジット救出デッドラインを過ぎてしまったか判定する。
        /// （= R5 終了時点で BalduinStronghold が未制圧 → 詰め寄りシーン発火条件）。
        /// </summary>
        public bool IsBridgetRescueDeadlinePassed(VSPrototypeMapState state, int roundJustFinished)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (roundJustFinished < BridgetRescueDeadline) return false;
            // デッドライン以降、まだ救出できていなければデッドライン過ぎ
            return !state.IsBridgetRescued;
        }

        // R7 ボス戦

        /// <summary>当該ラウンドが R7 ボス戦ラウンドか。</summary>
        public bool IsBossRound(int round)
        {
            return round == BossRound;
        }

        // ラン進行：StartRound / ResolveAllBattles

        /// <summary>戦闘解決時の単一ターン上限。</summary>
        public const int DefaultMaxBattleTurns = 15;

        /// <summary>
        /// ラウンド開始処理：全マスのラウンドリセット＋敵編成セット＋前ラウンドからの配置引き継ぎ。
        ///   R1-R6：制圧済マス／陥落自領は敵スキップ。それ以外に敵編成を配る
        ///   R7：本拠地のみにボス編成、他マスは空（演出として戦線封鎖）
        ///   R2-R6 開始時に前ラウンドの生存配置を同マス・同スロットへ自動復元
        ///   （対象：健在自領＋制圧済敵領／敵拠点。陥落自領・本拠地は対象外。
        ///   復元時は新規 RuntimeUnit で HP 全回復）。
        /// </summary>
        public void StartRound(VSPrototypeMapState state, int round)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (round < 1 || round > MaxRounds) throw new ArgumentOutOfRangeException(nameof(round));

            // 前ラウンドの生存配置スナップショット（R1 と R7 は対象外）
            var carryOver = round > 1 && round < BossRound
                ? CaptureCarryOverPlacements(state)
                : null;

            foreach (var node in state.AllNodes()) node.ResetForNewRound();

            if (round == BossRound)
            {
                // R7：本拠地にボス出現。Meta.HasNotedPendantPower で A-c1（必敗）/ A-c2（戦える）二分岐。
                // 他マスは BattleMode=None（敵編成なし・配置不可）。
                state.Home.SetEnemyComposition(
                    _enemyPatterns.CreateBossPattern(_metaProgress.HasNotedPendantPower));
                state.Home.SetBattleMode(MapNodeBattleMode.Boss);
                return;
            }

            // R1-R6：列ごとに「自陣最前線（防衛/奪還/取り戻し戦）」と「敵陣最前線（攻め込み戦）」
            // を判定し、該当マスに敵編成と BattleMode をセット。それ以外のマスは BattleMode=None
            // のままで ResolveAllBattles のスキップ対象になる。
            for (int col = 0; col < 3; col++)
            {
                var defenseNode = ResolveDefenseNode(state, col);
                if (defenseNode != null)
                {
                    defenseNode.SetEnemyComposition(_enemyPatterns.CreateFriendlyDefenseEnemies(round));
                    defenseNode.SetBattleMode(MapNodeBattleMode.Defense);
                }

                var attackNode = CalculateAttackFrontline(state, col);
                if (attackNode != null)
                {
                    if (attackNode.Kind == MapNodeKind.EnemyTerritory)
                        attackNode.SetEnemyComposition(_enemyPatterns.CreateEnemyTerritoryEnemies());
                    else
                        attackNode.SetEnemyComposition(_enemyPatterns.CreateEnemyStrongholdEnemies());
                    attackNode.SetBattleMode(MapNodeBattleMode.Attack);
                }
            }

            // 本拠地戦の予告編成：ラウンド開始時に陥落自領があれば本拠地にも侵攻軍をセット。
            // MapGUI のホバーツールチップで「攻めてくる敵」を予告でき、プレイヤーは本拠地配置の
            // 必要性を判断できる。ResolveHomeBattle はこの編成をそのまま使う（決定論性保証＋
            // 予告／実戦闘の編成一致）。全自領取り戻し成功時は ResolveAllBattles 末尾でクリアする。
            if (CountFallenFronts(state) > 0)
            {
                state.Home.SetEnemyComposition(_enemyPatterns.CreateHomeInvasionEnemies(round));
                state.Home.SetBattleMode(MapNodeBattleMode.Defense);
            }

            // 生存配置を復元（敵編成セット後・順序は重要でない）
            if (carryOver != null) RestoreCarryOverPlacements(carryOver);

            // 戦線変動で配置不可になったマスのユニットを自動回収（Roster 経由で手持ちに戻る）。
            // 例：敵拠点攻め込み敗北＋同列敵領奪還戦敗北で敵領が取り戻されると、敵拠点は攻略順序
            //   違反で CanAssign=false になりモーダルすら開けず手動回収不可になる。
            // R7 開始時も本拠地以外の既存配置はここで一括回収される。
            //   carryOver 復元後＋ BattleMode セット後にすべての配置を点検する。
            DischargeUnreachableAssignments(state, round);
        }

        /// <summary>
        /// CanAssign=false なマスから配置済みユニットを外す（Roster は所持の全集合なので
        /// 配置から外せば自然に「手持ち」に戻る）。StartRound 末尾から呼ばれる想定。
        /// </summary>
        private void DischargeUnreachableAssignments(VSPrototypeMapState state, int round)
        {
            foreach (var node in state.AllNodes())
            {
                if (node.AssignedAllies.Count == 0) continue;
                if (CanAssign(state, node, round)) continue;
                node.ClearAllies();
            }
        }

        // 列の「自陣最前線」マスを返す（防衛/奪還/取り戻し戦の対象マス）。
        // 自領陥落は取り戻し戦の対象として友軍マス自身を返す（陥落時の特殊扱い）。
        private static MapNode ResolveDefenseNode(VSPrototypeMapState state, int col)
        {
            var friendly = state.GetNode(col, VSPrototypeMapState.LayerFriendly);
            if (friendly.IsFallen) return friendly;
            return CalculateDefenseFrontline(state, col);
        }

        // 列の「自陣最前線」を返す（敵が攻めてくる場所＝防衛戦／奪還戦の対象マス）。
        // - 自領陥落 → null（列は本拠地連続防衛要因）
        // - 敵拠点占領済み → null（列の戦線完全終了）
        // - 敵領占領済み → 敵領（奪還戦）
        // - それ以外 → 自領（防衛戦）
        private static MapNode CalculateDefenseFrontline(VSPrototypeMapState state, int col)
        {
            var friendly = state.GetNode(col, 1);
            if (friendly.IsFallen) return null;

            var stronghold = state.GetNode(col, 3);
            if (stronghold.IsCaptured) return null;

            var territory = state.GetNode(col, 2);
            if (territory.IsCaptured) return territory;

            return friendly;
        }

        // 列の「敵陣最前線」を返す（プレイヤーが攻め込める場所＝攻略戦の対象マス）。
        // 仕様 §1.4「敵拠点は同列の敵領を制圧後に解放」に整合：
        // - 自領陥落 → null（陥落自領が戦線で、その先は隣接性が切れて攻め込めない）
        // - 敵領未制圧 → 敵領
        // - 敵領占領済み・敵拠点未制圧 → 敵拠点
        // - 敵拠点も占領済み → null
        private static MapNode CalculateAttackFrontline(VSPrototypeMapState state, int col)
        {
            var friendly = state.GetNode(col, VSPrototypeMapState.LayerFriendly);
            if (friendly.IsFallen) return null;

            var territory = state.GetNode(col, 2);
            if (!territory.IsCaptured) return territory;

            var stronghold = state.GetNode(col, 3);
            if (!stronghold.IsCaptured) return stronghold;

            return null;
        }

        /// <summary>
        /// 前ラウンドの配置をスナップショット保存する。
        /// 対象：健在自領＋制圧済敵領／敵拠点＋本拠地。陥落自領・占領取り戻されマスは対象外
        /// （これらは ApplyDefenseOutcome の段階で ClearAllies 済＝AssignedAllies が空）。
        /// 戦闘不能（HP=0）も含めて配置を維持する（HP は戦闘終了時 Bootstrap で Roster 全員一括回復）。
        /// 本拠地は §1.5 の本拠地戦予告に備えてプレイヤーが配置できる＝ carryOver 対象。
        /// R7 開始時は carryOver 自体が動かない（round &gt;= BossRound でガード）ためボス戦の
        /// 新規配置フローと衝突しない。
        /// </summary>
        private static Dictionary<MapNode, List<(Unit unit, int slotIndex)>> CaptureCarryOverPlacements(
            VSPrototypeMapState state)
        {
            var dict = new Dictionary<MapNode, List<(Unit, int)>>();
            foreach (var node in state.AllNodes())
            {
                if (node.Kind == MapNodeKind.Friendly && node.IsFallen) continue;

                var entries = node.AssignedAllies
                    .Select(a => (a.BaseUnit, a.SlotIndex))
                    .ToList();
                if (entries.Count > 0) dict[node] = entries;
            }
            return dict;
        }

        /// <summary>
        /// スナップショットされた配置を新規 <see cref="RuntimeUnit"/> として復元する。
        /// HP は呼び出し側（ResolveAllBattles 末尾）で Roster 全員一括回復されている前提なので
        /// ここでは個別回復しない。
        /// </summary>
        private static void RestoreCarryOverPlacements(
            Dictionary<MapNode, List<(Unit unit, int slotIndex)>> carryOver)
        {
            foreach (var kv in carryOver)
            {
                foreach (var (unit, slotIndex) in kv.Value)
                    kv.Key.AssignAlly(new RuntimeUnit(unit, slotIndex));
            }
        }

        /// <summary>
        /// ラウンドの戦闘を全解決する：R1-R6 は各マス＋本拠地連続防衛、R7 はボス戦のみ。
        /// </summary>
        public VSPrototypeRoundResult ResolveAllBattles(
            VSPrototypeMapState state, int round,
            BattleResolver resolver = null, Func<int> rng = null,
            int maxBattleTurns = DefaultMaxBattleTurns)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            rng = rng ?? (() => 0);

            var result = new VSPrototypeRoundResult { Round = round };

            if (round == BossRound)
            {
                var bossReport = ResolveBossBattle(state, resolver, rng, maxBattleTurns);
                if (bossReport != null) result.HomeBattleReports.Add(bossReport);
                result.BossDefeated = IsBattleWon(bossReport);
                result.EndingKind = _endingResolver.ResolveAfterBossRound(result.BossDefeated);
                return result;
            }

            // 本拠地戦の発火判定はラウンド開始時点の陥落自領数で決まる（§1.5「猶予あり」）。
            // マス処理前にスナップショットしておかないと、今ラウンドで自領が陥落したケースで
            // そのラウンド末に本拠地戦が発火＝未配置で即敗北 になる（旧バグ）。
            int fallenAtRoundStart = CountFallenFronts(state);

            // R1-R6：2 パス解決＝攻め込み戦（Attack）を先に処理し、その結果で「敵領と接しなく
            // なった健在自領」の防衛戦を不発化してから残りの Defense マスを処理する。
            // これで「敵領を占領したのに同ラウンドの自領防衛が湧く」事象を防ぐ。
            //
            // Pass 1: Attack マス（敵領／敵拠点の攻め込み戦）を全列で先に解決。
            //         ApplyAttackOutcome で勝利時に node.Capture() → territory.IsCaptured=true。
            ResolveBattlesByMode(state, MapNodeBattleMode.Attack, resolver, rng, maxBattleTurns, result);

            // Pass 1.5: 攻め込み成功で前線が後退した列の Defense マスを不発化（§1.4.2）。
            //  - 敵領占領 → 健在自領の防衛戦不発（敵領と接しなくなった自領）
            //  - 敵拠点占領 → 占領済み敵領の奪還戦不発（列の戦線完全終了）
            // 陥落自領の取り戻し戦はキャンセル対象外（IsFallen で守る）。
            CancelObsoleteDefense(state);

            // Pass 2: 残りの Defense マス（自領防衛／占領済み敵領の奪還戦／陥落自領の取り戻し戦）を解決。
            ResolveBattlesByMode(state, MapNodeBattleMode.Defense, resolver, rng, maxBattleTurns, result);

            // バルドゥイン拠点が制圧されたらブリジット救出フラグ
            TryMarkBridgetRescued(state);

            // 本拠地戦：「ラウンド開始時に陥落自領があった」かつ「ラウンド末も陥落自領が残る」場合に 1 回発生。
            // - 今ラウンド中に新規陥落しただけ → 不発（次ラウンドから本拠地戦・§1.5 猶予）
            // - 全自領取り戻し成功なら不発（本拠地未配置でも安全）＋予告編成クリア
            // 連戦廃止＝負けたら即ラン敗北。
            if (fallenAtRoundStart > 0 && CountFallenFronts(state) > 0)
            {
                var homeReport = ResolveHomeBattle(state, round, resolver, rng, maxBattleTurns);
                if (homeReport != null) result.HomeBattleReports.Add(homeReport);
                if (!IsBattleWon(homeReport))
                {
                    result.HomeCollapsed = true;
                    result.EndingKind = VSPrototypeEndingKind.Defeat;
                }
            }
            else if (fallenAtRoundStart > 0)
            {
                // 全自領取り戻し成功 → 本拠地戦不発 → StartRound でセットした予告編成をクリア。
                // クリアしないと MapGUI に「攻めてこなかった敵」が残り続けて誤解を生む。
                state.Home.ClearEnemyComposition();
                state.Home.SetBattleMode(MapNodeBattleMode.None);
            }

            return result;
        }

        // 指定 BattleMode のマスのみを順次戦闘解決する 2 パス処理用サブルーチン。
        // Home はそもそも AllNodes に含まれるが BattleMode が Boss / None なのでフィルタで除外される。
        private void ResolveBattlesByMode(
            VSPrototypeMapState state, MapNodeBattleMode targetMode,
            BattleResolver resolver, Func<int> rng, int maxBattleTurns,
            VSPrototypeRoundResult result)
        {
            foreach (var node in state.AllNodes())
            {
                if (node.Kind == MapNodeKind.Home) continue;
                if (node.BattleMode != targetMode) continue;

                var nodeResult = ResolveNodeBattle(node, resolver, rng, maxBattleTurns);
                result.NodeResults.Add(nodeResult);
                ApplyNodeOutcome(node, nodeResult);
            }
        }

        // Pass 1.5：攻め込み成功で前線が後退した列の Defense マスを不発化する（§1.4.2）。
        // StartRound 時点の戦線状態でセットされた Defense が、Pass1 Attack の結果で
        // 「敵と接しなくなった／戦線終了」になった場合に取り消す責務。
        //  - 敵領占領 → 健在自領の防衛戦不発（敵領と接しなくなった自領）
        //  - 敵拠点占領 → 占領済み敵領の奪還戦不発（列の戦線完全終了）
        // 陥落自領（取り戻し戦）は敵領との接続と独立した戦闘なのでキャンセル対象外。
        private static void CancelObsoleteDefense(VSPrototypeMapState state)
        {
            for (int col = 0; col < 3; col++)
            {
                var territory  = state.GetNode(col, VSPrototypeMapState.LayerEnemyTerritory);
                var stronghold = state.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);

                // 敵拠点占領：占領済み敵領の奪還戦（territory が Defense）を不発化。
                if (stronghold.IsCaptured && territory.BattleMode == MapNodeBattleMode.Defense)
                {
                    territory.ClearEnemyComposition();
                    territory.SetBattleMode(MapNodeBattleMode.None);
                }

                // 敵領占領：健在自領の防衛戦を不発化。
                if (territory.IsCaptured)
                {
                    var friendly = state.GetNode(col, VSPrototypeMapState.LayerFriendly);
                    if (friendly.IsFallen) continue;
                    if (friendly.BattleMode != MapNodeBattleMode.Defense) continue;

                    friendly.ClearEnemyComposition();
                    friendly.SetBattleMode(MapNodeBattleMode.None);
                }
            }
        }

        // 戦闘解決のサブルーチン

        /// <summary>
        /// 1マスの戦闘解決。呼び出し前提：node.BattleMode != None（= 敵編成がセット済み）。
        /// 味方未配置時は不戦敗（PlayerWon=false・BattleReport=null）を返す。
        /// </summary>
        public VSPrototypeNodeResult ResolveNodeBattle(
            MapNode node, BattleResolver resolver, Func<int> rng, int maxBattleTurns)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            var result = new VSPrototypeNodeResult
            {
                Col = node.Col,
                Layer = node.Layer,
                Kind = node.Kind,
                BattleMode = node.BattleMode,
            };

            if (node.AssignedAllies.Count == 0)
            {
                // 味方未配置＝不戦敗（敵あり前提＝敵が全力侵攻）。
                // 状態遷移ルールは ApplyNodeOutcome の BattleMode 別分岐に委ねる。
                result.PlayerWon = false;
                return result;
            }

            // 戦闘実行：resolver の delegate は List<RuntimeUnit> を要求するため、ここでコピーを作って渡す
            //（マスの内部状態を resolver に直接渡さないことで、戦闘中の mutate からノード状態を守る）
            var allies  = node.AssignedAllies.ToList();
            var enemies = node.EnemyComposition.ToList();
            var (isAttackingSide, terrainStrength) = ResolveBattleSituation(node.Kind);
            var report = resolver(allies, enemies, maxBattleTurns, rng, isAttackingSide, terrainStrength);
            result.BattleReport = report;
            result.PlayerWon = IsBattleWon(report);
            return result;
        }

        // MapNodeKind から戦闘の攻め守りと地形強度を判定する。
        // 攻め=敵領／敵拠点、守り=自領／本拠地。強度は層が深いほど大きい（自領=軽微・敵領=中・敵拠点=重）。
        // 地形種別（TerrainKind）は現状 Neutral 固定。
        private static (bool isAttackingSide, TerrainStrength terrainStrength) ResolveBattleSituation(MapNodeKind kind)
        {
            switch (kind)
            {
                case MapNodeKind.Friendly:        return (false, TerrainStrength.Light);
                case MapNodeKind.EnemyTerritory:  return (true,  TerrainStrength.Medium);
                case MapNodeKind.EnemyStronghold: return (true,  TerrainStrength.Heavy);
                case MapNodeKind.Home:            return (false, TerrainStrength.Light);
                default:                          return (false, TerrainStrength.Light);
            }
        }

        /// <summary>
        /// 戦闘結果をマス状態に反映する。BattleMode 別の宣言的ルールで分岐し、
        /// 「敵不在マスが自動勝利で誤 Capture される」ような暗黙副作用を構造的に防ぐ。
        /// BattleMode=None は本来 ResolveAllBattles でスキップされる（呼ばれない）。
        /// </summary>
        public void ApplyNodeOutcome(MapNode node, VSPrototypeNodeResult result)
        {
            if (node == null || result == null) return;

            switch (result.BattleMode)
            {
                case MapNodeBattleMode.Defense:
                    ApplyDefenseOutcome(node, result);
                    break;
                case MapNodeBattleMode.Attack:
                    ApplyAttackOutcome(node, result);
                    break;
                case MapNodeBattleMode.Boss:
                case MapNodeBattleMode.None:
                default:
                    // Boss は ResolveBossBattle が直接 result.BossDefeated を扱う＝個別ルールなし。
                    // None は呼ばれない（スキップ済）。フォールスルー＝何もしない。
                    break;
            }
        }

        // 自陣最前線：自領（防衛 / 取り戻し）／占領済み敵領（奪還）の状態遷移。
        // 敗北して敵に占領されたマスは ClearAllies でマスから外す（Roster は配置中ユニットも
        // 含む所持の全集合なので Add 不要。HP は ResolveAllBattles 末尾で全 Roster 一括回復）。
        private static void ApplyDefenseOutcome(MapNode node, VSPrototypeNodeResult result)
        {
            if (node.Kind == MapNodeKind.Friendly)
            {
                if (result.PlayerWon)
                {
                    if (node.IsFallen)
                    {
                        node.RevertFallen();
                        result.MarkedRecovered = true;
                    }
                    // 健在自領の防衛勝利は維持＝何もしない
                }
                else
                {
                    if (!node.IsFallen)
                    {
                        node.ClearAllies();
                        node.MarkFallen();
                        result.MarkedFallen = true;
                    }
                    // 陥落自領の取り戻し戦敗北・未配置は陥落維持＝何もしない
                }
            }
            else if (node.Kind == MapNodeKind.EnemyTerritory)
            {
                // 占領済み敵領の奪還戦：敗北 or 未配置 → 取り戻され、勝利 → 制圧維持
                if (!result.PlayerWon && node.IsCaptured)
                {
                    node.ClearAllies();
                    node.RevertCapture();
                    result.MarkedReverted = true;
                }
            }
        }

        // 敵陣最前線：未制圧敵領／敵拠点の攻め込み戦の状態遷移。
        private static void ApplyAttackOutcome(MapNode node, VSPrototypeNodeResult result)
        {
            if (node.Kind != MapNodeKind.EnemyTerritory && node.Kind != MapNodeKind.EnemyStronghold)
                return;

            if (result.PlayerWon && !node.IsCaptured)
            {
                node.Capture();
                result.MarkedCaptured = true;
            }
            // 攻めず（未配置 = PlayerWon=false）／敗北 → 制圧失敗のみ＝次ラウンドも敵が居続ける
        }

        /// <summary>
        /// 本拠地戦の解決（R1-R6 で陥落自領が 1 つ以上残っている時に 1 回だけ呼ばれる）。
        /// プレイヤー未配置なら null＝敗北扱い。敵編成は StartRound でセットされた予告編成を
        /// そのまま使う（再抽選しない＝予告／実戦闘の編成一致＋決定論性保証）。
        /// 連戦は廃止：負けたら即ラン敗北（呼び出し側で EndingKind.Defeat）。
        /// </summary>
        public BattleReport ResolveHomeBattle(
            VSPrototypeMapState state, int round, BattleResolver resolver, Func<int> rng, int maxBattleTurns)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Home.AssignedAllies.Count == 0) return null;

            var allies  = state.Home.AssignedAllies.ToList();
            var enemies = state.Home.EnemyComposition.ToList();
            var (isAttackingSide, terrainStrength) = ResolveBattleSituation(MapNodeKind.Home);
            return resolver(allies, enemies, maxBattleTurns, rng, isAttackingSide, terrainStrength);
        }

        /// <summary>R7 本拠地ボス戦の解決。プレイヤー未配置なら null＝敗北扱い。</summary>
        public BattleReport ResolveBossBattle(
            VSPrototypeMapState state, BattleResolver resolver, Func<int> rng, int maxBattleTurns)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Home.AssignedAllies.Count == 0) return null;
            if (state.Home.EnemyComposition.Count == 0) return null;

            var allies  = state.Home.AssignedAllies.ToList();
            var enemies = state.Home.EnemyComposition.ToList();
            var (isAttackingSide, terrainStrength) = ResolveBattleSituation(MapNodeKind.Home);
            return resolver(allies, enemies, maxBattleTurns, rng, isAttackingSide, terrainStrength);
        }

        /// <summary>戦闘結果が勝利か（PerfectVictory / AdvantageousVictory）。null は敗北扱い。</summary>
        public bool IsBattleWon(BattleReport report)
        {
            if (report == null) return false;
            return report.Result == BattleResult.PerfectVictory
                || report.Result == BattleResult.AdvantageousVictory;
        }

        // ラン終了処理：メタ反映

        /// <summary>
        /// ラン終了時のメタ進行反映：
        ///   - 救出済なら UnlockedUnits.Add("bridget")
        ///   - エンディング種別を MetaProgressState に反映
        ///   - メタ通貨獲得量を加算
        ///   - RunCount を進める
        /// </summary>
        public int FinishRun(
            VSPrototypeMapState mapState,
            MetaProgressState meta,
            VSPrototypeEndingKind ending,
            int roundsCompleted,
            bool reachedBossRound,
            bool bossDefeated)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            // メタ通貨獲得量を計算して加算（SO 由来の式を Registry で解決）
            var rewardCtx = new MetaRewardContext(
                roundsCompleted: roundsCompleted,
                reachedBossRound: reachedBossRound,
                bridgetRescued: mapState.IsBridgetRescued,
                bossDefeated: bossDefeated,
                trueEnd: ending == VSPrototypeEndingKind.True);
            int reward = CalculateMetaReward(rewardCtx);
            meta.EarnMemories(reward);

            // エンディング種別を反映（UnlockUnit / MarkTrueEndReached / IncrementRunCount）
            _endingResolver.ApplyEndingToMeta(ending, mapState.IsBridgetRescued, meta);
            return reward;
        }

        /// <summary>
        /// メタ通貨獲得量を SO 由来の式で計算する。
        /// VSプロト範囲では Catalog に式が 1 件のみ登録される想定（最初の 1 件を採用）。
        /// </summary>
        private int CalculateMetaReward(MetaRewardContext ctx)
        {
            var formulas = _rewardFormulaCatalog.GetAll();
            if (formulas == null || formulas.Count == 0)
                throw new InvalidOperationException(
                    "MetaRewardFormulaCatalog に式が 1 件も登録されていません。" +
                    "Editor で SO アセットを生成してください（Echolos/Data メニュー）。");
            return formulas[0].Calculate(ctx);
        }
    }
}
