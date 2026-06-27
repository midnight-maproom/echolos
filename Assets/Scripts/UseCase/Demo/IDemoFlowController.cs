// 試遊モード（DemoMode）進行制御の抽象。
//
// 通常版の本体ロジック（Bootstrap / RoundManager / 内政サービス等）はこの抽象に問い合わせるだけで、
// デモ実装の存在を意識しない。通常モードでは NullDemoFlowController が注入され、すべての問い合わせに
// 「介入しない」（false / DemoForcedOutcome.None / null）を返す。
//
// メソッド追加方針：問い合わせメソッドは Phase 3 以降で本体側に問い合わせ点を設置する直前に拡張する。
// 死蔵防止のため、ここに追加したメソッドは必ず同 Phase 内で本体から呼ばれる。
namespace Echolos.UseCase.Demo
{
    public interface IDemoFlowController
    {
        bool IsActive { get; }

        /// <summary>現在ロード中のセーブ。未ロード時は null。</summary>
        DemoSaveDefinition CurrentSave { get; }

        /// <summary>現在ロード中の進行シナリオ。未ロード時は null。</summary>
        DemoScenarioDefinition CurrentScenario { get; }

        /// <summary>指定シナリオ ID とそれに紐づくセーブをロードする（試遊シーン起動時に呼ばれる）。</summary>
        void LoadScenario(string scenarioId);

        /// <summary>指定ラウンドで内政フェーズ（ドラフト／強化／自動加入）を丸ごとスキップすべきか。</summary>
        bool ShouldSkipInteriorPhase(int round);

        /// <summary>指定マスの戦闘を自動勝利で解決すべきか（手動指定マスは false）。</summary>
        bool ShouldAutoResolveBattle(int round, int col, int layer);

        /// <summary>指定ラウンドで画面下部の目的バーに表示するテキスト。null/空なら非表示。</summary>
        string GetObjectiveText(int round);
    }
}
