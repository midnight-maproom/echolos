// MetaProgressState を Domain 層から読み取るための抽象 view。
//
// 【役割】
// - RoundManager / EnemyPatterns 等の Domain/UseCase 層が「Meta フラグを見る」
//   ときの単一窓口。直接 MetaProgressState を参照すると依存が広がるため、
//   read-only view として抽象化する。
// - 新フラグを追加するときはこの interface に追加するだけで、各層は変更不要。
//
// 【書き換え API は含まない】
// - 状態変化（Mark*）は MetaProgressState 側のみが提供。view は読み取り専用。
//   「状態変化は API メソッド経由で必ず通知発火」を担う Mark* と
//   読み取り経路の view は別責務。

namespace Echolos.Domain.Meta
{
    /// <summary>
    /// MetaProgressState の読み取り専用 view。Domain 層から Meta フラグを参照する単一窓口。
    /// </summary>
    public interface IMetaProgressView
    {
        /// <summary>トゥルーエンドに少なくとも1回到達したか。</summary>
        bool HasReachedTrueEnd { get; }

        /// <summary>R7 ボス戦に少なくとも1回到達したか（ノーマルエンド演出条件）。</summary>
        bool HasFirstReachedBoss { get; }

        /// <summary>バルドゥインを救出済か（マップ左列救援済化＋ B 系列スキップ条件）。</summary>
        bool HasRescuedBalduin { get; }

        /// <summary>ペンダント気づきイベントを完了したか（A-c2 経路解禁＋ B-e 発火条件）。</summary>
        bool HasNotedPendantPower { get; }

        /// <summary>完了したラン数。</summary>
        int RunCount { get; }

        /// <summary>指定強化の現在Lv（未適用は0）。</summary>
        int GetUpgradeLevel(string upgradeId);

        /// <summary>指定 StorySceneId を過去のラン含めて 1 回以上再生済みか（既見短縮の判定に使用）。</summary>
        bool HasSeenStoryScene(string sceneId);
    }
}
