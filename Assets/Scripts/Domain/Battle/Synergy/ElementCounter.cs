using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Synergy
{
    // 陣営の Element 別生存体数カウント純関数。
    // シナジー Processor が体数依存の強度算出に使う。
    public static class ElementCounter
    {
        public static int CountAliveByElement(IList<RuntimeUnit> sideUnits, Element element)
        {
            if (sideUnits == null) return 0;
            int count = 0;
            for (int i = 0; i < sideUnits.Count; i++)
            {
                var u = sideUnits[i];
                if (u == null || !u.IsAlive) continue;
                if (u.BaseUnit == null) continue;
                if (u.BaseUnit.UnitElement == element) count++;
            }
            return count;
        }
    }
}
