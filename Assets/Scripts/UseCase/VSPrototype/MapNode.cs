// 領地マップ上のマス1つを表すドメインモデル（純C#・UnityEngine 非依存）。
// 3列×3層（自領／敵領／敵拠点）＋本拠地1つで構成される領地マップの最小単位。
//
// 【設計方針】
// - 状態変化は API メソッド経由のみ。フィールドは private で外部に List を露出させない。
//   - IsCaptured / IsFallen は Capture() / MarkFallen() からのみ変更
//   - EnemyComposition / AssignedAllies は読み取り専用 view（IReadOnlyList<T>）として公開し、
//     変更は AssignAlly / UnassignAlly / SetEnemyComposition 等の API 経由のみ
//   - 変更時には OnAllyAssigned / OnAllyUnassigned / OnEnemyCompositionChanged を発火
//   これにより、ロジック層の状態変化と UI 層への通知漏れを構造的に防ぐ。
// - Kind に応じて許可される操作が異なる（敵領／敵拠点だけが Capture 可、自領だけが MarkFallen 可）。
//   不正操作は InvalidOperationException で早期検知する。
using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>マスの種別。</summary>
    public enum MapNodeKind
    {
        /// <summary>本拠地（王宮）。プレイヤーの最後の砦・R7 ボス戦の舞台。</summary>
        Home,
        /// <summary>自領。プレイヤー初期支配。陥落すると本拠地に直接侵攻が発生。</summary>
        Friendly,
        /// <summary>敵領。プレイヤーが攻略すると制圧可能（敵の中強度編成）。</summary>
        EnemyTerritory,
        /// <summary>敵拠点。最も敵が強い。バルドゥイン拠点は左列の敵拠点固定。</summary>
        EnemyStronghold,
    }

    /// <summary>
    /// このラウンドでマスがどう振る舞うかを明示する一級概念。
    /// <see cref="MapNodeKind"/>（不変属性）と直交する動的属性で、ラウンドごとに変わる。
    /// StartRound でセットし、ResolveAllBattles のスキップ判定／ApplyNodeOutcome の状態遷移分岐に使う。
    /// 「敵不在マスが自動勝利→Capture」のような暗黙副作用を防ぐため、戦線外マスは <see cref="None"/> で
    /// 明示的に「戦闘予定なし＋状態遷移なし」を表現する。
    /// </summary>
    public enum MapNodeBattleMode
    {
        /// <summary>戦線外。戦闘予定なし・状態遷移なし（列終了・敵領占領で接していない自領等）。</summary>
        None,
        /// <summary>自陣最前線。敵が来る場所＝防衛戦／奪還戦／取り戻し戦。</summary>
        Defense,
        /// <summary>敵陣最前線。プレイヤーが攻める場所＝攻め込み戦。</summary>
        Attack,
        /// <summary>R7 本拠地ボス戦。</summary>
        Boss,
    }

    /// <summary>領地マップ1マス分のドメインモデル。</summary>
    public sealed class MapNode
    {
        /// <summary>1マスに配置可能な味方ユニットの上限（前列3＋後列3）。</summary>
        public const int MaxAlliedSlots = 6;

        private readonly List<RuntimeUnit> _enemyComposition = new List<RuntimeUnit>();
        private readonly List<RuntimeUnit> _assignedAllies   = new List<RuntimeUnit>();

        public MapNode(MapNodeKind kind, int col, int layer, bool isBalduinStronghold = false)
        {
            Kind = kind;
            Col = col;
            Layer = layer;
            IsBalduinStronghold = isBalduinStronghold;
        }

        // 不変属性

        /// <summary>マスの種別。</summary>
        public MapNodeKind Kind { get; }

        /// <summary>列インデックス（0=左, 1=中, 2=右。Home は 1 固定）。</summary>
        public int Col { get; }

        /// <summary>層インデックス（0=Home, 1=自領, 2=敵領, 3=敵拠点）。</summary>
        public int Layer { get; }

        /// <summary>バルドゥイン拠点フラグ（左列の敵拠点のみ true）。
        /// 例外的に B-b2 バルドゥイン降伏演出完了時に <see cref="MarkBalduinStrongholdCleared"/> で
        /// false 化される（330 §3.3）。それ以外は不変。</summary>
        public bool IsBalduinStronghold { get; private set; }

        // 状態フラグ

        /// <summary>プレイヤーが制圧済みか（敵領/敵拠点のみ意味を持つ）。</summary>
        public bool IsCaptured { get; private set; }

        /// <summary>自領が陥落したか（自領のみ意味を持つ。陥落すると本拠地侵攻発生）。</summary>
        public bool IsFallen { get; private set; }

        /// <summary>
        /// このラウンドの戦闘モード（StartRound でセット・ResetForNewRound で None にリセット）。
        /// ResolveAllBattles は <see cref="MapNodeBattleMode.None"/> のマスをスキップする。
        /// </summary>
        public MapNodeBattleMode BattleMode { get; private set; } = MapNodeBattleMode.None;

        // ラウンド単位リスト（読み取り専用 view ＋ API 経由変更）

        /// <summary>このラウンドの敵編成（読み取り専用 view）。変更は SetEnemyComposition / ClearEnemyComposition 経由。</summary>
        public IReadOnlyList<RuntimeUnit> EnemyComposition => _enemyComposition;

        /// <summary>このラウンドにプレイヤーが配置した味方（読み取り専用 view）。変更は AssignAlly / UnassignAlly / ClearAllies 経由。</summary>
        public IReadOnlyList<RuntimeUnit> AssignedAllies => _assignedAllies;

        // 通知イベント

        /// <summary>配置が追加された時に発火（GUI/演出層の連動用）。</summary>
        public event Action<MapNode, RuntimeUnit> OnAllyAssigned;

        /// <summary>配置が解除された時に発火（ClearAllies 経由の一括解除でも各要素ごとに発火）。</summary>
        public event Action<MapNode, RuntimeUnit> OnAllyUnassigned;

        /// <summary>敵編成が変化した時に発火（Set / Clear いずれでも1回）。</summary>
        public event Action<MapNode> OnEnemyCompositionChanged;

        /// <summary>制圧が取り戻された時に発火（占領済み敵領／敵拠点の奪還戦敗北時）。</summary>
        public event Action<MapNode> OnCaptureReverted;

        /// <summary>陥落自領の取り戻しが成功した時に発火。</summary>
        public event Action<MapNode> OnFallenReverted;

        // 状態遷移 API

        /// <summary>
        /// このマスを「制圧済」にする（敵領／敵拠点のみ有効）。
        /// 戦線概念：制圧した敵領／敵拠点は「自陣の最前線」になる。
        /// 同列の敵拠点も制圧されれば列の戦線が終了して以降敵が湧かない。
        /// 敵領のみ制圧の状態では、次ラウンド以降「奪還戦」の対象になる。
        /// 冪等。
        /// </summary>
        public void Capture()
        {
            if (Kind != MapNodeKind.EnemyTerritory && Kind != MapNodeKind.EnemyStronghold)
                throw new InvalidOperationException(
                    $"Capture は敵領/敵拠点のみ有効 (Kind={Kind})");
            IsCaptured = true;
        }

        /// <summary>
        /// 制圧を取り戻された状態に戻す（敵領／敵拠点のみ有効）。
        /// 奪還戦で敗北 or 占領済みマスに未配置で自動取り戻されたケースで呼ばれる。
        /// IsCaptured が既に false なら何もしない（冪等）。OnCaptureReverted を発火。
        /// </summary>
        public void RevertCapture()
        {
            if (Kind != MapNodeKind.EnemyTerritory && Kind != MapNodeKind.EnemyStronghold)
                throw new InvalidOperationException(
                    $"RevertCapture は敵領/敵拠点のみ有効 (Kind={Kind})");
            if (!IsCaptured) return;
            IsCaptured = false;
            OnCaptureReverted?.Invoke(this);
        }

        /// <summary>
        /// この自領を「陥落」にする（自領のみ有効）。
        /// 陥落自領は次ラウンド以降「取り戻し戦」の対象（敵側に占領された状態として扱う）。
        /// ラウンド末に IsFallen 自領が 1 つ以上残っていれば本拠地戦が 1 回発生する。冪等。
        /// </summary>
        public void MarkFallen()
        {
            if (Kind != MapNodeKind.Friendly)
                throw new InvalidOperationException(
                    $"MarkFallen は自領のみ有効 (Kind={Kind})");
            IsFallen = true;
        }

        /// <summary>
        /// 陥落自領の取り戻しが成功した状態に戻す（自領のみ有効）。
        /// 取り戻し戦で勝利したケースで呼ばれる。
        /// IsFallen が既に false なら何もしない（冪等）。OnFallenReverted を発火。
        /// </summary>
        public void RevertFallen()
        {
            if (Kind != MapNodeKind.Friendly)
                throw new InvalidOperationException(
                    $"RevertFallen は自領のみ有効 (Kind={Kind})");
            if (!IsFallen) return;
            IsFallen = false;
            OnFallenReverted?.Invoke(this);
        }

        /// <summary>
        /// バルドゥイン降伏（B-b2 発火完了）後に呼ばれ、左列敵拠点を「通常敵拠点」扱いに転落させる。
        /// 以降このマスを制圧してもブリジット加入トリガしない＋マップ表示も通常敵拠点扱い。
        /// 冪等：既に false なら何もしない。
        /// </summary>
        public void MarkBalduinStrongholdCleared()
        {
            IsBalduinStronghold = false;
        }

        // 配置 API

        /// <summary>
        /// 味方ユニットをこのマスに配置する。
        /// 上限（<see cref="MaxAlliedSlots"/>）を超える追加は InvalidOperationException。
        /// </summary>
        public void AssignAlly(RuntimeUnit ally)
        {
            if (ally == null) throw new ArgumentNullException(nameof(ally));
            if (_assignedAllies.Count >= MaxAlliedSlots)
                throw new InvalidOperationException(
                    $"配置上限を超えています ({MaxAlliedSlots})");
            _assignedAllies.Add(ally);
            OnAllyAssigned?.Invoke(this, ally);
        }

        /// <summary>
        /// 指定の味方ユニットを配置から外す。外せたら true、見つからなければ false。
        /// </summary>
        public bool UnassignAlly(RuntimeUnit ally)
        {
            if (ally == null) return false;
            if (_assignedAllies.Remove(ally))
            {
                OnAllyUnassigned?.Invoke(this, ally);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 配置中の味方を全て解除する。各要素について OnAllyUnassigned が発火する。
        /// </summary>
        public void ClearAllies()
        {
            if (_assignedAllies.Count == 0) return;
            // イベント発火中に外部から変更される可能性に備え、スナップショットを取ってから Clear する
            var snapshot = _assignedAllies.ToArray();
            _assignedAllies.Clear();
            for (int i = 0; i < snapshot.Length; i++)
                OnAllyUnassigned?.Invoke(this, snapshot[i]);
        }

        // 敵編成 API

        /// <summary>
        /// 敵編成を入れ替える（既存をクリアして新規をセット）。
        /// null を渡した場合は空編成扱い。OnEnemyCompositionChanged を1回発火。
        /// </summary>
        public void SetEnemyComposition(IEnumerable<RuntimeUnit> enemies)
        {
            _enemyComposition.Clear();
            if (enemies != null)
            {
                foreach (var e in enemies)
                    if (e != null) _enemyComposition.Add(e);
            }
            OnEnemyCompositionChanged?.Invoke(this);
        }

        /// <summary>敵編成を空にする。既に空なら何もしない（イベントも発火しない）。</summary>
        public void ClearEnemyComposition()
        {
            if (_enemyComposition.Count == 0) return;
            _enemyComposition.Clear();
            OnEnemyCompositionChanged?.Invoke(this);
        }

        // 戦闘モード API

        /// <summary>このラウンドの戦闘モードをセットする（StartRound から呼ぶ）。</summary>
        public void SetBattleMode(MapNodeBattleMode mode)
        {
            BattleMode = mode;
        }

        // ラウンド単位リセット

        /// <summary>
        /// ラウンド開始時に呼ばれるラウンド単位状態のリセット。
        /// 敵編成・配置・戦闘モードをクリアする（API 経由なのでイベントも発火）。
        /// Capture / Fallen 等の永続フラグは保持される。
        /// </summary>
        public void ResetForNewRound()
        {
            ClearEnemyComposition();
            ClearAllies();
            BattleMode = MapNodeBattleMode.None;
        }
    }
}
