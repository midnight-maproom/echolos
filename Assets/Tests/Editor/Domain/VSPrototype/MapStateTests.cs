// VSPrototypeMapState の初期構造と GetNode・列挙の検証。
using System;
using System.Linq;
using NUnit.Framework;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MapStateTests
    {
        [Test]
        public void コンストラクタ_本拠地と9マスが初期化される()
        {
            var s = new VSPrototypeMapState();

            // 本拠地
            Assert.IsNotNull(s.Home);
            Assert.AreEqual(MapNodeKind.Home, s.Home.Kind);
            Assert.AreEqual(VSPrototypeMapState.HomeCol, s.Home.Col);
            Assert.AreEqual(VSPrototypeMapState.LayerHome, s.Home.Layer);

            // 9マス
            int friendlyCount = 0, territoryCount = 0, strongholdCount = 0;
            foreach (var n in s.AllNodes())
            {
                if (n.Kind == MapNodeKind.Friendly) friendlyCount++;
                else if (n.Kind == MapNodeKind.EnemyTerritory) territoryCount++;
                else if (n.Kind == MapNodeKind.EnemyStronghold) strongholdCount++;
            }
            Assert.AreEqual(3, friendlyCount);
            Assert.AreEqual(3, territoryCount);
            Assert.AreEqual(3, strongholdCount);
        }

        [Test]
        public void バルドゥイン拠点_左列の敵拠点のみ_フラグ_true()
        {
            var s = new VSPrototypeMapState();

            Assert.IsTrue(s.BalduinStronghold.IsBalduinStronghold);
            Assert.AreEqual(VSPrototypeMapState.BalduinCol, s.BalduinStronghold.Col);
            Assert.AreEqual(VSPrototypeMapState.LayerEnemyStronghold, s.BalduinStronghold.Layer);

            // 中・右列の敵拠点はバルドゥインではない
            for (int col = 0; col < VSPrototypeMapState.ColCount; col++)
            {
                var node = s.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);
                Assert.AreEqual(col == VSPrototypeMapState.BalduinCol, node.IsBalduinStronghold);
            }
        }

        [Test]
        public void GetNode_本拠地_LayerHome指定で取得()
        {
            var s = new VSPrototypeMapState();
            var home = s.GetNode(VSPrototypeMapState.HomeCol, VSPrototypeMapState.LayerHome);
            Assert.AreSame(s.Home, home);
        }

        [Test]
        public void GetNode_自領_敵領_敵拠点を取得()
        {
            var s = new VSPrototypeMapState();

            for (int col = 0; col < 3; col++)
            {
                var friendly  = s.GetNode(col, VSPrototypeMapState.LayerFriendly);
                var territory = s.GetNode(col, VSPrototypeMapState.LayerEnemyTerritory);
                var stronghold= s.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);

                Assert.AreEqual(MapNodeKind.Friendly,        friendly.Kind);
                Assert.AreEqual(MapNodeKind.EnemyTerritory,  territory.Kind);
                Assert.AreEqual(MapNodeKind.EnemyStronghold, stronghold.Kind);
                Assert.AreEqual(col, friendly.Col);
                Assert.AreEqual(col, territory.Col);
                Assert.AreEqual(col, stronghold.Col);
            }
        }

        [Test]
        public void GetNode_範囲外col_例外()
        {
            var s = new VSPrototypeMapState();
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetNode(-1, VSPrototypeMapState.LayerFriendly));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetNode(3, VSPrototypeMapState.LayerFriendly));
        }

        [Test]
        public void GetNode_範囲外layer_例外()
        {
            var s = new VSPrototypeMapState();
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetNode(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetNode(0, -1));
        }

        [Test]
        public void GetNode_本拠地_中列以外は例外()
        {
            var s = new VSPrototypeMapState();
            // Layer=0 (Home) は col=1 (HomeCol) のみ許可
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetNode(0, VSPrototypeMapState.LayerHome));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetNode(2, VSPrototypeMapState.LayerHome));
        }

        // ── MapNode.RevertCapture（戦線概念の取り戻され API） ──

        [Test]
        public void RevertCapture_占領済み敵領を未制圧に戻す()
        {
            var s = new VSPrototypeMapState();
            var territory = s.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            territory.Capture();
            Assert.IsTrue(territory.IsCaptured);

            territory.RevertCapture();
            Assert.IsFalse(territory.IsCaptured);
        }

        [Test]
        public void RevertCapture_未制圧マスでは何もしない_冪等()
        {
            var s = new VSPrototypeMapState();
            var territory = s.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            Assert.IsFalse(territory.IsCaptured);

            // 未制圧で呼んでも例外なし・状態変化なし
            Assert.DoesNotThrow(() => territory.RevertCapture());
            Assert.IsFalse(territory.IsCaptured);
        }

        [Test]
        public void RevertCapture_自領で例外()
        {
            var s = new VSPrototypeMapState();
            var friendly = s.GetNode(0, VSPrototypeMapState.LayerFriendly);
            Assert.Throws<InvalidOperationException>(() => friendly.RevertCapture());
        }

        [Test]
        public void RevertCapture_本拠地で例外()
        {
            var s = new VSPrototypeMapState();
            Assert.Throws<InvalidOperationException>(() => s.Home.RevertCapture());
        }

        [Test]
        public void RevertCapture_OnCaptureReverted発火()
        {
            var s = new VSPrototypeMapState();
            var stronghold = s.GetNode(0, VSPrototypeMapState.LayerEnemyStronghold);
            stronghold.Capture();

            MapNode notified = null;
            stronghold.OnCaptureReverted += n => notified = n;

            stronghold.RevertCapture();
            Assert.AreSame(stronghold, notified);
        }

        [Test]
        public void RevertCapture_未制圧時はイベント発火しない()
        {
            var s = new VSPrototypeMapState();
            var territory = s.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            bool fired = false;
            territory.OnCaptureReverted += _ => fired = true;

            territory.RevertCapture();
            Assert.IsFalse(fired);
        }

        // ── MapNode.RevertFallen（陥落自領の取り戻し API） ──

        [Test]
        public void RevertFallen_陥落自領を健在に戻す()
        {
            var s = new VSPrototypeMapState();
            var friendly = s.GetNode(0, VSPrototypeMapState.LayerFriendly);
            friendly.MarkFallen();
            Assert.IsTrue(friendly.IsFallen);

            friendly.RevertFallen();
            Assert.IsFalse(friendly.IsFallen);
        }

        [Test]
        public void RevertFallen_健在自領では何もしない_冪等()
        {
            var s = new VSPrototypeMapState();
            var friendly = s.GetNode(0, VSPrototypeMapState.LayerFriendly);
            Assert.IsFalse(friendly.IsFallen);

            Assert.DoesNotThrow(() => friendly.RevertFallen());
            Assert.IsFalse(friendly.IsFallen);
        }

        [Test]
        public void RevertFallen_敵領で例外()
        {
            var s = new VSPrototypeMapState();
            var territory = s.GetNode(0, VSPrototypeMapState.LayerEnemyTerritory);
            Assert.Throws<InvalidOperationException>(() => territory.RevertFallen());
        }

        [Test]
        public void RevertFallen_OnFallenReverted発火()
        {
            var s = new VSPrototypeMapState();
            var friendly = s.GetNode(0, VSPrototypeMapState.LayerFriendly);
            friendly.MarkFallen();

            MapNode notified = null;
            friendly.OnFallenReverted += n => notified = n;

            friendly.RevertFallen();
            Assert.AreSame(friendly, notified);
        }

        [Test]
        public void IsBridgetRescued_初期値false_MarkBridgetRescuedで真()
        {
            var s = new VSPrototypeMapState();
            Assert.IsFalse(s.IsBridgetRescued);

            s.MarkBridgetRescued();
            Assert.IsTrue(s.IsBridgetRescued);

            // 冪等：2回呼んでも true のまま
            s.MarkBridgetRescued();
            Assert.IsTrue(s.IsBridgetRescued);
        }

        [Test]
        public void IsBalduinRescuePlayed_初期値false_MarkBalduinRescuePlayedで真()
        {
            // 救援成功演出（B-d）の本ラン再生済フラグ：解放直後 1 回だけ発火する制御に使う。
            var s = new VSPrototypeMapState();
            Assert.IsFalse(s.IsBalduinRescuePlayed);

            s.MarkBalduinRescuePlayed();
            Assert.IsTrue(s.IsBalduinRescuePlayed);

            // 冪等
            s.MarkBalduinRescuePlayed();
            Assert.IsTrue(s.IsBalduinRescuePlayed);
        }

        [Test]
        public void IsBalduinRescuePlayed_IsBridgetRescuedとは独立した軸()
        {
            // 救出と演出再生は別の軸。救出してもまだ演出が流れていないラウンドが存在しうる。
            var s = new VSPrototypeMapState();
            s.MarkBridgetRescued();
            Assert.IsTrue(s.IsBridgetRescued);
            Assert.IsFalse(s.IsBalduinRescuePlayed,
                "MarkBridgetRescued 単体で IsBalduinRescuePlayed は立たない");
        }

        [Test]
        public void AllNodes_Home含む10ノードを列挙()
        {
            var s = new VSPrototypeMapState();
            Assert.AreEqual(10, s.AllNodes().Count());
        }

        [Test]
        public void FriendlyNodes_3つの自領を列挙()
        {
            var s = new VSPrototypeMapState();
            var list = s.FriendlyNodes().ToList();
            Assert.AreEqual(3, list.Count);
            for (int col = 0; col < 3; col++)
                Assert.AreEqual(MapNodeKind.Friendly, list[col].Kind);
        }

        // ── 救援済初期化オプション（バルドゥイン拠点扱い解除） ──

        [Test]
        public void balduinAlreadyRescued_true_左列敵拠点はバルドゥイン拠点扱いではない()
        {
            // バルドゥインが居らずすぐに降伏した世界線。左列敵拠点は中央・右列と同じ通常の敵拠点扱い。
            var s = new VSPrototypeMapState(balduinAlreadyRescued: true);

            for (int col = 0; col < VSPrototypeMapState.ColCount; col++)
            {
                var node = s.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold);
                Assert.IsFalse(node.IsBalduinStronghold,
                    $"col={col} 全ての敵拠点が通常扱い（IsBalduinStronghold=false）");
            }
            Assert.IsFalse(s.BalduinStronghold.IsBalduinStronghold,
                "BalduinStronghold プロパティ自体は左列敵拠点を指すが、IsBalduinStronghold は false");
        }

        [Test]
        public void balduinAlreadyRescued_true_全マス初期状態は通常と同じ_Captured等は立たない()
        {
            // バルドゥイン拠点扱いを外すだけで、Captured/Fallen 等の状態は触らない。
            var s = new VSPrototypeMapState(balduinAlreadyRescued: true);
            foreach (var n in s.AllNodes())
            {
                Assert.IsFalse(n.IsCaptured, $"{n.Kind} col={n.Col} layer={n.Layer} は初期 Captured=false");
                Assert.IsFalse(n.IsFallen, $"{n.Kind} col={n.Col} layer={n.Layer} は初期 Fallen=false");
            }
        }

        [Test]
        public void balduinAlreadyRescued_true_IsBridgetRescuedはfalseのまま()
        {
            // ラン中フラグなので「過去ラン救援済」だけでは立てない
            // （IsBalduinStronghold=false の左列敵拠点は制圧しても TryMarkBridgetRescued 経路に乗らない）
            var s = new VSPrototypeMapState(balduinAlreadyRescued: true);
            Assert.IsFalse(s.IsBridgetRescued);
        }

        [Test]
        public void balduinAlreadyRescued_デフォルトfalse_左列敵拠点はバルドゥイン拠点()
        {
            // 通常時は左列の敵拠点のみがバルドゥイン拠点。
            var s = new VSPrototypeMapState();
            Assert.IsTrue(s.BalduinStronghold.IsBalduinStronghold);
            for (int col = 1; col < VSPrototypeMapState.ColCount; col++)
            {
                Assert.IsFalse(
                    s.GetNode(col, VSPrototypeMapState.LayerEnemyStronghold).IsBalduinStronghold,
                    $"col={col} 中央/右列はバルドゥイン拠点ではない");
            }
        }

        [Test]
        public void balduinAlreadyRescued_デフォルトfalse_全マス初期状態()
        {
            var s = new VSPrototypeMapState(); // デフォルト引数
            foreach (var n in s.AllNodes())
            {
                Assert.IsFalse(n.IsCaptured, $"{n.Kind} col={n.Col} layer={n.Layer} は初期 Captured=false");
                Assert.IsFalse(n.IsFallen, $"{n.Kind} col={n.Col} layer={n.Layer} は初期 Fallen=false");
            }
        }
    }
}
