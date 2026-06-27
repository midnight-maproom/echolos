// メタ拠点強化定義の Domain 完成品クラス。
// Catalog（Data 層）が SO/POCO から組み立てて Presentation 層に渡す Domain 型。
// MetaRewardFormula と同様、Domain → Data の逆依存を避けるため Domain 型を返す。
// 振る舞いを持たず純粋な値オブジェクト（適用ロジックは MetaProgressState.ApplyUpgrade が担う）。
using System;
using System.Collections.Generic;

namespace Echolos.Domain.Meta
{
    /// <summary>メタ拠点強化 1 項目の Domain 完成品（Catalog 経由で組み立てられる不変オブジェクト）。</summary>
    public sealed class MetaUpgrade
    {
        /// <summary>SO 主キー（MetaUpgradeIds の const string と一致）。</summary>
        public string Id { get; }

        /// <summary>UI 表示名（例：「王女初期 ATK +3」）。</summary>
        public string DisplayName { get; }

        /// <summary>UI 効果説明文（例：「王女の初期攻撃力を恒久強化」）。</summary>
        public string EffectText { get; }

        /// <summary>
        /// 段階別購入コスト（王国の記憶）。長さは <see cref="Cap"/> と一致。
        /// Costs[N] = N 回目購入時のコスト（0-indexed）＝現 Lv N → Lv N+1 のコスト。
        /// </summary>
        public IReadOnlyList<int> Costs { get; }

        /// <summary>強化上限 Lv。</summary>
        public int Cap { get; }

        public MetaUpgrade(string id, string displayName, string effectText, IReadOnlyList<int> costs, int cap)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id は必須", nameof(id));
            if (cap <= 0)
                throw new ArgumentOutOfRangeException(nameof(cap), "cap は 1 以上");
            if (costs == null) throw new ArgumentNullException(nameof(costs));
            if (costs.Count != cap)
                throw new ArgumentException($"costs の長さ ({costs.Count}) は cap ({cap}) と一致させる", nameof(costs));
            for (int i = 0; i < costs.Count; i++)
                if (costs[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(costs), $"costs[{i}] は 0 以上");

            Id = id;
            DisplayName = displayName ?? "";
            EffectText = effectText ?? "";
            Costs = costs;
            Cap = cap;
        }

        /// <summary>
        /// 現 Lv（適用回数 0..Cap）から次段階購入のコストを返す。
        /// 上限到達（currentLv &gt;= Cap）時は 0 を返す（呼び出し側は購入不可フラグ判定で別途ガード）。
        /// </summary>
        public int GetCostForNextLevel(int currentLv)
        {
            if (currentLv < 0 || currentLv >= Costs.Count) return 0;
            return Costs[currentLv];
        }
    }
}
