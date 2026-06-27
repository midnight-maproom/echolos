// MetaProgressSerializer の JSON 往復・部分欠損・不正入力の検証。
using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Meta;
using Echolos.UseCase.VSPrototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class MetaProgressSerializerTests
    {
        // ── 初期状態の往復 ──

        [Test]
        public void 初期状態のJSON出力_既定フォーマット()
        {
            var state = new MetaProgressState();
            string json = MetaProgressSerializer.ToJson(state);

            // 基本構造のみ検証（順序固定なので可能）
            Assert.AreEqual(
                "{\"memories\":0,\"runCount\":0,\"hasReachedTrueEnd\":false,"
                + "\"hasFirstReachedBoss\":false,\"hasRescuedBalduin\":false,\"hasNotedPendantPower\":false,"
                + "\"unlockedUnits\":[],\"appliedUpgrades\":{},\"appliedUpgradeChoices\":{},"
                + "\"seenStorySceneIds\":[]}",
                json);
        }

        [Test]
        public void 初期状態の往復_完全に元に戻る()
        {
            var src = new MetaProgressState();
            string json = MetaProgressSerializer.ToJson(src);

            var restored = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, restored));
            Assert.AreEqual(0, restored.Memories);
            Assert.AreEqual(0, restored.RunCount);
            Assert.IsFalse(restored.HasReachedTrueEnd);
            Assert.IsFalse(restored.HasFirstReachedBoss);
            Assert.IsFalse(restored.HasRescuedBalduin);
            Assert.IsFalse(restored.HasNotedPendantPower);
            Assert.AreEqual(0, restored.UnlockedUnits.Count);
            Assert.AreEqual(0, restored.AppliedUpgrades.Count);
        }

        // ── 全項目埋まった往復 ──

        [Test]
        public void 全項目埋まった往復()
        {
            // cap 値は MetaUpgradeDefinitionSO 側で持つため、
            // Serializer 単体テストでは仕様値（PrincessLevel=2, InitialUnit=3）を直接渡す。
            const int CapPrincessLevel = 2;
            const int CapInitialUnit = 3;
            var src = new MetaProgressState();
            src.EarnMemories(250);
            src.IncrementRunCount();
            src.IncrementRunCount();
            src.IncrementRunCount();
            src.IncrementRunCount();
            src.IncrementRunCount(); // RunCount = 5
            src.MarkTrueEndReached();
            src.MarkFirstReachedBoss();
            src.MarkBalduinRescued();
            src.MarkPendantPowerNoted();
            src.UnlockUnit(MetaUnitIds.Bridget);
            src.ApplyUpgrade(MetaUpgradeIds.PrincessLevel, CapPrincessLevel);
            src.ApplyUpgrade(MetaUpgradeIds.InitialUnit, CapInitialUnit);
            src.ApplyUpgrade(MetaUpgradeIds.InitialUnit, CapInitialUnit);
            src.MarkStorySceneSeen("opening");
            src.MarkStorySceneSeen("b_a_balduin");

            string json = MetaProgressSerializer.ToJson(src);
            var restored = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, restored));

            Assert.AreEqual(250, restored.Memories);
            Assert.AreEqual(5, restored.RunCount);
            Assert.IsTrue(restored.HasReachedTrueEnd);
            Assert.IsTrue(restored.HasFirstReachedBoss);
            Assert.IsTrue(restored.HasRescuedBalduin);
            Assert.IsTrue(restored.HasNotedPendantPower);
            Assert.IsTrue(restored.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, restored.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
            Assert.AreEqual(2, restored.GetUpgradeLevel(MetaUpgradeIds.InitialUnit));
            Assert.IsTrue(restored.HasSeenStoryScene("opening"));
            Assert.IsTrue(restored.HasSeenStoryScene("b_a_balduin"));
            Assert.IsFalse(restored.HasSeenStoryScene("balduin_rescue"));
        }

        // ── 後方互換：旧スキーマ（新フラグなし）の読み込み ──

        [Test]
        public void ApplyJson_旧スキーマ_新フラグはfalseで初期化()
        {
            // 旧スキーマ JSON（HasFirstReachedBoss / HasRescuedBalduin / HasNotedPendantPower の 3 フラグなし）
            string oldSchema = "{\"memories\":100,\"runCount\":2,\"hasReachedTrueEnd\":true,"
                + "\"unlockedUnits\":[\"bridget\"],\"appliedUpgrades\":{}}";
            var state = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(oldSchema, state));

            Assert.AreEqual(100, state.Memories);
            Assert.AreEqual(2, state.RunCount);
            Assert.IsTrue(state.HasReachedTrueEnd);
            // 旧スキーマ由来＝新 3 フラグは false で初期化される
            Assert.IsFalse(state.HasFirstReachedBoss);
            Assert.IsFalse(state.HasRescuedBalduin);
            Assert.IsFalse(state.HasNotedPendantPower);
            // ストーリー既見集合も旧スキーマには存在しないため空既定（既見なし＝全シーン初見扱い）
            Assert.AreEqual(0, state.SeenStorySceneIds.Count);
        }

        // ── 不正入力 ──

        [Test]
        public void ApplyJson_空文字_falseを返す_Stateは触らない()
        {
            var state = new MetaProgressState();
            state.EarnMemories(50);

            Assert.IsFalse(MetaProgressSerializer.ApplyJson(string.Empty, state));
            Assert.AreEqual(50, state.Memories); // 既存値が保持される
        }

        [Test]
        public void ApplyJson_null_falseを返す()
        {
            var state = new MetaProgressState();
            Assert.IsFalse(MetaProgressSerializer.ApplyJson(null, state));
        }

        [Test]
        public void ApplyJson_完全に不正なJSON_falseかフォールバック()
        {
            var state = new MetaProgressState();
            // 「不正な」入力でも、ExtractInt 等は欠損扱いで初期値を返すため、
            // 例外を投げない限り true（既定値で復元）を返す可能性がある。
            // ここでは「例外を投げず・Memories が 0 のまま」を確認する。
            MetaProgressSerializer.ApplyJson("not a json at all", state);
            Assert.AreEqual(0, state.Memories);
        }

        // ── 部分欠損 ──

        [Test]
        public void ApplyJson_部分欠損_欠損フィールドは初期値で復元()
        {
            // memories と runCount だけ含む不完全な JSON
            string json = "{\"memories\":100,\"runCount\":3}";
            var state = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, state));

            Assert.AreEqual(100, state.Memories);
            Assert.AreEqual(3, state.RunCount);
            Assert.IsFalse(state.HasReachedTrueEnd);
            Assert.AreEqual(0, state.UnlockedUnits.Count);
            Assert.AreEqual(0, state.AppliedUpgrades.Count);
        }

        [Test]
        public void ApplyJson_余分な空白を含むJSON_正常にパース()
        {
            string json = "{ \"memories\" : 50 , \"runCount\" : 2 , "
                + "\"hasReachedTrueEnd\" : true , "
                + "\"unlockedUnits\" : [ \"bridget\" ] , "
                + "\"appliedUpgrades\" : { \"princess_level\" : 1 } }";
            var state = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, state));

            Assert.AreEqual(50, state.Memories);
            Assert.AreEqual(2, state.RunCount);
            Assert.IsTrue(state.HasReachedTrueEnd);
            Assert.IsTrue(state.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.AreEqual(1, state.GetUpgradeLevel(MetaUpgradeIds.PrincessLevel));
        }

        // ── ロードによる既存値の上書き ──

        [Test]
        public void ApplyJson_既存Stateを上書きする()
        {
            var state = new MetaProgressState();
            state.EarnMemories(500); // 既存値
            state.UnlockUnit("oldUnit");

            string json = "{\"memories\":100,\"runCount\":1,\"hasReachedTrueEnd\":false,"
                + "\"unlockedUnits\":[\"bridget\"],\"appliedUpgrades\":{}}";
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, state));

            // 既存値が上書きされる
            Assert.AreEqual(100, state.Memories);
            Assert.IsTrue(state.IsUnitUnlocked(MetaUnitIds.Bridget));
            Assert.IsFalse(state.IsUnitUnlocked("oldUnit"));
        }

        // ── 複数解禁・複数強化 ──

        [Test]
        public void 複数解禁_複数強化を含む往復()
        {
            var src = new MetaProgressState();
            src.UnlockUnit("bridget");
            src.UnlockUnit("viola");
            src.UnlockUnit("luca");
            src.ApplyUpgrade("a", 5);
            src.ApplyUpgrade("a", 5);
            src.ApplyUpgrade("b", 5);

            string json = MetaProgressSerializer.ToJson(src);
            var restored = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, restored));

            Assert.AreEqual(3, restored.UnlockedUnits.Count);
            Assert.IsTrue(restored.IsUnitUnlocked("bridget"));
            Assert.IsTrue(restored.IsUnitUnlocked("viola"));
            Assert.IsTrue(restored.IsUnitUnlocked("luca"));
            Assert.AreEqual(2, restored.GetUpgradeLevel("a"));
            Assert.AreEqual(1, restored.GetUpgradeLevel("b"));
        }

        // ── 固有ユニット Lv 強化の選択結果（appliedUpgradeChoices）──

        [Test]
        public void AppliedUpgradeChoices_往復で復元される()
        {
            var src = new MetaProgressState();
            src.ApplyUpgradeChoice("princess", "up_atk_plus_5");
            src.ApplyUpgradeChoice("princess", "up_hp_plus_20");
            src.ApplyUpgradeChoice("bridget", "up_def_plus_3");

            string json = MetaProgressSerializer.ToJson(src);
            var restored = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(json, restored));

            var princessChoices = restored.GetUpgradeChoices("princess");
            Assert.AreEqual(2, princessChoices.Count);
            Assert.AreEqual("up_atk_plus_5", princessChoices[0]);
            Assert.AreEqual("up_hp_plus_20", princessChoices[1]);

            var bridgetChoices = restored.GetUpgradeChoices("bridget");
            Assert.AreEqual(1, bridgetChoices.Count);
            Assert.AreEqual("up_def_plus_3", bridgetChoices[0]);
        }

        [Test]
        public void AppliedUpgradeChoices_旧スキーマ_空辞書で初期化()
        {
            // 旧スキーマ JSON（appliedUpgradeChoices フィールドなし）
            string oldSchema = "{\"memories\":100,\"runCount\":2,\"hasReachedTrueEnd\":false,"
                + "\"unlockedUnits\":[],\"appliedUpgrades\":{}}";
            var state = new MetaProgressState();
            Assert.IsTrue(MetaProgressSerializer.ApplyJson(oldSchema, state));

            Assert.AreEqual(0, state.GetUpgradeChoices("princess").Count);
            Assert.AreEqual(0, state.GetUpgradeChoices("bridget").Count);
        }

        [Test]
        public void ToJson_State_null_例外()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MetaProgressSerializer.ToJson(null));
        }

        [Test]
        public void ApplyJson_target_null_例外()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MetaProgressSerializer.ApplyJson("{}", null));
        }
    }
}
