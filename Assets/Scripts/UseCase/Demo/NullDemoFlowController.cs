// 通常モード（試遊モード非適用）向け no-op 実装。
// IsActive = false 固定で、本体ロジックはすべての問い合わせを「介入しない」として扱える。
namespace Echolos.UseCase.Demo
{
    public sealed class NullDemoFlowController : IDemoFlowController
    {
        public bool IsActive => false;
        public DemoSaveDefinition CurrentSave => null;
        public DemoScenarioDefinition CurrentScenario => null;

        public void LoadScenario(string scenarioId) { /* no-op */ }
        public bool ShouldSkipInteriorPhase(int round) => false;
        public bool ShouldAutoResolveBattle(int round, int col, int layer) => false;
        public string GetObjectiveText(int round) => null;
    }
}
