// 通常モード（試遊モード非適用）向け no-op 実装。
// IsActive = false 固定で、本体ロジックはすべての問い合わせを「介入しない」として扱える。
namespace Echolos.UseCase.Demo
{
    public sealed class NullDemoFlowController : IDemoFlowController
    {
        public bool IsActive => false;
        public DemoSaveDefinition CurrentSave => null;

        public void LoadSave(string saveId) { /* no-op */ }
    }
}
