// 王国軍リスト・ユニット強化リスト等で共通利用するユニットソートユーティリティ。
//
// 優先順：
//   1. 固有ユニット（王女 → ブリジット）
//   2. DraftPool Normal の index 順（前衛 6 → 後衛 6）
//   3. DraftPool Rare の index 順
//   4. プール非該当は末尾
// 二次ソート：同じ兵種なら Unit.Level 降順（Lv3 → Lv2 → Lv1）。
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;

namespace Echolos.Presentation.Common
{
    /// <summary>ユニット一覧表示用ソート（DraftPool 順 + 兵種内 Lv 降順）。</summary>
    public static class UnitRosterSorter
    {
        /// <summary>
        /// roster を DraftPool 順 + 同兵種は Lv 降順で並べた新しいリストを返す。
        /// 元 roster は変更しない。
        /// draftPoolCatalog が null またはプール 0 件のときは Normal/Rare キーが空で
        /// 全ユニットが「該当なし末尾」扱いになる（固有ユニットの優先と Lv 降順は機能する）。
        /// </summary>
        public static List<Unit> SortByPoolOrder(
            IEnumerable<Unit> roster,
            IDraftPoolCatalog draftPoolCatalog)
        {
            var pools = draftPoolCatalog?.GetAll();
            var pool = pools != null && pools.Count > 0 ? pools[0] : null;
            var normalIndex = new Dictionary<string, int>();
            var rareIndex = new Dictionary<string, int>();
            if (pool != null)
            {
                for (int i = 0; i < pool.NormalUnitIds.Count; i++) normalIndex[pool.NormalUnitIds[i]] = i;
                for (int i = 0; i < pool.RareUnitIds.Count; i++) rareIndex[pool.RareUnitIds[i]] = i;
            }

            int SortKey(Unit u)
            {
                if (u.Id == UniqueUnitIds.Princess) return -2;
                if (u.Id == UniqueUnitIds.Bridget) return -1;
                if (normalIndex.TryGetValue(u.Id, out int idx)) return idx;
                if (rareIndex.TryGetValue(u.Id, out int rIdx)) return 1000 + rIdx;
                return 9999;
            }

            return roster
                .OrderBy(SortKey)
                .ThenByDescending(u => u.Level)
                .ToList();
        }
    }
}
