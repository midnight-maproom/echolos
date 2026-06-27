// Assets/Scripts/Core/Prototype/Stage3CampaignModels.cs
// 段階3：ローグライト・ミニループ（H3）の戦略レイヤー・データモデル（純C#・MonoBehaviour非依存）。
//
// 段階2の CampaignModels.cs（本拠地HP方式）とは別構造。
// 仕様 210_prototype_spec.md §6・§7 に準拠：
//   - 戦線3つ（平原・街・砦）・累計負け点数で勝敗判定
//   - 7ラウンド構成（R1-6 通常／R7 街固定ボス）
//   - 内政4コマンド（行動力2・同一アクション禁止）
//   - 兵種強化（陣営共通レベル・同一兵種2回まで）
//   - 拠点強化（戦線単位・Lv上限戦線別）
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>戦線の種類（プロト段階3・§10.7）。</summary>
    public enum BattlefrontKind
    {
        /// <summary>平原（左）：余裕がある戦線。点数上限6・初期Lv0・最大Lv1。捨て戦線候補。</summary>
        Plain,
        /// <summary>街（中央）：中庸。点数上限4・初期Lv0・最大Lv2。R7 でボスが固定で攻めてくる最終決戦の場。</summary>
        City,
        /// <summary>砦（右）：最も脆い。点数上限2・初期Lv1・最大Lv3。事故率が高く全力守備が必要。</summary>
        Fortress,
    }

    /// <summary>敵パターンの強度区分（§10.6 教習ラダー）。</summary>
    public enum EnemyPatternTier
    {
        /// <summary>敵なし戦線（弱①／R7の平原・砦）。</summary>
        None,
        /// <summary>弱パターン（R1-R2）。</summary>
        Weak,
        /// <summary>中パターン（R3-R4）。</summary>
        Medium,
        /// <summary>強パターン（R5-R6）。</summary>
        Strong,
        /// <summary>ボスパターン（R7）。</summary>
        Boss,
    }

    /// <summary>内政アクション（プロト段階3・§10.8）。</summary>
    public enum InteriorActionKind
    {
        /// <summary>偵察：選んだ戦線の敵編成をこのラウンドだけ開示する。</summary>
        Scout,
        /// <summary>兵種強化：兵種を選んで永続強化する（同一兵種は最大2回）。</summary>
        UpgradeUnitType,
        /// <summary>拠点強化：戦線を選んで拠点強化レベルを1上げる（Lv上限は戦線別）。</summary>
        UpgradeBase,
        /// <summary>招集：3択ドラフトを行い1体加入させる（15%でレア抽選）。</summary>
        Conscript,
    }

    /// <summary>キャンペーンの進行結果（プロト段階3・§9.7）。</summary>
    public enum Stage3CampaignResult
    {
        /// <summary>継続中。</summary>
        None,
        /// <summary>R7 ボス戦に勝利。</summary>
        Cleared,
        /// <summary>いずれかの戦線が累計点数上限に到達。</summary>
        FrontLost,
        /// <summary>R7 ボス戦に勝てなかった（敗北・引き分け）。</summary>
        BossLost,
    }

    /// <summary>1つの戦線の状態。</summary>
    public sealed class Battlefront
    {
        public Battlefront(BattlefrontKind kind, int pointCap, int initialBaseLevel, int maxBaseLevel)
        {
            Kind = kind;
            PointCap = pointCap;
            BaseLevel = initialBaseLevel;
            MaxBaseLevel = maxBaseLevel;
        }

        /// <summary>戦線の種類。</summary>
        public BattlefrontKind Kind { get; }

        /// <summary>累計負け点数の上限（到達でゲームオーバー）。</summary>
        public int PointCap { get; }

        /// <summary>現在の累計負け点数（完敗+2／惜敗+1／勝利・引き分け0）。</summary>
        public int CumulativePoints { get; set; }

        /// <summary>現在の拠点強化レベル。</summary>
        public int BaseLevel { get; set; }

        /// <summary>拠点強化レベルの上限。</summary>
        public int MaxBaseLevel { get; }

        /// <summary>このラウンドに割り当てられた敵編成。</summary>
        public List<RuntimeUnit> EnemyComposition { get; set; } = new List<RuntimeUnit>();

        /// <summary>このラウンドの敵パターン区分。</summary>
        public EnemyPatternTier PatternTier { get; set; } = EnemyPatternTier.None;

        /// <summary>パターン識別ラベル（例：「Weak#2 散兵×3」・ログ・UI用）。</summary>
        public string PatternLabel { get; set; }

        /// <summary>このラウンド偵察済みか（敵編成が見えているか）。</summary>
        public bool IsScouted { get; set; }

        /// <summary>このラウンドに割り当てられた味方師団。</summary>
        public List<RuntimeUnit> AssignedAllies { get; } = new List<RuntimeUnit>();

        /// <summary>戦線の表示名（UI・ログ用）。</summary>
        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case BattlefrontKind.Plain: return "平原";
                    case BattlefrontKind.City: return "街";
                    case BattlefrontKind.Fortress: return "砦";
                    default: return Kind.ToString();
                }
            }
        }

        /// <summary>累計点数が上限に達したか（ゲームオーバー条件）。</summary>
        public bool IsExhausted => CumulativePoints >= PointCap;

        /// <summary>ラウンド開始時にラウンド単位の状態をリセットする（敵編成・偵察・配置）。</summary>
        public void ResetForNewRound()
        {
            EnemyComposition.Clear();
            PatternTier = EnemyPatternTier.None;
            PatternLabel = null;
            // §10.8 偵察は1コマンドで全戦線開示する仕様に変更されたため、自動偵察Lvは廃止。
            // 偵察コマンドが未実行ならどの戦線も未偵察スタート。
            IsScouted = false;
            AssignedAllies.Clear();
        }
    }

    /// <summary>キャンペーン全体の設定（プロト段階3）。</summary>
    public sealed class Stage3CampaignConfig
    {
        /// <summary>総ラウンド数（§9.3：R1-R6 通常 + R7 ボス＝7）。</summary>
        public int MaxRounds { get; set; } = 7;

        /// <summary>1ラウンドの行動力（§7.1 踏襲・§10.8）。</summary>
        public int ActionPointsPerRound { get; set; } = 2;

        /// <summary>
        /// ゲーム開始時の3択ドラフト回数（§9.5）。
        /// 2026-06-02 改訂：姫騎士（固有キャラ）が固定で1体加入するため 3→2 に減らした。
        /// </summary>
        public int InitialDraftPicks { get; set; } = 2;

        /// <summary>各ドラフトでレア抽選になる確率（§10.5）。</summary>
        public float RareDraftProbability { get; set; } = 0.15f;

        /// <summary>同一ラン内未抽選の場合に確定でレアにするラウンド（§10.5）。</summary>
        public int RareGuaranteeRound { get; set; } = 6;

        /// <summary>各内政アクションの行動力コスト（§10.8 全て1）。</summary>
        public int ActionCost { get; set; } = 1;
    }

    /// <summary>キャンペーン全体の状態（プロト段階3・H3検証用）。</summary>
    public sealed class Stage3CampaignState
    {
        public Stage3CampaignState(Stage3CampaignConfig config = null)
        {
            Config = config ?? new Stage3CampaignConfig();
            CurrentRound = 1;
            ActionPoints = Config.ActionPointsPerRound;
            Result = Stage3CampaignResult.None;

            // §10.7 戦線3つの仕様値。
            Battlefronts = new List<Battlefront>
            {
                new Battlefront(BattlefrontKind.Plain,    pointCap: 6, initialBaseLevel: 0, maxBaseLevel: 1),
                new Battlefront(BattlefrontKind.City,     pointCap: 4, initialBaseLevel: 0, maxBaseLevel: 2),
                new Battlefront(BattlefrontKind.Fortress, pointCap: 2, initialBaseLevel: 1, maxBaseLevel: 3),
            };
        }

        public Stage3CampaignConfig Config { get; }

        /// <summary>現在のラウンド（1始まり、MaxRounds まで）。</summary>
        public int CurrentRound { get; set; }

        /// <summary>このラウンドの残り行動力。</summary>
        public int ActionPoints { get; set; }

        /// <summary>3つの戦線（平原／街／砦）。</summary>
        public List<Battlefront> Battlefronts { get; }

        /// <summary>進行結果。None＝継続中。</summary>
        public Stage3CampaignResult Result { get; set; }

        /// <summary>
        /// プレイヤーの手駒（Unit エンティティ）。ドラフトで加入したユニットを保持する。
        /// 戦線への割り当ては Battlefront.AssignedAllies で別途管理（RuntimeUnit）。
        /// </summary>
        public List<Unit> Roster { get; } = new List<Unit>();

        /// <summary>
        /// 兵種ごとの現在の強化レベル（§10.10 陣営共通）。
        /// key = 兵種ID（Unit.Id, 例："s3_atk_multi"）, value = 強化レベル 0〜2。
        /// 同名兵種を複数体保有していてもこのテーブルから一括で参照・適用される。
        /// </summary>
        public Dictionary<string, int> UnitTypeEnhancementLevels { get; } = new Dictionary<string, int>();

        /// <summary>
        /// 同一ラウンド内で実行済みの内政アクション種別（§10.8 同一アクション禁止）。
        /// ラウンド進行時にクリアする。
        /// </summary>
        public HashSet<InteriorActionKind> ExecutedInteriorActions { get; } = new HashSet<InteriorActionKind>();

        /// <summary>
        /// このラン中に一度でもレアドラフトが走ったか（§10.5・§9.8 R6 レア確定保証用）。
        /// R6 のラウンド開始ドラフトで false ならレアを確定にする。
        /// </summary>
        public bool HasDrawnRareThisRun { get; set; }

        /// <summary>
        /// R7 ボス戦の最終結果（R7 のみ確定）。
        /// PerfectVictory / AdvantageousVictory のみクリア扱い、それ以外は BossLost（§9.7）。
        /// </summary>
        public BattleResult? BossBattleResult { get; set; }

        /// <summary>指定種別の戦線を返す。</summary>
        public Battlefront GetBattlefront(BattlefrontKind kind)
        {
            return Battlefronts.Find(f => f.Kind == kind);
        }

        // ══════════════════════════════════════════════
        // 兵種強化機構（§10.10）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 兵種を1段階強化する（§10.8 内政コマンド・§10.10）。
        /// 既に2段階の場合は強化せず false を返す。成功時は陣営の該当兵種すべての
        /// EnhancementLevel を新しい値に同期する。
        /// </summary>
        public bool UpgradeUnitType(string unitTypeId)
        {
            int current = GetUnitTypeEnhancementLevel(unitTypeId);
            if (current >= 2) return false;

            int newLevel = current + 1;
            UnitTypeEnhancementLevels[unitTypeId] = newLevel;

            // 該当兵種を持つ全ユニットの EnhancementLevel を同期。
            foreach (var u in Roster)
                if (u.Id == unitTypeId) u.EnhancementLevel = newLevel;

            return true;
        }

        /// <summary>指定兵種の現在の強化レベル（未登録は0扱い）。</summary>
        public int GetUnitTypeEnhancementLevel(string unitTypeId)
        {
            return UnitTypeEnhancementLevels.TryGetValue(unitTypeId, out var lv) ? lv : 0;
        }

        // ══════════════════════════════════════════════
        // 姫騎士（固有キャラ）の拠点Lv連動強化（§10.10・2026-06-02 追加）
        // ══════════════════════════════════════════════

        /// <summary>姫騎士の強化Lv上限（§10.10：拠点合計Lv 6 ÷ 2 = 3）。</summary>
        public const int PrincessMaxEnhancementLevel = 3;

        /// <summary>
        /// 姫騎士の動的な EnhancementLevel を返す（§10.10）。
        /// 算出式：全戦線の拠点 BaseLevel 合計 ÷ 2（切り捨て・上限 3）。
        /// Stage3RoundManager.StartRound で計算し、姫騎士の Unit.EnhancementLevel に反映する。
        /// 内政「兵種強化」コマンドの対象にはしない（姫騎士は別経路で強化される）。
        /// </summary>
        public int GetPrincessEnhancementLevel()
        {
            int totalBaseLevel = 0;
            foreach (var f in Battlefronts) totalBaseLevel += f.BaseLevel;
            int level = totalBaseLevel / 2;
            return level > PrincessMaxEnhancementLevel ? PrincessMaxEnhancementLevel : level;
        }

        /// <summary>
        /// 新規ユニットを手駒に加える。加入時に該当兵種の現在の強化レベルを自動適用する
        /// （仕様：同名兵種を複数体獲得していても、強化はすべてに適用される）。
        /// </summary>
        public void AddUnitToRoster(Unit u)
        {
            if (u == null) return;
            int level = GetUnitTypeEnhancementLevel(u.Id);
            if (level > 0) u.EnhancementLevel = level;
            Roster.Add(u);
        }

        // ══════════════════════════════════════════════
        // 内政アクション履歴
        // ══════════════════════════════════════════════

        /// <summary>同一ラウンドで指定アクションが既に実行済みか（§10.8）。</summary>
        public bool HasExecutedThisRound(InteriorActionKind action)
        {
            return ExecutedInteriorActions.Contains(action);
        }

        /// <summary>
        /// 内政アクションを実行可能か（行動力・同一アクション禁止）チェックする。
        /// 副作用なし。実行は別途 Mark/消費する。
        /// </summary>
        public bool CanExecuteInteriorAction(InteriorActionKind action)
        {
            if (ActionPoints < Config.ActionCost) return false;
            if (HasExecutedThisRound(action)) return false;
            return true;
        }

        /// <summary>
        /// 内政アクション実行のマーク：行動力を消費して同一アクション禁止リストに追加する。
        /// 戻り値：成功＝true、不可＝false（行動力不足や同一アクション既実行）。
        /// </summary>
        public bool MarkInteriorActionExecuted(InteriorActionKind action)
        {
            if (!CanExecuteInteriorAction(action)) return false;
            ActionPoints -= Config.ActionCost;
            ExecutedInteriorActions.Add(action);
            return true;
        }
    }
}
