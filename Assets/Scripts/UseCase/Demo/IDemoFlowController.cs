// 試遊モード（DemoMode）進行制御の抽象。
//
// 通常版の本体ロジック（Bootstrap 等）はこの抽象に問い合わせるだけで、デモ実装の存在を意識しない。
// 通常モードでは NullDemoFlowController が注入され、IsActive=false／CurrentSave=null を返す。
//
// 試遊版は「R4 開始・通常進行・救出戦体験」の 1 セーブのみで進行ルール介入はゼロ。
// IsActive はメタ進行非保存判定／救出ピーク後タイトル戻り判定／ラン終了後タイトル戻り判定で参照される。
namespace Echolos.UseCase.Demo
{
    public interface IDemoFlowController
    {
        bool IsActive { get; }

        /// <summary>現在ロード中のセーブ。未ロード時は null。</summary>
        DemoSaveDefinition CurrentSave { get; }

        /// <summary>指定セーブをロードする（試遊シーン起動時に呼ばれる）。</summary>
        void LoadSave(string saveId);
    }
}
