// MetaProgressState のロジック（通貨／周回／解禁／強化／TrueEnd フラグ）。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Meta;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MetaProgressStateTests
    {
        [Test]
        public void 初期状態_全項目が初期値()
        {
            var m = new MetaProgressState();
            Assert.AreEqual(0, m.Memories);
            Assert.AreEqual(0, m.RunCount);
            Assert.IsFalse(m.HasReachedTrueEnd);
            Assert.IsFalse(m.HasFirstReachedBoss);
            Assert.IsFalse(m.HasRescuedBalduin);
            Assert.IsFalse(m.HasNotedPendantPower);
            Assert.AreEqual(0, m.UnlockedUnits.Count);
            Assert.AreEqual(0, m.AppliedUpgrades.Count);
            Assert.AreEqual(0, m.SeenStorySceneIds.Count);
        }

        // ── ストーリー既見管理 ──

        [Test]
        public void HasSeenStoryScene_初期値false_MarkStorySceneSeenで真()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.HasSeenStoryScene("b_a_balduin"));

            m.MarkStorySceneSeen("b_a_balduin");
            Assert.IsTrue(m.HasSeenStoryScene("b_a_balduin"));

            // 別 ID は別軸
            Assert.IsFalse(m.HasSeenStoryScene("b_b1_letter"));
        }

        [Test]
        public void MarkStorySceneSeen_冪等()
        {
            var m = new MetaProgressState();
            m.MarkStorySceneSeen("b_a_balduin");
            m.MarkStorySceneSeen("b_a_balduin"); // 2 度呼んでも問題なし

            Assert.AreEqual(1, m.SeenStorySceneIds.Count);
        }

        [Test]
        public void MarkStorySceneSeen_空文字_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentException>(() => m.MarkStorySceneSeen(""));
            Assert.Throws<ArgumentException>(() => m.MarkStorySceneSeen(null));
        }

        [Test]
        public void HasSeenStoryScene_空文字_false()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.HasSeenStoryScene(""));
            Assert.IsFalse(m.HasSeenStoryScene(null));
        }

        [Test]
        public void LoadFromSerializedState_seenStorySceneIds復元()
        {
            var m = new MetaProgressState();
            m.LoadFromSerializedState(
                memories: 0, runCount: 0, hasReachedTrueEnd: false,
                unlockedUnits: null, appliedUpgrades: null,
                seenStorySceneIds: new[] { "b_a_balduin", "opening" });

            Assert.IsTrue(m.HasSeenStoryScene("b_a_balduin"));
            Assert.IsTrue(m.HasSeenStoryScene("opening"));
            Assert.AreEqual(2, m.SeenStorySceneIds.Count);
        }

        // ── メタ通貨 ──

        [Test]
        public void EarnMemories_加算される()
        {
            var m = new MetaProgressState();
            m.EarnMemories(100);
            Assert.AreEqual(100, m.Memories);
            m.EarnMemories(50);
            Assert.AreEqual(150, m.Memories);
        }

        [Test]
        public void EarnMemories_負値_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentOutOfRangeException>(() => m.EarnMemories(-1));
        }

        [Test]
        public void SpendMemories_残高足りる_成功()
        {
            var m = new MetaProgressState();
            m.EarnMemories(100);
            bool ok = m.SpendMemories(60);
            Assert.IsTrue(ok);
            Assert.AreEqual(40, m.Memories);
        }

        [Test]
        public void SpendMemories_残高不足_失敗_状態維持()
        {
            var m = new MetaProgressState();
            m.EarnMemories(50);
            bool ok = m.SpendMemories(100);
            Assert.IsFalse(ok);
            Assert.AreEqual(50, m.Memories);
        }

        [Test]
        public void SpendMemories_負値_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentOutOfRangeException>(() => m.SpendMemories(-1));
        }

        // ── 周回数 ──

        [Test]
        public void IncrementRunCount_1ずつ増える()
        {
            var m = new MetaProgressState();
            m.IncrementRunCount();
            m.IncrementRunCount();
            Assert.AreEqual(2, m.RunCount);
        }

        // ── 解禁 ──

        [Test]
        public void UnlockUnit_初回true_2回目はfalse_冪等()
        {
            var m = new MetaProgressState();
            Assert.IsTrue(m.UnlockUnit(MetaUnitIds.Bridget));
            Assert.IsFalse(m.UnlockUnit(MetaUnitIds.Bridget));
            Assert.IsTrue(m.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, m.UnlockedUnits.Count);
        }

        [Test]
        public void UnlockUnit_null_or_empty_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentException>(() => m.UnlockUnit(null));
            Assert.Throws<ArgumentException>(() => m.UnlockUnit(""));
        }

        [Test]
        public void IsUnitUnlocked_未登録はfalse()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.IsFalse(m.IsUnitUnlocked("nonexistent"));
        }

        // ── 強化適用 ──

        // cap 値は SO（MetaUpgradeDefinitionSO）側で持つため、
        // MetaProgressState 単体のテストでは cap 引数を仕様値（PrincessLevel=2, InitialUnit=3）で直接渡す。
        // SO ↔ 仕様値の整合性は MetaUpgradeCatalogTests で別途検証。
        private const int CapPrincessLevel = 2;
        private const int CapInitialUnit = 3;

        [Test]
        public void ApplyUpgrade_初回_Lv1になる()
        {
            var m = new MetaProgressState();
            bool ok = m.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, CapPrincessLevel);
            Assert.IsTrue(ok);
            Assert.AreEqual(1, m.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
        }

        [Test]
        public void ApplyUpgrade_cap到達後はfalse()
        {
            var m = new MetaProgressState();
            // CapPrincessLevel = 2 なので2回まで
            Assert.IsTrue(m.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, CapPrincessLevel));
            Assert.IsTrue(m.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, CapPrincessLevel));
            Assert.IsFalse(m.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, CapPrincessLevel));
            Assert.AreEqual(2, m.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
        }

        [Test]
        public void ApplyUpgrade_初期ユニット強化_3回まで適用可()
        {
            var m = new MetaProgressState();
            for (int i = 1; i <= CapInitialUnit; i++)
            {
                bool ok = m.ApplyUpgrade(MetaUpgradeIds.InitialUnit, CapInitialUnit);
                Assert.IsTrue(ok, $"{i} 回目の適用は成功すべき");
                Assert.AreEqual(i, m.GetUpgradeLevel(MetaUpgradeIds.InitialUnit));
            }
            // 4回目は失敗
            Assert.IsFalse(m.ApplyUpgrade(MetaUpgradeIds.InitialUnit, CapInitialUnit));
            Assert.AreEqual(CapInitialUnit, m.GetUpgradeLevel(MetaUpgradeIds.InitialUnit));
        }

        [Test]
        public void GetUpgradeLevel_未適用は0()
        {
            var m = new MetaProgressState();
            Assert.AreEqual(0, m.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
            Assert.AreEqual(0, m.GetUpgradeLevel("nonexistent"));
        }

        [Test]
        public void ApplyUpgrade_cap_0以下_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => m.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => m.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, -1));
        }

        [Test]
        public void ApplyUpgrade_id_null_or_empty_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentException>(() => m.ApplyUpgrade(null, 1));
            Assert.Throws<ArgumentException>(() => m.ApplyUpgrade("", 1));
        }

        // ── 固有ユニット Lv 強化の選択結果（ApplyUpgradeChoice / GetUpgradeChoices）──

        [Test]
        public void ApplyUpgradeChoice_購入順を保持して末尾追加()
        {
            var m = new MetaProgressState();
            m.ApplyUpgradeChoice("princess", "up_atk_plus_5");
            m.ApplyUpgradeChoice("princess", "up_hp_plus_20");

            var choices = m.GetUpgradeChoices("princess");
            Assert.AreEqual(2, choices.Count);
            Assert.AreEqual("up_atk_plus_5", choices[0]);
            Assert.AreEqual("up_hp_plus_20", choices[1]);
        }

        [Test]
        public void ApplyUpgradeChoice_別ユニットは独立に保持()
        {
            var m = new MetaProgressState();
            m.ApplyUpgradeChoice("princess", "up_atk_plus_5");
            m.ApplyUpgradeChoice("bridget", "up_hp_plus_20");

            Assert.AreEqual(1, m.GetUpgradeChoices("princess").Count);
            Assert.AreEqual("up_atk_plus_5", m.GetUpgradeChoices("princess")[0]);
            Assert.AreEqual(1, m.GetUpgradeChoices("bridget").Count);
            Assert.AreEqual("up_hp_plus_20", m.GetUpgradeChoices("bridget")[0]);
        }

        [Test]
        public void GetUpgradeChoices_未登録ユニット_空リスト()
        {
            var m = new MetaProgressState();
            Assert.AreEqual(0, m.GetUpgradeChoices("princess").Count);
            Assert.AreEqual(0, m.GetUpgradeChoices(null).Count);
            Assert.AreEqual(0, m.GetUpgradeChoices("").Count);
        }

        [Test]
        public void ApplyUpgradeChoice_null_or_empty_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentException>(() => m.ApplyUpgradeChoice(null, "up_atk"));
            Assert.Throws<ArgumentException>(() => m.ApplyUpgradeChoice("", "up_atk"));
            Assert.Throws<ArgumentException>(() => m.ApplyUpgradeChoice("princess", null));
            Assert.Throws<ArgumentException>(() => m.ApplyUpgradeChoice("princess", ""));
        }

        // ── トゥルーエンドフラグ ──

        [Test]
        public void MarkTrueEndReached_冪等()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.HasReachedTrueEnd);
            m.MarkTrueEndReached();
            Assert.IsTrue(m.HasReachedTrueEnd);
            m.MarkTrueEndReached();
            Assert.IsTrue(m.HasReachedTrueEnd);
        }

        // ── 物語進行フラグ（HasFirstReachedBoss / HasRescuedBalduin / HasNotedPendantPower）──

        [Test]
        public void MarkFirstReachedBoss_冪等()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.HasFirstReachedBoss);
            m.MarkFirstReachedBoss();
            Assert.IsTrue(m.HasFirstReachedBoss);
            m.MarkFirstReachedBoss();
            Assert.IsTrue(m.HasFirstReachedBoss);
        }

        [Test]
        public void MarkBalduinRescued_冪等()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.HasRescuedBalduin);
            m.MarkBalduinRescued();
            Assert.IsTrue(m.HasRescuedBalduin);
            m.MarkBalduinRescued();
            Assert.IsTrue(m.HasRescuedBalduin);
        }

        [Test]
        public void MarkPendantPowerNoted_冪等()
        {
            var m = new MetaProgressState();
            Assert.IsFalse(m.HasNotedPendantPower);
            m.MarkPendantPowerNoted();
            Assert.IsTrue(m.HasNotedPendantPower);
            m.MarkPendantPowerNoted();
            Assert.IsTrue(m.HasNotedPendantPower);
        }

        [Test]
        public void 新フラグ_独立に管理される()
        {
            var m = new MetaProgressState();
            m.MarkFirstReachedBoss();
            Assert.IsTrue(m.HasFirstReachedBoss);
            Assert.IsFalse(m.HasRescuedBalduin);
            Assert.IsFalse(m.HasNotedPendantPower);
            Assert.IsFalse(m.HasReachedTrueEnd);
        }

        // ── LoadFromSerializedState（シリアライザ呼び出し前提） ──

        [Test]
        public void LoadFromSerializedState_全項目を復元()
        {
            var m = new MetaProgressState();
            var unlocks = new[] { MetaUnitIds.Bridget };
            var upgrades = new Dictionary<string, int>
            {
                { MetaUpgradeIds.PrincessLevel, 1 },
                { MetaUpgradeIds.InitialUnit, 2 },
            };
            m.LoadFromSerializedState(
                memories: 250,
                runCount: 5,
                hasReachedTrueEnd: true,
                unlockedUnits: unlocks,
                appliedUpgrades: upgrades,
                hasFirstReachedBoss: true,
                hasRescuedBalduin: true,
                hasNotedPendantPower: true);

            Assert.AreEqual(250, m.Memories);
            Assert.AreEqual(5, m.RunCount);
            Assert.IsTrue(m.HasReachedTrueEnd);
            Assert.IsTrue(m.HasFirstReachedBoss);
            Assert.IsTrue(m.HasRescuedBalduin);
            Assert.IsTrue(m.HasNotedPendantPower);
            Assert.IsTrue(m.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, m.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
            Assert.AreEqual(2, m.GetUpgradeLevel(MetaUpgradeIds.InitialUnit));
        }

        [Test]
        public void LoadFromSerializedState_新フラグ省略時はfalse_後方互換()
        {
            var m = new MetaProgressState();
            // 旧スキーマからの読み込み相当（新 3 フラグはデフォルト引数）
            m.LoadFromSerializedState(
                memories: 100,
                runCount: 2,
                hasReachedTrueEnd: false,
                unlockedUnits: null,
                appliedUpgrades: null);

            Assert.IsFalse(m.HasFirstReachedBoss);
            Assert.IsFalse(m.HasRescuedBalduin);
            Assert.IsFalse(m.HasNotedPendantPower);
        }

        [Test]
        public void LoadFromSerializedState_負の数値_例外()
        {
            var m = new MetaProgressState();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => m.LoadFromSerializedState(-1, 0, false, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => m.LoadFromSerializedState(0, -1, false, null, null));
        }

        [Test]
        public void LoadFromSerializedState_nullコレクション許容()
        {
            var m = new MetaProgressState();
            m.LoadFromSerializedState(100, 1, false, null, null);
            Assert.AreEqual(0, m.UnlockedUnits.Count);
            Assert.AreEqual(0, m.AppliedUpgrades.Count);
        }
    }
}
