// バトル単体検証ランナー（純C#・MonoBehaviour非依存）。
//
// 既存の戦闘Core一式（BattleManager / TargetEvaluator / ActionExecutor / StatusEffectProcessor）を
// すべて結線して、味方編成 vs 敵編成を1戦闘ぶん完走させ、結果とターンログを返す。
// マッチアップNUnitテストとOnGUIサンドボックスの両方から使う共通部品。
//
// 重要：StatusEffectProcessor を登録するため、毒（燃焼）・麻痺・凍結・時限効果・置物オーラ剥奪が機能する。
using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Conditional;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>
    /// 味方編成 vs 敵編成を1戦闘完走させる検証用ランナー。
    /// 回避RNGは注入可能（テストでは固定値、サンドボックスではSystem.Random）。
    /// </summary>
    public static class BattleRunner
    {
        /// <summary>
        /// 1戦闘を完走して結果を返す。
        /// allies/enemies は配置済み（SlotIndex設定済み）の RuntimeUnit。HPは呼び出し側で全快にしておくこと。
        /// maxTurns はゲーム体験として一戦を冗長にしないため既定15ターンに設定。
        /// </summary>
        public static BattleReport Run(
            List<RuntimeUnit> allies, List<RuntimeUnit> enemies,
            int maxTurns = 15, Func<int> random0to99 = null)
        {
            if (allies == null) throw new ArgumentNullException(nameof(allies));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));

            var report = new BattleReport();
            var context = new BattleContext(maxTurns);
            context.AllyUnits.AddRange(allies);
            context.EnemyUnits.AddRange(enemies);

            // ログ用：味方/敵を判別して名前を付ける（同名ユニットの衝突対策）
            var allySet = new HashSet<RuntimeUnit>(allies);
            string Name(RuntimeUnit u) =>
                $"{(allySet.Contains(u) ? "味" : "敵")}{u.BaseUnit.Name}#{u.SlotIndex}";

            var evaluator = new TargetEvaluator();
            // Conditional バフ Processor 一覧。新規追加は本リストに足すだけ。
            // PartnerUnitId は AlliesRoster.PrincessId を直接参照すると Domain.Prototype への依存になるため、
            // 文字列リテラルで渡す（Roster 側の Id 値と一致していること）。
            var conditionalProcessors = new ConditionalBuffProcessor[]
            {
                new LonerWolfConditionalProcessor(),
                new PrinceDarkAuraConditionalProcessor(),
                new PendantConditionalProcessor(partnerUnitId: "princess", sourceAbilityName: "王家のペンダント"),
                new OffenseDefenseLinkConditionalProcessor(),
            };
            var manager = new BattleManager(evaluator, conditionalProcessors);
            var executor = new ActionExecutor(random0to99);
            var statusProcessor = new StatusEffectProcessor();

            // ── ログ結線（行動ヘッダは実行より先に記録する）──
            // 観戦ビュー用に構造化イベント列（report.Events）にも同じ情報を別経路で記録する。
            // 文字列ログと違って Actor/Target はオブジェクト参照のまま保持し、ビュー側でスロット位置やHPに使う。
            manager.OnStartPhase += ctx =>
            {
                report.Log.Add($"── ターン{ctx.CurrentTurn} ──");
                report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.TurnStart,
                    Turn = ctx.CurrentTurn,
                });
            };

            manager.OnActionExecuting += (ctx, decl) =>
            {
                if (decl.IsWaiting || decl.DeclaredWaza == null) return;
                string wazaName = decl.DeclaredWaza.Name;
                // ターゲット一覧は次行（AppendOutcomesLog）の対象表記で十分なのでヘッダから外す
                report.Log.Add($"{Name(decl.Actor)} → {wazaName}");
                report.Events.Add(new BattleEvent
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
                // 待機（OnActionSkipped）が呼ばれるのは麻痺・凍結による行動不能のみ
                // （攻撃手段なしユニットは自己防御フォールバックで「防御」発動）。
                report.Log.Add($"{Name(unit)} は行動できない（麻痺/凍結）");
                // SkipReason は厳密化できればより親切だが、現状 BattleManager が単一フックで集約しているため、
                // 観戦ビューでは「行動不能」とまとめて表示する想定で記録だけ残す。
                report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.ActionSkipped,
                    Turn = ctx.CurrentTurn,
                    Actor = unit,
                    SkipReason = "行動不能",
                });
            };

            // アクション完結イベント（集約）。観戦ビューはこれ 1 件で Outcomes を順次／一括適用する。
            // テキストログも Outcomes ベースで集約版をここで生成する。
            // 単一 Outcome を文字列断片に変換。
            string FormatSingleOutcome(HitOutcome o)
            {
                if (o.WasEvaded) return $"{Name(o.Target)}は回避";
                if (o.HealAmount > 0) return $"{Name(o.Target)}を{o.HealAmount}回復(残HP {o.TargetHPAfter})";
                if (o.Damage > 0)
                {
                    string deathTag = o.ResultedInDeath ? "・戦闘不能" : "";
                    string effectsTag = o.AppliedEffects.Count > 0
                        ? " " + string.Join("/", o.AppliedEffects.Select(e => "+" + e))
                        : "";
                    return $"{Name(o.Target)}に{o.Damage}ダメージ(残HP {o.TargetHPAfter}{deathTag}){effectsTag}";
                }
                if (o.AppliedEffects.Count > 0)
                {
                    string effectsTag = string.Join("/", o.AppliedEffects.Select(e => "+" + e));
                    return $"{Name(o.Target)}に{effectsTag}付与";
                }
                return null;
            }

            // 同一ターゲットへの複数 Outcome（多段ヒット）をヒット率分数表記で集約する。
            // 例：2 段中 1 段命中・1 段回避 → "Aに1/2回ヒット 30ダメージ(残HP 70)"
            // 例：2 段全命中 → "Aに2/2回ヒット 合計60ダメージ(残HP 40)"
            string FormatGroup(List<HitOutcome> group)
            {
                if (group.Count == 0) return null;
                if (group.Count == 1) return FormatSingleOutcome(group[0]);

                var target = group[0].Target;
                int totalHits = group.Count;
                int landedHits = group.Count(o => o.Damage > 0);
                int totalDamage = group.Sum(o => o.Damage);
                int finalHP = group[group.Count - 1].TargetHPAfter;
                bool died = group.Any(o => o.ResultedInDeath);

                if (landedHits == 0)
                {
                    // 全段回避（理論上ありうる）
                    return $"{Name(target)}は{totalHits}回回避";
                }

                string deathTag = died ? "・戦闘不能" : "";
                var distinctEffects = group.SelectMany(o => o.AppliedEffects).Distinct().ToList();
                string effectsTag = distinctEffects.Count > 0
                    ? " " + string.Join("/", distinctEffects.Select(e => "+" + e))
                    : "";
                string damageWord = landedHits >= 2 ? "合計" : "";
                return $"{Name(target)}に{landedHits}/{totalHits}回ヒット {damageWord}{totalDamage}ダメージ(残HP {finalHP}{deathTag}){effectsTag}";
            }

            // 同一ターゲットへの連続 Outcome をグルーピングしてから「 | 」で連結。
            // ActionExecutor のループ構造により、同ターゲットの多段ヒットは連続して並ぶ前提。
            void AppendOutcomesLog(IReadOnlyList<HitOutcome> outcomes)
            {
                if (outcomes == null || outcomes.Count == 0) return;

                var groups = new List<List<HitOutcome>>();
                RuntimeUnit lastTarget = null;
                foreach (var o in outcomes)
                {
                    if (o.Target != lastTarget)
                    {
                        groups.Add(new List<HitOutcome> { o });
                        lastTarget = o.Target;
                    }
                    else
                    {
                        groups[groups.Count - 1].Add(o);
                    }
                }

                var fragments = groups.Select(FormatGroup).Where(s => !string.IsNullOrEmpty(s));
                string joined = string.Join(" | ", fragments);
                if (!string.IsNullOrEmpty(joined))
                    report.Log.Add("    " + joined);
            }

            executor.OnActionResolved += (ctx, decl, outcomes) =>
            {
                AppendOutcomesLog(outcomes);
                report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.ActionResolved,
                    Turn = ctx.CurrentTurn,
                    Actor = decl.Actor,
                    WazaName = decl.DeclaredWaza?.Name,
                    Outcomes = new List<HitOutcome>(outcomes),
                });
            };

            statusProcessor.OnBurnTickDamage += (ctx, unit, dmg) =>
            {
                report.Log.Add($"    ☠ {Name(unit)} に毒/燃焼 {dmg}");
                report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.BurnTick,
                    Turn = ctx.CurrentTurn,
                    Target = unit,
                    Damage = dmg,
                    TargetHPAfter = unit.CurrentHP,
                });
            };

            // ── 戦闘ロジック結線（ログの後に実行）──
            manager.OnActionExecuting += executor.ExecuteAction;
            statusProcessor.RegisterTo(manager, executor);

            // Conditional バフ Processor の UnitDied フックを ActionExecutor.OnUnitDied 経由で BattleManager に dispatch
            executor.OnUnitDied += (ctx, deadUnit)
                => manager.DispatchConditional(ConditionalBuffHook.UnitDied, ctx, deadUnit);

            // ── 戦闘開始時の編成サマリ ──
            // スロット順に「前/後＋スロット番号：名前(HP/MaxHP)」を並べる。配置の読み・列単位の保護判定の参考に。
            report.Log.Add("== 編成 ==");
            report.Log.Add("味方: " + LineupSummary(allies));
            report.Log.Add("敵:   " + LineupSummary(enemies));

            // InfiltratorTag 持ちは前列で「敵陣に潜入」が成立、後列だと近接が前列タンクに阻まれ届かない。
            AppendInfiltratorDeclarations(report, allies, Name);
            AppendInfiltratorDeclarations(report, enemies, Name);

            // 観戦ビュー初期状態のために編成スナップショットを残す（参照保持で十分）。
            report.AllyLineup = new List<RuntimeUnit>(allies);
            report.EnemyLineup = new List<RuntimeUnit>(enemies);

            // 状態効果の付与・剥がれを観戦ビューが時系列追跡できるよう
            // RuntimeUnit.OnEffectAdded / OnEffectRemoved を購読してイベントログ・構造化イベント列に記録する。
            //
            // 戦闘前付与の取り扱い（Cover タグ持ちの永続 Cover など）：
            // Run() に渡される時点で既に ActiveEffects に乗っている効果は、購読登録より先に存在するため
            // 通常の OnEffectAdded では拾えない。これらは Turn=0 の StatusEffectApplied イベントとして
            // 先に記録しておき、観戦ビューが Initialize 後の最初の Tick で空 → 初期状態へ巻き戻せるようにする。
            //
            // BattleManager.InitializeBattle 内で追加される RowCover / Aura は購読後に AddEffect が呼ばれるので、
            // 通常の OnEffectAdded フローで自動的に event 化される。
            foreach (var u in allies)
                RecordInitialEffectsSnapshot(report, u);
            foreach (var u in enemies)
                RecordInitialEffectsSnapshot(report, u);

            foreach (var u in allies)
                SubscribeEffectEvents(report, context, u, Name, manager);
            foreach (var u in enemies)
                SubscribeEffectEvents(report, context, u, Name, manager);

            // ── 実行 ──
            manager.InitializeBattle(context);

            BattleResult result;
            int safety = 0;
            do
            {
                result = manager.ProcessTurn(context);
            }
            while (result == BattleResult.None && ++safety <= maxTurns + 5);

            report.Result = result;
            report.Turns = context.CurrentTurn;
            report.Log.Add($"== 結果: {ResultLabel(result)}({report.Turns}ターン) ==");
            report.Log.Add("味方生存: " + SurvivorSummary(allies));
            report.Log.Add("敵生存:   " + SurvivorSummary(enemies));

            // 観戦ビューが「結果オーバーレイ」を出す目印として終了イベントを残す。
            report.Events.Add(new BattleEvent
            {
                Kind = BattleEventKind.BattleEnd,
                Turn = context.CurrentTurn,
                Result = result,
            });

            return report;
        }

        /// <summary>
        /// InfiltratorTag 持ちユニットに対し、前列なら成立宣言、後列なら警告ログを
        /// 編成サマリ直後に追加する。SlotIndex 0-2 を前列、3-5 を後列として扱う。
        /// </summary>
        private static void AppendInfiltratorDeclarations(
            BattleReport report, List<RuntimeUnit> party, Func<RuntimeUnit, string> nameOf)
        {
            foreach (var u in party)
            {
                if (!u.BaseUnit.Tags.Contains(BattleManager.InfiltratorTag)) continue;
                bool frontRow = u.SlotIndex < 3;
                report.Log.Add(frontRow
                    ? $"    {nameOf(u)} 敵陣に潜入"
                    : $"    {nameOf(u)} 敵陣に潜入できない（前衛に配置してください）");
            }
        }

        /// <summary>
        /// Run() に渡される時点で既に付与されている状態効果（Cover タグ持ちの永続 Cover 等）を
        /// Turn=0 の StatusEffectApplied イベントとして記録する。
        /// </summary>
        private static void RecordInitialEffectsSnapshot(BattleReport report, RuntimeUnit unit)
        {
            foreach (var e in unit.ActiveEffects)
            {
                report.Events.Add(new BattleEvent
                {
                    Kind = BattleEventKind.StatusEffectApplied,
                    Turn = 0,
                    Target = unit,
                    EffectType = e.EffectType,
                });
            }
        }

        /// <summary>
        /// 戦闘中の OnEffectAdded / OnEffectRemoved を購読して、ログと構造化イベント列に記録する。
        /// 観戦ビュー側でこれを再生して HashSet&lt;StatusEffectType&gt; を更新することで
        /// バフデバフバッジの動的表示が成立する。
        /// 加えて Conditional バフ Processor の BuffApplied / BuffRemoved フックを発火する。
        /// </summary>
        private static void SubscribeEffectEvents(
            BattleReport report, BattleContext context, RuntimeUnit unit, Func<RuntimeUnit, string> nameOf,
            BattleManager manager)
        {
            unit.OnEffectAdded += eff =>
            {
                // ActionGuard は技名と効果が 1:1 対応で自明なためログ・バッジ表示ともに抑制。
                // Conditional フックへの伝播は維持。
                if (eff.Category != BuffCategory.ActionGuard)
                {
                    report.Log.Add($"    + {nameOf(unit)} に {FormatEffectWithSource(eff)} 付与");
                    report.Events.Add(new BattleEvent
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
                // Persistent / Conditional の剥奪は「源が消えたら自動消滅」が仕様であり、
                // 剥奪は副次効果なのでテキストログには出さない（プレイヤー価値が低く、
                // 攻防一体の値変更時の中間 remove や王女戦闘不能時の連鎖剥奪などのノイズになる）。
                // ActionGuard は付与時にバッジを出していないので解除も出さない（バッジ状態の不整合防止）。
                // Triggered（ターン切れ）・StatusAilment（Cleanse 解除）は判断材料なので残す。
                bool suppressTextLog = eff.Category == BuffCategory.Persistent
                                    || eff.Category == BuffCategory.Conditional
                                    || eff.Category == BuffCategory.ActionGuard;
                if (!suppressTextLog)
                {
                    report.Log.Add($"    - {nameOf(unit)} の {FormatEffectWithSource(eff)} 解除");
                }
                if (eff.Category != BuffCategory.ActionGuard)
                {
                    report.Events.Add(new BattleEvent
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
        /// 状態効果のログ表記。SourceAbilityName があれば「{ソース能力名}：{EffectType}」、
        /// なければ EffectType のみ（一般的なバフ技の付帯効果など、ソース表示が不要なケース）。
        /// </summary>
        private static string FormatEffectWithSource(StatusEffect eff)
        {
            return string.IsNullOrEmpty(eff.SourceAbilityName)
                ? eff.EffectType.ToString()
                : $"{eff.SourceAbilityName}：{eff.EffectType}";
        }

        private static string SurvivorSummary(List<RuntimeUnit> party)
        {
            var alive = party.Where(u => u.IsAlive)
                .Select(u => $"{u.BaseUnit.Name}({u.CurrentHP}/{u.MaxHP})");
            string joined = string.Join(", ", alive);
            return string.IsNullOrEmpty(joined) ? "全滅" : joined;
        }

        /// <summary>戦闘開始時の編成行を生成する（スロット順・前/後と最大HP）。</summary>
        private static string LineupSummary(List<RuntimeUnit> party)
        {
            var entries = party.OrderBy(u => u.SlotIndex)
                .Select(u => $"{(u.IsFrontRow ? "前" : "後")}{u.SlotIndex}:{u.BaseUnit.Name}({u.CurrentHP}/{u.MaxHP})");
            return string.Join(", ", entries);
        }

        private static string ResultLabel(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory:      return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat:      return "惜敗";
                case BattleResult.CrushingDefeat:      return "完敗";
                default:                               return "未決着";
            }
        }
    }
}
