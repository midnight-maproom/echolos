// Assets/Scripts/Core/Prototype/Stage3DraftService.cs
// 段階3：味方ユニットの3択ドラフトと招集（仕様 120 §9.5・§9.8・§10.5）。
//
// 適用場面：
//   - ゲーム開始時の初期3ドラフト（3回目は前衛セーフティネット適用）
//   - R1〜R6 のラウンド開始ドラフト（R6 はレア未抽選時に確定保証）
//   - 内政「招集」（§10.8・行動力1消費・同一ラウンド1回まで）
//
// プール（§10.5）：
//   通常プール 12体：前衛6（重装兵/大盾兵/双剣士/サムライ/大槌兵/アサシン）＋後衛6（弓兵/
//                     炎魔導士/雷魔導士/司祭/巫女/踊り子）
//   レアプール  3体：騎士★・忍者★・軍師★
//   敵専用ユニット（散兵・ボス）はドラフトに登場しない。
//
// レア抽選：各ドラフトで Stage3CampaignConfig.RareDraftProbability（15%）でレアプールへ切替。
// R6 のラウンド開始ドラフトのみ、同一ラン内未抽選なら確定でレアになる。
using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>ドラフトの候補プール定義（兵種ファクトリの不変リスト）。</summary>
    public static class Stage3DraftPools
    {
        /// <summary>通常プール 前衛枠（6体・Melee 近接）。</summary>
        public static readonly Func<Unit>[] NormalFront = new Func<Unit>[]
        {
            Stage3Roster.GeneralTank, // 重装兵
            Stage3Roster.HpTank,      // 大盾兵
            Stage3Roster.Attacker1,   // 双剣士
            Stage3Roster.Samurai,     // サムライ
            Stage3Roster.Debuffer,    // 大槌兵
            Stage3Roster.Assassin,    // アサシン
        };

        /// <summary>通常プール 後衛枠（6体・Mid/Ranged）。</summary>
        public static readonly Func<Unit>[] NormalBack = new Func<Unit>[]
        {
            Stage3Roster.Archer,   // 弓兵
            Stage3Roster.FireMage, // 炎魔導士
            Stage3Roster.AoeMage,  // 雷魔導士
            Stage3Roster.Healer1,  // 司祭
            Stage3Roster.Healer2,  // 巫女
            Stage3Roster.Buffer,   // 踊り子
        };

        /// <summary>通常プール全体（12体・前衛6＋後衛6）。</summary>
        public static readonly Func<Unit>[] Normal = NormalFront.Concat(NormalBack).ToArray();

        /// <summary>レアプール（3体・騎士★/忍者★/軍師★）。</summary>
        public static readonly Func<Unit>[] Rare = new Func<Unit>[]
        {
            Stage3Roster.Paladin,    // 騎士★
            Stage3Roster.Ninja,      // 忍者★
            Stage3Roster.Tactician,  // 軍師★
        };

        /// <summary>
        /// 兵種が「前衛枠」か判定する（§9.5 前衛セーフティネット用）。
        /// 仕様§10.3 の「前衛7体」と一致：Range が Melee（近接）のユニットを前衛扱い。
        /// レア騎士も Melee で前衛として扱われる。
        /// </summary>
        public static bool IsFrontUnit(Unit u) => u != null && u.Range == AttackRange.Melee;
    }

    /// <summary>ドラフトの候補オファー（3択）。</summary>
    public sealed class DraftOffer
    {
        public DraftOffer(List<Unit> candidates, bool isRare, bool isFrontOnly)
        {
            Candidates = candidates ?? new List<Unit>();
            IsRare = isRare;
            IsFrontOnly = isFrontOnly;
        }

        /// <summary>提示された候補ユニット（最大3体・レアプールが3体未満なら少なくなる）。</summary>
        public List<Unit> Candidates { get; }

        /// <summary>このドラフトがレアプールから抽選されたか。</summary>
        public bool IsRare { get; }

        /// <summary>このドラフトが前衛セーフティネットによって前衛のみで構成されたか（§9.5）。</summary>
        public bool IsFrontOnly { get; }
    }

    /// <summary>
    /// ドラフト・招集の3択生成サービス（純C#・MonoBehaviour非依存）。
    /// rng は 0〜int.MaxValue を返す乱数源（テスト時は固定可能・剰余で確率/索引化する）。
    /// </summary>
    public sealed class Stage3DraftService
    {
        private readonly Func<int> _rng;

        public Stage3DraftService(Func<int> rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        // ══════════════════════════════════════════════
        // ゲーム開始時の初期ドラフト（§9.5・2026-06-02 改訂）
        // ══════════════════════════════════════════════

        /// <summary>
        /// ゲーム開始時のドラフトのうち pickIndex 番目（0始まり）の3択を生成する。
        /// 2026-06-02 改訂：姫騎士（固有キャラ）が前衛固定で初期手駒に加入するため、
        /// 「前衛セーフティネット（3回目は前衛0なら前衛のみ）」を撤回。
        /// 初期ドラフトはレア抽選を行わない（通常プール固定）。
        /// </summary>
        public DraftOffer DrawInitialPick(Stage3CampaignState state, int pickIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return Pick3From(Stage3DraftPools.Normal, isRare: false, isFrontOnly: false);
        }

        // ══════════════════════════════════════════════
        // ラウンド開始ドラフト（§9.5・§10.5）
        // ══════════════════════════════════════════════

        /// <summary>
        /// R1-R6 のラウンド開始ドラフトを生成する。
        /// R6 のみ、同一ラン内未抽選なら確定でレアになる（§10.5）。それ以外はレア抽選率に従う。
        /// </summary>
        public DraftOffer DrawRoundStartPick(Stage3CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            bool forceRare = state.CurrentRound >= state.Config.RareGuaranteeRound
                && !state.HasDrawnRareThisRun;

            bool isRare = forceRare || RollRare(state);
            return DrawByRarity(isRare);
        }

        // ══════════════════════════════════════════════
        // 招集ドラフト（§10.8）
        // ══════════════════════════════════════════════

        /// <summary>内政「招集」コマンドのドラフトを生成する（15%でレア抽選・確定保証なし）。</summary>
        public DraftOffer DrawConscriptPick(Stage3CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            bool isRare = RollRare(state);
            return DrawByRarity(isRare);
        }

        // ══════════════════════════════════════════════
        // ユニット加入処理（ヘルパー）
        // ══════════════════════════════════════════════

        /// <summary>
        /// ドラフト結果から1体を選んで手駒に加える。state の兵種強化Lv が自動適用される。
        /// レア抽選フラグも記録する（R6保証用）。
        /// 返り値：加入したユニット（null＝候補が無効）。
        /// </summary>
        public Unit AcceptPick(Stage3CampaignState state, DraftOffer offer, int candidateIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (offer == null) return null;
            if (candidateIndex < 0 || candidateIndex >= offer.Candidates.Count) return null;

            var picked = offer.Candidates[candidateIndex];
            state.AddUnitToRoster(picked);
            if (offer.IsRare) state.HasDrawnRareThisRun = true;
            return picked;
        }

        // ══════════════════════════════════════════════
        // 内部ヘルパー
        // ══════════════════════════════════════════════

        private DraftOffer DrawByRarity(bool isRare)
        {
            var pool = isRare ? Stage3DraftPools.Rare : Stage3DraftPools.Normal;
            return Pick3From(pool, isRare, isFrontOnly: false);
        }

        /// <summary>
        /// レア抽選を行う。Config.RareDraftProbability を超える/未満の rng 値で判定。
        /// rng の値が 0〜int.MaxValue で、確率 p なら roll &lt; p * 100 のとき true。
        /// </summary>
        private bool RollRare(Stage3CampaignState state)
        {
            int roll = Math.Abs(_rng()) % 100; // 0〜99
            int threshold = (int)(state.Config.RareDraftProbability * 100);
            return roll < threshold;
        }

        /// <summary>
        /// プールから重複なく N=min(3, pool.Length) 体を抽出した DraftOffer を返す。
        /// Fisher-Yates シャッフルの先頭3要素を取る方式（プール本体は不変）。
        /// レアプール（3体）の場合は全件を順序ランダムで返す。
        /// </summary>
        private DraftOffer Pick3From(Func<Unit>[] pool, bool isRare, bool isFrontOnly)
        {
            if (pool == null || pool.Length == 0)
                return new DraftOffer(new List<Unit>(), isRare, isFrontOnly);

            int take = Math.Min(3, pool.Length);
            var indices = new int[pool.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            // 先頭 take 個だけ確定すれば十分（部分的 Fisher-Yates）。
            for (int i = 0; i < take; i++)
            {
                int span = indices.Length - i;
                int j = i + (Math.Abs(_rng()) % span);
                if (j != i) (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var picks = new List<Unit>(take);
            for (int i = 0; i < take; i++) picks.Add(pool[indices[i]]());
            return new DraftOffer(picks, isRare, isFrontOnly);
        }
    }
}
