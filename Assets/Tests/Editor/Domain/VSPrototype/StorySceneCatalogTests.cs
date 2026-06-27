// StorySceneCatalog（本物・Resources.LoadAll 実装）の SO 由来検証。
//
// 【前提】
// - SO アセット生成メニュー実行済で
//   Assets/Resources/Data/StoryScenes/story_scene_*.asset 14 個が存在すること。
//
// 【検証観点】
// - SO ロード（14 件）
// - 各シーンのページ件数（SO 生成側と等価）
// - VSPrototypeStorySceneIds の const と SO 主キーの整合
// - 全 StoryPage の ImagePath が「Images/」プレフィックスを持つ（Resources.Load 対象）
// - ナレ文が空でない（敗北エンド系の「ありがとう」など重要文言の存在）
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Echolos.Data;
using Echolos.Domain.Catalog;
using Echolos.Domain.Story;
using Echolos.UseCase.VSPrototype;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class StorySceneCatalogTests
    {
        private IStorySceneCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = new StorySceneCatalog();
        }

        // ══════════════════════════════════════════════
        // SO ロード
        // ══════════════════════════════════════════════

        [Test]
        public void GetAll_SOアセットから13件ロードされる()
        {
            var scenes = _catalog.GetAll();
            Assert.AreEqual(13, scenes.Count,
                "Resources/Data/StoryScenes/*.asset 13 件が見つからない。" +
                "Editor で Echolos/Data/SO アセットを生成 を実行してください。");
        }

        [Test]
        public void GetAll_13シーンすべて含まれる()
        {
            var ids = _catalog.GetAll().Select(s => s.Id).ToList();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    VSPrototypeStorySceneIds.Opening,
                    VSPrototypeStorySceneIds.BalduinIntro,
                    VSPrototypeStorySceneIds.BalduinLetter,
                    VSPrototypeStorySceneIds.BalduinSurrender,
                    VSPrototypeStorySceneIds.MysteriousGirl,
                    VSPrototypeStorySceneIds.BalduinRescue,
                    VSPrototypeStorySceneIds.SwordEmpowered,
                    VSPrototypeStorySceneIds.BossAttack,
                    VSPrototypeStorySceneIds.BossPurify,
                    VSPrototypeStorySceneIds.EndingDefeatFirst,
                    VSPrototypeStorySceneIds.EndingDefeatNormalClear,
                    VSPrototypeStorySceneIds.EndingDefeatRepeated,
                    VSPrototypeStorySceneIds.EndingTrue,
                },
                ids);
        }

        [Test]
        public void Get_未登録ID_例外()
        {
            Assert.Throws<KeyNotFoundException>(() => _catalog.Get("unknown_id_xyz"));
        }

        [Test]
        public void IsRegistered_13シーンすべてtrue()
        {
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.Opening));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.BalduinIntro));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.BalduinLetter));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.BalduinSurrender));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.MysteriousGirl));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.BalduinRescue));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.SwordEmpowered));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.BossAttack));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.BossPurify));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.EndingDefeatFirst));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.EndingDefeatNormalClear));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.EndingDefeatRepeated));
            Assert.IsTrue(_catalog.IsRegistered(VSPrototypeStorySceneIds.EndingTrue));
        }

        [Test]
        public void IsRegistered_未登録_false()
        {
            Assert.IsFalse(_catalog.IsRegistered("unknown_id_xyz"));
        }

        [Test]
        public void GetAll_旧5シーンIDは含まれない()
        {
            // bridget_bond / bridget_confront / ending_defeat / ending_bitter は廃止または改名済。
            // ending_true は ID 維持・コンテンツリライトのため新仕様側で別途検証。
            var ids = _catalog.GetAll().Select(s => s.Id).ToList();
            Assert.IsFalse(ids.Contains("bridget_bond"),
                "bridget_bond は balduin_rescue にリネーム済み。SO アセット再生成が必要");
            Assert.IsFalse(ids.Contains("bridget_confront"),
                "bridget_confront は廃止済み。SO アセット再生成が必要");
            Assert.IsFalse(ids.Contains("ending_defeat"),
                "ending_defeat は 3 分割済み。SO アセット再生成が必要");
            Assert.IsFalse(ids.Contains("ending_bitter"),
                "ending_bitter は廃止済み。SO アセット再生成が必要");
        }

        // ══════════════════════════════════════════════
        // ページ件数（SoAssetGenerator と等価性）
        // ══════════════════════════════════════════════

        [Test]
        public void Opening_6ページ()
        {
            // A-a プロローグは 6 ページ豪華化済（皇太子襲来→召喚秘術→1000 年後遺跡 ×2 →神殿対面→戦闘開始）
            Assert.AreEqual(6, _catalog.Get(VSPrototypeStorySceneIds.Opening).Pages.Count);
        }

        [Test]
        public void BalduinIntro_3ページ()
        {
            Assert.AreEqual(3, _catalog.Get(VSPrototypeStorySceneIds.BalduinIntro).Pages.Count);
        }

        [Test]
        public void BalduinLetter_3ページ()
        {
            Assert.AreEqual(3, _catalog.Get(VSPrototypeStorySceneIds.BalduinLetter).Pages.Count);
        }

        [Test]
        public void BalduinSurrender_3ページ()
        {
            Assert.AreEqual(3, _catalog.Get(VSPrototypeStorySceneIds.BalduinSurrender).Pages.Count);
        }

        [Test]
        public void MysteriousGirl_4ページ()
        {
            Assert.AreEqual(4, _catalog.Get(VSPrototypeStorySceneIds.MysteriousGirl).Pages.Count);
        }

        [Test]
        public void BalduinRescue_4ページ()
        {
            Assert.AreEqual(4, _catalog.Get(VSPrototypeStorySceneIds.BalduinRescue).Pages.Count);
        }

        [Test]
        public void SwordEmpowered_3ページ()
        {
            Assert.AreEqual(3, _catalog.Get(VSPrototypeStorySceneIds.SwordEmpowered).Pages.Count);
        }

        [Test]
        public void BossAttack_3ページ()
        {
            Assert.AreEqual(3, _catalog.Get(VSPrototypeStorySceneIds.BossAttack).Pages.Count);
        }

        [Test]
        public void BossPurify_3ページ()
        {
            Assert.AreEqual(3, _catalog.Get(VSPrototypeStorySceneIds.BossPurify).Pages.Count);
        }

        [Test]
        public void EndingDefeatFirst_9ページ()
        {
            Assert.AreEqual(9, _catalog.Get(VSPrototypeStorySceneIds.EndingDefeatFirst).Pages.Count);
        }

        [Test]
        public void EndingDefeatNormalClear_6ページ()
        {
            Assert.AreEqual(6, _catalog.Get(VSPrototypeStorySceneIds.EndingDefeatNormalClear).Pages.Count);
        }

        [Test]
        public void EndingDefeatRepeated_5ページ()
        {
            Assert.AreEqual(5, _catalog.Get(VSPrototypeStorySceneIds.EndingDefeatRepeated).Pages.Count);
        }

        [Test]
        public void EndingTrue_5ページ()
        {
            Assert.AreEqual(5, _catalog.Get(VSPrototypeStorySceneIds.EndingTrue).Pages.Count);
        }

        // ══════════════════════════════════════════════
        // ページ内容（重要文言・パス・ナレ非空）
        // ══════════════════════════════════════════════

        [Test]
        public void 全シーン全ページ_ImagePathは空または_Images_プレフィックス()
        {
            // 空文字列＝黒幕フォールバック（StoryGUI 側）として正規の表現手段。
            // 値があるなら Resources.Load 対象のため Images/ プレフィックス必須。
            foreach (var scene in _catalog.GetAll())
                foreach (var page in scene.Pages)
                    Assert.IsTrue(string.IsNullOrEmpty(page.ImagePath) || page.ImagePath.StartsWith("Images/"),
                        $"シーン '{scene.Id}' の ImagePath='{page.ImagePath}' は空または Images/ プレフィックスが必要");
        }

        [Test]
        public void 全シーン全ページ_NarrationTextが非空()
        {
            foreach (var scene in _catalog.GetAll())
                foreach (var page in scene.Pages)
                    Assert.IsNotEmpty(page.NarrationText,
                        $"シーン '{scene.Id}' にナレ文が空のページあり");
        }

        [Test]
        public void EndingDefeatFirst_ありがとう文言が含まれる()
        {
            // A-b1：神殿送り返しシーンの「―ありがとう。」（USP 核体験）。
            var scene = _catalog.Get(VSPrototypeStorySceneIds.EndingDefeatFirst);
            bool found = scene.Pages.Any(p => p.NarrationText.Contains("ありがとう"));
            Assert.IsTrue(found, "1 周目バッドエンドに「ありがとう」文言が必要（USP 核体験）");
        }

        [Test]
        public void EndingDefeatRepeated_ありがとう文言が含まれる()
        {
            // A-b2：2 周目以降バッドエンドでも王女の「ありがとう。」が継続する。
            var scene = _catalog.Get(VSPrototypeStorySceneIds.EndingDefeatRepeated);
            bool found = scene.Pages.Any(p => p.NarrationText.Contains("ありがとう"));
            Assert.IsTrue(found, "2 周目以降バッドエンドにも「ありがとう」文言が必要（USP 核体験の継続）");
        }

        [Test]
        public void EndingTrue_ありがとう文言が含まれる()
        {
            // A-d：王女が「本当に、ありがとう！」と抱きつくシーン。
            var scene = _catalog.Get(VSPrototypeStorySceneIds.EndingTrue);
            bool found = scene.Pages.Any(p => p.NarrationText.Contains("ありがとう"));
            Assert.IsTrue(found, "トゥルーエンドに「ありがとう」文言が必要");
        }

        [Test]
        public void EndingTrue_最終付近に_To_be_continued_が含まれる()
        {
            // A-d 締めの「To be continued...」（フル版への接続）。
            var scene = _catalog.Get(VSPrototypeStorySceneIds.EndingTrue);
            bool found = scene.Pages.Any(p => p.NarrationText.Contains("To be continued"));
            Assert.IsTrue(found, "トゥルーエンドに「To be continued」文言が必要（フル版への引き）");
        }

        [Test]
        public void BalduinRescue_ブリジット託す文言が含まれる()
        {
            // B-d：「このブリジットをお連れくださいませ。」がキーセリフ。
            var scene = _catalog.Get(VSPrototypeStorySceneIds.BalduinRescue);
            bool found = scene.Pages.Any(p => p.NarrationText.Contains("ブリジット"));
            Assert.IsTrue(found, "B-d 救援成功シーンにブリジット託す文言が必要");
        }

        [Test]
        public void Opening_発火タイミング草案文言が含まれる()
        {
            // A-a プロローグ：「召喚する秘術」が草案のキーフレーズ。
            var scene = _catalog.Get(VSPrototypeStorySceneIds.Opening);
            bool found = scene.Pages.Any(p => p.NarrationText.Contains("召喚"));
            Assert.IsTrue(found, "A-a プロローグに召喚モチーフの文言が必要");
        }
    }
}
