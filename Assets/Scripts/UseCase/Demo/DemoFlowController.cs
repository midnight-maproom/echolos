// 試遊モード本実装。シナリオとセーブを保持し、本体からの問い合わせに対しシナリオに従って応答する。
//
// 起動経路：試遊シーンの初期化スクリプトが本クラスをインスタンス化し、Bootstrap._demo に差し替える。
// その後 LoadScenario(scenarioId) でシナリオとセーブを同時ロード。
namespace Echolos.UseCase.Demo
{
    public sealed class DemoFlowController : IDemoFlowController
    {
        public bool IsActive => true;
        public DemoSaveDefinition CurrentSave { get; private set; }
        public DemoScenarioDefinition CurrentScenario { get; private set; }

        public void LoadScenario(string scenarioId)
        {
            CurrentScenario = DemoScenarioCatalog.Get(scenarioId);
            CurrentSave = DemoSaveCatalog.Get(CurrentScenario.StartingSaveId);
        }

        public bool ShouldSkipInteriorPhase(int round)
        {
            var rule = GetRule(round);
            return rule != null && rule.SkipInteriorPhase;
        }

        public bool ShouldAutoResolveBattle(int round, int col, int layer)
        {
            var rule = GetRule(round);
            if (rule == null || !rule.AutoResolveAllBattles) return false;
            // 手動指定マスは自動勝利の例外として除外
            if (rule.ManualBattleNode != null && rule.ManualBattleNode.Matches(col, layer))
                return false;
            return true;
        }

        public string GetObjectiveText(int round)
        {
            var rule = GetRule(round);
            return rule != null ? rule.ObjectiveText : null;
        }

        private DemoRoundRule GetRule(int round)
        {
            if (CurrentScenario == null) return null;
            return CurrentScenario.RoundRules.TryGetValue(round, out var rule) ? rule : null;
        }
    }
}
