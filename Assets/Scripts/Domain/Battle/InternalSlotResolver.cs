using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 配置 UI（SlotIndex で空き含む 6 枠）と内部スロット（生存者だけを詰めた 0 ベース連番）の橋渡し。
    // 配置 ATK 補正・ターゲティング・反撃判定は内部スロットで行う（仕様 320 §2.1.1）。
    public static class InternalSlotResolver
    {
        public static int GetInternalSlotIndex(IList<RuntimeUnit> sideUnits, RuntimeUnit target)
        {
            if (sideUnits == null || target == null) return -1;
            if (!target.IsAlive) return -1;

            var alive = sideUnits.Where(u => u != null && u.IsAlive)
                                 .OrderBy(u => u.SlotIndex)
                                 .ToList();

            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] == target) return i;
            }
            return -1;
        }

        public static int GetAliveCount(IList<RuntimeUnit> sideUnits)
        {
            if (sideUnits == null) return 0;
            int count = 0;
            for (int i = 0; i < sideUnits.Count; i++)
            {
                if (sideUnits[i] != null && sideUnits[i].IsAlive) count++;
            }
            return count;
        }
    }
}
