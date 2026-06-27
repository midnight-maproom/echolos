// Assets/Scripts/UnityView/DebugBattleStarter.cs
// Step 6.1: デバッグログ出力（APIの疎通確認）
// Step 6.2: 静的UIとプレハブ化（参照系Viewの作成）
// Step 6.3: イベントの配線（Pub/Subパターンの統合）
//
// 変更点（Step 6.3）:
//   - OnStartBattle() の冒頭で Clear() を呼び、再実行時の残骸カードを除去
//   - actionExecutor.OnHitLanded に UI更新（UpdateHp / UpdateShield）を登録
//   - actionExecutor.OnUnitDied  に UI更新（SetDead）を登録
//   - バトルループをコルーチン RunBattleCoroutine() に移行し、
//     ターン終了ごとに WaitForSeconds(_turnInterval) で1フレーム以上待機させることで
//     ターン単位のUI変化を可視化する
//   - FindCard() ヘルパーで味方・敵どちらのグリッドも横断検索可能にする

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;
using Echolos.Domain.Prototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>
    /// デバッグ用バトル起動コントローラ。
    /// 「バトル開始」ボタンを押すとダミーデータでバトルを走らせ、
    /// 結果をUIテキストとUnityコンソールの両方に出力する。
    ///
    /// Step 6.3 以降はターン単位でUIカードのHP・シールド・死亡状態を更新する。
    /// </summary>
    public class DebugBattleStarter : MonoBehaviour
    {
        // ─────────────────────────────────
        // Inspector から紐付けるUI参照
        // ─────────────────────────────────

        [SerializeField]
        [Tooltip("押下するとバトルが開始されるボタン")]
        private Button _startButton;

        [SerializeField]
        [Tooltip("バトルログを表示する ScrollView 内の Text コンポーネント")]
        private Text _logText;

        [SerializeField]
        [Tooltip("ログエリアの ScrollRect コンポーネント（Scroll View オブジェクトにある）")]
        private ScrollRect _scrollRect;

        [Header("Step 6.2: バトルグリッドView")]

        [SerializeField]
        [Tooltip("味方陣営の2×3グリッドを管理する BattleGridView コンポーネント")]
        private BattleGridView _allyGridView;

        [SerializeField]
        [Tooltip("敵陣営の2×3グリッドを管理する BattleGridView コンポーネント")]
        private BattleGridView _enemyGridView;

        [Header("Step 6.3: バトル進行速度")]

        [SerializeField]
        [Tooltip("各ターン終了後の待機時間（秒）。大きくするとターンごとの変化がわかりやすい。\n" +
                 "0 にすると全ターンが1フレームで完走するが、UI更新は最終状態のみ反映される。")]
        private float _turnInterval = 0.8f;

        // ─────────────────────────────────
        // 内部状態
        // ─────────────────────────────────

        /// <summary>バトルログを蓄積する StringBuilder</summary>
        private StringBuilder _log;

        /// <summary>現在実行中のバトルコルーチン（重複起動防止に使用）</summary>
        private Coroutine _battleCoroutine;

        /// <summary>味方の編成（配置はSlotIndexに保持。HP等は戦闘ごとにリセット）</summary>
        private List<RuntimeUnit> _allyParty;

        /// <summary>敵の編成</summary>
        private List<RuntimeUnit> _enemyParty;

        /// <summary>バトル実行中フラグ（実行中は配置スワップを受け付けない）</summary>
        private bool _isBattleRunning;

        /// <summary>配置スワップで選択中の味方カード（未選択ならnull）</summary>
        private UnitCardView _selectedCard;

        // ─────────────────────────────────
        // Unity ライフサイクル
        // ─────────────────────────────────

        private void Start()
        {
            // ボタンのクリックイベントにメソッドを登録する
            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartButtonPressed);
            else
                Debug.LogError("[DebugBattleStarter] _startButton が未設定です。Inspector で紐付けてください。");

            if (_logText == null)
                Debug.LogError("[DebugBattleStarter] _logText が未設定です。Inspector で紐付けてください。");

            if (_scrollRect == null)
                Debug.LogWarning("[DebugBattleStarter] _scrollRect が未設定です。スクロールが最下部に移動しません。");

            // 編成を一度だけ構築する。配置（SlotIndex）はこのインスタンスに保持され、
            // HP等の戦闘状態は各バトル開始時にリセットされる。
            _allyParty  = PrototypeRoster.BuildAllyParty();
            _enemyParty = PrototypeRoster.BuildEnemyParty();

            _log = new StringBuilder();
            Log("【配置編成】味方カードを2枚クリックすると配置を入れ替えられます。"
              + "配置を決めたら「バトル開始」を押してください。");
            if (_logText != null) _logText.text = _log.ToString();

            EnterArrangeMode();
        }

        // ──────────────────────────────────────────────────────────
        // 配置編成モード
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// 配置編成モードに入る。全ユニットを全快に戻してグリッドを再表示し、
        /// 味方カードのクリック（配置スワップ）を有効化する。
        /// </summary>
        private void EnterArrangeMode()
        {
            _isBattleRunning = false;
            _selectedCard = null;
            ResetAllUnits();
            RebuildGrids();
        }

        /// <summary>味方・敵の全ユニットを全快状態にリセットする（配置=SlotIndexは維持）。</summary>
        private void ResetAllUnits()
        {
            foreach (var u in _allyParty)  PrototypeRoster.ResetForBattle(u);
            foreach (var u in _enemyParty) PrototypeRoster.ResetForBattle(u);
        }

        /// <summary>両グリッドを現在の編成で作り直し、味方カードにクリックを配線する。</summary>
        private void RebuildGrids()
        {
            if (_allyGridView != null)
            {
                _allyGridView.Clear();
                _allyGridView.Initialize(_allyParty);
            }
            else
            {
                Debug.LogWarning("[DebugBattleStarter] _allyGridView が未設定です。");
            }

            if (_enemyGridView != null)
            {
                _enemyGridView.Clear();
                _enemyGridView.Initialize(_enemyParty);
            }
            else
            {
                Debug.LogWarning("[DebugBattleStarter] _enemyGridView が未設定です。");
            }

            WireAllyCardClicks();
        }

        /// <summary>味方カードのクリックイベントを配置スワップ処理に配線する。</summary>
        private void WireAllyCardClicks()
        {
            if (_allyGridView == null) return;
            foreach (var unit in _allyParty)
            {
                var card = _allyGridView.GetCard(unit);
                if (card != null) card.Clicked += OnAllyCardClicked;
            }
        }

        /// <summary>
        /// 味方カードがクリックされたときの処理（配置編成モードのみ有効）。
        /// 1枚目で選択、2枚目で配置スワップ、同じカード再クリックで選択解除。
        /// </summary>
        private void OnAllyCardClicked(UnitCardView card)
        {
            if (_isBattleRunning || card == null) return;

            if (_selectedCard == null)
            {
                _selectedCard = card;
                card.SetSelected(true);
                return;
            }

            if (_selectedCard == card)
            {
                card.SetSelected(false);
                _selectedCard = null;
                return;
            }

            // 2体のスロットを入れ替える
            var a = _selectedCard.Unit;
            var b = card.Unit;
            if (a != null && b != null)
            {
                int tmp = a.SlotIndex;
                a.SlotIndex = b.SlotIndex;
                b.SlotIndex = tmp;
            }

            _selectedCard.SetSelected(false);
            _selectedCard = null;

            // 新しい配置で再表示（全快状態で編成を続行できる）
            EnterArrangeMode();
        }

        // ──────────────────────────────────────────────────────────
        // バトル開始（「バトル開始」ボタンから呼ばれる）
        // ──────────────────────────────────────────────────────────

        /// <summary>ボタン押下。バトル中でなければ現在の配置でバトルを開始する。</summary>
        public void OnStartButtonPressed()
        {
            if (_isBattleRunning) return;
            StartBattle();
        }

        /// <summary>現在の配置・全快状態でバトルを開始する。</summary>
        private void StartBattle()
        {
            // 実行中のバトルコルーチンを停止する（連打対策）
            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }

            _isBattleRunning = true;
            if (_selectedCard != null) { _selectedCard.SetSelected(false); _selectedCard = null; }

            // 全快にリセットし、現在の配置でグリッドを作り直す
            ResetAllUnits();
            RebuildGrids();

            _log = new StringBuilder();
            if (_logText != null) _logText.text = "";

            // ── BattleContext の組み立て（保持中の編成インスタンスを使用） ──
            var context = new BattleContext(maxTurnLimit: 20);
            context.AllyUnits.AddRange(_allyParty);
            context.EnemyUnits.AddRange(_enemyParty);

            // ── Coreシステムのインスタンス生成 ───────────────────

            var targetEvaluator = new TargetEvaluator();
            var battleManager   = new BattleManager(targetEvaluator);
            var actionExecutor  = new ActionExecutor();

            // ── 5. イベントの登録（ログ出力） ───────────────────────

            // フェーズ変化ログ
            battleManager.OnPhaseChanged += phase =>
                Log($"[フェーズ] {PhaseToJapanese(phase)}");

            // ターン開始ログ
            battleManager.OnStartPhase += ctx =>
                Log($"\n========== ターン {ctx.CurrentTurn} 開始 ==========");

            // 行動宣言ログ
            battleManager.OnActionExecuting += (ctx, declaration) =>
            {
                string targetNames = declaration.Targets.Count > 0
                    ? string.Join(", ", declaration.Targets.Select(t => t.BaseUnit.Name))
                    : "なし";
                string wazaLabel = declaration.IsNormalAttackFallback
                    ? "通常攻撃"
                    : $"「{declaration.DeclaredWaza?.Name ?? "不明"}」";
                Log($"  {declaration.Actor.BaseUnit.Name} が {wazaLabel} を {targetNames} に使用！");
            };

            // ActionExecutor をダメージ処理本体として登録
            battleManager.OnActionExecuting += actionExecutor.ExecuteAction;

            // 行動スキップログ
            battleManager.OnActionSkipped += (ctx, unit) =>
                Log($"  {unit.BaseUnit.Name} は行動をスキップした（麻痺 / 凍結 / 待機）");

            // ターン終了ログ
            battleManager.OnTurnEnd += ctx =>
                Log($"---------- ターン {ctx.CurrentTurn - 1} 終了 ----------");

            // バトル終了ログ
            battleManager.OnBattleEnded += (ctx, result) =>
            {
                Log($"\n===== バトル終了: {ResultToJapanese(result)} =====");
                Log($"  撃破した敵数: {ctx.AllyKillCount}  撃破された味方数: {ctx.AllyDeathCount}");
            };

            // ── 5.5 イベントの登録（Step 6.3: UI更新） ──────────────
            //
            // 【設計方針】
            //   Core側のイベントはPOCOクラスが発火するため、Unity依存を含まない。
            //   View側（このクラス）がイベントを購読し、UnitCardView のAPIを呼び出す。
            //   データフローは Core → View の一方向を厳守する。

            // ヒット命中時: ログ出力 + UIカードのHP・シールドを更新
            actionExecutor.OnHitLanded += (ctx, attacker, target, damage, element) =>
            {
                // ログ出力（Step 6.1 から継続）
                string elementLabel = element != Element.None ? $" [{element}]" : "";
                Log($"    → {target.BaseUnit.Name} に {damage} ダメージ{elementLabel}"
                  + $"  （残HP: {target.CurrentHP} / {target.MaxHP}）");

                // Step 6.3: 被ダメージユニットのカードを更新する
                UnitCardView card = FindCard(target);
                if (card != null)
                {
                    card.UpdateHp(target.CurrentHP, target.MaxHP);
                    card.UpdateShield(target.CurrentShield);
                }
            };

            // 回避ログ（UIへの変化なし）
            actionExecutor.OnHitEvaded += (ctx, attacker, evader) =>
                Log($"    → {evader.BaseUnit.Name} が攻撃を回避！");

            // 死亡時: ログ出力 + UIカードをグレーアウト
            actionExecutor.OnUnitDied += (ctx, unit) =>
            {
                // ログ出力（Step 6.1 から継続）
                Log($"  ★ {unit.BaseUnit.Name} が倒された！");

                // Step 6.3: 死亡ユニットのカードを暗転させる
                UnitCardView card = FindCard(unit);
                card?.SetDead();
            };

            // ── 6. バトル初期化 ────────────────────────────────────

            battleManager.InitializeBattle(context, allyLeaderUnitId: _allyParty[0].BaseUnit.Id);

            // ── 7. バトルループをコルーチンとして開始 ─────────────────
            // Step 6.3 ではコルーチン化することでターン間に WaitForSeconds を挟み、
            // 各ターンの終わりに Unity が描画を行い UI の変化が目に見えるようにする。

            _battleCoroutine = StartCoroutine(
                RunBattleCoroutine(battleManager, context));
        }

        // ──────────────────────────────────────────────────────────
        // バトルコルーチン（Step 6.3 新規追加）
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// ターン単位でバトルを進行するコルーチン。
        /// ProcessTurn() は1ターン分の全行動を同期的に実行し、その間にすべての
        /// イベント（OnHitLanded, OnUnitDied 等）が発火してUIが更新される。
        /// ターン終了後に WaitForSeconds で待機することで、Unity が1フレーム以上
        /// 描画を行い、HPバーの変化や死亡グレーアウトが画面に映る。
        /// </summary>
        private IEnumerator RunBattleCoroutine(BattleManager battleManager, BattleContext context)
        {
            Log("===== バトル開始 =====");
            LogBattleParticipants(context);

            // 開幕のログを即座に表示する
            if (_logText != null) _logText.text = _log.ToString();

            BattleResult battleResult;
            do
            {
                // 1ターン分の全行動を実行する
                // このメソッド内で OnHitLanded / OnUnitDied イベントが同期的に発火し、
                // UnitCardView.UpdateHp() / SetDead() が呼ばれてUIのデータが更新される。
                battleResult = battleManager.ProcessTurn(context);

                // 全カードのHP/シールドを再描画する。
                // 回復・バフ/デバフはOnHitLandedを発火しないため、ここで一括反映して
                // 回復によるHP回復などを画面に反映させる。
                RefreshAllCards(context);

                // ターン終了後にログテキストを更新する（ターンの行動をまとめて表示）
                if (_logText != null) _logText.text = _log.ToString();

                // _turnInterval 秒待機する。
                // この間に Unity が描画ループを実行し、変更された HP バーや
                // 死亡オーバーレイが画面に反映される。
                if (_turnInterval > 0f)
                    yield return new WaitForSeconds(_turnInterval);
                else
                    yield return null; // 0秒の場合も最低1フレーム描画の機会を与える
            }
            while (battleResult == BattleResult.None);

            // ── 最終状態の出力 ──────────────────────────────────────

            Log("\n===== 最終ユニット状態 =====");
            Log("【味方】");
            foreach (var u in context.AllyUnits)
                Log($"  {u.BaseUnit.Name}: HP {u.CurrentHP} / {u.MaxHP}"
                  + (u.IsAlive ? "" : "  [戦闘不能]"));
            Log("【敵】");
            foreach (var u in context.EnemyUnits)
                Log($"  {u.BaseUnit.Name}: HP {u.CurrentHP} / {u.MaxHP}"
                  + (u.IsAlive ? "" : "  [戦闘不能]"));

            Log("\n配置を変えるか、もう一度「バトル開始」で再戦できます。");

            // バトル終了：配置編成の操作を再び受け付ける
            _isBattleRunning = false;

            // 最終ログをUIテキストに反映する
            if (_logText != null)
            {
                _logText.text = _log.ToString();

                // テキスト更新と同フレームではレイアウト計算がまだ終わっていないため、
                // 1フレーム待ってからスクロールを最下部に移動する。
                if (_scrollRect != null)
                    yield return StartCoroutine(ScrollToBottomNextFrame());
            }

            _battleCoroutine = null;
        }

        // ──────────────────────────────────────────────────────────
        // Step 6.3 追加: カード検索ヘルパー
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// 指定した RuntimeUnit に対応する UnitCardView を味方・敵どちらのグリッドからも検索して返す。
        /// 見つからない場合は null を返す。
        /// </summary>
        private UnitCardView FindCard(RuntimeUnit unit)
        {
            if (_allyGridView != null)
            {
                UnitCardView card = _allyGridView.GetCard(unit);
                if (card != null) return card;
            }

            if (_enemyGridView != null)
            {
                UnitCardView card = _enemyGridView.GetCard(unit);
                if (card != null) return card;
            }

            return null;
        }

        /// <summary>
        /// 全ユニットのカードのHP・シールド表示を現在値で再描画する。
        /// ダメージ以外のHP変化（回復）も画面へ反映するため、各ターン終了後に呼ぶ。
        /// </summary>
        private void RefreshAllCards(BattleContext context)
        {
            foreach (var u in context.AllyUnits.Concat(context.EnemyUnits))
            {
                UnitCardView card = FindCard(u);
                if (card == null) continue;
                card.UpdateHp(u.CurrentHP, u.MaxHP);
                card.UpdateShield(u.CurrentShield);
            }
        }

        // ──────────────────────────────────────────────────────────
        // 内部ユーティリティ
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// 1フレーム待ってから Canvas のレイアウトを再計算し、スクロールを最下部に移動する。
        /// Content Size Fitter によるサイズ更新はフレーム末に行われるため、
        /// yield return null で1フレーム譲ってから位置を設定する必要がある。
        /// </summary>
        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        /// <summary>ログに1行追記し、Unity コンソールにも出力する</summary>
        private void Log(string message)
        {
            _log.AppendLine(message);
            Debug.Log(message);
        }

        /// <summary>バトル開始時の参加ユニット一覧をログ出力する</summary>
        private void LogBattleParticipants(BattleContext context)
        {
            Log("【味方】");
            foreach (var u in context.AllyUnits)
                Log($"  [{(u.IsFrontRow ? "前衛" : "後衛")}] {u.BaseUnit.Name}"
                  + $"  HP:{u.MaxHP}  ATK:{u.BaseUnit.BaseATK}  SPD:{u.BaseUnit.BaseSPD}"
                  + (u.IsLeader ? "  ★リーダー" : ""));
            Log("【敵】");
            foreach (var u in context.EnemyUnits)
                Log($"  [{(u.IsFrontRow ? "前衛" : "後衛")}] {u.BaseUnit.Name}"
                  + $"  HP:{u.MaxHP}  ATK:{u.BaseUnit.BaseATK}  SPD:{u.BaseUnit.BaseSPD}");
        }

        /// <summary>PhaseState を日本語文字列に変換する</summary>
        private static string PhaseToJapanese(PhaseState phase)
        {
            switch (phase)
            {
                case PhaseState.Start:               return "ターン開始";
                case PhaseState.Main:                return "メインフェーズ";
                case PhaseState.InterventionStandby: return "介入待機";
                case PhaseState.End:                 return "ターン終了";
                default:                             return phase.ToString();
            }
        }

        /// <summary>BattleResult を日本語文字列に変換する</summary>
        private static string ResultToJapanese(BattleResult result)
        {
            switch (result)
            {
                case BattleResult.PerfectVictory:      return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat:      return "惜敗";
                case BattleResult.CrushingDefeat:      return "完敗";
                default:                               return "不明";
            }
        }
    }
}
