// VSプロト：StorySceneCatalog の主キー識別子集約（const string）。
//
// 【役割】
// - Bootstrap から `_storySceneCatalog.Get(VSPrototypeStorySceneIds.BalduinRescue)` 形式で参照する
//   ためのタイポ防止用 const。
// - SO アセット側の Id フィールドと一致させること（VSProto_StorySceneCatalogTests で整合検証）。
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロトのストーリーシーン主キー集約（13 件）。</summary>
    public static class VSPrototypeStorySceneIds
    {
        // メインストーリー A 系列

        /// <summary>A-a プロローグ（1 周目ラン開始のみ）。</summary>
        public const string Opening = "opening";

        /// <summary>A-c1 皇太子襲来（必敗版・`!HasNotedPendantPower`）。</summary>
        public const string BossAttack = "a_c1_attack";

        /// <summary>A-c2 オーラ祓い（戦える版・`HasNotedPendantPower`）。</summary>
        public const string BossPurify = "a_c2_purify";

        /// <summary>Defeat 演出 - 初回（`RunCount == 0`）。</summary>
        public const string EndingDefeatFirst = "ending_defeat_first";

        /// <summary>Defeat 演出 - 6R 達成版（本ラン中に <see cref="MetaProgressState.HasFirstReachedBoss"/> が新たに立つ）。</summary>
        public const string EndingDefeatNormalClear = "ending_defeat_normal_clear";

        /// <summary>Defeat 演出 - 2 周目以降通常（上記以外の敗北）。</summary>
        public const string EndingDefeatRepeated = "ending_defeat_repeated";

        /// <summary>トゥルーエンド（R7 ボス勝利・A-d）。</summary>
        public const string EndingTrue = "ending_true";

        // サブストーリー B 系列

        /// <summary>B-a バルドゥインの背景（R2 開始時）。</summary>
        public const string BalduinIntro = "b_a_balduin";

        /// <summary>B-b1 戦況悪化＋宰相握りつぶし（R3 開始時）。</summary>
        public const string BalduinLetter = "b_b1_letter";

        /// <summary>B-b2 バルドゥイン降伏（R5 開始時・救援失敗時）。</summary>
        public const string BalduinSurrender = "b_b2_surrender";

        /// <summary>B-c 謎の少女（R6 開始時・救援失敗時）。</summary>
        public const string MysteriousGirl = "b_c_girl";

        /// <summary>B-d 救援成功＋ブリジット託す（R5 終了時）。</summary>
        public const string BalduinRescue = "balduin_rescue";

        /// <summary>B-e 聖剣の真の力（ペンダントの力に気づき → 聖剣強化までを 1 シーンで描く）。完了で HasNotedPendantPower=true。</summary>
        public const string SwordEmpowered = "b_e_sword";
    }
}
