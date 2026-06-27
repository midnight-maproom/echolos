// VSPrototypeRoundManager の戦線概念（StartRound 最前線判定＋ ApplyNodeOutcome 取り戻され）検証。
//
// 【スコープ】
// - StartRound：列ごとの「自陣最前線（防衛/奪還）」「敵陣最前線（攻め込み）」に敵編成が湧くか
//   - 何も占領なし／敵領のみ占領／敵領＋敵拠点占領／自領陥落 の 4 ケース
// - ApplyNodeOutcome：占領済み敵領／敵拠点での敗北で RevertCapture が動くか
//   - 勝利時は制圧維持／敗北時は取り戻され／未占領敵領敗北は既存挙動（取り戻されない）
// - ResolveAllBattles：本拠地戦の発火タイミング（§1.5 猶予 = ラウンド開始時陥落のみ）
//
// 戦闘実行（resolver）には踏み込まず、Domain ロジックの結節点だけを検証する。
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;
using Echolos.Tests.Domain.Helpers;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class RoundManagerFrontlineTests
    {
        // ── Stub IUnitCatalog（EnemyPatternsTests と同等の軽量実装）──
        private sealed class StubUnitCatalog : IUnitCatalog
        {
            public Unit Get(string unitId)
            {
                var u = new Unit(unitId, $"name_{unitId}");
                u.SortOrder = ResolveSortOrder(unitId);
                return u;
            }
            public bool IsRegistered(string unitId) => true;
            public IEnumerable<string> GetAllIds() { yield break; }
            private static int ResolveSortOrder(string id)
            {
                if (id.Contains("tank")) return 1;
                if (id.Contains("paladin")) return 2;
                if (id.Contains("swordsman") || id.Contains("assassin")) return 3;
                if (id.Contains("buffer") || id.Contains("healer") || id.Contains("priest")) return 4;
                if (id.Contains("archer") || id.Contains("mage")) return 5;
                return 99;
            }
        }

        private VSPrototypeRoundManager BuildRoundManager()
        {
            var endingResolver = new VSPrototypeEndingResolver();
            var enemyPatterns = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var rewardCatalog = new StubMetaRewardFormulaCatalog();
            var metaProgress = new StubMetaProgressView();
            return new VSPrototypeRoundManager(endingResolver, enemyPatterns, rewardCatalog, metaProgress);
        }

        // ── StartRound：列ごと最前線への敵編成セット ──

        [Test]
        public void StartRound_何も占領なし_自領と敵領に敵編成_敵拠点はBattleModeNone()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            rm.StartRound(state, round: 1);

            for (int col = 0; col < 3; col++)
            {
                var friendly  = state.GetNode(col, VSPrototypeMapState.LayerFriendly);
                var territory = state.GetNode(col, VSPrototypeMapState.LayerEnemyTerritory);
                var stronghold= state.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);

                Assert.AreEqual(2, friendly.EnemyComposition.Count,
                    $"col={col} R1 弱プール＝自領（自陣最前線）に防衛戦の敵 2 体");
                Assert.AreEqual(MapNodeBattleMode.Defense, friendly.BattleMode,
                    $"col={col} 自領は Defense");
                Assert.Greater(territory.EnemyComposition.Count, 0,
                    $"col={col} 敵領（敵陣最前線）に攻め込み戦の敵");
                Assert.AreEqual(MapNodeBattleMode.Attack, territory.BattleMode,
                    $"col={col} 敵領は Attack");
                Assert.AreEqual(0, stronghold.EnemyComposition.Count,
                    $"col={col} 敵拠点は同列敵領未制圧なので攻め込み対象外＝空");
                Assert.AreEqual(MapNodeBattleMode.None, stronghold.BattleMode,
                    $"col={col} 敵拠点は BattleMode=None（旧バグ：自動勝利で誤 Capture される問題の根本対策）");
            }
        }

        [Test]
        public void StartRound_左敵領占領済み_左敵領で奪還戦_左自領は空_左敵拠点は攻め込み戦()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            // 左列の敵領を占領済みにする
            var leftTerritory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            leftTerritory.Capture();

            rm.StartRound(state, round: 3);

            var leftFriendly  = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            var leftStronghold= state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold);

            Assert.AreEqual(0, leftFriendly.EnemyComposition.Count,
                "左自領は最前線でないため空");
            Assert.Greater(leftTerritory.EnemyComposition.Count, 0,
                "占領済み左敵領が自陣最前線＝奪還戦の敵が湧く");
            Assert.Greater(leftStronghold.EnemyComposition.Count, 0,
                "左敵拠点は敵陣最前線＝攻め込み戦の敵が湧く");

            // 中央・右列は既存挙動（自領が最前線）
            var midFriendly = state.GetNode(1, VSPrototypeMapState.LayerFriendly);
            Assert.AreEqual(3, midFriendly.EnemyComposition.Count, "中央列は変化なし");
        }

        [Test]
        public void StartRound_左列全占領_左列は全マス空_戦線終了_BattleModeNone()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            // 左列の敵領＋敵拠点を両方占領済み
            state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory).Capture();
            state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold).Capture();

            rm.StartRound(state, round: 4);

            for (int layer = 1; layer <= 3; layer++)
            {
                var node = state.GetNode(0, layer);
                Assert.AreEqual(0, node.EnemyComposition.Count,
                    $"左列 layer={layer} は戦線終了で空");
                Assert.AreEqual(MapNodeBattleMode.None, node.BattleMode,
                    $"左列 layer={layer} は BattleMode=None（戦線外）");
            }
        }

        [Test]
        public void StartRound_自領陥落列_自領は取り戻し戦_敵領敵拠点は戦線外()
        {
            // 戦線管理：自領陥落でその先の敵領・敵拠点は隣接性が切れて戦線外（攻め込み不可）。
            // 陥落自領（本拠地隣接）の取り戻し戦のみ戦線として残る。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            state.GetNode(1, VSPrototypeMapState.LayerFriendly).MarkFallen();

            rm.StartRound(state, round: 5);

            var friendly  = state.GetNode(1, VSPrototypeMapState.LayerFriendly);
            var territory = state.GetNode(1, VSPrototypeMapState.LayerEnemyTerritory);
            var stronghold= state.GetNode(1, VSPrototypeMapState.LayerEnemyStronghold);

            Assert.AreEqual(3, friendly.EnemyComposition.Count,
                "陥落自領に取り戻し戦の敵 3 体（R 帯切替プール）");
            Assert.AreEqual(MapNodeBattleMode.Defense, friendly.BattleMode,
                "陥落自領は取り戻し戦＝Defense");
            Assert.AreEqual(0, territory.EnemyComposition.Count,
                "陥落列の敵領は戦線外＝攻め込み戦の敵編成なし");
            Assert.AreEqual(MapNodeBattleMode.None, territory.BattleMode,
                "陥落列の敵領は BattleMode=None");
            Assert.AreEqual(0, stronghold.EnemyComposition.Count,
                "陥落列の敵拠点も戦線外");
            Assert.AreEqual(MapNodeBattleMode.None, stronghold.BattleMode,
                "陥落列の敵拠点も BattleMode=None");
        }

        // ── ApplyNodeOutcome：占領済みマス敗北で取り戻され ──

        [Test]
        public void ApplyNodeOutcome_占領済み敵領で敗北_取り戻され_MarkedReverted()
        {
            // 占領済み敵領＝奪還戦＝BattleMode.Defense（StartRound で CalculateDefenseFrontline 経由）
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory.Capture();

            var result = new VSPrototypeNodeResult
            {
                Col = territory.Col, Layer = territory.Layer, Kind = territory.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = false,
            };

            rm.ApplyNodeOutcome(territory, result);

            Assert.IsFalse(territory.IsCaptured, "占領済み敵領で敗北 → 取り戻され");
            Assert.IsTrue(result.MarkedReverted);
            Assert.IsFalse(result.MarkedFallen);
        }

        [Test]
        public void ApplyNodeOutcome_占領済み敵拠点_BattleModeNone_状態遷移なし()
        {
            // 戦線概念：敵領＋敵拠点占領済みは列戦線終了＝BattleMode.None。
            // 旧仕様「占領済み敵拠点で敗北→取り戻され」は新仕様（戦線終了）では到達不能。
            // None のマスは ApplyNodeOutcome を呼んでも状態遷移しない（保険テスト）。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var stronghold = state.GetNode(2, VSPrototypeMapState.LayerEnemyStronghold);
            stronghold.Capture();

            var result = new VSPrototypeNodeResult
            {
                Col = stronghold.Col, Layer = stronghold.Layer, Kind = stronghold.Kind,
                BattleMode = MapNodeBattleMode.None,
                PlayerWon = false,
            };

            rm.ApplyNodeOutcome(stronghold, result);

            Assert.IsTrue(stronghold.IsCaptured, "BattleMode=None なら制圧維持＝取り戻されない");
            Assert.IsFalse(result.MarkedReverted);
        }

        [Test]
        public void ApplyNodeOutcome_占領済み敵領で勝利_制圧維持()
        {
            // 占領済み敵領＝奪還戦＝BattleMode.Defense、勝利＝奪還成功＝制圧維持
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory.Capture();

            var result = new VSPrototypeNodeResult
            {
                Col = territory.Col, Layer = territory.Layer, Kind = territory.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = true,
            };

            rm.ApplyNodeOutcome(territory, result);

            Assert.IsTrue(territory.IsCaptured, "占領済み敵領で勝利 → 制圧維持");
            Assert.IsFalse(result.MarkedReverted);
            Assert.IsFalse(result.MarkedCaptured, "既に Captured なので新規 Capture フラグは立たない");
        }

        [Test]
        public void ApplyNodeOutcome_未占領敵領で敗北_取り戻されは発生しない()
        {
            // 未占領敵領＝攻め込み戦＝BattleMode.Attack、敗北＝制圧失敗（取り戻されは奪還戦専用）
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            // 未占領のまま

            var result = new VSPrototypeNodeResult
            {
                Col = territory.Col, Layer = territory.Layer, Kind = territory.Kind,
                BattleMode = MapNodeBattleMode.Attack,
                PlayerWon = false,
            };

            rm.ApplyNodeOutcome(territory, result);

            Assert.IsFalse(territory.IsCaptured);
            Assert.IsFalse(result.MarkedReverted, "未占領敵領敗北は制圧失敗のみ＝取り戻されは発生しない");
        }

        // ── CanAssign：占領済み敵領も配置可能（奪還戦防衛配置のため）──

        [Test]
        public void CanAssign_占領済み敵領_StartRound後_奪還戦の防衛配置可()
        {
            // StartRound 経由で奪還戦の BattleMode=Defense がセットされて配置可。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory.Capture();
            rm.StartRound(state, round: 2);

            Assert.IsTrue(rm.CanAssign(state, territory, round: 2),
                "占領済み敵領は奪還戦の防衛配置として配置可");
        }

        [Test]
        public void CanAssign_戦線外の敵拠点_BattleModeNoneは常に配置不可()
        {
            // 戦線概念の純粋化：CanAssign は「戦闘が発生し得るか（BattleMode != None）」だけで判定。
            // 配置済みあり救済は廃止＝戦線変動で配置不可になったマスのユニットは
            // StartRound 末尾の自動回収で手持ちに戻る（DischargeUnreachableAssignments）。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            var stronghold = state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold);
            territory.Capture();
            stronghold.Capture();

            Assert.IsFalse(rm.CanAssign(state, stronghold, round: 1),
                "占領済み敵拠点（戦線外）は配置不可（純粋化）");
        }

        [Test]
        public void StartRound_攻略順序違反になった敵拠点配置は自動回収される()
        {
            // バグ再発防止のクリティカルテスト。
            // シナリオ：前 R で敵領占領→敵拠点に攻め込み配置→敵拠点戦敗北＋同列敵領奪還戦敗北
            //          → 敵領取り戻され（IsCaptured=false）＋敵拠点は未占領＋配置済み残存
            //          → 次 R の StartRound で敵拠点は CanAssign=false（攻略順序違反）
            //          → モーダルすら開けず手動回収不可＝StartRound で自動回収する設計
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var stronghold = state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold);

            var stranded = new Unit("stranded", "stranded")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            stronghold.AssignAlly(new RuntimeUnit(stranded, 0));

            // 同列敵領は未制圧（取り戻されたあと想定）。CanAssign(stronghold)=false の条件。
            Assert.IsFalse(rm.CanAssign(state, stronghold, round: 2),
                "前提：同列敵領未制圧で敵拠点は配置不可");

            rm.StartRound(state, round: 2);

            Assert.AreEqual(0, stronghold.AssignedAllies.Count,
                "攻略順序違反になった敵拠点の配置は自動回収される（手持ちに戻る）");
        }

        [Test]
        public void StartRound_戦線外の占領済み敵拠点配置も自動回収される()
        {
            // CanAssign 純粋化に伴う一貫対応：戦線終了で BattleMode=None になった占領済み敵拠点も
            // 配置が残っていれば自動回収する。プレイヤーは「占領後の番兵」を意図的に置いても
            // 次 R で勝手に手持ちに戻る＝戦線外配置に投資する意味がない設計。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            var stronghold = state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold);
            territory.Capture();
            stronghold.Capture();

            var sentinel = new Unit("sentinel", "sentinel")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            stronghold.AssignAlly(new RuntimeUnit(sentinel, 0));

            rm.StartRound(state, round: 3);

            Assert.AreEqual(MapNodeBattleMode.None, stronghold.BattleMode,
                "前提：列戦線終了で敵拠点は BattleMode=None");
            Assert.AreEqual(0, stronghold.AssignedAllies.Count,
                "戦線外の占領済み敵拠点配置も自動回収される");
        }

        [Test]
        public void StartRound_戦線外化した敵領配置も自動回収される()
        {
            // 自領陥落で敵領が戦線外化したケース。前 R で敵領占領＋配置済み→今 R で自領陥落
            // → 占領済み敵領は friendly.IsFallen で戦線外（ResolveDefenseNode が friendly を返す）
            // → 配置済みユニットは自動回収。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory.Capture();
            friendly.MarkFallen();

            var stranded = new Unit("stranded", "stranded")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            territory.AssignAlly(new RuntimeUnit(stranded, 0));

            rm.StartRound(state, round: 3);

            Assert.AreEqual(MapNodeBattleMode.None, territory.BattleMode,
                "前提：自領陥落列の占領済み敵領は戦線外");
            Assert.AreEqual(0, territory.AssignedAllies.Count,
                "戦線外化した敵領の配置も自動回収される");
        }

        [Test]
        public void CanAssign_未占領敵拠点_同列敵領未制圧_配置不可()
        {
            // 攻略順序依存：同列の敵領を制圧する前は敵拠点に配置できない。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var stronghold = state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold);

            Assert.IsFalse(rm.CanAssign(state, stronghold, round: 1),
                "同列敵領未制圧の敵拠点は配置不可（攻略順序依存）");
        }

        [Test]
        public void CanAssign_自領陥落列の敵領_StartRound後_配置不可()
        {
            // 戦線管理：自領陥落で敵領は隣接性切れの戦線外。配置不可。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            state.GetNode(0, VSPrototypeMapState.LayerFriendly).MarkFallen();
            rm.StartRound(state, round: 2);

            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            Assert.IsFalse(rm.CanAssign(state, territory, round: 2),
                "自領陥落列の敵領は戦線外＝配置不可");
        }

        [Test]
        public void CanAssign_自領陥落列の占領済み敵領_StartRound後_配置不可()
        {
            // 前ラウンドに敵領占領→今 R で自領陥落というケース（実バトルでは構造的に発生しない
            // が、ロジック単体としては念のため戦線外化を保証）。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory).Capture();
            state.GetNode(0, VSPrototypeMapState.LayerFriendly).MarkFallen();
            rm.StartRound(state, round: 2);

            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            Assert.IsFalse(rm.CanAssign(state, territory, round: 2),
                "自領陥落列の占領済み敵領も戦線外＝新規配置不可");
        }

        [Test]
        public void CanAssign_陥落自領_配置可能()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            friendly.MarkFallen();

            Assert.IsTrue(rm.CanAssign(state, friendly, round: 1),
                "陥落自領も配置可能（取り戻し戦の進攻配置のため）");
        }

        // ── R7 ボス戦：本拠地以外への配置禁止 ──

        [Test]
        public void CanAssign_R7_本拠地は配置可()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            Assert.IsTrue(rm.CanAssign(state, state.Home, round: VSPrototypeRoundManager.BossRound),
                "R7 でも本拠地のみ配置可（ボス戦の主舞台）");
        }

        [Test]
        public void CanAssign_R7_自領は配置不可()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            Assert.IsFalse(rm.CanAssign(state, friendly, round: VSPrototypeRoundManager.BossRound),
                "R7 では自領も配置不可（本拠地のみ配置可）");
        }

        [Test]
        public void CanAssign_R7_敵領は配置不可()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            Assert.IsFalse(rm.CanAssign(state, territory, round: VSPrototypeRoundManager.BossRound),
                "R7 では敵領も配置不可");
        }

        [Test]
        public void StartRound_R7_本拠地以外の既存配置は自動回収される()
        {
            // R6 で自領に配置されていたユニットが R7 突入時に手持ちに戻ることを確認。
            // StartRound 末尾の DischargeUnreachableAssignments が round=7 を渡されて
            // 本拠地以外を CanAssign=false 判定→ ClearAllies で回収される。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            var defender = new Unit("defender", "defender")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            friendly.AssignAlly(new RuntimeUnit(defender, 0));

            rm.StartRound(state, round: VSPrototypeRoundManager.BossRound);

            Assert.AreEqual(0, friendly.AssignedAllies.Count,
                "R7 開始時に自領の既存配置は自動回収される（手持ちに戻る）");
        }

        // ── 自領陥落マスへの敵編成セット（取り戻し戦の対象） ──

        [Test]
        public void StartRound_自領陥落_自領に取り戻し戦の敵編成セット()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var leftFriendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            leftFriendly.MarkFallen();

            rm.StartRound(state, round: 4);

            Assert.AreEqual(3, leftFriendly.EnemyComposition.Count,
                "陥落自領にも取り戻し戦の敵 3 体が湧く");
        }

        [Test]
        public void StartRound_自領陥落列の敵領は戦線外で攻め込み不可()
        {
            // 戦線管理：自領陥落で隣接性が切れる＝その先の敵領は攻め込み戦の対象外。
            // 陥落自領を取り返してから次 R で敵領に攻め込める設計。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var leftFriendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            leftFriendly.MarkFallen();

            rm.StartRound(state, round: 4);

            var leftTerritory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            Assert.AreEqual(0, leftTerritory.EnemyComposition.Count,
                "自領陥落列の敵領は戦線外＝攻め込み戦の敵編成なし");
            Assert.AreEqual(MapNodeBattleMode.None, leftTerritory.BattleMode);
        }

        // ── ApplyNodeOutcome：陥落自領の取り戻し ──

        [Test]
        public void ApplyNodeOutcome_陥落自領で勝利_取り戻し成功_MarkedRecovered()
        {
            // 陥落自領＝取り戻し戦＝BattleMode.Defense
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            friendly.MarkFallen();

            var result = new VSPrototypeNodeResult
            {
                Col = friendly.Col, Layer = friendly.Layer, Kind = friendly.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = true,
            };

            rm.ApplyNodeOutcome(friendly, result);

            Assert.IsFalse(friendly.IsFallen, "陥落自領で勝利 → 取り戻し成功");
            Assert.IsTrue(result.MarkedRecovered);
        }

        [Test]
        public void ApplyNodeOutcome_陥落自領で敗北_陥落維持()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            friendly.MarkFallen();

            var result = new VSPrototypeNodeResult
            {
                Col = friendly.Col, Layer = friendly.Layer, Kind = friendly.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = false,
            };

            rm.ApplyNodeOutcome(friendly, result);

            Assert.IsTrue(friendly.IsFallen, "陥落自領で敗北 → 陥落維持");
            Assert.IsFalse(result.MarkedRecovered);
            // 健在自領→陥落の MarkedFallen は発火しない（既に陥落しているため）
            Assert.IsFalse(result.MarkedFallen);
        }

        // ── ResolveAllBattles：本拠地戦の発火タイミング（§1.5 猶予 = ラウンド開始時陥落のみ） ──

        // 配置未済時は resolver が呼ばれない経路を使うので、ここでは throw する resolver で
        // 「呼ばれていないこと」も同時に保証する（呼ばれたら例外で fail）。
        private static BattleReport NeverCalledResolver(
            List<RuntimeUnit> allies, List<RuntimeUnit> enemies,
            int maxTurns, Func<int> rng, bool isAttackingSide, TerrainStrength terrainStrength)
        {
            throw new InvalidOperationException("resolver は呼ばれない想定");
        }

        [Test]
        public void ResolveAllBattles_ラウンド中に新規陥落_同ラウンドの本拠地戦は発生しない_猶予()
        {
            // §1.5「陥落しても**そのラウンドの本拠地戦は発生しない**（猶予あり）」
            // R1 開始時は陥落自領なし。プレイヤー全マス未配置 → 自領は MarkFallen される
            // が、ラウンド開始時スナップショット fallenAtRoundStart=0 のため本拠地戦は不発。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            rm.StartRound(state, round: 1);

            var result = rm.ResolveAllBattles(
                state, round: 1, resolver: NeverCalledResolver);

            Assert.AreEqual(3, rm.CountFallenFronts(state),
                "全マス未配置で 3 自領とも MarkFallen される（前提確認）");
            Assert.IsFalse(result.HomeCollapsed,
                "ラウンド中の新規陥落では本拠地戦は発火しない（次ラウンドまで猶予）");
            Assert.AreEqual(0, result.HomeBattleReports.Count,
                "本拠地戦が呼ばれていないこと");
            Assert.AreNotEqual(VSPrototypeEndingKind.Defeat, result.EndingKind,
                "EndingKind に Defeat は載らない");
        }

        [Test]
        public void ResolveAllBattles_敵拠点BattleModeNoneはスキップ_自動Captureしない_R2でも自領に敵編成湧く()
        {
            // 旧バグ再発防止のクリティカルテスト。
            // 旧実装：R1 開始時の敵拠点（敵領未制圧で敵編成なし）→ ResolveNodeBattle が
            //         「敵不在＝PlayerWon=true」で自動勝利扱い → ApplyNodeOutcome が Capture →
            //         R2 で stronghold.IsCaptured=true → CalculateDefenseFrontline が
            //         「列全制圧」と誤認 → 自領に敵編成セットされず「敵が攻めてこない」現象。
            // 新実装：BattleMode=None のマスは ResolveAllBattles でスキップ → Capture 副作用なし →
            //         R2 で正常に自領に敵編成セットされる。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            rm.StartRound(state, round: 1);

            // R1 全マス未配置で戦闘解決（resolver は呼ばれない経路）
            BattleReport NeverResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
                => throw new InvalidOperationException("敵拠点は BattleMode=None でスキップ＝呼ばれない");
            rm.ResolveAllBattles(state, round: 1, resolver: NeverResolver);

            // 敵拠点が Capture されていないこと（旧バグ：誤 Capture されていた）
            for (int col = 0; col < 3; col++)
            {
                Assert.IsFalse(
                    state.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold).IsCaptured,
                    $"col={col} 敵拠点は BattleMode=None でスキップ＝Capture されない");
            }

            // R2 開始時に自領に敵編成セットされる（旧バグでは消えていた）
            rm.StartRound(state, round: 2);
            for (int col = 0; col < 3; col++)
            {
                Assert.AreEqual(2, state.GetNode(col, VSPrototypeMapState.LayerFriendly).EnemyComposition.Count,
                    $"R2 col={col} 自領に敵編成 2 体（旧バグ：誤 Capture で 0 になっていた）");
                Assert.AreEqual(MapNodeBattleMode.Defense,
                    state.GetNode(col, VSPrototypeMapState.LayerFriendly).BattleMode,
                    $"R2 col={col} 自領は Defense");
            }
        }

        // ── 本拠地戦予告（StartRound で陥落自領>0 時に Home に予告編成セット） ──

        [Test]
        public void StartRound_陥落自領なし_本拠地に予告編成なし_BattleModeNone()
        {
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            rm.StartRound(state, round: 1);

            Assert.AreEqual(0, state.Home.EnemyComposition.Count,
                "陥落自領なし → 本拠地に予告編成セットされない");
            Assert.AreEqual(MapNodeBattleMode.None, state.Home.BattleMode);
        }

        [Test]
        public void StartRound_陥落自領あり_本拠地に予告編成セット_BattleModeDefense()
        {
            // ラウンド開始時に陥落自領が残っていれば本拠地に予告編成セット。
            // MapGUI のホバーツールチップで「攻めてくる敵」を予告表示できる。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            state.GetNode(0, VSPrototypeMapState.LayerFriendly).MarkFallen();

            rm.StartRound(state, round: 2);

            Assert.Greater(state.Home.EnemyComposition.Count, 0,
                "陥落自領>0 → 本拠地に予告編成セット");
            Assert.AreEqual(MapNodeBattleMode.Defense, state.Home.BattleMode,
                "本拠地予告は自陣最前線扱い＝Defense");
        }

        [Test]
        public void ResolveAllBattles_全自領取り戻し成功_本拠地予告クリア()
        {
            // ラウンド開始時に陥落自領があったが、ラウンド中の取り戻し戦で全部回収した場合、
            // 本拠地戦は不発＋予告編成は MapGUI 上で「攻めてこなかった敵」として残らないよう
            // クリアされる。fallenAtRoundStart は ResolveAllBattles 冒頭でスナップショットされる
            // ため、resolver 経由で実際の取り戻し戦勝利をシミュレートする必要がある。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            for (int col = 0; col < 3; col++)
                state.GetNode(col, VSPrototypeMapState.LayerFriendly).MarkFallen();

            rm.StartRound(state, round: 2);
            Assert.Greater(state.Home.EnemyComposition.Count, 0, "前提：予告編成セット");

            // 全 3 自領に味方配置（取り戻し戦勝利のため）
            for (int col = 0; col < 3; col++)
            {
                var ally = new Unit($"ally_{col}", $"ally{col}")
                {
                    MaxHP = 100,
                    CurrentHP = 100,
                    SortOrder = 1,
                };
                state.GetNode(col, VSPrototypeMapState.LayerFriendly)
                    .AssignAlly(new RuntimeUnit(ally, 0));
            }

            // resolver は常に勝利を返す＝全 3 自領で取り戻し戦勝利＝全自領健在へ
            BattleReport WinResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
                => new BattleReport { Result = BattleResult.PerfectVictory };

            var result = rm.ResolveAllBattles(state, round: 2, resolver: WinResolver);

            for (int col = 0; col < 3; col++)
                Assert.IsFalse(state.GetNode(col, VSPrototypeMapState.LayerFriendly).IsFallen,
                    $"col={col} 取り戻し戦勝利で IsFallen=false");

            Assert.AreEqual(0, state.Home.EnemyComposition.Count,
                "全自領取り戻し成功 → 本拠地予告クリア");
            Assert.AreEqual(MapNodeBattleMode.None, state.Home.BattleMode);
            Assert.AreNotEqual(VSPrototypeEndingKind.Defeat, result.EndingKind);
            Assert.IsFalse(result.HomeCollapsed);
        }

        [Test]
        public void ResolveHomeBattle_予告編成と実戦闘編成が一致_決定論性保証()
        {
            // StartRound で セットした予告編成（state.Home.EnemyComposition）が
            // ResolveHomeBattle に渡される＝再抽選しない。MapGUI の予告と実戦闘で
            // 同じ敵が出ることを保証。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            state.GetNode(0, VSPrototypeMapState.LayerFriendly).MarkFallen();

            rm.StartRound(state, round: 3);

            // 予告編成スナップショット
            var predicted = state.Home.EnemyComposition.Select(r => r.BaseUnit.Id).ToList();
            Assert.IsNotEmpty(predicted, "前提：予告編成セット");

            // 本拠地に味方配置（戦闘発生のため）
            var ally = new Unit("test_ally", "test")
            {
                MaxHP = 100,
                CurrentHP = 100,
                SortOrder = 1,
            };
            state.Home.AssignAlly(new RuntimeUnit(ally, 0));

            List<string> actualEnemies = null;
            BattleReport CaptureResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
            {
                actualEnemies = e.Select(u => u.BaseUnit.Id).ToList();
                return null; // BattleReport なしでも IsBattleWon=false で敗北扱いになる（テスト目的では問題なし）
            }

            rm.ResolveAllBattles(state, round: 3, resolver: CaptureResolver);

            Assert.IsNotNull(actualEnemies, "resolver が本拠地戦で呼ばれた");
            CollectionAssert.AreEqual(predicted, actualEnemies,
                "予告編成と実戦闘編成が完全一致＝決定論性保証");
        }

        [Test]
        public void ResolveAllBattles_前ラウンドから陥落継続_本拠地戦発火_未配置で敗北()
        {
            // 前ラウンドで陥落した自領がそのまま残ったまま次ラウンドへ → ラウンド開始時
            // fallenAtRoundStart>0、取り戻し失敗で本拠地戦発火、本拠地未配置で即敗北。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            state.GetNode(0, VSPrototypeMapState.LayerFriendly).MarkFallen();

            rm.StartRound(state, round: 2);

            // 全マス未配置：resolver は呼ばれない（hasAllies=false → PlayerWon=false 即返り）。
            // 本拠地戦も Home.AssignedAllies.Count == 0 で null 返り＝敗北扱い。
            var result = rm.ResolveAllBattles(
                state, round: 2, resolver: NeverCalledResolver);

            Assert.IsTrue(result.HomeCollapsed,
                "前ラウンドから陥落継続 → 本拠地戦発火・本拠地未配置で敗北");
            Assert.AreEqual(VSPrototypeEndingKind.Defeat, result.EndingKind);
        }

        // ── R1→R2 進行で自領に敵編成が再セットされるか（試遊で「2R 開始時に敵が消える」報告の調査用） ──

        [Test]
        public void StartRound_R1健在自領防衛勝利_R2で自領に敵編成が再セット()
        {
            // 試遊報告：「R1 で自領防衛勝利した後、R2 冒頭演出を見たら自領の敵が消えてる」
            // ロジック単体では R2 StartRound で必ず自領に敵編成セットされるはず。
            // 落ちれば StartRound 本体のバグ、通れば Unity 実行時の別経路で消えている。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            rm.StartRound(state, round: 1);

            // R1 開始時点で 3 自領とも敵編成が湧いていることを前提確認
            for (int col = 0; col < 3; col++)
            {
                Assert.Greater(
                    state.GetNode(col, VSPrototypeMapState.LayerFriendly).EnemyComposition.Count, 0,
                    $"前提：R1 col={col} 自領に敵編成");
            }

            // R1 戦闘で全勝（敵全滅／自領陥落なし／敵領未制圧）を想定。
            // carryOver で復元される味方の有無に関わらず敵編成は再セットされるはず。
            // 念のため col=0 のみ味方 1 体配置済みにして carryOver 経路も動かす。
            var sampleUnit = new Unit("test_ally", "test ally")
            {
                MaxHP = 100,
                CurrentHP = 100,
                SortOrder = 1,
            };
            state.GetNode(0, VSPrototypeMapState.LayerFriendly)
                .AssignAlly(new RuntimeUnit(sampleUnit, 0));

            rm.StartRound(state, round: 2);

            for (int col = 0; col < 3; col++)
            {
                Assert.Greater(
                    state.GetNode(col, VSPrototypeMapState.LayerFriendly).EnemyComposition.Count, 0,
                    $"R2 col={col} 自領に敵編成が再セットされる（敵領未制圧・自領健在）");
            }

            // col=0 の味方が carryOver で復元されていることも確認
            Assert.AreEqual(1, state.GetNode(0, VSPrototypeMapState.LayerFriendly).AssignedAllies.Count,
                "carryOver で味方配置も復元される");
        }

        // ── 戦闘不能ユニットの配置維持＋敗北マスでマスから外す ──

        [Test]
        public void StartRound_戦闘不能ユニットもcarryOverで配置維持される()
        {
            // 旧仕様：CaptureCarryOverPlacements が CurrentHP > 0 でフィルタ → 戦闘不能ユニットは
            //         次ラウンドで配置外れる（負傷で離脱の名残）
            // 新仕様：戦闘不能でも配置維持。HP は ResolveAllBattles 末尾で Bootstrap が Roster
            //         全員一括回復する想定なので、ここ（Domain 単体）の Assert は配置維持のみ。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);

            var koUnit = new Unit("ko_unit", "ko")
            {
                MaxHP = 100,
                CurrentHP = 0, // 戦闘不能
                SortOrder = 1,
            };
            friendly.AssignAlly(new RuntimeUnit(koUnit, 0));

            rm.StartRound(state, round: 2);

            Assert.AreEqual(1, friendly.AssignedAllies.Count,
                "戦闘不能ユニットも次ラウンドに配置維持される（HP 回復は Presentation 責務）");
        }

        [Test]
        public void ApplyNodeOutcome_健在自領で敗北_マスから外される()
        {
            // 敗北して敵に占領された自領の配置ユニットはマスから外される。
            // Roster は所持の全集合（配置中も含む）なので Domain は Add しない。
            // HP 回復も Bootstrap で一括（VSプロトは戦闘中以外 HP=MaxHP 前提）。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);

            var unit1 = new Unit("unit_1", "u1") { MaxHP = 100, CurrentHP = 50, SortOrder = 1 };
            var unit2 = new Unit("unit_2", "u2") { MaxHP = 100, CurrentHP = 0, SortOrder = 2 };
            friendly.AssignAlly(new RuntimeUnit(unit1, 0));
            friendly.AssignAlly(new RuntimeUnit(unit2, 1));

            var result = new VSPrototypeNodeResult
            {
                Col = 0, Layer = friendly.Layer, Kind = friendly.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = false,
            };

            rm.ApplyNodeOutcome(friendly, result);

            Assert.IsTrue(friendly.IsFallen, "自領陥落");
            Assert.AreEqual(0, friendly.AssignedAllies.Count, "配置はクリアされる（マスから外れる）");
        }

        [Test]
        public void ApplyNodeOutcome_占領済み敵領で敗北_マスから外される()
        {
            // 占領済み敵領の奪還戦敗北＝取り戻され。配置ユニットはマスから外れる。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory.Capture();

            var unit = new Unit("guard", "guard") { MaxHP = 100, CurrentHP = 80, SortOrder = 1 };
            territory.AssignAlly(new RuntimeUnit(unit, 0));

            var result = new VSPrototypeNodeResult
            {
                Col = 0, Layer = territory.Layer, Kind = territory.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = false,
            };

            rm.ApplyNodeOutcome(territory, result);

            Assert.IsFalse(territory.IsCaptured, "取り戻された");
            Assert.AreEqual(0, territory.AssignedAllies.Count, "配置はクリアされる");
        }

        // ── 2 パス解決：敵領占領で同ラウンドの自領防衛を不発化 ──

        [Test]
        public void ResolveAllBattles_敵領攻め込み成功_同列の自領防衛は免除される()
        {
            // 攻め込み戦勝利 → territory.Capture → 自領が前線から外れる → 防衛戦不発
            // 健在自領は防衛戦の敵編成セット済みだったが、戦闘発生なしで通り過ぎる。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            rm.StartRound(state, round: 1);

            // 敵領 col=0 にだけ味方配置（攻め込み戦勝利のため）
            var attacker = new Unit("attacker", "atk")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory)
                .AssignAlly(new RuntimeUnit(attacker, 0));

            // resolver：味方ありなら勝利、味方なしなら ResolveNodeBattle が早期 false 返却で resolver は呼ばれない
            BattleReport WinResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
                => new BattleReport { Result = BattleResult.PerfectVictory };

            var result = rm.ResolveAllBattles(state, round: 1, resolver: WinResolver);

            // 敵領 col=0：占領済み
            Assert.IsTrue(state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory).IsCaptured,
                "前提：col=0 敵領攻め込み成功");

            // 自領 col=0：防衛戦は不発（BattleMode=None・敵編成クリア・陥落していない）
            var friendly0 = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            Assert.IsFalse(friendly0.IsFallen,
                "col=0 自領は防衛戦が免除されたので味方未配置でも陥落しない");
            Assert.AreEqual(MapNodeBattleMode.None, friendly0.BattleMode,
                "col=0 自領 BattleMode は None に降格");
            Assert.AreEqual(0, friendly0.EnemyComposition.Count,
                "col=0 自領の敵編成はクリア");

            // 比較：他列（col=1,2）は敵領未占領で自領防衛戦が通常通り発生（味方未配置→陥落）
            Assert.IsTrue(state.GetNode(1, VSPrototypeMapState.LayerFriendly).IsFallen,
                "col=1 敵領未占領のため自領防衛戦発生＆未配置で陥落");
            Assert.IsTrue(state.GetNode(2, VSPrototypeMapState.LayerFriendly).IsFallen,
                "col=2 同上");
        }

        [Test]
        public void ResolveAllBattles_敵領攻め込み失敗_自領防衛戦は通常通り発生()
        {
            // 攻め込み戦に味方配置なし → 占領できない → 自領防衛戦はキャンセルされない
            // → 自領も味方未配置のため陥落
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            rm.StartRound(state, round: 1);

            // 全マス未配置で戦闘解決（resolver は呼ばれない経路）
            BattleReport NeverResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
                => throw new InvalidOperationException("呼ばれない想定");

            rm.ResolveAllBattles(state, round: 1, resolver: NeverResolver);

            Assert.IsFalse(state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory).IsCaptured);
            Assert.IsTrue(state.GetNode(0, VSPrototypeMapState.LayerFriendly).IsFallen,
                "敵領未占領 → 自領防衛戦は発生 → 未配置で陥落");
        }

        [Test]
        public void ResolveAllBattles_別列の敵領占領は陥落自領の取り戻し戦に影響しない()
        {
            // 「敵領と接続喪失で防衛免除」は同列の健在自領にのみ作用する。
            // 別列の敵領占領は陥落自領（取り戻し戦）に影響しない（独立した戦闘軸）。
            // 新仕様では陥落列の敵領自体が戦線外なので、別列で検証する。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly0 = state.GetNode(0, VSPrototypeMapState.LayerFriendly);
            friendly0.MarkFallen(); // col=0 が前ラウンドで陥落していた前提

            rm.StartRound(state, round: 2);

            // 別列（col=1）の敵領に味方配置（攻め込み成功）
            var attacker = new Unit("attacker", "atk")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            state.GetNode(1, VSPrototypeMapState.LayerEnemyTerritory)
                .AssignAlly(new RuntimeUnit(attacker, 0));

            BattleReport WinResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
                => new BattleReport { Result = BattleResult.PerfectVictory };

            rm.ResolveAllBattles(state, round: 2, resolver: WinResolver);

            Assert.IsTrue(state.GetNode(1, VSPrototypeMapState.LayerEnemyTerritory).IsCaptured,
                "別列 col=1 敵領占領済み");
            // col=0 の陥落自領は取り戻し戦の対象＝Defense マスとして残り、味方未配置で陥落維持
            Assert.IsTrue(friendly0.IsFallen,
                "別列の敵領占領は陥落自領の取り戻し戦に影響しない（独立した戦闘軸）");
        }

        [Test]
        public void ResolveAllBattles_敵拠点攻め込み成功_同列の占領済み敵領の奪還戦は免除される()
        {
            // 攻め込み戦勝利で敵拠点 Capture → 列の戦線完全終了 → 占領済み敵領の奪還戦不発
            // （StartRound 時点で territory は IsCaptured=true・stronghold 未占領 →
            //  territory に Defense（奪還戦）がセットされている状態を Pass1.5 で取り消す）
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var territory0 = state.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory0.Capture(); // 前ラウンドで敵領占領済み前提

            rm.StartRound(state, round: 2);

            // 前提確認：territory に Defense（奪還戦）、stronghold に Attack がセット
            Assert.AreEqual(MapNodeBattleMode.Defense, territory0.BattleMode,
                "前提：占領済み敵領は StartRound で奪還戦の Defense マス");
            Assert.AreEqual(MapNodeBattleMode.Attack,
                state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold).BattleMode,
                "前提：敵拠点は StartRound で攻め込み戦の Attack マス");

            // 敵拠点 col=0 に味方配置（攻め込み戦勝利のため）
            var attacker = new Unit("attacker", "atk")
            {
                MaxHP = 100, CurrentHP = 100, SortOrder = 1,
            };
            state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold)
                .AssignAlly(new RuntimeUnit(attacker, 0));

            BattleReport WinResolver(List<RuntimeUnit> a, List<RuntimeUnit> e, int t,
                Func<int> r, bool isAtk, TerrainStrength ts)
                => new BattleReport { Result = BattleResult.PerfectVictory };

            rm.ResolveAllBattles(state, round: 2, resolver: WinResolver);

            // 敵拠点 col=0：占領済み
            Assert.IsTrue(state.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold).IsCaptured,
                "前提：col=0 敵拠点攻め込み成功");

            // 占領済み敵領 col=0：奪還戦は不発（IsCaptured 維持・BattleMode=None・敵編成クリア）
            Assert.IsTrue(territory0.IsCaptured,
                "占領済み敵領は奪還戦免除で取り返されない");
            Assert.AreEqual(MapNodeBattleMode.None, territory0.BattleMode,
                "占領済み敵領 BattleMode は None に降格");
            Assert.AreEqual(0, territory0.EnemyComposition.Count,
                "占領済み敵領の敵編成はクリア");
        }

        [Test]
        public void StartRound_本拠地配置も次ラウンドに維持される()
        {
            // 旧バグ：CaptureCarryOverPlacements が本拠地（Home）を明示的にスキップしていたため、
            //         R1 で本拠地に配置したユニットが R2 開始時に消えていた（毎ラウンド手持ちに戻る）。
            //         3c の「R1-R6 でも本拠地戦予告に備えて配置」仕様への追従漏れ。
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();

            var sentinel = new Unit("sentinel", "sentinel")
            {
                MaxHP = 100,
                CurrentHP = 100,
                SortOrder = 1,
            };
            state.Home.AssignAlly(new RuntimeUnit(sentinel, 0));

            rm.StartRound(state, round: 2);

            Assert.AreEqual(1, state.Home.AssignedAllies.Count,
                "本拠地配置も他マスと同じく carryOver で次ラウンドに維持される");
            Assert.AreEqual("sentinel", state.Home.AssignedAllies[0].BaseUnit.Id);
        }

        [Test]
        public void ApplyNodeOutcome_健在自領で防衛勝利_配置維持()
        {
            // 防衛勝利マスは配置維持（次ラウンドへ carryOver される）
            var rm = BuildRoundManager();
            var state = new VSPrototypeMapState();
            var friendly = state.GetNode(0, VSPrototypeMapState.LayerFriendly);

            var unit = new Unit("victor", "victor") { MaxHP = 100, CurrentHP = 30, SortOrder = 1 };
            friendly.AssignAlly(new RuntimeUnit(unit, 0));

            var result = new VSPrototypeNodeResult
            {
                Col = 0, Layer = friendly.Layer, Kind = friendly.Kind,
                BattleMode = MapNodeBattleMode.Defense,
                PlayerWon = true,
            };

            rm.ApplyNodeOutcome(friendly, result);

            Assert.IsFalse(friendly.IsFallen);
            Assert.AreEqual(1, friendly.AssignedAllies.Count, "防衛勝利は配置維持");
        }
    }
}
