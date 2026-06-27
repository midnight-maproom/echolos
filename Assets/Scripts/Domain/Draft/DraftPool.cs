// ドラフト候補プールの Domain 完成品クラス。
// Catalog（Data 層）が SO/POCO から組み立てて UseCase 層に渡す Domain 型。
// 振る舞いを持たず純粋な値オブジェクト（抽選ロジックは VSPrototypeDraftService が担う）。
using System;
using System.Collections.Generic;

namespace Echolos.Domain.Draft
{
    /// <summary>ドラフト候補プールの Domain 完成品（Catalog 経由で組み立てられる不変オブジェクト）。</summary>
    public sealed class DraftPool
    {
        /// <summary>SO 主キー（VSプロト範囲では "vsproto_standard_pool" 1 件）。</summary>
        public string Id { get; }

        /// <summary>通常プールの兵種 ID リスト（初期ドラフト＋召集の非レア枠で使用）。</summary>
        public IReadOnlyList<string> NormalUnitIds { get; }

        /// <summary>レアプールの兵種 ID リスト（召集ドラフトの Rare 枠／全 Rare スペシャルで使用）。</summary>
        public IReadOnlyList<string> RareUnitIds { get; }

        /// <summary>
        /// 召集ドラフトの「★全 Rare スペシャル」確率（0.0〜1.0）。
        /// 当選すると 3 枠全てが Rare プールから抽出される。
        /// </summary>
        public float AllRareSpecialProbability { get; }

        /// <summary>
        /// 通常モード時の枠別 Rare 抽選確率（要素数は <see cref="CandidatesPerOffer"/> と一致）。
        /// 各枠ごとに独立判定され、true の枠は Rare プール／false の枠は Normal プールから抽出。
        /// </summary>
        public IReadOnlyList<float> RarePerSlotProbabilities { get; }

        /// <summary>1 ドラフトの提示数（VSプロトは 3 択固定）。</summary>
        public int CandidatesPerOffer { get; }

        public DraftPool(
            string id,
            IReadOnlyList<string> normalUnitIds,
            IReadOnlyList<string> rareUnitIds,
            float allRareSpecialProbability,
            IReadOnlyList<float> rarePerSlotProbabilities,
            int candidatesPerOffer)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id は必須", nameof(id));
            if (allRareSpecialProbability < 0f || allRareSpecialProbability > 1f)
                throw new ArgumentOutOfRangeException(nameof(allRareSpecialProbability), "0.0〜1.0 の範囲");
            if (candidatesPerOffer <= 0)
                throw new ArgumentOutOfRangeException(nameof(candidatesPerOffer), "1 以上");
            if (rarePerSlotProbabilities == null)
                throw new ArgumentNullException(nameof(rarePerSlotProbabilities));
            if (rarePerSlotProbabilities.Count != candidatesPerOffer)
                throw new ArgumentException(
                    $"RarePerSlotProbabilities の要素数 ({rarePerSlotProbabilities.Count}) は " +
                    $"CandidatesPerOffer ({candidatesPerOffer}) と一致する必要がある",
                    nameof(rarePerSlotProbabilities));
            for (int i = 0; i < rarePerSlotProbabilities.Count; i++)
            {
                float p = rarePerSlotProbabilities[i];
                if (p < 0f || p > 1f)
                    throw new ArgumentOutOfRangeException(nameof(rarePerSlotProbabilities),
                        $"枠 {i} の確率が範囲外: {p}");
            }
            Id = id;
            NormalUnitIds = normalUnitIds ?? Array.Empty<string>();
            RareUnitIds = rareUnitIds ?? Array.Empty<string>();
            AllRareSpecialProbability = allRareSpecialProbability;
            RarePerSlotProbabilities = rarePerSlotProbabilities;
            CandidatesPerOffer = candidatesPerOffer;
        }
    }
}
