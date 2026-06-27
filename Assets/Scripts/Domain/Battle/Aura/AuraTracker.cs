using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Aura
{
    // オーラの動的解除を担当するトラッカー。
    // 戦闘中にユニットが戦闘不能になったとき、その unit に依存するオーラ
    // （SourceUnit または RequiredPartner）を陣営から自動剥奪する。
    //
    // 【Event 順序保証】
    // HandleUnitDied は「死亡 unit を pending キューに追加」するだけで即時剥奪しない。
    // 実際の剥奪は FlushPendingDeaths を「Died Event が積まれた後の節目」で呼ぶ：
    //   - Executor.OnActionResolved 後（攻撃由来の死亡経路・ActionResolved Event の後に flush）
    //   - Manager.OnEndPhase 後（Burn / 呪い即死経路・OnStatusEffectKill 経由の Died Event の後）
    // これにより Aura 解除 Event は構造的に Died Event より後に積まれ、ログ進行と画面表示が 1:1 で揃う。
    //
    // 【識別子】
    // AuraApplier は付与時に eff.AuraSourceId = def.SourceAbilityName をセット。
    // 本クラスはこの一致で「どのオーラ起源か」を判別する。
    public sealed class AuraTracker
    {
        private readonly BattleContext _context;
        private readonly IReadOnlyList<AuraDefinition> _definitions;
        private readonly List<RuntimeUnit> _pendingDeaths = new List<RuntimeUnit>();

        public AuraTracker(BattleContext context, IReadOnlyList<AuraDefinition> definitions)
        {
            _context = context;
            _definitions = definitions;
        }

        /// <summary>
        /// 死亡 unit を pending キューに追加するだけ。即時剥奪はしない。
        /// Executor.OnUnitDied / StatusProcessor.OnStatusEffectKill の両経路から呼ばれる。
        /// 同一 unit の重複追加は無視（複数経路で発火しうるため）。
        /// </summary>
        public void HandleUnitDied(BattleContext context, RuntimeUnit deadUnit)
        {
            if (deadUnit == null) return;
            if (!_pendingDeaths.Contains(deadUnit))
                _pendingDeaths.Add(deadUnit);
        }

        /// <summary>
        /// pending キュー内の全死亡について Aura 剥奪を実行＋クリア。
        /// Executor.OnActionResolved 後 / Manager.OnEndPhase 後の節目で呼ぶ。
        /// </summary>
        public void FlushPendingDeaths(BattleContext context)
        {
            if (_pendingDeaths.Count == 0) return;
            if (_definitions == null) { _pendingDeaths.Clear(); return; }

            foreach (var dead in _pendingDeaths)
                ProcessDeath(dead);
            _pendingDeaths.Clear();
        }

        private void ProcessDeath(RuntimeUnit deadUnit)
        {
            if (deadUnit == null || deadUnit.BaseUnit == null) return;

            string deadId = deadUnit.BaseUnit.Id;
            if (string.IsNullOrEmpty(deadId)) return;

            // 死亡 unit に依存する Def を全部洗い出し、両陣営から該当オーラを剥奪する。
            foreach (var def in _definitions)
            {
                if (def == null) continue;
                if (!DependsOn(def, deadId)) continue;
                RemoveAuraFromSide(_context.AllyUnits, def.SourceAbilityName);
                RemoveAuraFromSide(_context.EnemyUnits, def.SourceAbilityName);
            }
        }

        private static bool DependsOn(AuraDefinition def, string deadUnitId)
        {
            if (def.SourceUnitId == deadUnitId) return true;
            if (def.RequiredPartnerUnitIds != null)
            {
                foreach (var id in def.RequiredPartnerUnitIds)
                    if (id == deadUnitId) return true;
            }
            return false;
        }

        private static void RemoveAuraFromSide(IList<RuntimeUnit> side, string sourceAbilityName)
        {
            if (side == null || string.IsNullOrEmpty(sourceAbilityName)) return;
            foreach (var u in side)
            {
                if (u == null) continue;
                u.RemoveEffectsWhere(e => e != null && e.AuraSourceId == sourceAbilityName);
            }
        }
    }
}
