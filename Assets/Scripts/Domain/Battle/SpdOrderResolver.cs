using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 行動順決定の純関数。SPD 降順 → 陣営間タイブレーク（攻め側優先）→ 同一陣営内 slot 昇順 で
    // 完全決定論的に行動順を返す（ランダム性排除）。
    // getSpd 省略時は BaseSPD を用いる。凍結等で実効 SPD が変動する場合は呼び出し側で
    // 算出関数を渡す（Waza.SPD 依存ロジックは呼び出し側責務）。
    public static class SpdOrderResolver
    {
        public static List<RuntimeUnit> OrderByTurnPriority(
            IList<RuntimeUnit> allies,
            IList<RuntimeUnit> enemies,
            bool isAlliesAttackingSide,
            Func<RuntimeUnit, int> getSpd = null)
        {
            if (getSpd == null) getSpd = u => u.BaseUnit.BaseSPD;

            var entries = new List<(RuntimeUnit Unit, bool IsAlly, int Spd)>();
            if (allies != null)
            {
                foreach (var u in allies)
                {
                    if (u == null || !u.IsAlive) continue;
                    entries.Add((u, true, getSpd(u)));
                }
            }
            if (enemies != null)
            {
                foreach (var u in enemies)
                {
                    if (u == null || !u.IsAlive) continue;
                    entries.Add((u, false, getSpd(u)));
                }
            }

            return entries
                .OrderByDescending(e => e.Spd)
                .ThenByDescending(e => isAlliesAttackingSide ? e.IsAlly : !e.IsAlly)
                .ThenBy(e => e.Unit.SlotIndex)
                .Select(e => e.Unit)
                .ToList();
        }

        // 動的順序方式：未行動かつ生存ユニットの中から実行時 SPD で次の 1 体を選ぶ。
        // 行動順番が回ってきたタイミングで毎回呼ぶことで、ターン中の SPD 変動（バフ/デバフ・
        // Freeze スタック増減等）を即反映する。該当ユニットがいなければ null。
        // タイブレークは OrderByTurnPriority と完全同一（陣営間：攻め側優先 → slot 昇順）。
        public static RuntimeUnit SelectNext(
            IList<RuntimeUnit> allies,
            IList<RuntimeUnit> enemies,
            bool isAlliesAttackingSide,
            Func<RuntimeUnit, int> getSpd = null)
        {
            if (getSpd == null) getSpd = u => u.BaseUnit.BaseSPD;

            var entries = new List<(RuntimeUnit Unit, bool IsAlly, int Spd)>();
            if (allies != null)
            {
                foreach (var u in allies)
                {
                    if (u == null || !u.IsAlive || u.HasActedThisTurn) continue;
                    entries.Add((u, true, getSpd(u)));
                }
            }
            if (enemies != null)
            {
                foreach (var u in enemies)
                {
                    if (u == null || !u.IsAlive || u.HasActedThisTurn) continue;
                    entries.Add((u, false, getSpd(u)));
                }
            }

            if (entries.Count == 0) return null;

            return entries
                .OrderByDescending(e => e.Spd)
                .ThenByDescending(e => isAlliesAttackingSide ? e.IsAlly : !e.IsAlly)
                .ThenBy(e => e.Unit.SlotIndex)
                .First().Unit;
        }
    }
}
