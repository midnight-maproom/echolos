using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Conditional;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Battle
{
    /// <summary>
    /// バトルの進行を管理するオーケストレータ。
    /// PhaseStateの遷移・ターンの進行・勝敗判定を担う純粋なC#クラス。
    ///
    /// 設計方針：
    /// - MonoBehaviourを継承せず、Unity依存を完全に排除する
    /// - 各フェーズの処理はイベント（C# Action）で公開し、
    ///   ダメージ・状態異常・UIの処理を後付けできるよう設計する
    /// - TargetEvaluatorに依存性注入でターゲット評価ロジックを委譲する
    /// </summary>
    public class BattleManager
    {
        private readonly TargetEvaluator _targetEvaluator;

        /// <summary>
        /// 登録された Conditional バフ Processor 一覧。
        /// BattleStart / TurnStart / UnitDied フック発火時に該当 Processor だけが Refresh される。
        /// 新規 Conditional バフは派生クラスを作成して Bootstrap 経由で本リストに追加する。
        /// </summary>
        private readonly IReadOnlyList<ConditionalBuffProcessor> _conditionalProcessors;

        /// <summary>
        /// 横列かばうのユニットタグ。このタグを持つユニットには戦闘開始時に
        /// 同じ横列の味方をかばうCover効果が自動付与される。
        /// </summary>
        public const string RowCoverTag = "RowCover";

        /// <summary>
        /// 「敵が受ける回復を減衰させる」パッシブのユニットタグ。
        /// このタグを持つユニットが生存している間、敵チームのユニットへの回復は1/3に減衰する。
        /// 用途：耐久つぶしのギミックボス（プロト：どくどく男爵）。
        /// </summary>
        public const string AntiHealPassiveTag = "AntiHealPassive";

        /// <summary>
        /// 「魔導士特攻」パッシブの内部処理タグ（Unit.Tags に付与）。
        /// このタグを持つユニットが攻撃する際、対象が MageRole を持つなら基礎ダメージを
        /// MageHunterDamageMultiplier 倍にする。用途：アサシンの常時パッシブ。
        /// </summary>
        public const string MageHunterTag = "MageHunter";

        /// <summary>
        /// 「敵陣潜入者」パッシブのユニットタグ。
        /// このタグを持つユニットは、敵から攻撃される際に味方タンクのかばう対象外になる
        /// （ActionExecutor.ResolveCoverTarget で originalTarget が本タグ持ちなら味方かばう判定をスキップ）。
        /// 「敵陣に潜るため自陣のタンクからは守られない」というフレーバーを 1 行のメカニズムで表現する。
        /// </summary>
        public const string InfiltratorTag = "Infiltrator";

        /// <summary>
        /// 「魔導士」ロール（Unit.TargetTags に付与）。
        /// 戦闘ロジックでは魔導士特攻などの種別判定に用いる。属性（Element）とは別概念。
        /// </summary>
        public const string MageRole = "Mage";

        /// <summary>
        /// 魔導士特攻の基礎ダメージ倍率。攻撃側 MageHunterTag × 対象 MageRole で適用される。
        /// 防御は通常通り適用される（基礎ダメージ段階で倍化、その後 PDEF/MDEF を減算）。
        /// </summary>
        public const float MageHunterDamageMultiplier = 2f;

        // 基礎ダメージ算出（評価フェーズ／実行フェーズ共通）

        /// <summary>
        /// 攻撃側 × 対象の組み合わせから、基礎ダメージに乗る倍率を返す。
        /// 魔導士特攻（攻撃側 MageHunterTag × 対象 MageRole で MageHunterDamageMultiplier）。
        /// </summary>
        public static float GetDamageMultiplier(RuntimeUnit attacker, RuntimeUnit target)
        {
            if (attacker == null || target == null) return 1f;

            if (attacker.BaseUnit.Tags.Contains(MageHunterTag)
                && target.BaseUnit.TargetTags.Contains(MageRole))
                return MageHunterDamageMultiplier;

            return 1f;
        }

        /// <summary>
        /// 「基礎予定ダメージ」を算出する単一経路。評価フェーズ（TargetEvaluator のスコア計算）と
        /// 実行フェーズ（ActionExecutor のダメージ計算）の両方がここを通る。
        ///
        /// 内部処理：技固有のダメージ式 → 攻撃側パッシブ倍率（魔導士特攻等）。
        /// 防御減算・最低1保証・回避判定は実行フェーズ側でのみ行う（評価は基礎値のみ）。
        ///
        /// 二経路化していると修飾の追加忘れでバグるため（例：魔導士特攻を実行側にだけ実装した結果
        /// ターゲット選定が考慮されなかった件）、ここに一元化する。
        /// </summary>
        public static int ComputeBaseDamage(RuntimeUnit attacker, RuntimeWaza waza, RuntimeUnit target)
        {
            if (waza?.CalculateBaseDamage == null) return 0;

            int raw = waza.CalculateBaseDamage(attacker, target);

            float multiplier = GetDamageMultiplier(attacker, target);
            if (multiplier != 1f)
                raw = (int)(raw * multiplier);

            return raw;
        }

        // イベント

        /// <summary>フェーズが変化したときに発火する。引数：新しいPhaseState</summary>
        public event Action<PhaseState> OnPhaseChanged;

        /// <summary>
        /// ターン開始フェーズ（Start Phase）の処理フック。
        /// 開幕スキル・かばう付与などをここに登録する。
        /// </summary>
        public event Action<BattleContext> OnStartPhase;

        /// <summary>
        /// ユニットの行動開始直前に発火する。
        /// 呪いによる即死判定・麻痺行動不能カウントの更新をここに登録する。
        /// 引数：バトルコンテキスト、行動しようとしているユニット
        /// </summary>
        public event Action<BattleContext, RuntimeUnit> OnActionStart;

        /// <summary>
        /// 1ユニットの行動が実行される直前に発火する。
        /// ダメージ計算・状態異常付与・HP変更処理をここに登録する。
        /// 引数：バトルコンテキスト、宣言された行動内容
        /// </summary>
        public event Action<BattleContext, ActionDeclaration> OnActionExecuting;

        /// <summary>
        /// ユニットが行動をスキップした（待機・麻痺・完全凍結）ときに発火する。
        /// 引数：バトルコンテキスト、スキップしたユニット
        /// </summary>
        public event Action<BattleContext, RuntimeUnit> OnActionSkipped;

        /// <summary>
        /// ユニットの行動終了後（スキップ含む）に発火する。
        /// 燃焼ダメージなどの行動終了時処理をここに登録する。
        /// 引数：バトルコンテキスト、行動を終えたユニット
        /// </summary>
        public event Action<BattleContext, RuntimeUnit> OnActionEnd;

        /// <summary>
        /// プレイヤー介入待機フェーズ（InterventionStandby）の開始フック。
        /// UIでタイマーを開始し、プレイヤー入力を受け付ける処理をここに登録する。
        /// </summary>
        public event Action<BattleContext> OnInterventionStandby;

        /// <summary>
        /// ターン終了フェーズ（End Phase）の処理フック。
        /// 燃焼ダメージ・凍結スタック減算・シールドリセットをここに登録する。
        /// </summary>
        public event Action<BattleContext> OnEndPhase;

        /// <summary>
        /// ターン終了後（HasActedThisTurnリセット・CurrentTurn更新後）に発火する。
        /// </summary>
        public event Action<BattleContext> OnTurnEnd;

        /// <summary>
        /// 戦闘が終了したときに発火する。引数：戦闘評価結果
        /// </summary>
        public event Action<BattleContext, BattleResult> OnBattleEnded;

        public BattleManager(
            TargetEvaluator targetEvaluator,
            IReadOnlyList<ConditionalBuffProcessor> conditionalProcessors = null)
        {
            _targetEvaluator = targetEvaluator
                ?? throw new ArgumentNullException(nameof(targetEvaluator));
            _conditionalProcessors = conditionalProcessors
                ?? Array.Empty<ConditionalBuffProcessor>();
        }

        /// <summary>
        /// Conditional バフ Processor へフックを dispatch する。
        /// 該当する Hook を購読している Processor だけが呼ばれる。
        /// UnitDied フックは deadUnit を渡し、それ以外は DispatchRefresh 経由で再評価する
        /// （基底クラスの再帰ガードを通すため）。
        /// 外部（BattleRunner / ActionExecutor）から UnitDied 時に呼べるよう public とする。
        /// </summary>
        public void DispatchConditional(
            ConditionalBuffHook hook, BattleContext context, RuntimeUnit deadUnit = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            foreach (var p in _conditionalProcessors)
            {
                bool subscribed = false;
                foreach (var h in p.Hooks)
                {
                    if (h == hook) { subscribed = true; break; }
                }
                if (!subscribed) continue;

                if (hook == ConditionalBuffHook.UnitDied)
                    p.OnUnitDied(context, deadUnit);
                else
                    p.DispatchRefresh(context);
            }
        }

        // 公開API

        /// <summary>
        /// バトルを初期化する。BattleContext内の全ユニットに対して以下を行う：
        /// - BaseUnit.BaseWazasのバトルコピー（CDリセット済み）をBattleWazasに設定
        /// - リーダーフラグをallyLeaderUnitIdと照合して設定
        /// - BattleContextの統計・ターン・フェーズをリセット
        ///
        /// ProcessTurnを呼ぶ前に必ず一度呼び出すこと。
        /// </summary>
        public void InitializeBattle(BattleContext context, string allyLeaderUnitId = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            foreach (var unit in GetAllUnits(context))
            {
                // RuntimeWaza のコンストラクタが CD 初期化＋兵種強化 Magnitude の deep copy 吸収まで行う。
                int magnitudeBonus = unit.BaseUnit.EnhancementMagnitudePerLevel * unit.BaseUnit.EnhancementLevel;
                unit.BattleWazas = unit.BaseUnit.BaseWazas
                    .Select(w => new RuntimeWaza(w, magnitudeBonus))
                    .ToList();

                unit.IsLeader = !string.IsNullOrEmpty(allyLeaderUnitId)
                    && unit.BaseUnit.Id == allyLeaderUnitId
                    && context.AllyUnits.Contains(unit);

                unit.HasActedThisTurn = false;
            }

            // 横列かばう・置物オーラを戦闘開始時に自動付与する
            ApplyRowCoverAtBattleStart(context);
            ApplyAurasAtBattleStart(context);

            context.CurrentTurn = 1;
            context.CurrentPhase = PhaseState.Start;
            context.Result = BattleResult.None;
            context.InitialAllyCount  = context.AllyUnits.Count(u => !u.IsSummoned);
            context.InitialEnemyCount = context.EnemyUnits.Count(u => !u.IsSummoned);
            context.ReactionStack.Clear();

            // Conditional バフ Processor へ BattleStart フックを dispatch
            DispatchConditional(ConditionalBuffHook.BattleStart, context);
        }

        /// <summary>
        /// 1ターンを進行する。
        /// Start → Main → InterventionStandby → End の順でフェーズを遷移し、
        /// 勝敗が決まった場合はBattleResult（None以外）を返す。
        /// BattleResult.Noneを返した場合は戦闘継続（次ターンへ）。
        ///
        /// 呼び出しの前提：InitializeBattleが呼ばれていること。
        /// </summary>
        public BattleResult ProcessTurn(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // ── Start Phase ──
            context.CurrentPhase = PhaseState.Start;
            OnPhaseChanged?.Invoke(context.CurrentPhase);
            OnStartPhase?.Invoke(context);

            // Conditional バフ Processor へ TurnStart フックを dispatch
            DispatchConditional(ConditionalBuffHook.TurnStart, context);

            var startResult = CheckImmediateVictory(context);
            if (startResult != BattleResult.None)
                return FinalizeBattle(context, startResult);

            // ── Main Phase ──
            context.CurrentPhase = PhaseState.Main;
            OnPhaseChanged?.Invoke(context.CurrentPhase);
            var mainResult = ExecuteMainPhase(context);
            if (mainResult != BattleResult.None)
                return FinalizeBattle(context, mainResult);

            // ── InterventionStandby ──
            context.CurrentPhase = PhaseState.InterventionStandby;
            OnPhaseChanged?.Invoke(context.CurrentPhase);
            OnInterventionStandby?.Invoke(context);

            // ── End Phase ──
            context.CurrentPhase = PhaseState.End;
            OnPhaseChanged?.Invoke(context.CurrentPhase);
            ExecuteEndPhase(context);

            // ── ターン制限チェック ──
            if (context.CurrentTurn >= context.MaxTurnLimit)
            {
                var limitResult = context.IsAdvantageousVictoryCondition
                    ? BattleResult.AdvantageousVictory // 辛勝：損耗差で自軍有利
                    : BattleResult.MarginalDefeat;     // 惜敗：損耗差で敵有利（同数含む）
                return FinalizeBattle(context, limitResult);
            }

            // ── ターン終了処理 ──
            foreach (var unit in GetAllUnits(context))
                unit.HasActedThisTurn = false;

            context.CurrentTurn++;
            OnTurnEnd?.Invoke(context);

            return BattleResult.None;
        }

        /// <summary>
        /// 横列かばうタグを持つユニットに、同じ横列の味方をかばうCover効果を自動付与する。
        /// 味方・敵の両陣営に対称に適用する。既に横列かばうが付与済みなら重複させない（再初期化対策）。
        /// </summary>
        private static void ApplyRowCoverAtBattleStart(BattleContext context)
        {
            foreach (var unit in GetAllUnits(context))
            {
                if (!unit.BaseUnit.Tags.Contains(RowCoverTag)) continue;

                bool alreadyHasRowCover = unit.ActiveEffects.Any(
                    e => e.EffectType == StatusEffectType.Cover && e.CoversSameRow);
                if (alreadyHasRowCover) continue;

                unit.AddEffect(new StatusEffect(StatusEffectType.Cover)
                {
                    CoversSameRow = true,
                    RemainingTurns = -1
                });
            }
        }

        /// <summary>
        /// 置物オーラを戦闘開始時に付与する。
        /// AuraEffectを持つユニットの効果を、同陣営の生存ユニット全員に源ID付きで付与する。
        /// 源の死亡時の剥奪は StatusEffectProcessor が担う。既に同源のオーラがあれば重複させない。
        /// </summary>
        private static void ApplyAurasAtBattleStart(BattleContext context)
        {
            ApplyTeamAuras(context.AllyUnits);
            ApplyTeamAuras(context.EnemyUnits);
        }

        private static void ApplyTeamAuras(List<RuntimeUnit> team)
        {
            foreach (var source in team)
            {
                if (source.BaseUnit.AuraEffect == null || !source.IsAlive) continue;

                string sourceId = source.BaseUnit.Id;
                // 兵種強化のMagnitude強化を吸収（軍師の置物オーラ等）。
                int magnitudeBonus = source.BaseUnit.EnhancementMagnitudePerLevel
                    * source.BaseUnit.EnhancementLevel;

                foreach (var member in team)
                {
                    if (!member.IsAlive) continue;
                    if (member.ActiveEffects.Any(e => e.AuraSourceId == sourceId)) continue; // 再初期化対策

                    var applied = source.BaseUnit.AuraEffect.Clone();
                    applied.AuraSourceId = sourceId;
                    if (magnitudeBonus > 0) applied.Magnitude += magnitudeBonus;
                    member.AddEffect(applied);
                }
            }
        }

        /// <summary>
        /// 即時の勝敗条件を評価する（メインフェーズ外でも使用可能）。
        /// 敵全滅→PerfectVictory（完勝）、味方全滅→CrushingDefeat（完敗）、それ以外→None。
        /// </summary>
        public BattleResult CheckImmediateVictory(BattleContext context)
        {
            if (context.IsEnemyWiped) return BattleResult.PerfectVictory;
            if (context.IsAllyWiped)  return BattleResult.CrushingDefeat;
            return BattleResult.None;
        }

        /// <summary>
        /// 奇襲成功時の敵陣スロット入れ替え処理。
        /// 敵陣の SlotIndex 0↔3、1↔4、2↔5 をスワップし、前衛と後衛を逆転させる。
        ///
        /// 注意：奇襲の成否判定は呼び出し元が行う。
        ///       InitializeBattle の呼び出し前（PhaseState.Start の前）に実行すること。
        /// </summary>
        public static void ApplySurpriseAttackSwap(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // 前衛スロット(0,1,2)それぞれについて、対応する後衛(3,4,5)とSlotIndexをスワップ
            for (int frontSlot = 0; frontSlot <= 2; frontSlot++)
            {
                int backSlot  = frontSlot + 3;
                var frontUnit = context.EnemyUnits.Find(u => u.SlotIndex == frontSlot);
                var backUnit  = context.EnemyUnits.Find(u => u.SlotIndex == backSlot);

                if (frontUnit != null) frontUnit.SlotIndex = backSlot;
                if (backUnit  != null) backUnit.SlotIndex  = frontSlot;
            }
        }

        // フェーズ実行

        /// <summary>
        /// メインフェーズを実行する。
        /// 生存・未行動ユニット全員の行動を動的にSPDソートして順番に処理する。
        ///
        /// 状態異常処理：
        /// - OnActionStart で呪い判定・麻痺カウントを処理する
        /// - 呪いで死亡した場合は行動をスキップしReactionStackを処理する
        /// - 麻痺（IsParalyzed）または完全凍結（IsFullyFrozen）の場合は行動をスキップする
        /// - OnActionEnd で燃焼ダメージを処理する
        /// </summary>
        private BattleResult ExecuteMainPhase(BattleContext context)
        {
            while (true)
            {
                // 生存かつ未行動のユニットを収集（動的再計算）
                var unacted = GetUnactedUnits(context);
                if (unacted.Count == 0) break;

                // 全未行動ユニットが行動を「宣言」する
                var declarations = unacted
                    .Select(u => _targetEvaluator.DeclareAction(u, context))
                    .ToList();

                // SPD降順でソート（同率：味方優先 → SlotIndex昇順）
                declarations = SortDeclarationsBySpeed(declarations, context);

                var fastest = declarations[0];
                fastest.Actor.HasActedThisTurn = true;

                // ── 行動開始フック（呪い判定・麻痺カウント）──
                OnActionStart?.Invoke(context, fastest.Actor);

                // 呪いによる即死後はスキップ（OnActionStartで死亡判定される）
                if (!fastest.Actor.IsAlive)
                {
                    ProcessReactionStack(context);
                    var deathResult = CheckImmediateVictory(context);
                    if (deathResult != BattleResult.None) return deathResult;
                    continue;
                }

                // 待機 / 麻痺 / 完全凍結 の場合は行動をスキップ
                bool isSkipped = fastest.IsWaiting
                    || fastest.Actor.IsParalyzed
                    || fastest.Actor.IsFullyFrozen;

                if (isSkipped)
                {
                    OnActionSkipped?.Invoke(context, fastest.Actor);
                }
                else
                {
                    // 行動実行（ActionExecutorがここでダメージを処理する）
                    OnActionExecuting?.Invoke(context, fastest);

                    // 使用した技のCDと使用回数を更新
                    if (fastest.DeclaredWaza != null)
                    {
                        fastest.DeclaredWaza.CurrentCooldown = fastest.DeclaredWaza.Cooldown;
                        if (fastest.DeclaredWaza.MaxUsesPerBattle >= 0)
                            fastest.DeclaredWaza.CurrentUses++;
                    }
                }

                // ── 即時勝敗チェック（行動で全滅したか） ──
                // 行動の直接ダメージで敵/味方が全滅したなら、ここで決着させる。
                // この後の OnActionEnd（燃焼ダメージ）や ReactionStack はスキップする。
                // 例：行動で敵を倒した直後に行動者が自分の燃焼で死ぬと相打ち判定になってしまうため、
                // 「行動で勝った瞬間に勝利確定」を優先する。
                var immediateResult = CheckImmediateVictory(context);
                if (immediateResult != BattleResult.None) return immediateResult;

                // ── 行動終了フック（燃焼ダメージ等）──
                // 呪い死亡後はcontinueで到達しないため、ここは生存ユニットのみ
                if (fastest.Actor.IsAlive)
                    OnActionEnd?.Invoke(context, fastest.Actor);

                // ReactionStack（反撃・自爆等）をすべて消化する
                ProcessReactionStack(context);

                // 即時勝敗チェック（継続ダメージ／反撃の結果で全滅したか）
                var result = CheckImmediateVictory(context);
                if (result != BattleResult.None) return result;
            }

            return BattleResult.None;
        }

        /// <summary>
        /// ターン終了フェーズを実行する。
        /// OnEndPhaseフックで状態異常スタック減算を呼び出す。
        /// また、全ユニットのWaza CDを1減らし、シールドをリセットする。
        /// </summary>
        private void ExecuteEndPhase(BattleContext context)
        {
            OnEndPhase?.Invoke(context);

            // ターン終了時にすべての使用中Wazaのクールダウンを1減らす
            foreach (var unit in GetAllUnits(context))
            {
                if (!unit.IsAlive) continue;

                foreach (var waza in unit.BattleWazas)
                {
                    if (waza.CurrentCooldown > 0)
                        waza.CurrentCooldown--;
                }

                // シールドのターン終了リセット（CanCarryOverShieldがfalseの場合）
                if (!unit.CanCarryOverShield)
                    unit.CurrentShield = 0;
            }
        }

        // 内部ユーティリティ

        /// <summary>ReactionStackに積まれた全アクションを順次実行する</summary>
        private static void ProcessReactionStack(BattleContext context)
        {
            while (context.ReactionStack.Count > 0)
            {
                var reaction = context.ReactionStack.Dequeue();
                reaction?.Invoke();
            }
        }

        /// <summary>
        /// 行動宣言リストをSPD降順でソートする。
        /// 同率タイブレーク：1. AllyUnitsが先（味方優先）、2. SlotIndex昇順
        /// </summary>
        private static List<ActionDeclaration> SortDeclarationsBySpeed(
            List<ActionDeclaration> declarations, BattleContext context)
        {
            return declarations
                .OrderByDescending(d => d.EffectiveSPD)
                .ThenByDescending(d => context.AllyUnits.Contains(d.Actor) ? 1 : 0)
                .ThenBy(d => d.Actor.SlotIndex)
                .ToList();
        }

        /// <summary>生存かつ未行動のユニットを、味方→敵の順で返す</summary>
        private static List<RuntimeUnit> GetUnactedUnits(BattleContext context)
        {
            var result = new List<RuntimeUnit>();
            foreach (var u in context.AllyUnits)
                if (u.IsAlive && !u.HasActedThisTurn) result.Add(u);
            foreach (var u in context.EnemyUnits)
                if (u.IsAlive && !u.HasActedThisTurn) result.Add(u);
            return result;
        }

        /// <summary>味方・敵全ユニットを結合して返す</summary>
        private static IEnumerable<RuntimeUnit> GetAllUnits(BattleContext context)
        {
            return context.AllyUnits.Concat(context.EnemyUnits);
        }

        /// <summary>戦闘結果を確定し、OnBattleEndedを発火する</summary>
        private BattleResult FinalizeBattle(BattleContext context, BattleResult result)
        {
            context.Result = result;
            OnBattleEnded?.Invoke(context, result);
            return result;
        }
    }
}
