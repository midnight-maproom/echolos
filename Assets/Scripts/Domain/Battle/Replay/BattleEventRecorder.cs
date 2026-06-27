using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    // BattleManager / ActionExecutor / StatusEffectProcessor /
    // RuntimeUnit のイベントを購読し、BattleReport.Log と BattleReport.Events に
    // 書き出す Recorder。文字列整形は BattleLogFormatter に委譲。
    //
    // 旧 BattleEventRecorder との差分：
    // - 行動宣言ログを OnActionResolved 内で集約（OnActionExecuting イベントが
    //   BattleManager に存在しないため）
    // - Conditional Hook 結線（DispatchConditional）は廃止（属性シナジー Persistent 化）
    // - OnHealOverTimePhase の購読：陣営単位で 1 行集約（per-unit Event は LogLine=null）
    public sealed class BattleEventRecorder
    {
        private readonly BattleReport _report;
        private readonly Func<RuntimeUnit, string> _nameOf;

        // 1 ユニットの OnActionStart 〜 OnActionResolved/OnActionSkipped の間 true。
        // この間に発火する OnEffectAdded は Waza 由来の付与として Log/Events 両方を抑制する
        // （ActionResolved の Outcome 集約行に「+EffectType」として含まれるため二重表示になる）。
        // アクション外（OnBattleStart のシナジー付与・テスト直接 Add 等）は引き続き記録する。
        private bool _inActionResolution = false;

        // 戦闘開始時のシナジー Persistent 付与を 1 行に集約するためのバッファ。
        // OnBattleStart 〜 FlushBattleStartBuffer の間に発火する OnEffectAdded はここに溜め、
        // FlushBattleStartBuffer 呼び出し時に SourceAbilityName ごとに 1 行ずつ集約する。
        private bool _collectingBattleStart = false;
        private BattleContext _battleStartContext;
        private readonly List<(RuntimeUnit Unit, IEffect Effect)> _battleStartBuffer
            = new List<(RuntimeUnit, IEffect)>();

        public BattleEventRecorder(BattleReport report, Func<RuntimeUnit, string> nameOf)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _nameOf = nameOf ?? throw new ArgumentNullException(nameof(nameOf));
        }

        // Log と Events の 1:1 を構造的に保証する共通ヘルパー。
        // logLine が null/空でも Event は LogLine=null で必ず Add される（cursor 進行は揃う＝
        // Event 単体で HP 更新等を Apply できる）。観戦ビューは Events[_cursor].LogLine を表示し、
        // null/空ならログ表示をスキップする。BattleReport.Log は Events.LogLine から動的生成。
        private void AddEvent(BattleEvent ev, string logLine)
        {
            if (ev == null) return;
            ev.LogLine = logLine;
            _report.Events.Add(ev);
        }

        public void AttachToManager(BattleManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            manager.OnStartPhase += ctx =>
            {
                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.TurnStart,
                    Turn = ctx.CurrentTurn,
                }, $"── ターン{ctx.CurrentTurn} ──");
            };

            manager.OnBattleStart += ctx =>
            {
                // SynergyApplier.ApplyAll 由来の付与をバッファに溜める準備。
                // BattleRunner.Run が InitializeBattle 完了後に FlushBattleStartBuffer を呼ぶ。
                _collectingBattleStart = true;
                _battleStartContext = ctx;
            };

            manager.OnActionStart += (ctx, unit) =>
            {
                _inActionResolution = true;
            };

            manager.OnActionEnd += (ctx, unit) =>
            {
                // 1 ユニットの行動完了。OnActionResolved/Skipped/呪い即死いずれの経路でも
                // 必ず呼ばれるため、ここで _inActionResolution を確実にリセットする。
                _inActionResolution = false;
            };

            manager.OnActionSkipped += (ctx, unit) =>
            {
                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.ActionSkipped,
                    Turn = ctx.CurrentTurn,
                    Actor = unit,
                    SkipReason = "行動不能",
                }, $"{_nameOf(unit)} は行動できない（麻痺/凍結）");
            };
        }

        public void AttachToExecutor(ActionExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            executor.OnActionResolved += (ctx, decl, outcomes) =>
            {
                // Log と Events を 1:1 に揃えるため、宣言行と Outcome 集約行を 1 行に統合する。
                // 観戦ビュー側は cursor を Log のインデックスにも流用しており、行数がずれると
                // HP バー反映とログ表示のタイミングが累積的にずれる。
                string wazaLine = (decl?.DeclaredWaza != null && decl.Actor != null)
                    ? $"{_nameOf(decl.Actor)} → {decl.DeclaredWaza.Name}"
                    : null;
                string outcomeLine = BattleLogFormatter.FormatOutcomesLogLine(outcomes, _nameOf);

                string combined;
                if (!string.IsNullOrEmpty(wazaLine) && !string.IsNullOrEmpty(outcomeLine))
                    combined = $"{wazaLine}  |  {outcomeLine}";
                else if (!string.IsNullOrEmpty(wazaLine))
                    combined = wazaLine;
                else
                    combined = outcomeLine;

                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.ActionResolved,
                    Turn = ctx.CurrentTurn,
                    Actor = decl?.Actor,
                    WazaName = decl?.DeclaredWaza?.Name,
                    Outcomes = new System.Collections.Generic.List<HitOutcome>(outcomes),
                }, combined);
                // _inActionResolution の解除は OnActionEnd で一元化（OnActionResolved/
                // OnActionSkipped/呪い即死いずれの経路でも確実に false に戻る）。
            };
        }

        public void AttachToStatusProcessor(StatusEffectProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            processor.OnBurnTickDamage += (ctx, unit, dmg) =>
            {
                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.BurnTick,
                    Turn = ctx.CurrentTurn,
                    Target = unit,
                    Damage = dmg,
                    TargetHPAfter = unit.CurrentHP,
                }, $"    ☠ {_nameOf(unit)} に毒/燃焼 {dmg}");
            };

            processor.OnHealOverTimePhase += (ctx, ticks) =>
            {
                if (ticks == null || ticks.Count == 0) return;

                // 1 Event に全 ticks＋集約 LogLine を載せる。観戦ビューは ApplyEvent で
                // HealTicks 全件の HP を一括反映＋ LogLine 1 行表示（ActionResolved と同形式）。
                string aggregated = BattleLogFormatter.FormatHealOverTimePhaseLine(ticks, ctx, _nameOf);
                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.HealOverTimePhase,
                    Turn = ctx.CurrentTurn,
                    HealTicks = new System.Collections.Generic.List<StatusEffectProcessor.HealOverTimeTick>(ticks),
                }, aggregated);
            };

            processor.OnStatusEffectKill += (ctx, unit) =>
            {
                // Burn / 呪い即死などで戦闘不能になった場合の通知。
                // 観戦ビューは BattleEventKind.Died で _alive=false に遷移してグレーアウト。
                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.Died,
                    Turn = ctx.CurrentTurn,
                    Target = unit,
                    TargetHPAfter = 0,
                }, $"    ☠ {_nameOf(unit)} が戦闘不能");
            };
        }

        // ユニットの OnEffectAdded / OnEffectRemoved を購読する。
        // ActionGuard は技と効果が 1:1 対応で自明なためログ・イベントとも抑制。
        // Persistent の剥奪は副次効果のためテキストログ抑制（イベントは残す）。
        public void SubscribeUnitEffects(RuntimeUnit unit, BattleContext context)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (context == null) throw new ArgumentNullException(nameof(context));

            // 購読前に既に乗っている PersistentEffects（Bootstrap.PrepareForBattle で
            // Unit.PersistentEffects Clone 付与済のユニット固有パッシブ：水の大盾兵「専守」等）も
            // 戦闘開始時集約に含めるため、_battleStartBuffer に予め積む。
            // FlushBattleStartBuffer 側で SourceAbilityName ごとに集約＋ BulkEffectChanges 埋め込み。
            if (unit.ActiveEffects != null)
            {
                foreach (var eff in unit.ActiveEffects)
                {
                    if (eff == null) continue;
                    if (eff is SelfGuard) continue;
                    _battleStartBuffer.Add((unit, eff));
                }
            }

            unit.OnEffectAdded += eff =>
            {
                if (eff is SelfGuard) return;
                // 戦闘開始時のシナジー付与は集約用バッファに溜め、FlushBattleStartBuffer で 1 行化。
                if (_collectingBattleStart)
                {
                    _battleStartBuffer.Add((unit, eff));
                    return;
                }
                // Waza 由来の付与（アクション解決中）は ActionResolved 集約行に含まれるため抑制。
                if (_inActionResolution) return;

                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.StatusEffectApplied,
                    Turn = context.CurrentTurn,
                    Target = unit,
                    EffectChange = EffectChange.From(eff),
                }, $"    + {_nameOf(unit)} に {BattleLogFormatter.FormatEffectWithSource(eff)} 付与");
            };

            unit.OnEffectRemoved += eff =>
            {
                // Waza 由来の解除（Dispel/Cleanse 経路）は ActionResolved 集約行に
                // RemovedEffects として含まれるため、ここでは Log/Events 両方を抑制する。
                // ただし Aura 起因の解除（AuraTracker が依存ユニット死亡で剥がす）は
                // ActionResolved に乗らないため、_inActionResolution 中でも Event を残す
                // ＝ UI snapshot の段階更新が漏れない（バッジが正しく消える）。
                bool isAuraOriginated = !string.IsNullOrEmpty(eff.AuraSourceId);
                if (_inActionResolution && !isAuraOriginated) return;
                if (eff is SelfGuard) return;

                // パッシブ系（Permanent + IsUndispellable=true）の剥奪は LogLine=null
                // （Event は残してバッジ更新に使う・ログ表示はしない）。
                // それ以外（Triggered / 状態異常）は通常通り解除ログを出す。
                bool suppressTextLog = eff.Lifetime == Lifetime.Permanent && eff.IsUndispellable;
                string logLine = suppressTextLog
                    ? null
                    : $"    - {_nameOf(unit)} の {BattleLogFormatter.FormatEffectWithSource(eff)} 解除";
                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.StatusEffectExpired,
                    Turn = context.CurrentTurn,
                    Target = unit,
                    EffectChange = EffectChange.From(eff),
                }, logLine);
            };
        }

        /// <summary>
        /// InitializeBattle 後（SynergyApplier.ApplyAll 完了後）に呼んで、戦闘開始時の Persistent
        /// 付与を SourceAbilityName ごとに 1 行へ集約する。同 Source の effect は「&」連結、対象は
        /// 「味方陣営の生存全員」と一致するなら「味方全体」、それ以外は unit 名 or 件数で表記する。
        /// </summary>
        public void FlushBattleStartBuffer()
        {
            _collectingBattleStart = false;
            if (_battleStartBuffer.Count == 0)
            {
                _battleStartContext = null;
                return;
            }

            var bySource = _battleStartBuffer.GroupBy(x => x.Effect.SourceAbilityName ?? "");
            int turn = _battleStartContext?.CurrentTurn ?? 0;

            foreach (var srcGroup in bySource)
            {
                // 同 Kind の代表 Effect から値（+Magnitude / Shield 残数）を取り出して
                // 「DefenseUp +10 & Shield 3」のように表記する。
                var byKind = srcGroup.GroupBy(x => x.Effect.Kind).ToList();
                var labels = byKind.Select(g => BattleLogFormatter.FormatEffectValue(g.First().Effect)).ToList();
                var representativeEffect = byKind[0].First().Effect;
                var distinctUnits = srcGroup.Select(x => x.Unit).Distinct().ToList();
                string effectsStr = string.Join(" & ", labels);
                string srcLabel = string.IsNullOrEmpty(srcGroup.Key) ? "" : $"{srcGroup.Key}：";
                string targetStr = ResolveBattleStartTargetLabel(distinctUnits);

                // 集約 Event に (unit, effect) ペアを全件積む。観戦ビューは BulkEffectChanges を
                // 順に snapshot へ反映＝複数 unit/複数 Kind が同 Event に乗っても全部バッジ表示される。
                var bulk = new List<EffectApplication>(srcGroup.Count());
                foreach (var pair in srcGroup)
                    bulk.Add(new EffectApplication(pair.Unit, EffectChange.From(pair.Effect)));

                AddEvent(new BattleEvent
                {
                    Kind = BattleEventKind.StatusEffectApplied,
                    Turn = turn,
                    Target = distinctUnits.Count == 1 ? distinctUnits[0] : null,
                    EffectChange = EffectChange.From(representativeEffect),
                    BulkEffectChanges = bulk,
                }, $"    + {targetStr} に {srcLabel}{effectsStr} 付与");
            }

            _battleStartBuffer.Clear();
            _battleStartContext = null;
        }

        private string ResolveBattleStartTargetLabel(IList<RuntimeUnit> units)
        {
            var ctx = _battleStartContext;
            if (ctx != null && ctx.AllyUnits != null)
            {
                var aliveAllies = ctx.AllyUnits.Where(u => u != null && u.IsAlive).ToList();
                if (aliveAllies.Count > 0 && units.Count == aliveAllies.Count
                    && units.All(u => aliveAllies.Contains(u)))
                {
                    return "味方全体";
                }
            }
            if (units.Count == 1) return _nameOf(units[0]);
            return $"味方{units.Count}体";
        }

        public void RecordBattleEnd(BattleResult result, int turn,
            string allySurvivors = null, string enemySurvivors = null)
        {
            // Log と Events の 1:1 を保つため、戦闘終了の結果行も Log.Add してから Event.Add する。
            // 観戦ビュー側は cursor 連動で Event 数だけステップを刻むため、行数ずれが累積しない。
            string resultLabel = BattleLogFormatter.ResultLabel(result);
            string line = (!string.IsNullOrEmpty(allySurvivors) || !string.IsNullOrEmpty(enemySurvivors))
                ? $"結果：{resultLabel} | 味方：{allySurvivors} | 敵：{enemySurvivors}"
                : $"結果：{resultLabel}";
            AddEvent(new BattleEvent
            {
                Kind = BattleEventKind.BattleEnd,
                Turn = turn,
                Result = result,
            }, line);
        }
    }
}
