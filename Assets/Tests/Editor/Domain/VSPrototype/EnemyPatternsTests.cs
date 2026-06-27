// VSPrototypeEnemyPatterns のプール抽選契約検証。
// Stub IUnitCatalog（インメモリ・固定値）で決定論的に書く（[900 §3.8]）。
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Data.Roster;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class EnemyPatternsTests
    {
        // ── Stub IUnitCatalog ──
        // Resources を踏まず、id → 同じ Unit を返す軽量実装。
        // AvailableUpgrades は Roster プリセットに合わせ 3 件詰める（Lv 適用検証用）。
        // SortOrder は id 部分マッチ（"tank"/"paladin"/"archer" 等）で本物 Roster と
        // 同じ値を返す。これで抽選後ソート順の検証が決定論的に書ける。
        private sealed class StubUnitCatalog : IUnitCatalog
        {
            public Unit Get(string unitId)
            {
                var u = new Unit(unitId, $"name_{unitId}");
                u.SortOrder = ResolveSortOrder(unitId);
                foreach (var upId in UpgradeRoster.AttackerTankPreset())
                {
                    // Roster の id を再利用するが、Stub 内なので Magnitude=0 でも検証は通る
                    u.AvailableUpgrades.Add(
                        new UnitUpgrade(upId, upId, "", UpgradeKind.AtkBoost, 0));
                }
                return u;
            }
            public bool IsRegistered(string unitId) => true;
            public IEnumerable<string> GetAllIds() { yield break; }

            // 帝国軍 ID から SortOrder を推定（本物 AlliesRoster と同じ値を返す）
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

        [Test]
        public void CreateFriendlyDefenseEnemies_R1弱プール2体生成_SlotIndex連番()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateFriendlyDefenseEnemies(round: 1);
            Assert.AreEqual(2, enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
                Assert.AreEqual(i, enemies[i].SlotIndex);
        }

        // ── 自領防衛のラウンド帯プール切替（R1-2 弱 / R3-5 中 / R6 強）──

        [Test]
        public void 自領防衛_R1_弱プール_全員Lv1()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateFriendlyDefenseEnemies(round: 1);
            foreach (var e in enemies)
                Assert.AreEqual(1, e.BaseUnit.Level, $"弱プールは Lv1 のみ: {e.BaseUnit.Id}");
        }

        [Test]
        public void 自領防衛_R2_弱プール_全員Lv1()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateFriendlyDefenseEnemies(round: 2);
            foreach (var e in enemies)
                Assert.AreEqual(1, e.BaseUnit.Level);
        }

        [Test]
        public void 自領防衛_R3_中プール_タンク確定Lv1()
        {
            // 中プールは Required タンク Lv1 ＋ Random Lv1 × 残り
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateFriendlyDefenseEnemies(round: 3);
            bool hasTank = false;
            foreach (var e in enemies)
                if (e.BaseUnit.Id.Contains("tank")) { hasTank = true; break; }
            Assert.IsTrue(hasTank, "R3 自領防衛は中プール＝タンク確定枠あり");
            Assert.AreEqual(1, enemies[0].BaseUnit.Level, "SortOrder で先頭はタンク Lv1");
        }

        [Test]
        public void 自領防衛_R5_中プール_タンク確定Lv1()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateFriendlyDefenseEnemies(round: 5);
            Assert.AreEqual(1, enemies[0].BaseUnit.Level, "R5 自領防衛は中プール＝先頭タンク Lv1");
        }

        [Test]
        public void 自領防衛_R6_強プール_タンクとバッファ確定()
        {
            // 強プールは Required タンク Lv1 ＋ バッファ Lv1 ＋ Random Lv1 × 残り（試遊調整中・全 Lv1）
            // R6 自領は 5 体抽選なので「タンク＋バッファ＋ランダム 3」になる（敵拠点と完全一致）
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateFriendlyDefenseEnemies(round: 6);
            bool hasTank = false, hasBuffer = false;
            foreach (var e in enemies)
            {
                if (e.BaseUnit.Id.Contains("tank"))   hasTank = true;
                if (e.BaseUnit.Id.Contains("buffer")) hasBuffer = true;
            }
            Assert.IsTrue(hasTank,   "R6 自領防衛は強プール＝タンク確定枠あり");
            Assert.IsTrue(hasBuffer, "R6 自領防衛は強プール＝バッファ確定枠あり");
        }

        [Test]
        public void CreateForNode_Friendly_R6_強プール相当()
        {
            // CreateForNode(Friendly, round) ルートで強プールが選ばれることを担保
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateForNode(MapNodeKind.Friendly, round: 6);
            bool hasBuffer = false;
            foreach (var e in enemies)
                if (e.BaseUnit.Id.Contains("buffer")) { hasBuffer = true; break; }
            Assert.IsTrue(hasBuffer, "R6 自領防衛＝強プールにはバッファが必ず含まれる");
        }

        [Test]
        public void CreateEnemyStrongholdEnemies_5体生成_重複なし()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateEnemyStrongholdEnemies();
            Assert.AreEqual(5, enemies.Count);

            var seenIds = new HashSet<string>();
            foreach (var e in enemies)
                Assert.IsTrue(seenIds.Add(e.BaseUnit.Id), $"重複: {e.BaseUnit.Id}");
        }

        // ── Required（確定枠）─────────────────────────

        [Test]
        public void 中プール_タンクが必ず含まれる()
        {
            // rng が何を返してもタンク 1 体は Required で確定する
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateEnemyTerritoryEnemies();
            bool hasTank = false;
            foreach (var e in enemies)
                if (e.BaseUnit.Id.Contains("tank")) { hasTank = true; break; }
            Assert.IsTrue(hasTank, "中プールにはタンクが必ず含まれる");
        }

        [Test]
        public void 強プール_タンクとバッファが必ず含まれる()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateEnemyStrongholdEnemies();
            bool hasTank = false, hasBuffer = false;
            foreach (var e in enemies)
            {
                if (e.BaseUnit.Id.Contains("tank")) hasTank = true;
                if (e.BaseUnit.Id.Contains("buffer")) hasBuffer = true;
            }
            Assert.IsTrue(hasTank, "強プールにはタンクが必ず含まれる");
            Assert.IsTrue(hasBuffer, "強プールにはバッファが必ず含まれる");
        }

        // ── SortOrder 並び替え ────────────────────────

        [Test]
        public void 抽選後_SortOrder昇順で並ぶ_タンクが先頭()
        {
            // 強プールはタンク（SortOrder=1）が確定枠で含まれるので、ソート後の先頭はタンク
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateEnemyStrongholdEnemies();
            Assert.IsTrue(enemies[0].BaseUnit.Id.Contains("tank"),
                $"SortOrder 昇順なら先頭はタンク。実際: {enemies[0].BaseUnit.Id}");

            // SortOrder が単調非減少であることを確認
            for (int i = 1; i < enemies.Count; i++)
                Assert.LessOrEqual(enemies[i - 1].BaseUnit.SortOrder, enemies[i].BaseUnit.SortOrder,
                    $"SortOrder 逆転: slot {i - 1}={enemies[i - 1].BaseUnit.SortOrder} > slot {i}={enemies[i].BaseUnit.SortOrder}");
        }

        // ── Lv 適用 ────────────────────────────────────

        [Test]
        public void 強プール_タンク確定枠_Lv1のままAppliedUpgrades0件()
        {
            // 強プール Required タンクは試遊調整中 Lv1 で運用（全プール Lv1 統一・コミット 7dbddd7）。
            // SortOrder=1 で先頭スロットに来る。
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateEnemyStrongholdEnemies();
            var first = enemies[0];
            Assert.IsTrue(first.BaseUnit.Id.Contains("tank"));
            Assert.AreEqual(1, first.BaseUnit.Level, "Required タンクは Lv1（全プール Lv1 統一）");
            Assert.AreEqual(0, first.BaseUnit.AppliedUpgrades.Count, "Lv1 なら Upgrade 適用なし");
        }

        [Test]
        public void CreateForNode_Home_例外()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            Assert.Throws<System.InvalidOperationException>(
                () => p.CreateForNode(MapNodeKind.Home, round: 1));
        }

        // ── R7 ラスボス固定編成（A-c2 通常版・hasNotedPendantPower=true）─────────────

        [Test]
        public void CreateBossPattern_通常版_6体生成()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateBossPattern(hasNotedPendantPower: true);
            Assert.AreEqual(6, enemies.Count);
        }

        [Test]
        public void CreateBossPattern_通常版_皇太子がSlot1()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateBossPattern(hasNotedPendantPower: true);
            Assert.AreEqual(UniqueUnitIds.Prince, enemies[1].BaseUnit.Id,
                "皇太子はラスボス編成 Slot 1（前列 2 番目）に固定");
        }

        [Test]
        public void CreateBossPattern_通常版_SortOrderソート無視_編成順が保たれる()
        {
            // List 順：water_tank(1) → prince(99) → fire_buffer(4) → light_priest(4)
            //         → fire_mage(5) → light_paladin(2)
            // SortOrder ソートされていたら 1→2→4→4→5→99 になる。逆転（5→2）が
            // 残っていることで「ソートされていない＝順番保持」が証明される。
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateBossPattern(hasNotedPendantPower: true);

            Assert.AreEqual("imperial_water_tank",    enemies[0].BaseUnit.Id);
            Assert.AreEqual(UniqueUnitIds.Prince,     enemies[1].BaseUnit.Id);
            Assert.AreEqual("imperial_fire_buffer",   enemies[2].BaseUnit.Id);
            Assert.AreEqual("imperial_light_priest",  enemies[3].BaseUnit.Id);
            Assert.AreEqual("imperial_fire_mage",     enemies[4].BaseUnit.Id);
            Assert.AreEqual("imperial_light_paladin", enemies[5].BaseUnit.Id);
        }

        [Test]
        public void CreateBossPattern_通常版_取り巻きLv2_皇太子Lv1()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateBossPattern(hasNotedPendantPower: true);

            Assert.AreEqual(2, enemies[0].BaseUnit.Level, "水の大盾兵は Lv2");
            Assert.AreEqual(1, enemies[1].BaseUnit.Level, "皇太子は Lv1 固定（Lv 強化なし）");
            Assert.AreEqual(2, enemies[2].BaseUnit.Level, "炎の鼓舞師は Lv2");
            Assert.AreEqual(2, enemies[5].BaseUnit.Level, "光の騎士は Lv2");
        }

        // ── R7 ラスボス必敗版（A-c1・hasNotedPendantPower=false）─────────────

        [Test]
        public void CreateBossPattern_必敗版_6体生成_皇太子だけ闇に置換()
        {
            // hasNotedPendantPower=false → R7FinalBossDark()
            //   ＝通常編成と同じ取り巻き＋皇太子だけ闇皇太子に置換（編成見栄えのため）。
            // 取り巻きを倒しても闇皇太子の AttackUp +15 永続スタックが累積し T4 で全滅級＝詰む。
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateBossPattern(hasNotedPendantPower: false);

            Assert.AreEqual(6, enemies.Count, "必敗版も通常版と同じ 6 体編成");
            Assert.AreEqual("imperial_water_tank",    enemies[0].BaseUnit.Id);
            Assert.AreEqual(UniqueUnitIds.PrinceDark, enemies[1].BaseUnit.Id,
                "Slot 1 が闇皇太子に置換（通常版は UniqueUnitIds.Prince）");
            Assert.AreEqual("imperial_fire_buffer",   enemies[2].BaseUnit.Id);
            Assert.AreEqual("imperial_light_priest",  enemies[3].BaseUnit.Id);
            Assert.AreEqual("imperial_fire_mage",     enemies[4].BaseUnit.Id);
            Assert.AreEqual("imperial_light_paladin", enemies[5].BaseUnit.Id);
        }

        [Test]
        public void CreateBossPattern_必敗版_闇皇太子はLv1固定_取り巻きLv2()
        {
            var p = new VSPrototypeEnemyPatterns(new StubUnitCatalog(), rng: () => 0);
            var enemies = p.CreateBossPattern(hasNotedPendantPower: false);

            Assert.AreEqual(2, enemies[0].BaseUnit.Level, "水の大盾兵は Lv2");
            Assert.AreEqual(1, enemies[1].BaseUnit.Level, "闇皇太子は Lv1 固定（Lv 強化なし）");
            Assert.AreEqual(2, enemies[2].BaseUnit.Level, "炎の鼓舞師は Lv2");
            Assert.AreEqual(2, enemies[5].BaseUnit.Level, "光の騎士は Lv2");
        }
    }
}
