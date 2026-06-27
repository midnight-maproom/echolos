// 3択ドラフトの生成と選択処理。
//
// 【適用場面】
// - Phase=InitialDraft 中の初期ドラフト（全枠 Normal プール固定・Rare 抽選なし）
// - Phase=InteriorAction 中の召集コマンド：
//   1) AllRareSpecialProbability で「★全 Rare スペシャル」判定（当選時 3 枠全 Rare）
//   2) 通常モードでは枠ごとに RarePerSlotProbabilities[i] で独立抽選
//
// DraftPool（Domain）を IDraftPoolCatalog から取得し、Unit 生成は IUnitCatalog 経由。
// VSプロト範囲では Catalog に DraftPool 1 件のみ登録される想定（先頭の 1 件を採用）。
using System;
using System.Collections.Generic;
using Echolos.Domain.Catalog;
using Echolos.Domain.Draft;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>1 ドラフトオファー（最大 3 択・枠ごとに Rare/Normal を保持）。</summary>
    public sealed class VSPrototypeDraftOffer
    {
        public VSPrototypeDraftOffer(List<Unit> candidates, IReadOnlyList<bool> candidateRarities, bool isAllRare)
        {
            Candidates = candidates ?? new List<Unit>();
            CandidateRarities = candidateRarities ?? Array.Empty<bool>();
            IsRare = isAllRare;
        }

        /// <summary>提示候補（最大 3 体）。</summary>
        public List<Unit> Candidates { get; }

        /// <summary>各候補が Rare プール由来か否か（Candidates と並列・要素数一致）。</summary>
        public IReadOnlyList<bool> CandidateRarities { get; }

        /// <summary>
        /// このオファーが「全枠 Rare」状態か（演出ヘッダ表示用）。
        /// 明示の AllRareSpecial 当選／独立抽選で偶発的に全 Rare／のどちらでも true。
        /// </summary>
        public bool IsRare { get; }
    }

    /// <summary>VSプロト 3 択ドラフト・選択処理サービス。</summary>
    public sealed class VSPrototypeDraftService
    {
        private readonly Func<int> _rng;
        private readonly IDraftPoolCatalog _draftPoolCatalog;
        private readonly IUnitCatalog _unitCatalog;

        public VSPrototypeDraftService(
            Func<int> rng,
            IDraftPoolCatalog draftPoolCatalog,
            IUnitCatalog unitCatalog)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _draftPoolCatalog = draftPoolCatalog ?? throw new ArgumentNullException(nameof(draftPoolCatalog));
            _unitCatalog = unitCatalog ?? throw new ArgumentNullException(nameof(unitCatalog));
        }

        // ドラフト生成

        /// <summary>
        /// 初期ドラフト用：全枠を通常プールから抽出（Rare 抽選なし）。
        /// </summary>
        public VSPrototypeDraftOffer DrawForInitial()
        {
            var pool = ActivePool();
            int take = Math.Min(pool.CandidatesPerOffer, pool.NormalUnitIds.Count);
            var picks = PickN(pool.NormalUnitIds, take);
            var rarities = new bool[picks.Count];
            return new VSPrototypeDraftOffer(picks, rarities, isAllRare: false);
        }

        /// <summary>
        /// 召集ドラフト用：
        ///   1) AllRareSpecialProbability で「★全 Rare スペシャル」判定
        ///   2) 通常モードでは枠ごとに RarePerSlotProbabilities[i] で独立抽選
        /// プール間の重複は発生しない（同一 ID は片プールにしか存在しない前提）。
        /// プール内の重複は PickN による部分シャッフルで排除する。
        /// </summary>
        public VSPrototypeDraftOffer DrawForConscript()
        {
            var pool = ActivePool();
            int take = pool.CandidatesPerOffer;

            bool allRareSpecial = RollProbability(pool.AllRareSpecialProbability);
            var perSlotIsRare = new bool[take];
            if (allRareSpecial)
            {
                for (int i = 0; i < take; i++) perSlotIsRare[i] = true;
            }
            else
            {
                for (int i = 0; i < take; i++)
                {
                    float p = i < pool.RarePerSlotProbabilities.Count
                        ? pool.RarePerSlotProbabilities[i] : 0f;
                    perSlotIsRare[i] = RollProbability(p);
                }
            }

            int rareCount = 0;
            for (int i = 0; i < take; i++) if (perSlotIsRare[i]) rareCount++;
            int normalCount = take - rareCount;

            var rarePicks   = PickN(pool.RareUnitIds,   Math.Min(rareCount,   pool.RareUnitIds.Count));
            var normalPicks = PickN(pool.NormalUnitIds, Math.Min(normalCount, pool.NormalUnitIds.Count));

            // 枠順を維持して Rare/Normal を差し込む（プレイヤー視点の枠位置を保つ）。
            var candidates = new List<Unit>(take);
            int rareIdx = 0, normalIdx = 0;
            for (int i = 0; i < take; i++)
            {
                if (perSlotIsRare[i])
                {
                    if (rareIdx < rarePicks.Count) candidates.Add(rarePicks[rareIdx++]);
                    else if (normalIdx < normalPicks.Count)
                    {
                        // レアプール枯渇時のフォールバック：枠に Normal を入れて Rarity も降格
                        candidates.Add(normalPicks[normalIdx++]);
                        perSlotIsRare[i] = false;
                    }
                }
                else
                {
                    if (normalIdx < normalPicks.Count) candidates.Add(normalPicks[normalIdx++]);
                    else if (rareIdx < rarePicks.Count)
                    {
                        candidates.Add(rarePicks[rareIdx++]);
                        perSlotIsRare[i] = true;
                    }
                }
            }

            bool isAllRare = candidates.Count > 0;
            for (int i = 0; i < candidates.Count; i++)
                if (!perSlotIsRare[i]) { isAllRare = false; break; }

            return new VSPrototypeDraftOffer(candidates, perSlotIsRare, isAllRare);
        }

        /// <summary>
        /// 自動加入用：プールから 1 体を抽選する。
        /// 優先順位：① 手持ち未所持の Normal 枠 → ② 手持ち未所持の Rare 枠 → ③ 全候補（Normal+Rare）からランダム（重複加入）。
        /// 候補ゼロ（プール未登録など）は null を返す。
        /// </summary>
        public Unit DrawAutoConscript(IReadOnlyCollection<string> ownedUnitIds)
        {
            var pool = ActivePool();
            var owned = ownedUnitIds != null
                ? new HashSet<string>(ownedUnitIds)
                : new HashSet<string>();

            var unownedNormal = FilterUnowned(pool.NormalUnitIds, owned);
            if (unownedNormal.Count > 0) return _unitCatalog.Get(PickOne(unownedNormal));

            var unownedRare = FilterUnowned(pool.RareUnitIds, owned);
            if (unownedRare.Count > 0) return _unitCatalog.Get(PickOne(unownedRare));

            var all = new List<string>(pool.NormalUnitIds.Count + pool.RareUnitIds.Count);
            all.AddRange(pool.NormalUnitIds);
            all.AddRange(pool.RareUnitIds);
            if (all.Count == 0) return null;
            return _unitCatalog.Get(PickOne(all));
        }

        private static List<string> FilterUnowned(IReadOnlyList<string> ids, HashSet<string> owned)
        {
            var list = new List<string>(ids.Count);
            foreach (var id in ids) if (!owned.Contains(id)) list.Add(id);
            return list;
        }

        private string PickOne(IReadOnlyList<string> ids) =>
            ids[Math.Abs(_rng()) % ids.Count];

        // 選択処理

        /// <summary>
        /// ドラフトから候補 1 体を選び、その時点の兵種強化 Lv を反映して返す。
        /// 呼び出し側（Bootstrap）が王国軍リスト（Roster）への追加と Phase 進行を担当する。
        /// 範囲外 candidateIndex や空オファーは null を返す（呼び出し側で握りつぶしてもよい）。
        /// </summary>
        public Unit AcceptPick(VSPrototypeInteriorState state, VSPrototypeDraftOffer offer, int candidateIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (offer == null) return null;
            if (candidateIndex < 0 || candidateIndex >= offer.Candidates.Count) return null;

            var picked = offer.Candidates[candidateIndex];
            // 新規召集ユニットは Lv1 で加わる（個別 Lv 強化は内政画面で実施）。
            return picked;
        }

        // 内部ヘルパー

        /// <summary>VSプロト範囲では Catalog に DraftPool 1 件のみ登録される想定（先頭を採用）。</summary>
        private DraftPool ActivePool()
        {
            var pools = _draftPoolCatalog.GetAll();
            if (pools == null || pools.Count == 0)
                throw new InvalidOperationException(
                    "DraftPoolCatalog に DraftPool が 1 件も登録されていません。" +
                    "Editor で SO アセットを生成してください（Echolos/Data メニュー）。");
            return pools[0];
        }

        /// <summary>確率 0.0〜1.0 で当選判定（0 以下は常に false・1 以上は常に true）。</summary>
        private bool RollProbability(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            int roll = Math.Abs(_rng()) % 100; // 0〜99
            int threshold = (int)(probability * 100);
            return roll < threshold;
        }

        /// <summary>
        /// 兵種 ID リストから重複なく N 体を抽出して Unit に変換する（部分 Fisher-Yates）。
        /// N が ids.Count を超える場合は ids.Count に丸める（呼び出し側で min 済みを推奨）。
        /// </summary>
        private List<Unit> PickN(IReadOnlyList<string> ids, int n)
        {
            if (ids == null || ids.Count == 0 || n <= 0) return new List<Unit>(0);

            int take = Math.Min(n, ids.Count);
            var indices = new int[ids.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            for (int i = 0; i < take; i++)
            {
                int span = indices.Length - i;
                int j = i + (Math.Abs(_rng()) % span);
                if (j != i)
                {
                    int tmp = indices[i];
                    indices[i] = indices[j];
                    indices[j] = tmp;
                }
            }

            var picks = new List<Unit>(take);
            for (int i = 0; i < take; i++) picks.Add(_unitCatalog.Get(ids[indices[i]]));
            return picks;
        }
    }
}
