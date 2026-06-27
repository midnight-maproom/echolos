using System;
using System.Collections.Generic;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Battle
{
    /// <summary>
    /// 技の実行フェーズを担うクラス。
    /// ターゲット評価（ActionDeclaration）の結果を受け取り、
    /// ヒットループ・回避判定・ダメージ計算・シールド優先消費・死亡処理を実行する。
    ///
    /// 利用側は以下のように登録する：
    ///   var executor = new ActionExecutor();
    ///   battleManager.OnActionExecuting += executor.ExecuteAction;
    /// </summary>
    public class ActionExecutor
    {
        /// <summary>0〜99の整数を返す乱数プロバイダー。回避確率判定に使用する。</summary>
        private readonly Func<int> _random0to99;

        // イベント

        /// <summary>
        /// 回避が成功したときに発火する。
        /// 引数: (context, 攻撃者, 回避したユニット)
        /// </summary>
        public event Action<BattleContext, RuntimeUnit, RuntimeUnit> OnHitEvaded;

        /// <summary>
        /// 1ヒットが命中してダメージが確定・適用された後に発火する。
        /// トゲ（反射）などのパッシブスキルがここで ReactionStack に積む。
        /// 引数: (context, 攻撃者, 被ダメージユニット, 確定ダメージ量, 技の属性)
        /// </summary>
        public event Action<BattleContext, RuntimeUnit, RuntimeUnit, int, Element> OnHitLanded;

        /// <summary>
        /// ユニットが死亡（HP0到達）したときに発火する。
        /// アンデッド復活・死亡時自爆などをここに登録する。
        /// 引数: (context, 死亡したユニット)
        /// </summary>
        public event Action<BattleContext, RuntimeUnit> OnUnitDied;

        /// <summary>
        /// 回復（Category=Heal）が適用された後に発火する。
        /// HitLanded と対をなすイベント：観戦ビューでの緑色ポップアップ表示・戦闘ログ記録に使う。
        /// AntiHealPassive による減衰後の実効回復量・上限クランプ後の値を渡す。
        /// 引数: (context, 回復者, 回復対象, 実効回復量)
        /// </summary>
        public event Action<BattleContext, RuntimeUnit, RuntimeUnit, int> OnHealed;

        /// <summary>
        /// 1 アクション（ExecuteAction 1 回）の実行が完了した直後に発火する。
        /// 各ターゲットへの結果（命中/回避/回復/死亡/付帯効果）を 1 件 = 1 HitOutcome として束ねる。
        /// Replay 層（BattleRunner）が ActionResolved イベントに変換し、GUI と集約ログで使う。
        /// 個別の OnHitLanded / OnHealed / OnHitEvaded / OnUnitDied も並行発火するため、
        /// 既存購読者は無修正のまま動作する（後方互換）。
        /// 引数: (context, 宣言, アウトカム列）
        /// </summary>
        public event Action<BattleContext, ActionDeclaration, IReadOnlyList<HitOutcome>> OnActionResolved;

        /// <param name="random0to99">
        /// 0〜99の整数を返すデリゲート。
        /// null の場合は System.Random を使用したデフォルト実装が使われる。
        /// テスト時は固定値を返すデリゲートを渡すことで乱数に依存しないテストが可能。
        /// </param>
        public ActionExecutor(Func<int> random0to99 = null)
        {
            if (random0to99 != null)
            {
                _random0to99 = random0to99;
            }
            else
            {
                // 本番用: スレッドセーフではないが、バトルは単一スレッドを想定
                var rng = new System.Random();
                _random0to99 = () => rng.Next(0, 100);
            }
        }

        // 公開API（BattleManager.OnActionExecuting に登録する）

        /// <summary>
        /// BattleManagerから呼ばれる行動実行メソッド。
        /// ActionDeclarationのターゲットリストを順番に処理し、各ターゲットに対してヒットループを実行する。
        /// 待機宣言（IsWaiting）や技がない場合は何もしない。
        /// </summary>
        /// <param name="context">現在のバトルコンテキスト</param>
        /// <param name="declaration">評価フェーズで生成された行動宣言</param>
        public void ExecuteAction(BattleContext context, ActionDeclaration declaration)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));

            // 待機または技がない場合は何もしない
            if (declaration.IsWaiting || declaration.DeclaredWaza == null) return;

            var actor = declaration.Actor;
            var waza = declaration.DeclaredWaza;

            // アクション 1 回分の結果を集約するバッファ。各ターゲットの処理が outcome を Add する。
            var outcomes = new List<HitOutcome>();

            // 全ターゲットに対して技カテゴリ別の処理を実行
            // （単体技は1体、全体技は複数体に対して個別に処理する）
            foreach (var originalTarget in declaration.Targets)
            {
                // ターゲットが既に死亡している場合（別ターゲットへの攻撃で連鎖死亡等）はスキップ
                if (!originalTarget.IsAlive) continue;

                switch (waza.Category)
                {
                    case WazaCategory.Heal:
                        ApplyHeal(context, actor, waza, originalTarget, outcomes);
                        ApplySupportEffects(waza, originalTarget, outcomes); // 回復＋付帯バフ（騎士の防御up等）
                        ApplyCleanseDispel(waza, originalTarget);  // 回復＋cleanse（回復役2）
                        break;

                    case WazaCategory.Buff:
                        ApplySupportEffects(waza, originalTarget, outcomes);
                        ApplyCleanseDispel(waza, originalTarget); // バフ＋dispel（軍師）
                        break;

                    case WazaCategory.Debuff:
                        ApplySupportEffects(waza, originalTarget, outcomes);
                        ApplyCleanseDispel(waza, originalTarget); // 敵バフ解除（軍師の tac_purge 等・DispelsBuffs）
                        break;

                    default: // WazaCategory.Attack
                        var anchor = ExecuteHitLoop(context, actor, waza, originalTarget, outcomes);
                        // メインヒット完了後の同列スプラッシュ。anchor が死亡していても SlotIndex で列判定可能。
                        if (waza.SameRowSplashMultiplier > 0f)
                            ApplyRowSplash(context, actor, waza, anchor, outcomes);
                        break;
                }
            }

            // アクション完結イベント。Outcomes が空でも発火する（待機・空ターゲットでない限り
            // 何かしらの行動意図はあったため、観戦ビューはアクション 1 回として記録できる）。
            OnActionResolved?.Invoke(context, declaration, outcomes);
        }

        // 支援系（回復・バフ・デバフ）処理

        /// <summary>
        /// 回復を適用する（Category=Heal）。
        /// CalculateHealAmountで算出した量だけ対象のHPを回復する（最大HPでクランプ）。
        /// 対象の敵チームに AntiHealPassive 持ちが生存していれば回復量を1/3に減衰する。
        /// 防御計算・死亡判定は行わない。死亡（HP0）ユニットは対象外（呼び出し側で生存確認済み）。
        /// </summary>
        private void ApplyHeal(BattleContext context, RuntimeUnit actor, RuntimeWaza waza, RuntimeUnit target, List<HitOutcome> outcomes)
        {
            if (waza.CalculateHealAmount == null) return;

            int heal = waza.CalculateHealAmount(actor, target);
            if (heal <= 0) return;

            // 対象の敵側に AntiHealPassive 持ちの生存ユニットが居れば回復を1/3に減衰する（耐久つぶしギミック用）
            if (IsHealReducedByEnemy(context, target))
                heal = heal / 3;
            if (heal <= 0) return;

            // 上限クランプ込みの実効回復量を確定させてから OnHealed を発火する。
            // ※「対象の HP がすでに満タンに近く実効回復が 0 だった」場合はイベントを発火しない。
            int before = target.BaseUnit.CurrentHP;
            int after = Math.Min(before + heal, target.MaxHP);
            int effectiveHeal = after - before;
            if (effectiveHeal <= 0) return;

            target.BaseUnit.CurrentHP = after;
            OnHealed?.Invoke(context, actor, target, effectiveHeal);
            outcomes.Add(new HitOutcome(
                target: target,
                healAmount: effectiveHeal,
                targetHPAfter: after));
        }

        /// <summary>
        /// 対象の敵チームに AntiHealPassive 持ちの生存ユニットが居るか判定する。
        /// 居れば対象への回復は減衰される（現状1/3）。
        /// </summary>
        private static bool IsHealReducedByEnemy(BattleContext context, RuntimeUnit target)
        {
            bool targetIsAlly = context.AllyUnits.Contains(target);
            var opponents = targetIsAlly ? context.EnemyUnits : context.AllyUnits;
            foreach (var enemy in opponents)
                if (enemy.IsAlive && enemy.BaseUnit.Tags.Contains(BattleManager.AntiHealPassiveTag))
                    return true;
            return false;
        }

        /// <summary>
        /// バフ/デバフを適用する（Category=Buff/Debuff）。
        /// AppliedEffectsの各テンプレートを対象へ付与する（スタック上限つき蓄積）。
        /// 付与した効果は outcomes に 1 件の HitOutcome として記録する（同ターゲットに対する
        /// 既存 outcome があれば AppliedEffects のみマージしてもよいが、まずは単純に追加する）。
        /// </summary>
        private static void ApplySupportEffects(RuntimeWaza waza, RuntimeUnit target, List<HitOutcome> outcomes)
        {
            var applied = ApplyTemplatedEffects(waza, target);
            if (applied.Count > 0)
            {
                outcomes.Add(new HitOutcome(
                    target: target,
                    appliedEffects: applied,
                    targetHPAfter: target.CurrentHP));
            }
        }

        /// <summary>
        /// 技のAppliedEffectsを対象へ付与する（支援技・攻撃の付帯効果で共用）。
        /// 戻り値は付与しようとした効果の種別リスト（実際にスタック上限で弾かれた場合も「付与意図」は記録）。
        /// </summary>
        private static List<StatusEffectType> ApplyTemplatedEffects(RuntimeWaza waza, RuntimeUnit target)
        {
            var applied = new List<StatusEffectType>();
            if (waza.AppliedEffects == null) return applied;

            foreach (var template in waza.AppliedEffects)
            {
                ApplyEffectWithStacking(target, template);
                applied.Add(template.EffectType);
            }
            return applied;
        }

        /// <summary>
        /// 状態異常/バフ/デバフを対象へ付与する（種類ごとのスタック上限つき）。
        /// 同種効果が既にある場合：Stacksを上限（MaxStacks）まで加算し、残ターン・強度・上限をリフレッシュする。
        /// なければ複製し、初期Stacksを上限でクランプして追加する。
        /// </summary>
        private static void ApplyEffectWithStacking(RuntimeUnit target, StatusEffect template)
        {
            // 状態異常無効：燃焼/毒・凍結・麻痺・呪いのみ弾く（能力デバフは通す）
            if (target.BaseUnit.ImmuneToStatusAilments && StatusEffect.IsStatusAilment(template.EffectType))
                return;

            int cap = template.MaxStacks > 0 ? template.MaxStacks : 1;

            var existing = target.FindEffect(template.EffectType);
            if (existing != null)
            {
                existing.Stacks = Math.Min(existing.Stacks + template.Stacks, cap);
                existing.RemainingTurns = template.RemainingTurns; // 時限をリフレッシュ
                existing.Magnitude = template.Magnitude;
                existing.MaxStacks = template.MaxStacks;
                existing.CoverTargetSlotIndex = template.CoverTargetSlotIndex;
                existing.CoversSameRow = template.CoversSameRow;
            }
            else
            {
                var clone = template.Clone();
                clone.Stacks = Math.Min(clone.Stacks, cap);
                target.AddEffect(clone);
            }
        }

        /// <summary>
        /// cleanse/dispel/purge を対象に適用する。
        /// CleansesStatusAilments → 味方の状態異常（燃焼/毒・凍結・麻痺・呪い）を解除。
        /// DispelsDebuffs → 味方の能力デバフ（攻撃/防御/回避ダウン）を解除（IsUndispellable は対象外）。
        /// DispelsBuffs → 敵の能力バフ（攻撃/防御/回避アップ）を解除（IsUndispellable は対象外）。
        /// 味方/敵の判定は呼び出し側 Waza.Category で行う（Buff＝味方／Debuff＝敵）。
        /// </summary>
        private static void ApplyCleanseDispel(RuntimeWaza waza, RuntimeUnit target)
        {
            if (waza.CleansesStatusAilments)
                target.RemoveEffectsWhere(e => StatusEffect.IsStatusAilment(e.EffectType));

            if (waza.DispelsDebuffs)
                target.RemoveEffectsWhere(e =>
                    StatusEffect.IsAbilityDebuff(e.EffectType) && !e.IsUndispellable);

            if (waza.DispelsBuffs)
                target.RemoveEffectsWhere(e =>
                    StatusEffect.IsAbilityBuff(e.EffectType) && !e.IsUndispellable);
        }

        // ヒットループ処理

        /// <summary>
        /// 1つのターゲットに対してHitCount回のヒットループを実行する。
        /// 各ヒットで「かばう判定→回避判定→ダメージ計算→ダメージ適用→リアクション通知→死亡判定」を行う。
        /// 戻り値は最後のヒットで参照した currentTarget（かばう移動後の最終着弾先）。同列スプラッシュの anchor として使う。
        /// </summary>
        private RuntimeUnit ExecuteHitLoop(BattleContext context, RuntimeUnit actor, RuntimeWaza waza, RuntimeUnit originalTarget, List<HitOutcome> outcomes)
        {
            RuntimeUnit lastTarget = originalTarget;
            for (int hitIndex = 0; hitIndex < waza.HitCount; hitIndex++)
            {
                // 1. かばう判定（ターゲットのすり替え）
                // 各ヒットの直前に確認する。タンクが前ヒットで死亡していれば、
                // ResolveCoverTargetはoriginalTargetをそのまま返す。
                // 複数対象技（全体・横列・後列直撃）は「かばう」を貫通する
                // （AoEは概念上「全員に同時着弾」であり、1体のタンクが他人の弾まで肩代わりしない）。
                var currentTarget = ResolveCoverTarget(context, originalTarget, waza);

                // splash の anchor として最新の currentTarget を記録（生存可否に関わらず列判定に使う）
                lastTarget = currentTarget;

                // かばうユニット（またはoriginalTarget自身）が既に死亡していればスキップ
                if (!currentTarget.IsAlive) continue;

                // このヒットで「かばう」によるすり替えが発生したか
                bool wasCoveredHit = (currentTarget != originalTarget);

                // 2. 回避判定
                // IsSureHit（必中）の場合は回避判定を完全にスキップする。
                // 回避成功時はダメージ0・技の追加効果も無効化（次のヒットへ）。
                if (!waza.IsSureHit && IsEvaded(currentTarget))
                {
                    OnHitEvaded?.Invoke(context, actor, currentTarget);
                    outcomes.Add(new HitOutcome(
                        target: currentTarget,
                        wasEvaded: true,
                        targetHPAfter: currentTarget.CurrentHP));
                    continue;
                }

                // 3. ダメージ計算: Max(1, 基礎威力 - DEF)
                // 基礎ダメージは BattleManager.ComputeBaseDamage 経由で算出する（評価フェーズと同じ経路）。
                // 攻撃側パッシブ倍率（魔導士特攻など）はこの中で適用済み。
                int baseDamage = BattleManager.ComputeBaseDamage(actor, waza, currentTarget);

                // 実効防御（バフ/デバフ適用後）を参照する
                int defense = waza.IsPhysical ? currentTarget.EffectivePDEF : currentTarget.EffectiveMDEF;
                // 防御無視率を適用（0=通常、1=完全無視）
                if (waza.DefenseIgnoreRatio > 0f)
                    defense = (int)(defense * (1f - waza.DefenseIgnoreRatio));
                int finalDamage = Math.Max(1, baseDamage - defense);

                // 4. ダメージ適用（シールド優先消費→HP減算）
                bool targetDied = ApplyDamage(currentTarget, finalDamage);

                // 5. ヒット通知（リアクションフック）
                // トゲ（反射）などのパッシブスキルがここで ReactionStack に積む。
                // 死亡したユニットへのリアクションもここで通知する。
                OnHitLanded?.Invoke(context, actor, currentTarget, finalDamage, waza.WazaElement);

                // 6. 攻撃に乗せた状態異常/デバフの付与
                // 命中したヒットのみ付与する（回避時はcontinueで到達しない）。
                // 死亡した相手には付与しない（毒/麻痺/防御低下などは生存中のみ意味を持つ）。
                IReadOnlyList<StatusEffectType> appliedEffects = System.Array.Empty<StatusEffectType>();
                if (!targetDied)
                    appliedEffects = ApplyTemplatedEffects(waza, currentTarget);

                outcomes.Add(new HitOutcome(
                    target: currentTarget,
                    damage: finalDamage,
                    resultedInDeath: targetDied,
                    damageElement: waza.WazaElement,
                    appliedEffects: appliedEffects,
                    targetHPAfter: currentTarget.CurrentHP));

                // 7. 死亡処理とかばう解除判定
                if (targetDied)
                {
                    HandleUnitDeath(context, currentTarget);

                    if (wasCoveredHit)
                    {
                        // 【かばうタンクの途中死亡】
                        // このヒットの超過ダメージは発生せず（ApplyDamageでHPが0にクランプ済み）。
                        // 次のヒット以降は、ResolveCoverTargetがタンクの死亡を検知して
                        // originalTargetを返すようになるため、続きはoriginalTargetに着弾する。
                        continue;
                    }
                    // タンクでない通常ターゲットが死亡した場合もループは続くが、
                    // 次のhitIndexでIsAlive == falseとなりスキップされる
                }
            }

            return lastTarget;
        }

        /// <summary>
        /// 同列スプラッシュを適用する。
        /// メインヒットで確定した anchor の同列にいる自陣営の生存ユニットへ、
        /// メインの基礎ダメージ × SameRowSplashMultiplier で巻き込みダメージを与える。
        /// - anchor が死亡していても列判定（SlotIndex 由来）は機能する
        /// - HitCount に関わらず splash は 1 回
        /// - 必中／回避／防御無視率は通常ヒットと同様に適用
        /// - riders（付帯効果）はメイン限定で splash には適用しない
        /// </summary>
        private void ApplyRowSplash(BattleContext context, RuntimeUnit actor, RuntimeWaza waza, RuntimeUnit anchor, List<HitOutcome> outcomes)
        {
            // anchor が所属するチームを取得（味方狙いの巻き込みは対象外。Attack カテゴリのみ呼ばれる前提で
            // anchor は敵チーム所属だが、汎用性のため安全側で同チームを巻き込む）。
            bool anchorIsAlly = context.AllyUnits.Contains(anchor);
            IReadOnlyList<RuntimeUnit> team = anchorIsAlly
                ? (IReadOnlyList<RuntimeUnit>)context.AllyUnits
                : context.EnemyUnits;

            foreach (var splashTarget in team)
            {
                if (splashTarget == anchor) continue;
                if (!splashTarget.IsAlive) continue;
                if (!IsSameRow(splashTarget, anchor)) continue;

                // 回避判定（必中なら skip）
                if (!waza.IsSureHit && IsEvaded(splashTarget))
                {
                    OnHitEvaded?.Invoke(context, actor, splashTarget);
                    outcomes.Add(new HitOutcome(
                        target: splashTarget,
                        wasEvaded: true,
                        targetHPAfter: splashTarget.CurrentHP));
                    continue;
                }

                // ダメージ計算：BaseDamage × splash 倍率 → DEF 減算
                int baseDamage = BattleManager.ComputeBaseDamage(actor, waza, splashTarget);
                baseDamage = (int)(baseDamage * waza.SameRowSplashMultiplier);
                int defense = waza.IsPhysical ? splashTarget.EffectivePDEF : splashTarget.EffectiveMDEF;
                if (waza.DefenseIgnoreRatio > 0f)
                    defense = (int)(defense * (1f - waza.DefenseIgnoreRatio));
                int finalDamage = Math.Max(1, baseDamage - defense);

                bool died = ApplyDamage(splashTarget, finalDamage);
                OnHitLanded?.Invoke(context, actor, splashTarget, finalDamage, waza.WazaElement);

                outcomes.Add(new HitOutcome(
                    target: splashTarget,
                    damage: finalDamage,
                    resultedInDeath: died,
                    damageElement: waza.WazaElement,
                    targetHPAfter: splashTarget.CurrentHP));

                if (died) HandleUnitDeath(context, splashTarget);
            }
        }

        // 内部ユーティリティ

        /// <summary>
        /// かばう対象のすり替えを解決する。
        /// originalTargetと同じチームのユニットの中から、
        /// originalTargetをかばっている（Cover効果を持つ）ユニットを探して返す。
        /// 複数存在する場合は最後に発動した（リストの末尾に近い）ものを優先する。
        /// かばうユニットが存在しない、またはすべて死亡している場合はoriginalTargetをそのまま返す。
        ///
        /// 複数対象技（全体・横列・後列直撃）は「かばう」を貫通し、各対象が直撃を受ける。
        /// AoEを1体のタンクが他人の弾まで肩代わりする状況を避け、AoEの本来の役割
        /// （広く散布して回復・薄い対象を突く）を機能させるため。
        /// </summary>
        private static RuntimeUnit ResolveCoverTarget(BattleContext context, RuntimeUnit originalTarget, RuntimeWaza waza)
        {
            // 「かばう」を貫通する条件：
            // (a) 技側で明示的に IgnoresCover を宣言している（単体技でも貫通する例外）
            // (b) 複数対象技（全体・横列） … AoE 概念上「全員に同時着弾」なのでタンクが肩代わりしない
            // (c) 対象が InfiltratorTag 持ち … 敵陣に潜るため自陣タンクからは守られない
            if (waza.IgnoresCover || IsMultiTargetWaza(waza.TargetingType)
                || originalTarget.BaseUnit.Tags.Contains(BattleManager.InfiltratorTag))
                return originalTarget;

            // originalTargetが所属するチームを取得
            bool targetIsAlly = context.AllyUnits.Contains(originalTarget);
            IReadOnlyList<RuntimeUnit> team = targetIsAlly
                ? (IReadOnlyList<RuntimeUnit>)context.AllyUnits
                : context.EnemyUnits;

            RuntimeUnit coveringUnit = null;

            foreach (var unit in team)
            {
                // 死亡済みのユニット・自分自身はかばう対象から外す
                if (!unit.IsAlive || unit == originalTarget) continue;

                // かばえる状態でない（Cover効果なし or 麻痺/完全凍結で無効）ならスキップ
                if (!unit.IsCovering) continue;

                foreach (var effect in unit.ActiveEffects)
                {
                    if (effect.EffectType != StatusEffectType.Cover) continue;

                    // 横列かばう：かばうユニットと同じ横列（前列/後列）の味方のみかばう
                    // それ以外：CoverTargetSlotIndex == -1 → 全員 / 指定スロット → そのユニットのみ
                    bool intercepts = effect.CoversSameRow
                        ? IsSameRow(unit, originalTarget)
                        : (effect.CoverTargetSlotIndex == -1 ||
                           effect.CoverTargetSlotIndex == originalTarget.SlotIndex);

                    if (intercepts)
                    {
                        // 後から見つかったものが優先（最後に発動した効果）
                        coveringUnit = unit;
                    }
                }
            }

            return coveringUnit ?? originalTarget;
        }

        /// <summary>2体が同じ横列（前列同士／後列同士）にいるかを判定する。</summary>
        private static bool IsSameRow(RuntimeUnit a, RuntimeUnit b)
        {
            return (a.IsFrontRow && b.IsFrontRow) || (a.IsBackRow && b.IsBackRow);
        }

        /// <summary>
        /// 純粋な「複数対象技」判定。全体（AllEnemies/AllAllies）と横列（FrontRow*）が該当。
        /// これらは概念上「全員に同時着弾」するため「かばう」を貫通する。
        ///
        /// 注意：BackRowEnemies は実装上ターゲット選定で単体に絞り込まれる（スコア最大の後列1体・
        /// TargetEvaluator.ComputeFinalTargets 参照）ため、ここでは複数対象として扱わない。
        /// 後列直撃技をかばう貫通させたい場合は、技側で IgnoresCover=true を明示する
        /// （対象数とかばう貫通可否を独立した概念として扱う設計）。
        /// </summary>
        private static bool IsMultiTargetWaza(TargetingType type)
        {
            return type == TargetingType.AllEnemies
                || type == TargetingType.AllAllies
                || type == TargetingType.FrontRowEnemies
                || type == TargetingType.FrontRowAllies;
        }

        /// <summary>
        /// 回避判定を行う。
        /// TotalEvasionが100以上なら確実に回避する。
        /// 1〜99の範囲なら乱数で判定する（値が高いほど回避しやすい）。
        /// 0以下なら絶対に回避しない。
        /// </summary>
        private bool IsEvaded(RuntimeUnit target)
        {
            int evasion = target.TotalEvasion;
            if (evasion >= 100) return true;    // 確実回避
            if (evasion <= 0) return false;      // 回避不可
            // 0〜99の乱数 < 回避率 なら回避成功
            return _random0to99() < evasion;
        }

        /// <summary>
        /// ダメージをターゲットに適用する。
        /// CurrentShieldを優先して消費し、シールドを超過した分のみCurrentHPから減算する。
        /// HPが0以下になった場合はHP=0にクランプしてtrueを返す（超過ダメージは消滅する）。
        /// </summary>
        /// <returns>ターゲットがこのダメージで死亡（HP0到達）した場合はtrue</returns>
        private static bool ApplyDamage(RuntimeUnit target, int damage)
        {
            // シールドがダメージを全て吸収できる場合
            if (target.CurrentShield >= damage)
            {
                target.CurrentShield -= damage;
                return false; // 死亡しない
            }

            // シールドを全て削り切り、超過分をHPに適用
            int overflow = damage - target.CurrentShield;
            target.CurrentShield = 0;
            target.BaseUnit.CurrentHP -= overflow;

            if (target.BaseUnit.CurrentHP <= 0)
            {
                // HPを0にクランプ（マイナスにしない。超過ダメージは消滅）
                target.BaseUnit.CurrentHP = 0;
                return true; // 死亡
            }

            return false; // 死亡しない
        }

        /// <summary>
        /// ユニットの死亡処理を行う。
        /// HPが0にクランプされた後に呼ばれる想定。
        /// OnUnitDied イベントを発火し、ハンドラ側へ後続処理を委譲する。
        ///
        /// State = Dead の設定は LostProcessor.ProcessPermanentLost が担う（Single Source of Truth）。
        /// 呼び出し時点で CurrentHP は既に0のため IsAlive == false となっている。
        /// </summary>
        private void HandleUnitDeath(BattleContext context, RuntimeUnit unit)
        {
            // State = Dead の設定は行わない（永続データの書き換えは LostProcessor の責務）。
            // 死亡通知：アンデッド復活・装備返還等をここにフックする。
            OnUnitDied?.Invoke(context, unit);
        }
    }
}
