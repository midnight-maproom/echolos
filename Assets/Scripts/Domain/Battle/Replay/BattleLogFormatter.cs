using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>
    /// 戦闘ログ用テキスト整形。すべて純関数で、戦闘ロジックを参照しない。
    /// HitOutcome / RuntimeUnit / StatusEffect / BattleResult を受け取って文字列を返す責務のみ。
    /// </summary>
    public static class BattleLogFormatter
    {
        /// <summary>
        /// 味方/敵の判別 prefix 付きの表示名 Resolver を生成する。
        /// 同名ユニットの衝突対策として SlotIndex を含める。
        /// </summary>
        public static Func<RuntimeUnit, string> CreateNameResolver(IEnumerable<RuntimeUnit> allies)
        {
            var allySet = new HashSet<RuntimeUnit>(allies ?? Array.Empty<RuntimeUnit>());
            return u => $"{(allySet.Contains(u) ? "味" : "敵")}{u.BaseUnit.Name}#{u.SlotIndex}";
        }

        /// <summary>単一 Outcome を文字列断片に変換する。空相当の場合は null を返す。</summary>
        public static string FormatSingleOutcome(HitOutcome o, Func<RuntimeUnit, string> nameOf)
        {
            if (o == null) return null;
            if (o.WasEvaded) return $"{nameOf(o.Target)}は回避";
            if (o.WasShielded) return $"{nameOf(o.Target)}のシールドが防いだ";
            if (o.HealAmount > 0) return $"{nameOf(o.Target)}を{o.HealAmount}回復(残HP {o.TargetHPAfter})";
            if (o.Damage > 0)
            {
                string deathTag = o.ResultedInDeath ? "・戦闘不能" : "";
                string effectsTag = o.AppliedEffects.Count > 0
                    ? " " + string.Join("/", o.AppliedEffects.Select(e => "+" + e.Kind))
                    : "";
                return $"{nameOf(o.Target)}に{o.Damage}ダメージ(残HP {o.TargetHPAfter}{deathTag}){effectsTag}";
            }
            if (o.AppliedEffects.Count > 0)
            {
                string effectsTag = string.Join("/", o.AppliedEffects.Select(e => "+" + e.Kind));
                return $"{nameOf(o.Target)}に{effectsTag}付与";
            }
            if (o.RemovedEffects.Count > 0)
            {
                string effectsTag = string.Join("/", o.RemovedEffects.Select(e => "-" + e.Kind));
                return $"{nameOf(o.Target)}から{effectsTag}解除";
            }
            return null;
        }

        /// <summary>
        /// 同一ターゲットへの多段ヒットを 1 行に集約する。
        /// hit カウントは「ダメージ発生 or 回避 or シールド吸収」の Outcome のみ。
        /// ApplyStatusEffectEffect 経由の付与専用 Outcome（Damage=0 かつ WasEvaded=false かつ
        /// WasShielded=false）はカウントせず effectsTag に集約のみ。
        /// 例：2 段全命中             → "Aに2/2回ヒット 合計60ダメージ(残HP 40)"
        /// 例：1 段命中＋付帯付与     → "Aに30ダメージ(残HP 70) +Burn"
        /// 例：シールドで完全吸収     → "Aのシールドが防いだ"
        /// 例：同 target に付与のみ複数 → "Aに+AttackUp付与"
        /// </summary>
        public static string FormatGroup(IReadOnlyList<HitOutcome> group, Func<RuntimeUnit, string> nameOf)
        {
            if (group == null || group.Count == 0) return null;
            if (group.Count == 1) return FormatSingleOutcome(group[0], nameOf);

            var target = group[0].Target;

            int totalHits = 0, landedHits = 0, totalDamage = 0, shieldedHits = 0;
            foreach (var o in group)
            {
                if (o.Damage > 0 || o.WasEvaded || o.WasShielded)
                {
                    totalHits++;
                    if (o.Damage > 0) landedHits++;
                    if (o.WasShielded) shieldedHits++;
                    totalDamage += o.Damage;
                }
            }
            int finalHP = group[group.Count - 1].TargetHPAfter;
            bool died = group.Any(o => o.ResultedInDeath);
            var distinctKinds = group.SelectMany(o => o.AppliedEffects).Select(e => e.Kind).Distinct().ToList();
            string effectsTag = distinctKinds.Count > 0
                ? " " + string.Join("/", distinctKinds.Select(k => "+" + k))
                : "";

            if (totalHits == 0)
            {
                // 付与専用 Outcome が同 target で複数連続したケース（バフ Waza の AoE 等）
                return $"{nameOf(target)}に{string.Join("/", distinctKinds.Select(k => "+" + k))}付与";
            }

            // 攻撃 hit が 1 件しかない場合は「N/M 回ヒット」表記を省略し単発攻撃と同じ形にする
            // （付帯付与が別 Outcome として積まれた火矢・焦熱波などのケース）。
            if (totalHits == 1)
            {
                var single = group.First(o => o.Damage > 0 || o.WasEvaded || o.WasShielded);
                if (single.WasShielded) return $"{nameOf(target)}のシールドが防いだ";
                if (single.WasEvaded) return $"{nameOf(target)}は回避";
                string deathTagSingle = died ? "・戦闘不能" : "";
                return $"{nameOf(target)}に{totalDamage}ダメージ(残HP {finalHP}{deathTagSingle}){effectsTag}";
            }

            if (landedHits == 0 && shieldedHits == 0)
            {
                return $"{nameOf(target)}は{totalHits}回回避";
            }

            string deathTag = died ? "・戦闘不能" : "";
            string shieldedTag = shieldedHits > 0 ? $"（うちシールド吸収{shieldedHits}）" : "";
            string damageWord = landedHits >= 2 ? "合計" : "";
            return $"{nameOf(target)}に{landedHits}/{totalHits}回ヒット {damageWord}{totalDamage}ダメージ(残HP {finalHP}{deathTag}){shieldedTag}{effectsTag}";
        }

        /// <summary>
        /// 1 アクション分の Outcomes をターゲット単位でグルーピングし「 | 」で連結した 1 行を返す。
        /// 攻撃 Outcomes と反撃 Outcomes（IsCounterAttack=true）を別々にグルーピングし、反撃側は
        /// 「（反撃）」プレフィックスを付けて連結する。同 target 多段攻撃が反撃で分断されない設計。
        /// 戦闘ログ用の先頭インデント（4 スペース）込み。空相当の場合は null を返す。
        /// </summary>
        public static string FormatOutcomesLogLine(IReadOnlyList<HitOutcome> outcomes, Func<RuntimeUnit, string> nameOf)
        {
            if (outcomes == null || outcomes.Count == 0) return null;

            var attackOutcomes = new List<HitOutcome>();
            var counterOutcomes = new List<HitOutcome>();
            foreach (var o in outcomes)
            {
                if (o == null) continue;
                if (o.IsCounterAttack) counterOutcomes.Add(o);
                else attackOutcomes.Add(o);
            }

            var fragments = new List<string>();
            foreach (var g in GroupByTarget(attackOutcomes))
            {
                string s = FormatGroup(g, nameOf);
                if (!string.IsNullOrEmpty(s)) fragments.Add(s);
            }
            foreach (var g in GroupByTarget(counterOutcomes))
            {
                string s = FormatGroup(g, nameOf);
                if (!string.IsNullOrEmpty(s)) fragments.Add("（反撃）" + s);
            }

            if (fragments.Count == 0) return null;
            return "    " + string.Join(" | ", fragments);
        }

        /// <summary>
        /// ターン終了時 HealOverTime の陣営単位 1 行集約ログを返す。
        /// 対象表記：味方/敵いずれかの生存全員と一致するなら「味方全体」/「敵全体」、
        /// 1 体なら個別名、それ以外は「味方N体」/「敵N体」。同陣営に絞らない部分対象は連名表記。
        /// SourceAbilityName は ticks 全件で同じものを採用（混在時は最初のもの）。
        /// </summary>
        public static string FormatHealOverTimePhaseLine(
            IReadOnlyList<StatusEffectProcessor.HealOverTimeTick> ticks,
            BattleContext context,
            Func<RuntimeUnit, string> nameOf)
        {
            if (ticks == null || ticks.Count == 0) return null;

            string sourceLabel = ticks.Select(t => t.SourceLabel)
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));
            string srcPrefix = string.IsNullOrEmpty(sourceLabel) ? "" : $"{sourceLabel}：";

            string targetLabel = ResolveSideAggregationLabel(
                ticks.Select(t => t.Unit).ToList(), context, nameOf);

            string values = string.Join("/", ticks.Select(t => $"+{t.Healed}"));
            return $"    ✚ {targetLabel}に {srcPrefix}継続回復 {values}";
        }

        private static string ResolveSideAggregationLabel(
            IList<RuntimeUnit> units, BattleContext context, Func<RuntimeUnit, string> nameOf)
        {
            if (units == null || units.Count == 0) return "";

            if (context != null)
            {
                if (MatchesAllAlive(units, context.AllyUnits)) return "味方全体";
                if (MatchesAllAlive(units, context.EnemyUnits)) return "敵全体";
                if (AllBelongTo(units, context.AllyUnits)) return $"味方{units.Count}体";
                if (AllBelongTo(units, context.EnemyUnits)) return $"敵{units.Count}体";
            }
            if (units.Count == 1) return nameOf(units[0]);
            return string.Join(", ", units.Select(u => nameOf(u)));
        }

        private static bool MatchesAllAlive(IList<RuntimeUnit> units, IList<RuntimeUnit> side)
        {
            if (side == null) return false;
            var alive = side.Where(u => u != null && u.IsAlive).ToList();
            if (alive.Count == 0 || alive.Count != units.Count) return false;
            return units.All(u => alive.Contains(u));
        }

        private static bool AllBelongTo(IList<RuntimeUnit> units, IList<RuntimeUnit> side)
        {
            if (side == null) return false;
            return units.All(u => side.Contains(u));
        }

        /// <summary>戦闘終了後の生存サマリ。全滅なら "全滅"。</summary>
        public static string SurvivorSummary(IEnumerable<RuntimeUnit> party)
        {
            if (party == null) return "全滅";
            var alive = party.Where(u => u.IsAlive)
                .Select(u => $"{u.BaseUnit.Name}({u.CurrentHP}/{u.MaxHP})");
            string joined = string.Join(", ", alive);
            return string.IsNullOrEmpty(joined) ? "全滅" : joined;
        }

        /// <summary>戦闘開始時の編成行（スロット順・slot 番号＋名前＋HP）。</summary>
        public static string LineupSummary(IEnumerable<RuntimeUnit> party)
        {
            if (party == null) return string.Empty;
            var entries = party.OrderBy(u => u.SlotIndex)
                .Select(u => $"slot{u.SlotIndex}:{u.BaseUnit.Name}({u.CurrentHP}/{u.MaxHP})");
            return string.Join(", ", entries);
        }

        /// <summary>戦闘結果ラベル。未決着は "未決着"。</summary>
        public static string ResultLabel(BattleResult r)
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

        /// <summary>
        /// 状態効果の値表記。Shield は Stacks（残数）、その他で Magnitude>0 なら「+{値}」を
        /// Kind 名に付ける。フラグ系（SilencedCounter 等・Magnitude 0）は Kind 名のみ。
        /// </summary>
        public static string FormatEffectValue(IEffect eff)
        {
            if (eff == null) return string.Empty;
            if (eff is ShieldEffect)
                return $"Shield {eff.Stacks}";
            float magnitude = GetMagnitude(eff);
            if (magnitude > 0)
                return $"{eff.Kind} +{(int)magnitude}";
            return eff.Kind.ToString();
        }

        /// <summary>
        /// 状態効果の表記。SourceAbilityName があれば「{ソース能力名}：{値表記}」、
        /// なければ値表記のみ。
        /// </summary>
        public static string FormatEffectWithSource(IEffect eff)
        {
            if (eff == null) return string.Empty;
            string valueLabel = FormatEffectValue(eff);
            return string.IsNullOrEmpty(eff.SourceAbilityName)
                ? valueLabel
                : $"{eff.SourceAbilityName}：{valueLabel}";
        }

        /// <summary>派生クラス固有の Magnitude を取り出す。持たない型は 0。</summary>
        private static float GetMagnitude(IEffect eff)
        {
            switch (eff)
            {
                case AbilityModifier m:        return m.Magnitude;
                case EvasionModifier m:        return m.Magnitude;
                case OutgoingDamageModifier m: return m.Magnitude;
                case IncomingDamageModifier m: return m.Magnitude;
                case CriticalRateModifier m:   return m.Magnitude;
                case CounterDamageModifier m:  return m.Magnitude;
                case HealReceivedModifier m:   return m.Magnitude;
                case ContinuousDot m:          return m.Magnitude;
                case ContinuousHot m:          return m.Magnitude;
                case SelfGuard m:              return m.Magnitude;
                default: return 0f;
            }
        }

        private static List<List<HitOutcome>> GroupByTarget(IReadOnlyList<HitOutcome> outcomes)
        {
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
            return groups;
        }
    }
}
