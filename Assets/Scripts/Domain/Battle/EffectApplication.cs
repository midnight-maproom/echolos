using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // 1 Event 内で「どの unit に」「どの効果が」付与されたかをペアで表す値オブジェクト。
    // 戦闘開始時のシナジー/オーラ/ユニット固有 PersistentEffects のように、複数 unit ×
    // 複数 effect が同時に付与される集約イベント（BattleEvent.BulkEffectChanges）で使う。
    public sealed class EffectApplication
    {
        public RuntimeUnit Unit { get; }
        public EffectChange Change { get; }

        public EffectApplication(RuntimeUnit unit, EffectChange change)
        {
            Unit = unit;
            Change = change;
        }
    }
}
