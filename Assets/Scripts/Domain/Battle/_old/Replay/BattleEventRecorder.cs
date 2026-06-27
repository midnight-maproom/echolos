using System;
using System.Collections.Generic;
using Echolos.Domain.Battle.Conditional;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>
    /// BattleManager / ActionExecutor / StatusEffectProcessor / RuntimeUnit が発火するイベントを購読し、
    /// 結果を BattleReport.Log（テキスト）と BattleReport.Events（構造化）の両方に書き出すクラス。
    /// 文字列整形はすべて <see cref="BattleLogFormatter"/> に委譲する。
    ///
    /// 利用順序：Recorder の Attach*／Subscribe* を先に呼び、戦闘ロジックの結線（BattleAssembly.WireBattleLogic）は後で呼ぶ。
    /// 旧 BattleRunner.Run の購読順をそのまま保ち、行動ヘッダログが実行より先に並ぶ契約を維持する。
    /// </summary>
    public sealed class BattleEventRecorder
    {
        private readonly BattleReport _report;
        private readonly Func<RuntimeUnit, string> _nameOf;

        public BattleEventRecorder(BattleReport report, Func<RuntimeUnit, string> nameOf)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _nameOf = nameOf ?? throw new ArgumentNullException(nameof(nameOf));
        }

        /// <summary>ターン開始・行動宣言・スキップを購読する。</summary>
        public void AttachToManager(BattleManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            manager.OnStartPhase += ctx =>
            {
                _report.Log.Add($"── ターン{ctx.CurrentTurn} ──");
                _report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.TurnStart,
                    Turn = ctx.CurrentTurn,
                });
            };

            manager.OnActionExecuting += (ctx, decl) =>
            {
                if (decl.IsWaiting || decl.DeclaredWaza == null) return;
                string wazaName = decl.DeclaredWaza.Name;
                _report.Log.Add($"{_nameOf(decl.Actor)} → {wazaName}");
                _report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.ActionDeclared,
                    Turn = ctx.CurrentTurn,
                    Actor = decl.Actor,
                    Targets = new List<RuntimeUnit>(decl.Targets),
                    WazaName = wazaName,
                });
            };

            manager.OnActionSkipped += (ctx, unit) =>
            {
                _report.Log.Add($"{_nameOf(unit)} は行動できない（麻痺/凍結）");
                _report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.ActionSkipped,
                    Turn = ctx.CurrentTurn,
                    Actor = unit,
                    SkipReason = "行動不能",
                });
            };
        }

        /// <summary>アクション完結を購読し、Outcomes を集約してログ 1 行 + ActionResolved イベントを残す。</summary>
        public void AttachToExecutor(ActionExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            executor.OnActionResolved += (ctx, decl, outcomes) =>
            {
                string line = BattleLogFormatter.FormatOutcomesLogLine(outcomes, _nameOf);
                if (!string.IsNullOrEmpty(line)) _report.Log.Add(line);

                _report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.ActionResolved,
                    Turn = ctx.CurrentTurn,
                    Actor = decl.Actor,
                    WazaName = decl.DeclaredWaza?.Name,
                    Outcomes = new List<HitOutcome>(outcomes),
                });
            };
        }

        /// <summary>燃焼/毒の継続ダメージを購読する。</summary>
        public void AttachToStatusProcessor(StatusEffectProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            processor.OnBurnTickDamage += (ctx, unit, dmg) =>
            {
                _report.Log.Add($"    ☠ {_nameOf(unit)} に毒/燃焼 {dmg}");
                _report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.BurnTick,
                    Turn = ctx.CurrentTurn,
                    Target = unit,
                    Damage = dmg,
                    TargetHPAfter = unit.CurrentHP,
                });
            };
        }

        /// <summary>
        /// ユニットの OnEffectAdded / OnEffectRemoved を購読する。
        /// ActionGuard はバッジ表示しない 1:1 対応で自明な効果のためログ・イベントを抑制。
        /// Persistent / Conditional の剥奪は副次効果のためテキストログ抑制（イベントは残す）。
        /// Conditional バフ Processor の BuffApplied / BuffRemoved フックもここで発火する。
        /// </summary>
        public void SubscribeUnitEffects(RuntimeUnit unit, BattleContext context, BattleManager manager)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            unit.OnEffectAdded += eff =>
            {
                if (eff.Category != BuffCategory.ActionGuard)
                {
                    _report.Log.Add($"    + {_nameOf(unit)} に {BattleLogFormatter.FormatEffectWithSource(eff)} 付与");
                    _report.Events.Add(new BattleEvent
                    {
                        Kind = BattleEventKind.StatusEffectApplied,
                        Turn = context.CurrentTurn,
                        Target = unit,
                        EffectType = eff.EffectType,
                    });
                }
                manager.DispatchConditional(ConditionalBuffHook.BuffApplied, context);
            };

            unit.OnEffectRemoved += eff =>
            {
                bool suppressTextLog = eff.Category == BuffCategory.Persistent
                                    || eff.Category == BuffCategory.Conditional
                                    || eff.Category == BuffCategory.ActionGuard;
                if (!suppressTextLog)
                {
                    _report.Log.Add($"    - {_nameOf(unit)} の {BattleLogFormatter.FormatEffectWithSource(eff)} 解除");
                }
                if (eff.Category != BuffCategory.ActionGuard)
                {
                    _report.Events.Add(new BattleEvent
                    {
                        Kind = BattleEventKind.StatusEffectExpired,
                        Turn = context.CurrentTurn,
                        Target = unit,
                        EffectType = eff.EffectType,
                    });
                }
                manager.DispatchConditional(ConditionalBuffHook.BuffRemoved, context);
            };
        }

        /// <summary>
        /// Run() に渡された時点で既に付与されている状態効果を Turn=0 の StatusEffectApplied として記録する。
        /// 観戦ビューが初期スナップショットを復元できるよう、購読登録より先に呼ぶこと。
        /// </summary>
        public void RecordInitialEffectsSnapshot(RuntimeUnit unit)
        {
            if (unit == null) return;
            foreach (var e in unit.ActiveEffects)
            {
                _report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.StatusEffectApplied,
                    Turn = 0,
                    Target = unit,
                    EffectType = e.EffectType,
                });
            }
        }

        /// <summary>戦闘終了イベントを記録する。観戦ビューの結果オーバーレイ表示の目印。</summary>
        public void RecordBattleEnd(BattleResult result, int turn)
        {
            _report.Events.Add(new BattleEvent
            {
                Kind = BattleEventKind.BattleEnd,
                Turn = turn,
                Result = result,
            });
        }
    }
}
