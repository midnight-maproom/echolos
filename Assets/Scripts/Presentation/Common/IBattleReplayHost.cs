// 戦闘再生 UI（VSPrototypeBattleGUI）が依存する「再生ホスト」抽象。
//
// VSプロト本シーン（VSPrototypeBootstrap）と Debug シーン（DebugBattleSandboxBootstrap）で
// 戦闘再生 GUI を共有するための薄い抽象。BattleGUI 側はこの interface だけを参照し、
// 本番シーン／Debug シーンのどちらに置かれても同じ描画ロジックで動く。
//
// 同 GameObject 上の MonoBehaviour で本 interface を実装するものを 1 つだけ配置する運用。
// BattleGUI.Awake で `GetComponents<MonoBehaviour>()` から最初の実装を取り出す
// （[RequireComponent] は C# interface に対応しないためこの形）。
using System.Collections.Generic;
using Echolos.Presentation.VSPrototype;

namespace Echolos.Presentation.Common
{
    /// <summary>
    /// 戦闘再生キューを提供するホスト。VSPrototypeBattleGUI が描画対象とする。
    /// </summary>
    public interface IBattleReplayHost
    {
        /// <summary>戦闘再生が現在アクティブか（描画 ON/OFF 判定）。</summary>
        bool IsActive { get; }

        /// <summary>再生キュー全体（IsActive=true の間のみ非 null/非空）。</summary>
        IReadOnlyList<VSPrototypeBattleSegment> Segments { get; }

        /// <summary>現在再生中のセグメント index（0-based）。</summary>
        int CurrentIndex { get; }

        /// <summary>現在再生中のセグメント（範囲外なら null）。</summary>
        VSPrototypeBattleSegment CurrentSegment { get; }

        /// <summary>
        /// ヘッダに表示する進捗ラベル。
        /// VSプロト本番：「R3/7」のようなラウンド表示／Debug：「Debug Sandbox」など固定文言。
        /// </summary>
        string HeaderProgressLabel { get; }

        /// <summary>
        /// 次のセグメントへ進める。次があれば true、末尾なら終了処理（FinishAll 相当）して false。
        /// </summary>
        bool AdvanceToNext();

        /// <summary>全戦闘の再生を中止して呼び出し元（VSプロト本番なら Phase=Run／Debug なら配置画面）へ戻る。</summary>
        void FinishAll();
    }
}
