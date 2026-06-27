using System;
using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // ActionDeclaration を受け取り、Waza の Effects を多段ループで適用する。
    //
    // 多段攻撃のターゲット死亡時挙動：「不発で消化（自動再選定なし）」。
    // - 単体技：ターゲット死亡で残ヒットスキップ
    // - 範囲技：1 体死亡しても他生存対象には継続適用
    //
    // 命中/回避・配置補正・地形補正・反撃・クリ等の攻撃チェーンは AttackEffect 内部に凝集。
    // 本クラスは Effects を順次 Apply するシン・コーディネーターに徹する。
    //
    // 終端で OnActionResolved（Outcomes 集約）と OnUnitDied（重複なし）を発火する。
    public sealed class ActionExecutor
    {
        private readonly Func<int> _random0To99;

        /// <summary>
        /// 1 ユニットの行動が完全に処理された直後に発火する。Outcomes は反撃・Rider 含む全件。
        /// BattleEventRecorder が購読して BattleReport.Events / Log に書き出す想定。
        /// </summary>
        public event Action<BattleContext, ActionDeclaration, IReadOnlyList<HitOutcome>> OnActionResolved;

        /// <summary>
        /// ユニットが死亡（HP0 到達）したときに発火する。
        /// StatusEffectProcessor.HandleUnitDied のフック点・アンデッド復活処理のため必須。
        /// 同一 Target を重複通知しない（HashSet で 1 行動 1 回保証）。
        /// </summary>
        public event Action<BattleContext, RuntimeUnit> OnUnitDied;

        /// <param name="random0To99">
        /// 0〜99 の整数を返す関数。null の場合は System.Random によるデフォルト実装が使われる。
        /// テスト時は固定値を返す関数を渡すことで決定論的に動作する。
        /// </param>
        public ActionExecutor(Func<int> random0To99 = null)
        {
            if (random0To99 != null)
            {
                _random0To99 = random0To99;
            }
            else
            {
                // 本番用：System.Random（戦闘は単一スレッド想定）
                var rng = new Random();
                _random0To99 = () => rng.Next(0, 100);
            }
        }

        /// <summary>
        /// 行動宣言を実行し、発生した HitOutcome リストを返す。
        /// 待機・行動者死亡・Waza 不在のいずれかなら空リストを返す。
        /// CD・使用回数を更新する。
        /// </summary>
        public IList<HitOutcome> Execute(
            ActionDeclaration declaration,
            BattleContext context)
        {
            var outcomes = new List<HitOutcome>();
            if (declaration == null) return outcomes;
            if (declaration.IsWaiting) return outcomes;
            if (declaration.DeclaredWaza == null) return outcomes;

            var actor = declaration.Actor;
            if (actor == null || !actor.IsAlive) return outcomes;

            var waza = declaration.DeclaredWaza;
            var targets = declaration.Targets;
            if (targets == null) return outcomes;

            int hitCount = waza.HitCount > 0 ? waza.HitCount : 1;
            for (int hit = 0; hit < hitCount; hit++)
            {
                if (!actor.IsAlive) break;

                var aliveTargets = new List<RuntimeUnit>();
                foreach (var t in targets)
                    if (t != null && t.IsAlive) aliveTargets.Add(t);
                if (aliveTargets.Count == 0) break;

                var ctx = new ActionContext
                {
                    Battle = context,
                    Actor = actor,
                    Targets = aliveTargets,
                    Random0To99 = _random0To99,
                    Outcomes = outcomes,
                    CurrentWazaId = waza.Id,
                };

                if (waza.Effects != null)
                {
                    foreach (var effect in waza.Effects)
                        effect?.Apply(ctx);
                }
            }

            if (waza.Cooldown > 0)
                waza.CurrentCooldown = waza.Cooldown;
            waza.CurrentUses++;

            NotifyDeaths(context, outcomes);
            OnActionResolved?.Invoke(context, declaration, outcomes);

            return outcomes;
        }

        private void NotifyDeaths(BattleContext context, IList<HitOutcome> outcomes)
        {
            if (OnUnitDied == null || outcomes == null || outcomes.Count == 0) return;
            var notified = new HashSet<RuntimeUnit>();
            foreach (var outcome in outcomes)
            {
                if (outcome == null || !outcome.ResultedInDeath || outcome.Target == null) continue;
                if (notified.Add(outcome.Target))
                    OnUnitDied.Invoke(context, outcome.Target);
            }
        }

        // 内部 ActionContext 実装（外部に IActionContext の実装クラスを露出させない）
        private sealed class ActionContext : IActionContext
        {
            public BattleContext Battle { get; set; }
            public RuntimeUnit Actor { get; set; }
            public IList<RuntimeUnit> Targets { get; set; }
            public Func<int> Random0To99 { get; set; }
            public IList<HitOutcome> Outcomes { get; set; }
            public string CurrentWazaId { get; set; }
        }
    }
}
