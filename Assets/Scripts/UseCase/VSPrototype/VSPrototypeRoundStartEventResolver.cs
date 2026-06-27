// ラウンド開始時演出のシーン ID を決定する純関数。
//
// 【役割】
// - Bootstrap.AdvanceBeforeRound() が呼ぶ単一の窓口。round 番号と Meta / MapState を見て、
//   発火すべきシーン ID を返す（該当なしなら null）。
// - 純関数として切り出すことで、ラウンド開始演出の発火条件を Editor テストで完結検証できる。
//
// 【設計判断】
// - R6 の SwordEmpowered（B-e 聖剣の真の力）は B-c（MysteriousGirl）と排他：HasRescuedBalduin が
//   立っていれば SwordEmpowered 経路、立っていなければ MysteriousGirl 経路。両方の前提条件
//   （IsBridgetRescued=false 等）が衝突しないよう、HasRescuedBalduin 優先で分岐する。
// - SwordEmpowered 完了時の HasNotedPendantPower セットは Bootstrap 側で扱う（副作用のため）。
using System;
using Echolos.Domain.Meta;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>ラウンド開始時演出のシーン ID 決定（純関数）。</summary>
    public static class VSPrototypeRoundStartEventResolver
    {
        /// <summary>
        /// 指定ラウンド開始時の演出シーン ID を返す。該当なしなら null（演出スキップ）。
        /// </summary>
        /// <param name="round">これから開始するラウンド番号（1〜<see cref="VSPrototypeRoundManager.MaxRounds"/>）。</param>
        /// <param name="meta">永続フラグ参照（HasRescuedBalduin / HasNotedPendantPower 等）。</param>
        /// <param name="mapState">ラン中フラグ参照（IsBridgetRescued）。</param>
        public static string Resolve(int round, IMetaProgressView meta, VSPrototypeMapState mapState)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));

            switch (round)
            {
                case 2:
                    // B-a：バルドゥインの背景。救援済世界（永続）／本ラン先行救援済では出さない。
                    return (!meta.HasRescuedBalduin && !mapState.IsBridgetRescued)
                        ? VSPrototypeStorySceneIds.BalduinIntro
                        : null;

                case 3:
                    // B-b1：戦況悪化＋宰相握りつぶし。救援済世界／本ラン先行救援済では出さない
                    //（本ラン R1-R2 でバルドゥイン拠点制圧済なら「救援の手紙」イベントは矛盾するため抑制）。
                    return (!meta.HasRescuedBalduin && !mapState.IsBridgetRescued)
                        ? VSPrototypeStorySceneIds.BalduinLetter
                        : null;

                case 5:
                    // B-b2：バルドゥイン降伏。R4 までにバルドゥイン拠点未制圧かつ未救援済の場合のみ。
                    return (!mapState.IsBridgetRescued && !meta.HasRescuedBalduin)
                        ? VSPrototypeStorySceneIds.BalduinSurrender
                        : null;

                case 6:
                    // バルドゥイン救援済（永続 or 当該ラン R5 までに解放）＆ペンダント未気づき
                    //  → B-e 聖剣の真の力（気づき＋強化を 1 シーンで描く）→ R7 A-c2 経路（撃破可能）へ。
                    //  当該ラン救出を含めることで「ブリジットを救った周回でそのままクリア」を成立させる。
                    if ((meta.HasRescuedBalduin || mapState.IsBridgetRescued) && !meta.HasNotedPendantPower)
                        return VSPrototypeStorySceneIds.SwordEmpowered;
                    // 未救援世界 → B-c 謎の少女。R5 B-b2 既発火（救援打ち切り後）が前提。
                    // R5 を経由していないセーブ（試遊版 R4 開始等）では発火しない。
                    if (!mapState.IsBridgetRescued && !meta.HasRescuedBalduin && mapState.IsBalduinSurrendered)
                        return VSPrototypeStorySceneIds.MysteriousGirl;
                    return null;

                case 7:
                    // R6 中に救出した場合は R7 開始時に B-e 連鎖（ペンダント未気づき＆救援済）。
                    // B-e 完了時に HasNotedPendantPower=true が立つので、Bootstrap.OnSwordEmpoweredCompleted
                    // が同 R7 を再判定し A-c2 BossPurify に流れる（→ A-c2 経路でボス撃破可能）。
                    if ((meta.HasRescuedBalduin || mapState.IsBridgetRescued) && !meta.HasNotedPendantPower)
                        return VSPrototypeStorySceneIds.SwordEmpowered;
                    // A-c 開幕分岐：HasNotedPendantPower で必敗版／戦える版を切替
                    return meta.HasNotedPendantPower
                        ? VSPrototypeStorySceneIds.BossPurify
                        : VSPrototypeStorySceneIds.BossAttack;

                default:
                    return null; // R1 / R4 等は開始時演出なし
            }
        }
    }
}
