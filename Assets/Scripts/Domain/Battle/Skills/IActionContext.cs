using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // IActionEffect.Apply が参照する戦況コンテキスト。
    // - Battle: 戦闘全体の状態（陣営・ターン等）
    // - Actor: この行動の実行ユニット
    // - Targets: この Effect が対象とするユニット群（ActionExecutor が事前に解決）
    // - Random0To99: 0-99 の整数を返す確率ロール関数（テスト時は固定値注入で決定論的）
    // - Outcomes: 各 Effect が結果を追加していく蓄積先（ActionExecutor が完了時に集約）
    // - CurrentWazaId: 実行中の Waza ID（WazaPowerBoost Upgrade で対象 Waza を特定するため）
    public interface IActionContext
    {
        BattleContext Battle { get; }
        RuntimeUnit Actor { get; }
        IList<RuntimeUnit> Targets { get; }
        Func<int> Random0To99 { get; }
        IList<HitOutcome> Outcomes { get; }
        string CurrentWazaId { get; }
    }
}
