// Assets/Scripts/Core/Prototype/CampaignModels.cs
// プロト 段階2: 多戦線ミニループ（H2）の戦略レイヤー・データモデル（純C#・MonoBehaviour非依存）
//
// H2＝「限られた手駒を複数戦線にどう割り振るか（どこに主力を向けるか）」の判断が面白いか。
// それを最小コストで成立させる非対称性（仕様 210_prototype_spec.md §5.2）:
//   - 手駒 < 戦線数：全戦線に主力は置けない
//   - 戦線ごとに敵強度が違う：偵察しないと分からない（情報の非対称）
//   - 捨てた戦線の帰結が結果に出る：本拠地HPが削られる（トレードオフが可視）
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>内政アクションの種類（行動力を消費して実行する）。</summary>
    public enum PoliticsActionType
    {
        Fortify,   // 守備強化：対象ユニットの守備（PDEF/MDEF）を恒久的に上げる
        Scout,     // 偵察：対象戦線の敵編成を可視化する（情報の非対称を解消）
        Reinforce, // 攻撃強化：対象ユニットの火力（ATK）を恒久的に上げる
    }

    /// <summary>キャンペーン（多戦線ミニループ）の進行結果。</summary>
    public enum CampaignResult
    {
        None,    // 継続中
        Clear,   // 規定ターン本拠地を守り切った
        Defeat,  // 本拠地が陥落した
    }

    /// <summary>
    /// 戦略層が調整するチューニング値。すべて暫定でプレイテストで詰める前提。
    /// </summary>
    public sealed class CampaignConfig
    {
        /// <summary>毎ターン回復する行動力。これより内政アクション数が少ないため取捨選択が生じる。</summary>
        public int ActionPointsPerTurn = 2;

        /// <summary>各内政アクションの行動力コスト。</summary>
        public int FortifyCost = 1;
        public int ScoutCost = 1;
        public int ReinforceCost = 1;

        /// <summary>守備強化1回で上がるDEF量（PDEF/MDEFの両方・恒久）。</summary>
        public int FortifyDefBonus = 6;

        /// <summary>攻撃強化1回で上がるATK量（恒久）。</summary>
        public int ReinforceAtkBonus = 10;
    }

    /// <summary>
    /// 1つの戦線（自領と敵領の境界）。敵編成を持ち、プレイヤーが味方師団を割り当てる。
    /// </summary>
    public sealed class FrontState
    {
        public FrontState(string name, List<RuntimeUnit> enemyDivision, int baseBreakthroughDamage)
        {
            Name = name;
            EnemyDivision = enemyDivision ?? new List<RuntimeUnit>();
            BaseBreakthroughDamage = baseBreakthroughDamage;
        }

        /// <summary>戦線名（例：北の関門）。</summary>
        public string Name { get; }

        /// <summary>この戦線の敵編成。</summary>
        public List<RuntimeUnit> EnemyDivision { get; }

        /// <summary>突破された（味方が負ける／無防備）ときに本拠地HPへ与える基礎ダメージ。</summary>
        public int BaseBreakthroughDamage { get; }

        /// <summary>偵察済みか（敵編成が見えているか）。情報の非対称を表す。</summary>
        public bool IsScouted { get; set; }

        /// <summary>このターンに割り当てられた味方師団（空＝この戦線を捨てる）。</summary>
        public List<RuntimeUnit> AssignedAllies { get; } = new List<RuntimeUnit>();
    }

    /// <summary>1戦線の解決結果（レポート用）。</summary>
    public sealed class FrontResolution
    {
        public string FrontName { get; set; }

        /// <summary>味方師団が割り当てられていたか（false＝無防備で捨てた戦線）。</summary>
        public bool Defended { get; set; }

        /// <summary>戦線を維持できたか（味方勝利＝true）。</summary>
        public bool Held { get; set; }

        /// <summary>戦闘評価（完勝〜完敗）。無防備の場合は None。</summary>
        public BattleResult BattleResult { get; set; }

        /// <summary>この戦線の突破により本拠地が受けたダメージ（維持できたら0）。</summary>
        public int BreakthroughDamage { get; set; }

        /// <summary>この戦線で戦闘不能（HP0）になった味方。翌ターン休みになる。</summary>
        public List<RuntimeUnit> DownedAllies { get; set; } = new List<RuntimeUnit>();
    }

    /// <summary>
    /// 1戦線を解決する責務の抽象。
    /// 実装は既存の戦闘Coreを回す（BattleFrontResolver）。
    /// テストでは戦闘RNGに依存しないスタブを注入できる。
    /// </summary>
    public interface IFrontResolver
    {
        /// <summary>
        /// 割り当て味方 vs 戦線敵編成 を解決する。
        /// 戦闘不能になった味方は CurrentHP を 0 にして返すこと（戦略層が負傷判定に使う）。
        /// </summary>
        FrontResolution Resolve(IReadOnlyList<RuntimeUnit> assignedAllies, FrontState front);
    }

    /// <summary>
    /// 多戦線ミニループ全体の状態（本拠地・行動力・手駒・戦線・ターン）。
    /// </summary>
    public sealed class CampaignState
    {
        public CampaignState(int homeBaseMaxHp, int maxTurns, List<RuntimeUnit> roster, List<FrontState> fronts)
        {
            HomeBaseMaxHP = homeBaseMaxHp;
            HomeBaseHP = homeBaseMaxHp;
            MaxTurns = maxTurns;
            Roster = roster ?? new List<RuntimeUnit>();
            Fronts = fronts ?? new List<FrontState>();
        }

        /// <summary>本拠地の現在耐久。0以下で敗北。</summary>
        public int HomeBaseHP { get; set; }

        /// <summary>本拠地の最大耐久。</summary>
        public int HomeBaseMaxHP { get; }

        /// <summary>現在の行動力。</summary>
        public int ActionPoints { get; set; }

        /// <summary>手駒（プレイヤーが各戦線に割り振る全ユニット）。</summary>
        public List<RuntimeUnit> Roster { get; }

        /// <summary>現在の戦線群（2〜3）。</summary>
        public List<FrontState> Fronts { get; }

        /// <summary>現在ターン（1始まり）。</summary>
        public int CurrentTurn { get; set; } = 1;

        /// <summary>規定ターン数。これを守り切ればクリア。</summary>
        public int MaxTurns { get; }

        /// <summary>進行結果。</summary>
        public CampaignResult Result { get; set; } = CampaignResult.None;

        /// <summary>このターンは休み（前ターンに戦闘不能）で割り当て不可のユニット。</summary>
        public HashSet<RuntimeUnit> RestingUnits { get; } = new HashSet<RuntimeUnit>();

        /// <summary>直近の解決で戦闘不能になり、翌ターン休みになるユニット（AdvanceTurnで消費）。</summary>
        public HashSet<RuntimeUnit> NewlyDownedUnits { get; } = new HashSet<RuntimeUnit>();

        /// <summary>このターンに割り当て可能（手駒のうち休みでないユニット）。</summary>
        public IEnumerable<RuntimeUnit> AvailableUnits()
        {
            foreach (var u in Roster)
                if (!RestingUnits.Contains(u)) yield return u;
        }
    }
}
