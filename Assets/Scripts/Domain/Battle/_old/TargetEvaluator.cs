using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Battle
{
    /// <summary>
    /// ターゲット評価ロジック（評価フェーズ）。
    /// 実行フェーズ（ダメージ計算）とは完全に分離した純粋なロジッククラス。
    ///
    /// 責務：
    /// - 技の選択とフォールバック判定
    /// - 射程（リーチ）と列単位の保護の原則による到達可能な敵の判定
    /// - スコア評価（基礎予定ダメージ / 現在HP）によるターゲット決定
    /// - 同率時のタイブレーク（陣営 → スロットインデックス順）
    /// </summary>
    public class TargetEvaluator
    {
        // 仕様パラメータ

        /// <summary>自己防御フォールバック用 DefenseUp の効果値（控えめ）。</summary>
        private const int SelfGuardDefenseMagnitude = 3;

        /// <summary>
        /// 自己防御フォールバック用の Waza「防御」。
        /// 支援対象が一切無いとき、行動者自身に ActionGuard カテゴリの DefenseUp を付与する。
        /// AppliedEffects の StatusEffect は ApplyEffectWithStacking 内で Clone される
        /// ため、Waza インスタンスを使い回しても各ターン独立に効く（残ターン共有リスクなし）。
        /// ActionGuard は StatusEffectProcessor.HandleActionStart で「次の自分の行動順」に
        /// 厳密削除されるため、OnEndPhase の時限減算では消えない。
        /// </summary>
        private static readonly Waza SelfGuardWaza = CreateSelfGuardWaza();

        private static Waza CreateSelfGuardWaza()
        {
            return new Waza("def_guard", "防御")
            {
                Category = WazaCategory.Buff,
                TargetingType = TargetingType.Self,
                SPD = 5,
                AppliedEffects = new List<StatusEffect>
                {
                    StatusEffect.CreateActionGuard(
                        StatusEffectType.DefenseUp,
                        magnitude: SelfGuardDefenseMagnitude,
                        sourceAbilityName: "防御"),
                }
            };
        }

        // 公開API

        /// <summary>
        /// 指定ユニットの行動を評価フェーズで宣言する。
        /// 最適な技とターゲットを決定してActionDeclarationを返す。
        ///
        /// 選択ロジック：
        /// 1. forced（IsForcedWhenReady の CD 完了技）
        /// 2. support（回復・cleanse・dispel・debuff・buff の優先順位ルール）
        /// 3. 通常評価：BattleWazasの中からCD=0・使用回数・TargetingConditionをすべて満たす技を抽出
        ///    → 最大スコア（damage/HP）の技+ターゲットの組み合わせを選択
        /// 4. すべて空振り → 自己防御フォールバック（SelfGuardWaza）
        ///
        /// 攻撃ユニットは Attack カテゴリの Waza を必ず明示的に持つ前提（通常攻撃フォールバックは持たない）。
        /// </summary>
        public ActionDeclaration DeclareAction(RuntimeUnit actor, BattleContext context)
        {
            // ── 優先発動（チャージ） ──
            // 使用可能になった優先発動技があれば、AI評価を介さず確定的に発動する（台本的な行動切替）。
            var forcedAction = TryDeclareForcedWaza(actor, context);
            if (forcedAction != null) return forcedAction;

            // ── 支援行動の判断（条件ルール：回復 ＞ デバフ ＞ バフ） ──
            // 支援が妥当な状況なら支援行動を返し、そうでなければ従来の攻撃評価へ進む。
            var supportAction = TryDeclareSupportAction(actor, context);
            if (supportAction != null) return supportAction;

            // 使用可能な技を評価して最善のものを選ぶ
            float bestScore = float.MinValue;
            RuntimeWaza selectedWaza = null;
            List<RuntimeUnit> selectedCandidates = null;

            foreach (var waza in actor.BattleWazas)
            {
                // CDチェック：CDが残っていれば使用不可
                if (waza.CurrentCooldown > 0) continue;

                // 使用回数チェック：バトル内上限を超えていれば使用不可
                if (waza.MaxUsesPerBattle >= 0 && waza.CurrentUses >= waza.MaxUsesPerBattle) continue;

                // 射程を適用した有効ターゲットリストを取得
                var validTargets = GetValidTargets(actor, waza, context);

                // TargetingConditionでさらに絞り込む（HPが50%以下の対象のみ、など）
                if (waza.TargetingCondition != null)
                    validTargets = validTargets.Where(t => waza.TargetingCondition(t)).ToList();

                // 有効なターゲットが一つもなければこの技は使用不可
                if (validTargets.Count == 0) continue;

                // この技で達成できる最大スコアを計算
                float wazaScore = ComputeBestScore(actor, waza, validTargets);

                if (wazaScore > bestScore)
                {
                    bestScore = wazaScore;
                    selectedWaza = waza;
                    selectedCandidates = validTargets;
                }
            }

            // 使用可能な技が見つかった場合：その技でターゲットを確定する
            if (selectedWaza != null)
            {
                var finalTargets = ComputeFinalTargets(actor, selectedWaza, selectedCandidates, context);
                int spd = GetEffectiveSPD(actor, selectedWaza);
                return new ActionDeclaration(actor, selectedWaza, finalTargets, spd, false);
            }

            // ── フォールバック処理：自己防御 ──
            // 技が一切使えないユニット（重装兵などの攻撃手段なしユニット、攻撃技だけ持つが全 CD 中のユニット等）は
            // 自己防御に流れる。
            return BuildSelfGuardDeclaration(actor, context);
        }

        /// <summary>
        /// 自己防御アクションを宣言する（フォールバック専用）。
        /// SelfGuardWaza は static 共有テンプレなので RuntimeWaza でラップして CD・使用回数の共有を避ける。
        /// </summary>
        private ActionDeclaration BuildSelfGuardDeclaration(RuntimeUnit actor, BattleContext context)
        {
            var waza = new RuntimeWaza(SelfGuardWaza);
            return new ActionDeclaration(actor, waza, new List<RuntimeUnit> { actor },
                GetEffectiveSPD(actor, waza), false);
        }

        /// <summary>
        /// 指定技の有効なターゲットリストを返す（射程を適用済み）。
        ///
        /// 射程（横列単位・行動者の射程に依存）：
        /// - 近接：行動者が前列にいる時のみ行動可（後列なら待機＝対象なし）。届くのは敵前列（敵前列が全滅なら敵後列）
        /// - 中射程：位置を問わず行動可。届くのは敵前列（敵前列が全滅なら敵後列）
        /// - 遠隔：位置を問わず行動可。敵前列・後列どちらにも届く
        /// - 味方対象（回復/バフ等）は射程の制約を受けない（支援は誰にでも届く）
        /// - AllEnemies/AllAllies は射程を無視して全員が対象
        /// - FrontRowEnemies/Allies は横列単位：生存する前列全員（前列が全滅なら後列全員）
        /// </summary>
        public List<RuntimeUnit> GetValidTargets(RuntimeUnit actor, RuntimeWaza waza, BattleContext context)
        {
            switch (waza.TargetingType)
            {
                case TargetingType.SingleEnemy:
                    return GetReachableEnemies(actor, GetEnemyPool(actor, context), waza.IgnoresFrontRowGuard);

                case TargetingType.SingleAlly:
                    // 味方対象は射程の制約を受けない（後列の味方も回復・バフできる）
                    return GetAliveUnits(GetAllyPool(actor, context));

                case TargetingType.AllEnemies:
                    // 全体攻撃は射程を無視して全敵に命中する
                    return GetAliveUnits(GetEnemyPool(actor, context));

                case TargetingType.AllAllies:
                    // 全体回復等は射程を無視して全味方に命中する
                    return GetAliveUnits(GetAllyPool(actor, context));

                case TargetingType.FrontRowEnemies:
                    // 横列単位：生存する敵前列全員（前列が全滅なら後列が最前線として対象）
                    return GetFrontLineTargets(GetEnemyPool(actor, context));

                case TargetingType.FrontRowAllies:
                    return GetFrontLineTargets(GetAllyPool(actor, context));

                case TargetingType.BackRowEnemies:
                    // 後列直接狙い：前列の射程ブロックを貫通して敵後列を対象にする（かばうは実行時に有効）
                    return GetAliveUnits(GetEnemyPool(actor, context)).Where(u => u.IsBackRow).ToList();

                case TargetingType.Self:
                    return new List<RuntimeUnit> { actor };

                default:
                    return new List<RuntimeUnit>();
            }
        }

        /// <summary>
        /// 凍結スタックによるSPD低下を考慮した実効SPDを計算する。
        /// 凍結：1スタックにつきSPDが10%低下。0未満にはならない。
        /// </summary>
        public int GetEffectiveSPD(RuntimeUnit unit, RuntimeWaza waza)
        {
            if (waza == null) return unit.BaseUnit.BaseSPD;

            int freezeStacks = GetFreezeStacks(unit);
            if (freezeStacks <= 0) return waza.SPD;

            // 10スタック以上は完全凍結（行動不能）でSPD=0
            if (freezeStacks >= 10) return 0;

            // 1スタックにつき10%低下（四捨五入、最低0）
            // float精度誤差を避けるため整数演算で計算する：SPD × (10 - stacks) / 10
            return Math.Max(0, waza.SPD * (10 - freezeStacks) / 10);
        }

        // 内部ロジック：優先発動（チャージ）

        /// <summary>
        /// 優先発動フラグを持つ技が使用可能なら、AI評価を介さず確定発動のActionDeclarationを返す。
        /// 使用可能（CD・使用回数・有効ターゲットあり）な最初の優先発動技を採用する。なければnull。
        /// </summary>
        private ActionDeclaration TryDeclareForcedWaza(RuntimeUnit actor, BattleContext context)
        {
            foreach (var waza in actor.BattleWazas)
            {
                if (!waza.IsForcedWhenReady) continue;
                if (!IsWazaUsable(waza)) continue;

                var validTargets = GetValidTargets(actor, waza, context);
                if (waza.TargetingCondition != null)
                    validTargets = validTargets.Where(t => waza.TargetingCondition(t)).ToList();
                if (validTargets.Count == 0) continue;

                var finalTargets = ComputeFinalTargets(actor, waza, validTargets, context);
                if (finalTargets.Count == 0) continue;

                return new ActionDeclaration(actor, waza, finalTargets, GetEffectiveSPD(actor, waza), false);
            }
            return null;
        }

        // 内部ロジック：支援行動（条件ルール）

        /// <summary>
        /// 条件ルールに基づき支援行動（回復・デバフ・バフ）を判断する。
        /// 妥当な支援があればActionDeclarationを返し、なければnull（＝攻撃評価へ）。
        ///
        /// 優先度と条件：
        /// 1. 回復：味方の誰かがHP50%未満のとき、HP割合が最も低い味方へ
        /// 2. cleanse：味方に状態異常があれば、その味方へ（回復役2）
        /// 3. dispel：味方に能力デバフがあれば、その味方へ（軍師）
        /// 4. purge：敵に能力バフがあれば、そこへ（軍師）
        /// 5. デバフ：使用可能なら最大脅威（実効ATK最大の敵）へ
        /// 6. バフ：使用可能なら主力（実効ATK最大の味方）へ
        ///
        /// 支援系のターゲットは射程を無視する（後列の味方も回復でき、射程外の敵も弱体できる）。
        /// </summary>
        private ActionDeclaration TryDeclareSupportAction(RuntimeUnit actor, BattleContext context)
        {
            // 使用可能な支援技を能力別に1つずつ収集する（cleanse/dispel/purgeはフラグで分類）
            RuntimeWaza healWaza = null, buffWaza = null, debuffWaza = null,
                cleanseWaza = null, dispelWaza = null, purgeWaza = null;
            foreach (var waza in actor.BattleWazas)
            {
                if (waza.Category == WazaCategory.Attack) continue;
                if (!IsWazaUsable(waza)) continue;

                if (waza.CleansesStatusAilments && cleanseWaza == null) cleanseWaza = waza;
                if (waza.DispelsDebuffs && dispelWaza == null) dispelWaza = waza;

                // DispelsBuffs フラグ持ちは purge 専用分岐で処理する。
                // Category=Debuff でも debuffWaza には入れない（排他）。
                // これがないと「敵がバフを持たなくても最大脅威に発動」してしまい、
                // 軍師の「対象不在なら防御フォールバック」設計が崩れる。
                if (waza.DispelsBuffs)
                {
                    if (purgeWaza == null) purgeWaza = waza;
                    continue;
                }

                if (waza.Category == WazaCategory.Heal && healWaza == null) healWaza = waza;
                else if (waza.Category == WazaCategory.Debuff && debuffWaza == null) debuffWaza = waza;
                // バフは実際にバフを付与する技のみ（純粋なdispel技を主力バフと誤発動させない）
                else if (waza.Category == WazaCategory.Buff && buffWaza == null
                         && waza.AppliedEffects != null && waza.AppliedEffects.Count > 0) buffWaza = waza;
            }

            // ── 1. 回復（緊急）──
            if (healWaza != null)
            {
                var allies = AlivePool(actor, context, allySide: true);
                var wounded = SelectLowestHpRatioUnit(allies);
                if (wounded != null && IsBelowHealThreshold(wounded))
                    return BuildSupportDeclaration(actor, healWaza, wounded, allies);
            }

            // ── 2. cleanse（状態異常のある味方へ）──
            if (cleanseWaza != null)
            {
                var allies = AlivePool(actor, context, allySide: true);
                var ailing = allies.Find(HasCleansableAilment);
                if (ailing != null)
                    return BuildSupportDeclaration(actor, cleanseWaza, ailing, allies);
            }

            // ── 3. dispel（能力デバフのある味方へ）──
            if (dispelWaza != null)
            {
                var allies = AlivePool(actor, context, allySide: true);
                var debuffed = allies.Find(HasAbilityDebuff);
                if (debuffed != null)
                    return BuildSupportDeclaration(actor, dispelWaza, debuffed, allies);
            }

            // ── 4. purge（能力バフのある敵へ）──
            // 不在時は null を返して攻撃評価へ→最終的に防御フォールバック。
            // 軍師の「対象不在なら防御」設計の本体。
            if (purgeWaza != null)
            {
                var enemies = AlivePool(actor, context, allySide: false);
                var buffed = enemies.Find(HasAbilityBuff);
                if (buffed != null)
                    return BuildSupportDeclaration(actor, purgeWaza, buffed, enemies);
            }

            // ── 5. デバフ（最大脅威へ）──
            if (debuffWaza != null)
            {
                var enemies = AlivePool(actor, context, allySide: false);
                var threat = SelectHighestAtkUnit(enemies);
                if (threat != null)
                    return BuildSupportDeclaration(actor, debuffWaza, threat, enemies);
            }

            // ── 6. バフ（主力へ）──
            if (buffWaza != null)
            {
                var allies = AlivePool(actor, context, allySide: true);
                var mainForce = SelectHighestAtkUnit(allies);
                if (mainForce != null)
                    return BuildSupportDeclaration(actor, buffWaza, mainForce, allies);
            }

            // 支援判定で何も発動できなかったら null を返して通常の攻撃評価へ。
            // 攻撃評価でも何も選ばれなければ DeclareAction のフォールバックで
            // BuildSelfGuardDeclaration（自己防御）に流れる。
            return null;
        }

        /// <summary>味方が解除対象の状態異常（燃焼/毒・凍結・麻痺・呪い）を持つか</summary>
        private static bool HasCleansableAilment(RuntimeUnit unit)
        {
            return unit.ActiveEffects.Any(e => StatusEffect.IsStatusAilment(e.EffectType));
        }

        /// <summary>味方が解除対象の能力デバフ（攻撃/防御/回避ダウン）を持つか</summary>
        private static bool HasAbilityDebuff(RuntimeUnit unit)
        {
            return unit.ActiveEffects.Any(e => StatusEffect.IsAbilityDebuff(e.EffectType));
        }

        /// <summary>敵が解除対象の能力バフ（攻撃/防御/回避アップ）を持つか</summary>
        private static bool HasAbilityBuff(RuntimeUnit unit)
        {
            return unit.ActiveEffects.Any(e =>
                StatusEffect.IsAbilityBuff(e.EffectType) && !e.IsUndispellable);
        }

        /// <summary>
        /// 支援行動のActionDeclarationを構築する。
        /// 全体対象（AllAllies/AllEnemies）なら候補全員、それ以外は単体（best）を対象にする。
        /// </summary>
        private ActionDeclaration BuildSupportDeclaration(
            RuntimeUnit actor, RuntimeWaza waza, RuntimeUnit best, List<RuntimeUnit> pool)
        {
            bool isAoe = waza.TargetingType == TargetingType.AllAllies
                      || waza.TargetingType == TargetingType.AllEnemies;
            var targets = isAoe ? new List<RuntimeUnit>(pool) : new List<RuntimeUnit> { best };
            return new ActionDeclaration(actor, waza, targets, GetEffectiveSPD(actor, waza), false);
        }

        /// <summary>技がCD・使用回数の点で使用可能かを判定する</summary>
        private static bool IsWazaUsable(RuntimeWaza waza)
        {
            if (waza.CurrentCooldown > 0) return false;
            if (waza.MaxUsesPerBattle >= 0 && waza.CurrentUses >= waza.MaxUsesPerBattle) return false;
            return true;
        }

        /// <summary>
        /// HP が 1 でも減っていれば回復対象にする（CurrentHP &lt; MaxHP）。
        /// 「最も HP 割合の低い味方を回復」のための発動可否判定であり、
        /// 候補が一切いなければ自己防御フォールバックに流れる。
        /// </summary>
        private static bool IsBelowHealThreshold(RuntimeUnit unit)
        {
            return unit.CurrentHP < unit.MaxHP;
        }

        /// <summary>
        /// 行動者から見た味方/敵プールの生存ユニットを返す（射程は適用しない）。
        /// </summary>
        private static List<RuntimeUnit> AlivePool(RuntimeUnit actor, BattleContext context, bool allySide)
        {
            bool actorIsAlly = context.AllyUnits.Contains(actor);
            var pool = (allySide == actorIsAlly) ? context.AllyUnits : context.EnemyUnits;

            var result = new List<RuntimeUnit>();
            foreach (var u in pool)
                if (u.IsAlive) result.Add(u);
            return result;
        }

        /// <summary>HP割合（CurrentHP/MaxHP）が最も低い生存ユニットを返す。候補が空ならnull</summary>
        private static RuntimeUnit SelectLowestHpRatioUnit(List<RuntimeUnit> candidates)
        {
            RuntimeUnit best = null;
            foreach (var u in candidates)
            {
                if (!u.IsAlive) continue;
                if (best == null)
                {
                    best = u;
                    continue;
                }
                // u.CurrentHP/u.MaxHP < best.CurrentHP/best.MaxHP をクロス乗算で比較
                if ((long)u.CurrentHP * best.MaxHP < (long)best.CurrentHP * u.MaxHP)
                    best = u;
            }
            return best;
        }

        /// <summary>
        /// 実効ATKが最も高い生存ユニットを返す。候補が空ならnull。
        /// 攻撃手段を持たないユニット（HasAttackingMeans=false）は候補から除外する。
        /// これにより全員が攻撃しない陣営にバフを当てて空振りする事故を防ぐ
        /// （バフ評価で候補不在なら自己防御フォールバックへ流れる）。
        /// 永続データ（BaseATK / Tags / BaseWazas）のみで判定するため、
        /// 戦術隊形等の AttackUp 全体バフを受けて判定がブレることはない。
        /// </summary>
        private static RuntimeUnit SelectHighestAtkUnit(List<RuntimeUnit> candidates)
        {
            RuntimeUnit best = null;
            foreach (var u in candidates)
            {
                if (!u.IsAlive) continue;
                if (!u.HasAttackingMeans) continue;
                if (best == null || u.EffectiveATK > best.EffectiveATK) best = u;
            }
            return best;
        }

        // 内部ロジック：ターゲット決定

        /// <summary>
        /// スコア評価でベストな単体ターゲットを選定する。
        /// スコア = 基礎予定ダメージ / 対象の現在HP（シールド除外）
        /// 同率タイブレーク：1. 陣営（AllyUnitsを優先）、2. SlotIndex昇順
        ///
        /// 注意：整数演算（クロス乗算）でfloat精度問題を回避する。
        /// damage_A / hp_A > damage_B / hp_B ⟺ damage_A * hp_B > damage_B * hp_A
        /// </summary>
        public RuntimeUnit SelectBestSingleTarget(
            RuntimeUnit actor, RuntimeWaza waza,
            List<RuntimeUnit> candidates, BattleContext context)
        {
            if (candidates == null || candidates.Count == 0) return null;

            RuntimeUnit bestTarget = null;
            int bestDamage = 0;
            int bestHP = 1; // ゼロ除算回避の初期値

            foreach (var target in candidates)
            {
                if (!target.IsAlive) continue;
                if (target.CurrentHP <= 0) continue;

                // 基礎ダメージは BattleManager.ComputeBaseDamage を経由＝攻撃側パッシブ倍率
                // （魔導士特攻など）を含む。評価と実行で算出経路を一致させる（一元化）。
                int damage = BattleManager.ComputeBaseDamage(actor, waza, target);
                int hp = target.CurrentHP; // シールドは除外（CurrentHP = BaseUnit.CurrentHP）

                if (bestTarget == null)
                {
                    bestTarget = target;
                    bestDamage = damage;
                    bestHP = hp;
                    continue;
                }

                // クロス乗算でfloat精度問題を回避して大小比較
                long newScore = (long)damage * bestHP;
                long curScore = (long)bestDamage * hp;

                if (newScore > curScore)
                {
                    // 新しいターゲットがよりスコアが高い
                    bestTarget = target;
                    bestDamage = damage;
                    bestHP = hp;
                }
                else if (newScore == curScore)
                {
                    // 同率：タイブレークで決定
                    if (ShouldPreferCandidate(bestTarget, target, context))
                    {
                        bestTarget = target;
                        bestDamage = damage;
                        bestHP = hp;
                    }
                }
            }

            return bestTarget;
        }

        // 内部ロジック：射程（リーチ）

        /// <summary>
        /// 行動者の射程で到達できる生存中の敵を返す（単体攻撃用・列単位の保護の原則）。
        /// - 近接で行動者が後列にいる場合は行動不能（空リスト＝待機/置物）
        /// - 近接・中射程：敵前列に届く。敵後列は「真上の同列前列（slot-3）が死亡/不在＝むき出し」のときのみ届く
        /// - 遠隔：列単位の保護を貫通し、敵前列・後列どちらにも届く
        /// - 列単位保護貫通フラグ（waza.IgnoresFrontRowGuard）：近接・中射程でも敵後列を候補に入れる
        /// </summary>
        private static List<RuntimeUnit> GetReachableEnemies(RuntimeUnit actor, List<RuntimeUnit> enemyPool, bool ignoresFrontRowGuard = false)
        {
            var range = actor.BaseUnit.Range;

            // 近接は前列にいる時のみ行動できる（後列にいると待機＝置物）
            if (range == AttackRange.Melee && actor.IsBackRow)
                return new List<RuntimeUnit>();

            var result = new List<RuntimeUnit>();
            foreach (var unit in enemyPool)
            {
                if (!unit.IsAlive) continue;

                if (range == AttackRange.Ranged || ignoresFrontRowGuard)
                {
                    // 遠隔／列単位保護貫通技は列構造を無視して全敵に届く
                    result.Add(unit);
                    continue;
                }

                // 近接/中射程：列単位の保護の原則
                if (unit.IsFrontRow)
                {
                    result.Add(unit); // 前列は常に届く
                }
                else if (IsBackRowExposed(unit, enemyPool))
                {
                    result.Add(unit); // むき出しの後列（真上の前列が死亡/不在）
                }
            }
            return result;
        }

        /// <summary>
        /// 後列ユニットが「むき出し」か判定する。
        /// 真上の同列前列スロット（slot-3）のユニットが死亡または不在ならむき出し。
        /// </summary>
        private static bool IsBackRowExposed(RuntimeUnit backUnit, List<RuntimeUnit> pool)
        {
            int columnFrontSlot = backUnit.SlotIndex - 3; // slot3→0, slot4→1, slot5→2
            foreach (var u in pool)
            {
                if (u.SlotIndex == columnFrontSlot)
                    return !u.IsAlive; // 居るが死亡 → むき出し / 居て生存 → 保護される
            }
            return true; // 該当スロットに誰も居ない（不在） → むき出し
        }

        /// <summary>
        /// 横列範囲攻撃（薙ぎ払い等）のターゲットリストを構築する（前衛範囲攻撃の貫通）。
        /// 各列について「前列が生存していれば前列、不在/死亡なら真後ろのむき出し後列」を集める。
        /// 全前列生存 → 前列3体／一部の前列が空 → 残った前列＋空き列のむき出し後列／全前列死亡 → 後列3体。
        /// </summary>
        private static List<RuntimeUnit> GetFrontLineTargets(List<RuntimeUnit> allUnitsInFaction)
        {
            var result = new List<RuntimeUnit>();
            for (int column = 0; column < 3; column++)
            {
                int frontSlot = column;
                int backSlot = column + 3;
                RuntimeUnit front = null, back = null;
                foreach (var u in allUnitsInFaction)
                {
                    if (!u.IsAlive) continue;
                    if (u.SlotIndex == frontSlot) front = u;
                    else if (u.SlotIndex == backSlot) back = u;
                }
                if (front != null) result.Add(front);
                else if (back != null) result.Add(back); // むき出しの後列を巻き込む
            }
            return result;
        }

        // 内部ロジック：スコア計算とタイブレーク

        /// <summary>
        /// 候補リストの中でこの技が達成できる最大スコアを返す。
        /// 複数のWazaを比較して最善の技を選ぶために使用する。
        /// </summary>
        private static float ComputeBestScore(RuntimeUnit actor, RuntimeWaza waza, List<RuntimeUnit> targets)
        {
            float best = float.MinValue;
            foreach (var t in targets)
            {
                if (!t.IsAlive || t.CurrentHP <= 0) continue;
                if (waza.CalculateBaseDamage == null) continue;

                // スコア = 基礎予定ダメージ / 現在HP（シールドは含まない）。
                // 基礎ダメージは BattleManager.ComputeBaseDamage 経由＝攻撃側パッシブ倍率込み。
                float score = (float)BattleManager.ComputeBaseDamage(actor, waza, t) / t.CurrentHP;
                if (score > best) best = score;
            }
            return best;
        }

        /// <summary>
        /// 単体技のターゲットを確定する。AoEの場合は全有効ターゲットを返す。
        /// </summary>
        private List<RuntimeUnit> ComputeFinalTargets(
            RuntimeUnit actor, RuntimeWaza waza,
            List<RuntimeUnit> validCandidates, BattleContext context)
        {
            switch (waza.TargetingType)
            {
                case TargetingType.SingleEnemy:
                case TargetingType.SingleAlly:
                case TargetingType.BackRowEnemies: // 後列直接狙いは単体（スコア最大の後列を選ぶ）
                {
                    var best = SelectBestSingleTarget(actor, waza, validCandidates, context);
                    return best != null ? new List<RuntimeUnit> { best } : new List<RuntimeUnit>();
                }

                case TargetingType.Self:
                    return new List<RuntimeUnit> { actor };

                default:
                    // AoE系：有効なターゲット全員を対象にする
                    return new List<RuntimeUnit>(validCandidates);
            }
        }

        /// <summary>
        /// タイブレーク：candidateがcurrentよりも優先されるべきかを判定する。
        /// 優先順位：1. AllyUnitsに属するユニット（味方優先）、2. SlotIndex昇順
        /// </summary>
        private static bool ShouldPreferCandidate(
            RuntimeUnit current, RuntimeUnit candidate, BattleContext context)
        {
            bool currentIsAlly = context.AllyUnits.Contains(current);
            bool candidateIsAlly = context.AllyUnits.Contains(candidate);

            // 1. 陣営：AllyUnits > EnemyUnits
            if (candidateIsAlly && !currentIsAlly) return true;
            if (!candidateIsAlly && currentIsAlly) return false;

            // 2. 同陣営：スロットインデックスが小さい方（前衛左>中>右>後衛左>中>右）
            return candidate.SlotIndex < current.SlotIndex;
        }

        // 内部ロジック：プール取得・ユーティリティ

        /// <summary>
        /// 行動者にとっての「敵」ユニットプールを返す。
        /// 行動者がAllyUnitsに属すれば敵プール=EnemyUnits、逆も然り。
        /// </summary>
        private static List<RuntimeUnit> GetEnemyPool(RuntimeUnit actor, BattleContext context)
        {
            return context.AllyUnits.Contains(actor) ? context.EnemyUnits : context.AllyUnits;
        }

        /// <summary>
        /// 行動者にとっての「味方」ユニットプールを返す。
        /// </summary>
        private static List<RuntimeUnit> GetAllyPool(RuntimeUnit actor, BattleContext context)
        {
            return context.AllyUnits.Contains(actor) ? context.AllyUnits : context.EnemyUnits;
        }

        /// <summary>指定リストから生存しているユニットのみを返す</summary>
        private static List<RuntimeUnit> GetAliveUnits(List<RuntimeUnit> units)
        {
            var result = new List<RuntimeUnit>();
            foreach (var u in units)
                if (u.IsAlive) result.Add(u);
            return result;
        }

        /// <summary>ユニットに付与された凍結スタックの合計を返す</summary>
        private static int GetFreezeStacks(RuntimeUnit unit)
        {
            int total = 0;
            foreach (var effect in unit.ActiveEffects)
                if (effect.EffectType == StatusEffectType.Freeze)
                    total += effect.Stacks;
            return total;
        }

    }
}
