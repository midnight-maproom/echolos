// 試遊モード本実装。セーブを保持するだけのシンプルな状態コンテナ。
//
// 起動経路：試遊シーンのタイトル画面ボタンから VSPrototypeBootstrap.StartDemoMode(saveId) 経由で
// 本クラスをインスタンス化し、_demo に差し替える。LoadSave(saveId) でセーブをロード。
namespace Echolos.UseCase.Demo
{
    public sealed class DemoFlowController : IDemoFlowController
    {
        public bool IsActive => true;
        public DemoSaveDefinition CurrentSave { get; private set; }

        public void LoadSave(string saveId)
        {
            CurrentSave = DemoSaveCatalog.Get(saveId);
        }
    }
}
