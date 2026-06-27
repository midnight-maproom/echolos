// ラン終了時のエンディング種別を決定する純ロジック関数群。
//
// 分岐ロジック：
//   1. ラン途中で本拠地連続防衛敗北 → Defeat
//   2. R7 ボス戦敗北               → Defeat
//   3. R7 ボス戦勝利               → True
//
// 「R7 で勝てる」のは A-c2 経路（HasNotedPendantPower=true・ペンダント＋聖剣強化済）に
// 限定される。A-c1 経路（未取得）は皇太子のステータスが実質無敵化されるので必敗。
// この経路分岐は EnemyPatterns 側で吸収するため、EndingResolver は単純な 2 分岐となる。
//
// 「ブリジット加入」は ApplyEndingToMeta で別軸として扱う：MetaProgressState の解禁判定は
// このランで救出した（IsBridgetRescued）かどうかで決まる。エンディング分岐の条件ではない。
using System;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロトのエンディング種別（2 分岐）。</summary>
    public enum VSPrototypeEndingKind
    {
        /// <summary>ラン継続中（未確定）。</summary>
        None,
        /// <summary>ラン敗北（本拠地連続防衛 or R7 ボス戦敗北）。</summary>
        Defeat,
        /// <summary>トゥルーエンド：R7 ボス戦勝利（A-c2 経路でのみ可能）。</summary>
        True,
    }

    /// <summary>エンディング分岐の判定ロジック（純関数・テスト容易）。</summary>
    public sealed class VSPrototypeEndingResolver
    {
        /// <summary>
        /// 本拠地連続防衛の途中で1戦でも敗北したときのエンディング決定（常に Defeat）。
        /// 1戦でも敗北すれば即ラン敗北 → EndingDefeat。
        /// </summary>
        public VSPrototypeEndingKind ResolveAfterHomeDefense(bool allWon)
        {
            return allWon ? VSPrototypeEndingKind.None : VSPrototypeEndingKind.Defeat;
        }

        /// <summary>
        /// R7 ボス戦の結果からエンディング種別を決定する。
        /// 勝利＝必ず True（A-c2 経路でのみ勝てる構造のため）／敗北＝Defeat。
        /// </summary>
        /// <param name="bossWon">ボス戦に勝利したか。</param>
        public VSPrototypeEndingKind ResolveAfterBossRound(bool bossWon)
        {
            return bossWon ? VSPrototypeEndingKind.True : VSPrototypeEndingKind.Defeat;
        }

        /// <summary>
        /// エンディング確定後にメタ進行状態へ反映する副作用処理。
        /// - Bridget が救出済なら UnlockUnit を実行（冪等）。
        /// - True エンドに到達したら MarkTrueEndReached を実行（冪等）。
        /// - ラン完了として IncrementRunCount を実行（必ず +1）。
        /// 呼び出し側は、ラン1回終了につき1度だけ呼ぶこと。
        /// </summary>
        public void ApplyEndingToMeta(
            VSPrototypeEndingKind ending,
            bool bridgetRescuedThisRun,
            MetaProgressState meta)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (ending == VSPrototypeEndingKind.None)
                throw new InvalidOperationException("エンディング未確定の状態で ApplyEndingToMeta を呼んではいけない");

            // ブリジット加入済の場合のみ UnlockUnit を実行。
            // クリア・敗北は問わない（敗北エンドでも救出済なら次ランで永続加入）。
            if (bridgetRescuedThisRun)
                meta.UnlockUnit(MetaUnitIds.Bridget);

            if (ending == VSPrototypeEndingKind.True)
                meta.MarkTrueEndReached();

            meta.IncrementRunCount();
        }
    }
}
