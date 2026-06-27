// Assets/Scripts/Core/Prototype/Stage3InteriorService.cs
// 段階3：内政4コマンドの実行サービス（仕様 120 §10.8・§10.9）。
//
// 4コマンド（行動力1ずつ・同一アクション禁止）：
//   - 偵察：選んだ戦線の敵編成をこのラウンドだけ開示する
//   - 兵種強化：兵種を選んで永続強化（同一兵種は最大2回）
//   - 拠点強化：戦線を選んで拠点強化レベルを1上げる（Lv上限は戦線別）
//   - 招集：3択ドラフトを行い1体加入（同一ラウンド1回まで・15%でレア抽選）
//
// プロト版限定：治療コマンドはラウンド開始時HP全回復に置換されたため廃止（§9.6・§10.8）。
using System.Linq;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>
    /// 内政4コマンドの実行サービス（純C#・MonoBehaviour非依存）。
    /// 行動力消費・同一アクション禁止チェックは Stage3CampaignState に委譲し、
    /// このサービスは各コマンド固有の前提条件と副作用を担う。
    /// </summary>
    public sealed class Stage3InteriorService
    {
        private readonly Stage3DraftService _draftService;

        public Stage3InteriorService(Stage3DraftService draftService)
        {
            _draftService = draftService
                ?? throw new System.ArgumentNullException(nameof(draftService));
        }

        // ══════════════════════════════════════════════
        // 偵察（§10.8 新仕様：1コマンドで全戦線開示）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 偵察が可能か。全戦線が既に偵察済みなら無駄なので不可。
        /// </summary>
        public bool CanScout(Stage3CampaignState state)
        {
            if (state == null) return false;
            if (!state.CanExecuteInteriorAction(InteriorActionKind.Scout)) return false;
            bool allScouted = true;
            foreach (var f in state.Battlefronts)
                if (!f.IsScouted) { allScouted = false; break; }
            return !allScouted;
        }

        /// <summary>偵察を実行する。3戦線すべての IsScouted = true となる（§10.8）。</summary>
        public bool ExecuteScout(Stage3CampaignState state)
        {
            if (!CanScout(state)) return false;
            state.MarkInteriorActionExecuted(InteriorActionKind.Scout);
            foreach (var f in state.Battlefronts) f.IsScouted = true;
            return true;
        }

        // ══════════════════════════════════════════════
        // 兵種強化
        // ══════════════════════════════════════════════

        /// <summary>
        /// 指定兵種の強化が可能か。
        /// ロスターに該当兵種を1体以上保有していること（保有していない兵種は強化不可）。
        /// 既に2段階の場合も不可。
        /// </summary>
        public bool CanUpgradeUnitType(Stage3CampaignState state, string unitTypeId)
        {
            if (state == null || string.IsNullOrEmpty(unitTypeId)) return false;
            if (!state.CanExecuteInteriorAction(InteriorActionKind.UpgradeUnitType)) return false;
            if (state.GetUnitTypeEnhancementLevel(unitTypeId) >= 2) return false;
            if (!state.Roster.Any(u => u.Id == unitTypeId)) return false;
            // §10.10 姫騎士は兵種強化コマンドの対象外（拠点Lv連動で別経路強化される）。
            if (unitTypeId == Stage3Roster.PrincessId) return false;
            return true;
        }

        /// <summary>兵種強化を実行する（陣営の該当兵種すべてに反映される）。</summary>
        public bool ExecuteUpgradeUnitType(Stage3CampaignState state, string unitTypeId)
        {
            if (!CanUpgradeUnitType(state, unitTypeId)) return false;
            state.MarkInteriorActionExecuted(InteriorActionKind.UpgradeUnitType);
            return state.UpgradeUnitType(unitTypeId);
        }

        // ══════════════════════════════════════════════
        // 拠点強化
        // ══════════════════════════════════════════════

        /// <summary>
        /// 指定戦線の拠点強化が可能か。
        /// 戦線ごとの Lv 上限を超えると不可（平原1・街2・砦3）。
        /// </summary>
        public bool CanUpgradeBase(Stage3CampaignState state, Battlefront target)
        {
            if (state == null || target == null) return false;
            if (!state.CanExecuteInteriorAction(InteriorActionKind.UpgradeBase)) return false;
            if (target.BaseLevel >= target.MaxBaseLevel) return false;
            return true;
        }

        /// <summary>
        /// 拠点強化を実行する。Lv が +1 され、効果（PDEF/MDEF/ATK バフ）は戦闘開始時に適用される（§10.9）。
        /// 旧仕様の Lv3 自動偵察は §10.8 偵察コマンドの全戦線化に伴い廃止。
        /// </summary>
        public bool ExecuteUpgradeBase(Stage3CampaignState state, Battlefront target)
        {
            if (!CanUpgradeBase(state, target)) return false;
            state.MarkInteriorActionExecuted(InteriorActionKind.UpgradeBase);
            target.BaseLevel++;
            return true;
        }

        // ══════════════════════════════════════════════
        // 招集
        // ══════════════════════════════════════════════

        /// <summary>招集（追加3択ドラフト）が可能か。</summary>
        public bool CanConscript(Stage3CampaignState state)
        {
            if (state == null) return false;
            return state.CanExecuteInteriorAction(InteriorActionKind.Conscript);
        }

        /// <summary>
        /// 招集を実行し、3択ドラフトを返す。プレイヤーは返された DraftOffer から
        /// AcceptPick で1体を選ぶ流れ。
        /// 行動力と同一アクション禁止チェックを満たさない場合は null を返す。
        /// </summary>
        public DraftOffer ExecuteConscript(Stage3CampaignState state)
        {
            if (!CanConscript(state)) return null;
            state.MarkInteriorActionExecuted(InteriorActionKind.Conscript);
            return _draftService.DrawConscriptPick(state);
        }
    }
}
