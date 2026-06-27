// 内政フェーズと初期ドラフトの状態を保持するモデル。
//
// 【設計方針】
// - 行動力・実行済アクション集合・初期ドラフト残数を集約。
// - ユニット個別 Lv は Unit.Level（敵味方共通軸）が SSoT なので State には保持しない。
// - 状態変化はメソッド経由（イベント駆動原則）。
using System;
using System.Collections.Generic;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロト 内政・初期ドラフトの状態（純Core・MonoBehaviour非依存）。</summary>
    public sealed class VSPrototypeInteriorState
    {
        /// <summary>1コマンドあたりの行動力コスト（固定 1）。</summary>
        public const int ActionCost = 1;

        /// <summary>ユニットの Lv 上限（Lv1〜Lv3・強化 2 回まで）。</summary>
        public const int MaxUnitLevel = 3;

        /// <summary>初期ドラフトの基本回数（固定 5・王女＋ブリジット任意含めず）。</summary>
        public const int BaseInitialDraftCount = 5;

        private readonly HashSet<VSPrototypeInteriorAction> _executedActions
            = new HashSet<VSPrototypeInteriorAction>();

        /// <summary>このラウンドで残っている行動力。</summary>
        public int ActionPoints { get; private set; }

        /// <summary>ラン開始時に決定された行動力上限（メタ強化「行動力 +1」反映後）。</summary>
        public int ActionPointsPerRound { get; private set; }

        /// <summary>初期ドラフトの残回数（ラン開始時に決定・Phase=InitialDraft で消費）。</summary>
        public int InitialDraftRemaining { get; private set; }

        /// <summary>このラウンドで実行済みのアクション（読み取り専用 view）。</summary>
        public IReadOnlyCollection<VSPrototypeInteriorAction> ExecutedActions => _executedActions;

        /// <summary>
        /// ラン開始時の初期化。
        /// 行動力上限／初期ドラフト回数を確定する。
        /// メタ強化「行動力 +1」「初期所持ユニット +1」のレベル値は呼び出し側が加算済みの値を渡す。
        /// </summary>
        public void InitializeForRun(int actionPointsPerRound, int initialDraftCount)
        {
            if (actionPointsPerRound < 1)
                throw new ArgumentOutOfRangeException(nameof(actionPointsPerRound));
            if (initialDraftCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialDraftCount));

            ActionPointsPerRound = actionPointsPerRound;
            InitialDraftRemaining = initialDraftCount;
            ActionPoints = 0; // R1 開始時の ResetForNewRound で確定する
            _executedActions.Clear();
        }

        /// <summary>
        /// ラウンド開始時のリセット：行動力を上限まで回復し、実行済アクションをクリアする。
        /// </summary>
        public void ResetForNewRound()
        {
            ActionPoints = ActionPointsPerRound;
            _executedActions.Clear();
        }

        // 初期ドラフト

        /// <summary>初期ドラフトが残っているか。</summary>
        public bool HasInitialDraftRemaining => InitialDraftRemaining > 0;

        /// <summary>初期ドラフトを1回消費する（Phase=InitialDraft 中の選択時に呼ぶ）。</summary>
        public void ConsumeInitialDraft()
        {
            if (InitialDraftRemaining <= 0)
                throw new InvalidOperationException("初期ドラフト残数がゼロです");
            InitialDraftRemaining--;
        }

        // 行動力・アクション履歴

        /// <summary>同一ラウンドで指定アクションが既に実行済みか。</summary>
        public bool HasExecutedThisRound(VSPrototypeInteriorAction action)
        {
            return _executedActions.Contains(action);
        }

        /// <summary>
        /// アクションを実行可能か（行動力残＋同一未実行）チェック。副作用なし。
        /// </summary>
        public bool CanExecuteAction(VSPrototypeInteriorAction action)
        {
            if (ActionPoints < ActionCost) return false;
            if (HasExecutedThisRound(action)) return false;
            return true;
        }

        /// <summary>
        /// アクション実行のマーク：行動力を消費して実行済集合に追加する。
        /// 戻り値：成功＝true、不可＝false（行動力不足 or 同一既実行）。
        /// </summary>
        public bool MarkActionExecuted(VSPrototypeInteriorAction action)
        {
            if (!CanExecuteAction(action)) return false;
            ActionPoints -= ActionCost;
            _executedActions.Add(action);
            return true;
        }

        /// <summary>
        /// 行動力だけ消費する（実行履歴に記録しない・同一ラウンド複数回可能なアクション用）。
        /// 戻り値：成功＝true、不可＝false（行動力不足）。
        /// </summary>
        public bool TryConsumeActionPoint()
        {
            if (ActionPoints < ActionCost) return false;
            ActionPoints -= ActionCost;
            return true;
        }
    }
}
