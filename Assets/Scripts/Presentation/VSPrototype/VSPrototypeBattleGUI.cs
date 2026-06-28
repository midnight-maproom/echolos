// 戦闘 UI：単一戦闘の再生＋複数戦闘連鎖を描画する。
//
// 【描画責務】
// - Phase=Battle のときのみ全画面描画する
// - 上部ヘッダ：マスタイトル＋ラウンド表記＋戦闘番号
// - 中央：味方6＋敵6 の前列3＋後列3 構図（敵側は flipHorizontal で対峙構図）
// - 下部 3 列：左 味方ステータス／中央 速度コントロール＋スキップ／右 敵ステータス
// - 最下部：戦闘ログ 1 行＋「ログ」ボタン
//
// 【ホストとの分担】
// - 再生キュー：IBattleReplayHost.Segments / CurrentSegment / CurrentIndex
// - キュー進行：IBattleReplayHost.AdvanceToNext / FinishAll
// - 単一戦闘の再生状態：本 GUI 内で保持
//
// 【メインボタンの挙動】
// - 自動連鎖：Auto モード時、_isFinished から余韻 0.8 秒で次戦闘へ自動遷移
// - 単一戦闘スキップ：再生中なら残り Events を一気に消化＋ _displayHp 即同期＝戦闘終了状態へ
// - ラベル分岐：再生中＝スキップ／終了＆次あり＝次戦闘／終了＆末尾＝ラン続行
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;
using Echolos.UseCase.VSPrototype;
using Echolos.Presentation.Common;

namespace Echolos.Presentation.VSPrototype
{
    /// <summary>
    /// 戦闘再生 GUI。<see cref="IBattleReplayHost"/> を実装する MonoBehaviour が同 GameObject 上にある
    /// 前提で動く（VSプロト本シーン=VSPrototypeBootstrap／Debug シーン=DebugBattleSandboxBootstrap）。
    /// </summary>
    public sealed class VSPrototypeBattleGUI : MonoBehaviour
    {
        // 色定義（MapGUI / InteriorGUI と統一感を持たせる）

        private static readonly Color ColorBg          = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color ColorPanelBg     = new Color(0.14f, 0.15f, 0.20f);
        // スロット背景塗りは行わない（戦場背景を活かす＋透過キャラの矩形枠が見えるのを避ける）。
        // 敵味方区別は位置（左右）＋ flipHorizontal ＋下部ステータスリストで十分視認可。
        // 死亡視認は GUI.color による暗色化（DrawSlot 内）で代替。
        private static readonly Color ColorText        = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ColorTextMuted   = new Color(0.65f, 0.65f, 0.72f);
        // HP バー：枠＝半透明黒／トレイル＝明るめ黄色（暗い背景でも視認）
        private static readonly Color ColorHpBarFrame  = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color ColorHpBarTrail  = new Color(1f, 0.85f, 0.2f, 0.85f);
        // 補間時間係数：SecondsPerEvent × 0.7 で「次イベント前に追いつく」程度の追従感
        private const float HpBarCatchupRatio = 0.7f;

        // シナジーバッジ（アリーナ左上に重畳・常時表示・ホバーでツールチップ）
        // アイコン画像は Resources/Icons/Synergy/synergy_{fire/water/light}.png に置けば優先採用。
        // 無ければ属性カラー帯＋「F2」「W4」「L0」のような英字+Lv 文字フォールバック。
        private const float SynergyBadgeSize = 32f;
        private const float SynergyBadgeGap = 4f;
        private const float SynergyBadgePad = 10f;     // アリーナ左上からの内側余白
        private const float SynergyTooltipW = 260f;
        private const float SynergyTooltipH = 46f;
        private static readonly Color ColorSynergyFireActive  = new Color(0.92f, 0.34f, 0.28f);
        private static readonly Color ColorSynergyWaterActive = new Color(0.32f, 0.56f, 0.92f);
        private static readonly Color ColorSynergyLightActive = new Color(0.96f, 0.86f, 0.32f);
        private static readonly Color ColorSynergyInactive    = new Color(0.45f, 0.45f, 0.48f);
        private static readonly Color ColorSynergyBadgeBg     = new Color(0.05f, 0.05f, 0.08f, 0.78f);
        private static readonly Color ColorSynergyTooltipBg   = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        private static readonly Color ColorSynergyTooltipEdge = new Color(1f, 1f, 1f, 0.35f);

        // 状態異常バッジ（各ユニット画像の左下・敵側は右下）
        // アイコン画像は Resources/Icons/StatusEffects/status_{kind}.png に置けば優先採用。
        // 無ければ「AU / DD / Br」等の英字 2 文字＋カテゴリ色枠フォールバック。
        // カテゴリ色：Persistent=青枠／Triggered=黄枠／Cleansable=赤枠。
        private const float StatusBadgeW = 22f;
        private const float StatusBadgeH = 16f;
        private const float StatusBadgeGap = 2f;
        private const int   StatusBadgeMax = 6;          // 1 ユニットに表示できるバッジ数上限
        private const float StatusTooltipW = 240f;
        private const float StatusTooltipH = 60f;
        private static readonly Color ColorStatusPersistent  = new Color(0.42f, 0.62f, 0.95f);
        private static readonly Color ColorStatusTriggered   = new Color(0.95f, 0.82f, 0.32f);
        private static readonly Color ColorStatusCleansable  = new Color(0.92f, 0.36f, 0.30f);
        private static readonly Color ColorStatusBadgeBg     = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        private static readonly Color ColorStatusStackBg     = new Color(0f, 0f, 0f, 0.75f);

        // 再生モード

        /// <summary>再生モード。Step は手動で 1 イベントずつ／Auto は時間ベース連続再生。</summary>
        private enum PlayMode { Step, Auto }

        // Auto 各速度の 1 イベント表示時間（秒）。x1=0.5・x2=0.25・x4=0.125。
        private const float SecondsPerEventX1 = 0.5f;
        private const float SecondsPerEventX2 = 0.25f;
        private const float SecondsPerEventX4 = 0.125f;

        // 戦闘終了 → 次戦闘自動遷移までの余韻時間（Auto モード時のみ・結果を見る時間を確保）
        private const float AutoAdvanceDelay = 0.8f;

        // 内部状態

        private IBattleReplayHost _host;
        // 現在再生中のセグメント参照。Bootstrap.CurrentBattleSegment との同一性で
        // セグメント切替（連鎖時）を検知し、_hp / _alive / カーソルを再初期化する。
        private VSPrototypeBattleSegment _activeSegment;

        // 単一戦闘の再生ステート（セグメント切替時に InitializeForSegment で再構築）
        private PlayMode _mode = PlayMode.Auto;
        private float _secondsPerEvent = SecondsPerEventX1;
        private float _accumDelay;
        private int _cursor;                     // 次に再生する Events[_cursor]
        private bool _isFinished;
        private float _finishedTime;             // 戦闘終了後の経過時間（Auto 連鎖判定用）
        private readonly Dictionary<RuntimeUnit, int>   _hp        = new Dictionary<RuntimeUnit, int>();
        private readonly Dictionary<RuntimeUnit, bool>  _alive     = new Dictionary<RuntimeUnit, bool>();
        // 黄色トレイル用：減少時は時間補間で _hp に追従、増加時は瞬時追従。
        private readonly Dictionary<RuntimeUnit, float> _displayHp = new Dictionary<RuntimeUnit, float>();
        // 状態異常スナップショット：戦闘終了時の RuntimeUnit.ActiveEffects 直読を避け、
        // 各 BattleEvent 再生時点での効果リストを段階的に構築する（ホバー時の Stacks /
        // RemainingTurns 表示も再生時点の値を返せる）。同 Kind は 1 件のみ保持＝最新で上書き。
        private readonly Dictionary<RuntimeUnit, List<EffectChange>> _effectsSnapshot
            = new Dictionary<RuntimeUnit, List<EffectChange>>();

        // ログモーダル表示状態（戦闘ごとにリセット）
        private bool _showLogModal;
        private Vector2 _logScroll;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _statusListNameStyle;
        private GUIStyle _logLineStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _hpTextStyle;
        private GUIStyle _synergyBadgeStyle;
        private GUIStyle _synergyTooltipTitleStyle;
        private GUIStyle _synergyTooltipBodyStyle;
        private bool _stylesBuilt;

        // シナジーバッジのホバー判定用（このフレームで描いた Rect とカウントを記録）
        private readonly Rect[] _synergyBadgeRects = new Rect[3];
        private readonly int[]  _synergyBadgeCounts = new int[3];
        private int _hoveredSynergyIdx = -1;

        // シナジーアイコン画像の遅延ロードキャッシュ（画像差し替え対応・無ければ文字フォールバック）
        private static readonly Dictionary<Element, Texture2D> _synergyIconCache = new Dictionary<Element, Texture2D>();
        private static readonly HashSet<Element> _synergyIconMissing = new HashSet<Element>();

        // 状態異常バッジのホバー判定用（このフレームで最後にヒットした 1 件を表示）
        private EffectChange _hoveredStatusEffect;
        private Rect _hoveredStatusEffectRect;
        private GUIStyle _statusBadgeStyle;
        private GUIStyle _statusStackStyle;
        private GUIStyle _statusTooltipTitleStyle;
        private GUIStyle _statusTooltipBodyStyle;

        // 状態異常アイコン画像の遅延ロードキャッシュ（画像差し替え対応・無ければ文字フォールバック）
        private static readonly Dictionary<EffectKind, Texture2D> _statusIconCache = new Dictionary<EffectKind, Texture2D>();
        private static readonly HashSet<EffectKind> _statusIconMissing = new HashSet<EffectKind>();

        // ライフサイクル

        private void Awake()
        {
            // 同 GameObject 上の MonoBehaviour で IBattleReplayHost を実装するものを取り出す。
            // [RequireComponent] は C# interface に対応しないためこの形。
            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb is IBattleReplayHost host) { _host = host; break; }

            if (_host == null)
                Debug.LogError("[VSPrototypeBattleGUI] IBattleReplayHost 実装が同 GameObject に見つかりません");
        }

        private void Update()
        {
            // 再生駆動と HP バー補間。Step モードや戦闘終了後も補間だけは続けて、
            // 直前の被弾が黄バーとして残らないようにする。
            if (_host == null || !_host.IsActive) return;

            if (!_isFinished && _mode == PlayMode.Auto) Tick(Time.deltaTime);
            UpdateDisplayHp(Time.deltaTime);

            // 自動連鎖：Auto モード時のみ、戦闘終了から AutoAdvanceDelay 経過後に次戦闘へ
            // （結果を見る間を取る・モーダル表示中は連鎖を抑止してプレイヤー操作を尊重）
            if (_isFinished && _mode == PlayMode.Auto && !_showLogModal)
            {
                _finishedTime += Time.deltaTime;
                if (_finishedTime >= AutoAdvanceDelay)
                {
                    _finishedTime = 0f;
                    _host.AdvanceToNext();
                }
            }
        }

        private void OnGUI()
        {
            if (_host == null || !_host.IsActive) return;

            var segment = _host.CurrentSegment;
            if (segment == null)
            {
                // セグメント不在は不整合（キュー空でアクティブのまま）。安全に呼び出し元へ戻す。
                _host.FinishAll();
                return;
            }

            // 新セグメント検知 → 再生ステート全リセット
            if (!ReferenceEquals(_activeSegment, segment))
            {
                _activeSegment = segment;
                InitializeForSegment(segment);
            }

            BuildStylesIfNeeded();

            // ベース塗り：全画面を ColorBg で先に塗る（パネル間の隙間／フォールバック兼用）
            var fullscreen = new Rect(0, 0, Screen.width, Screen.height);
            FillRect(fullscreen, ColorBg);

            // レイアウト：上＝ヘッダ／中＝アリーナ／下＝3 列ステータス&コントロール／最下＝ログ 1 行
            const float headerH = 56f;
            const float bottomColsH = 200f;
            const float footerH = 52f;
            float arenaY = headerH + 6f;
            float arenaH = Mathf.Max(120f, Screen.height - headerH - bottomColsH - footerH - 18f);
            var arenaRect = new Rect(0, arenaY, Screen.width, arenaH);

            // 戦場背景はアリーナ領域だけに ScaleAndCrop で描画。
            // ・全画面描画にしないことで、画像下部が下部 UI に隠れて無駄になる問題＋
            //   パネル間 gap から背景が透ける問題を同時解消
            // ・アセット無しならベース塗り ColorBg がそのまま見える＝フォールバック自然動作
            BackgroundRegistry.TryDrawCover(arenaRect, "battlefield_grassland");

            // モーダル表示中は背景ボタン全てを disabled にしてホット争奪を防ぐ
            // （OnGUI モーダル 3 大落とし穴・他モーダルと同じパターン）
            bool prevGuiEnabled = GUI.enabled;
            if (_showLogModal) GUI.enabled = false;

            DrawHeader(new Rect(0, 0, Screen.width, headerH), segment);
            DrawArena(arenaRect, segment.Report);
            // シナジーバッジはアリーナ左上に重畳（戦闘画面サイズ変更なし）。
            // ホバー判定もここで行い、ツールチップ描画は OnGUI 末尾でモーダル抑止と整合させる。
            DrawSynergyBadges(arenaRect, segment.Report);
            DrawBottomColumns(new Rect(0, arenaY + arenaH + 6f, Screen.width, bottomColsH), segment.Report);
            DrawLogBar(new Rect(0, Screen.height - footerH, Screen.width, footerH), segment.Report);

            GUI.enabled = prevGuiEnabled;

            // モーダル（最前面）
            if (_showLogModal) DrawLogModal(segment.Report);
            // ツールチップはモーダル中は出さない（ホット争奪回避・OnGUI モーダル落とし穴整合）。
            // 同フレームでシナジー／状態異常両方にホバーする幾何状況はないが、両方記録があれば
            // 状態異常を優先（ユニット上に出るのでマウスが乗っている文脈と一致）。
            else
            {
                DrawSynergyTooltip();
                DrawStatusEffectTooltip();
            }
        }

        // 再生エンジン

        /// <summary>
        /// 新しいセグメントの再生を初期化する：カーソル・HP・生存状態を Lineup スナップショットから復元。
        /// モード（_mode / _secondsPerEvent）は前セグメントから引き継ぐ（プレイヤー設定を尊重）。
        /// </summary>
        private void InitializeForSegment(VSPrototypeBattleSegment segment)
        {
            _cursor = 0;
            _accumDelay = 0f;
            _finishedTime = 0f;
            _hp.Clear();
            _alive.Clear();
            _displayHp.Clear();
            _effectsSnapshot.Clear();
            // ログ表示も戦闘ごとにリセット（前戦闘のスクロール位置やモーダル開きっぱなしを引き継がない）
            _showLogModal = false;
            _logScroll = Vector2.zero;

            var report = segment.Report;
            _isFinished = report?.Events == null || report.Events.Count == 0;

            if (report?.AllyLineup != null)
                foreach (var u in report.AllyLineup)
                {
                    _hp[u] = u.MaxHP; _alive[u] = true; _displayHp[u] = u.MaxHP;
                    _effectsSnapshot[u] = new List<EffectChange>();
                }
            if (report?.EnemyLineup != null)
                foreach (var u in report.EnemyLineup)
                {
                    _hp[u] = u.MaxHP; _alive[u] = true; _displayHp[u] = u.MaxHP;
                    _effectsSnapshot[u] = new List<EffectChange>();
                }
        }

        /// <summary>Auto モードの自動進行。SecondsPerEvent ごとに 1 ステップ消化する。</summary>
        private void Tick(float deltaTime)
        {
            var report = _activeSegment?.Report;
            if (report == null || report.Events == null) return;

            _accumDelay += deltaTime;
            while (_accumDelay >= _secondsPerEvent && _cursor < report.Events.Count)
            {
                AdvanceOneStep();
                _accumDelay -= _secondsPerEvent;
            }
            if (_cursor >= report.Events.Count) _isFinished = true;
        }

        /// <summary>Step モードで 1 ステップ進める。終端なら IsFinished に遷移。</summary>
        private void StepOne()
        {
            var report = _activeSegment?.Report;
            if (report == null || report.Events == null) return;
            if (_isFinished) return;
            if (_cursor < report.Events.Count)
            {
                AdvanceOneStep();
                if (_cursor >= report.Events.Count) _isFinished = true;
            }
        }

        // 1 ステップの正規化：LogLine 非空 Event を 1 件 Apply するまで一気に消化する。
        // LogLine が null/空の Event（Permanent + IsUndispellable な Aura 解除等の suppressTextLog 経路）は
        // 「無音ステップが何回も続く」現象を避けるためまとめて消化される。snapshot/HP/Alive は
        // 各 Apply で順次反映されるため整合性は ApplyEvent の冪等性で保証される。
        // 安全弁として 1 ステップでの消化上限を設け、暴走を防ぐ。
        private const int MaxEventsPerStep = 64;
        private void AdvanceOneStep()
        {
            var report = _activeSegment?.Report;
            if (report?.Events == null) return;
            int consumed = 0;
            while (_cursor < report.Events.Count && consumed < MaxEventsPerStep)
            {
                var ev = report.Events[_cursor++];
                ApplyEvent(ev);
                consumed++;
                if (!string.IsNullOrEmpty(ev.LogLine)) break;
            }
        }

        /// <summary>
        /// イベント適用：HP 変化と死亡を再生ステートに反映する。
        /// ActionResolved / HealOverTimePhase 1 件で集約結果全体を一括反映する（1 Event = 1 Tick）。
        /// BurnTick は引き続き個別イベント。
        /// HitLanded / Healed / Died / Evaded は後方互換のため switch に残すが、現状の BattleRunner は
        /// アクション内分を ActionResolved に集約するため通常は到達しない。
        /// </summary>
        private void ApplyEvent(BattleEvent ev)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.TurnStart:
                    // 新ターン開始時に Triggered 効果の RemainingTurns を 1 ずつ減らす（StatusEffectProcessor の
                    // OnEndPhase 減算をスナップショット側でも反映＝ホバー時の残ターン段階表示）。
                    // 0 まで減ったものはこのターン中に StatusEffectExpired Event で削除されるため、ここでは減算のみ。
                    DecrementTriggeredRemainingTurns();
                    break;
                case BattleEventKind.ActionResolved:
                    if (ev.Outcomes != null)
                    {
                        foreach (var o in ev.Outcomes)
                        {
                            if (o.Target == null) continue;
                            if (_hp.ContainsKey(o.Target))
                                _hp[o.Target] = o.TargetHPAfter;
                            if (o.ResultedInDeath && _alive.ContainsKey(o.Target))
                            {
                                _alive[o.Target] = false;
                                ClearEffectsSnapshot(o.Target);
                            }
                            // 付帯付与・解除を snapshot に反映（同 Kind は上書き＝最新の Stacks/RemainingTurns へ）
                            foreach (var added in o.AppliedEffects)
                                AddOrReplaceEffectSnapshot(o.Target, added);
                            foreach (var removed in o.RemovedEffects)
                                RemoveEffectSnapshotBySource(o.Target, removed.Kind, removed.SourceAbilityName);
                        }
                    }
                    break;
                case BattleEventKind.HealOverTimePhase:
                    if (ev.HealTicks != null)
                    {
                        foreach (var t in ev.HealTicks)
                        {
                            if (t.Unit == null) continue;
                            if (_hp.ContainsKey(t.Unit))
                                _hp[t.Unit] = t.HPAfter;
                        }
                    }
                    break;
                case BattleEventKind.HitLanded:
                case BattleEventKind.Healed:
                case BattleEventKind.BurnTick:
                    if (ev.Target != null && _hp.ContainsKey(ev.Target))
                        _hp[ev.Target] = ev.TargetHPAfter;
                    break;
                case BattleEventKind.Died:
                    if (ev.Target != null && _alive.ContainsKey(ev.Target))
                    {
                        _alive[ev.Target] = false;
                        ClearEffectsSnapshot(ev.Target);
                    }
                    break;
                case BattleEventKind.StatusEffectApplied:
                    // 戦闘開始時集約（シナジー/オーラ/ユニット固有 PersistentEffects）は BulkEffectChanges
                    // に (unit, effect) を全件積んでくるため、まず Bulk を優先反映。それ以外（通常の
                    // OnEffectAdded 経由）は EffectChange 単体を Target に反映する。
                    if (ev.BulkEffectChanges != null && ev.BulkEffectChanges.Count > 0)
                    {
                        foreach (var app in ev.BulkEffectChanges)
                            if (app != null && app.Unit != null && app.Change != null)
                                AddOrReplaceEffectSnapshot(app.Unit, app.Change);
                    }
                    else if (ev.Target != null && ev.EffectChange != null)
                    {
                        AddOrReplaceEffectSnapshot(ev.Target, ev.EffectChange);
                    }
                    break;
                case BattleEventKind.StatusEffectExpired:
                    if (ev.Target != null && ev.EffectChange != null)
                        RemoveEffectSnapshotBySource(ev.Target, ev.EffectChange.Kind,
                            ev.EffectChange.SourceAbilityName);
                    break;
            }
        }

        // 同 Kind かつ同 SourceAbilityName の既存があれば上書き（StatusEffectStacker と整合）。
        // 異なる SourceAbilityName は別効果として共存＝バッジが 2 つ並び、ホバーで個別表示。
        private void AddOrReplaceEffectSnapshot(RuntimeUnit unit, EffectChange change)
        {
            if (unit == null || change == null) return;
            if (!_effectsSnapshot.TryGetValue(unit, out var list))
            {
                list = new List<EffectChange>();
                _effectsSnapshot[unit] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Kind == change.Kind
                    && SameSource(list[i].SourceAbilityName, change.SourceAbilityName))
                {
                    list[i] = change;
                    return;
                }
            }
            list.Add(change);
        }

        private void RemoveEffectSnapshotBySource(RuntimeUnit unit, EffectKind kind, string sourceAbilityName)
        {
            if (unit == null) return;
            if (!_effectsSnapshot.TryGetValue(unit, out var list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Kind == kind && SameSource(list[i].SourceAbilityName, sourceAbilityName))
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }

        private static bool SameSource(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;
            return a == b;
        }

        private void ClearEffectsSnapshot(RuntimeUnit unit)
        {
            if (unit == null) return;
            if (_effectsSnapshot.TryGetValue(unit, out var list)) list.Clear();
        }

        // 全ユニットの Triggered 効果の RemainingTurns を 1 ずつ減らす（クランプ 0）。
        // Permanent (RemainingTurns=-1) は不変。EffectChange は immutable なので新インスタンスで置換。
        private void DecrementTriggeredRemainingTurns()
        {
            foreach (var kv in _effectsSnapshot)
            {
                var list = kv.Value;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    if (c == null || c.Lifetime != Lifetime.Triggered) continue;
                    if (c.RemainingTurns <= 0) continue;
                    list[i] = new EffectChange(c.Kind, c.Stacks, c.RemainingTurns - 1,
                        c.Lifetime, c.SourceAbilityName, c.IsCleansable, c.IsUndispellable);
                }
            }
        }

        /// <summary>
        /// 単一戦闘スキップ：残り Events を一気に消化し、_displayHp を即同期して
        /// 「戦闘終了状態」に到達させる（黄バーは即引き戻す＝演出残しの違和感を回避）。
        /// </summary>
        private void SkipToEnd()
        {
            var report = _activeSegment?.Report;
            if (report == null || report.Events == null) return;
            while (_cursor < report.Events.Count)
                ApplyEvent(report.Events[_cursor++]);
            _isFinished = true;

            var keys = new List<RuntimeUnit>(_displayHp.Keys);
            foreach (var key in keys)
                if (_hp.TryGetValue(key, out var realHp))
                    _displayHp[key] = realHp;
        }

        /// <summary>
        /// 黄バー（残 HP 演出）の補間更新。
        /// _displayHp を _hp に向けて減少時のみ時間ベースで追従させる（増加=回復は瞬時）。
        /// 補間時間は SecondsPerEvent × HpBarCatchupRatio で倍速時も自然にスケール。
        /// </summary>
        private void UpdateDisplayHp(float deltaTime)
        {
            if (_displayHp.Count == 0) return;

            float catchupSec = _secondsPerEvent * HpBarCatchupRatio;
            if (catchupSec <= 0f) catchupSec = 0.01f;

            // foreach 中のコレクション変更を避けるためキーをスナップショット
            var keys = new List<RuntimeUnit>(_displayHp.Keys);
            foreach (var key in keys)
            {
                float target = _hp.TryGetValue(key, out var realHp) ? realHp : 0f;
                float current = _displayHp[key];

                if (current > target)
                {
                    // 減少：MaxHP を 1 イベント時間で渡り切る速度
                    float rate = key.MaxHP / catchupSec;
                    float next = current - rate * deltaTime;
                    _displayHp[key] = Mathf.Max(next, target);
                }
                else if (current < target)
                {
                    _displayHp[key] = target;
                }
            }
        }

        // 上部ヘッダ

        private void DrawHeader(Rect area, VSPrototypeBattleSegment segment)
        {
            FillRect(area, ColorPanelBg);

            int idx = _host.CurrentIndex + 1;
            int total = _host.Segments != null ? _host.Segments.Count : 0;

            GUI.Label(new Rect(area.x + 20, area.y + 14, area.width - 40, 28),
                $"{segment.Title}    {_host.HeaderProgressLabel}    戦闘 {idx}/{total}",
                _titleStyle);
        }

        // 中央アリーナ（前列3＋後列3 の対峙構図）

        private void DrawArena(Rect area, BattleReport report)
        {
            // 状態異常ホバーは毎フレームリセット。各 DrawSlot が描画中にヒットしたら記録。
            _hoveredStatusEffect = null;

            // 左右で 2 分割（左=味方／右=敵）。中央軸で両陣営の前列が向き合う鏡像配置にする。
            // gap は両陣営の前列同士が無人地帯を挟んで対峙する空間として広めに取る。
            const float gap = 80f;
            float halfW = (area.width - gap) * 0.5f;
            var allyArea  = new Rect(area.x,                area.y, halfW, area.height);
            var enemyArea = new Rect(area.x + halfW + gap,  area.y, halfW, area.height);

            DrawTeamGrid(allyArea,  report.AllyLineup,  isAlly: true);
            DrawTeamGrid(enemyArea, report.EnemyLineup, isAlly: false);
        }

        /// <summary>
        /// 編成グリッドを縦3行×横2列で描く。
        /// 味方陣営は「左=後列、右=前列」、敵陣営は「左=前列、右=後列」と鏡像配置にすることで
        /// 画面中央軸で両陣営の前列がレーン同士で向き合う JRPG 風レイアウトになる。
        /// </summary>
        private void DrawTeamGrid(Rect area, List<RuntimeUnit> lineup, bool isAlly)
        {
            if (lineup == null) return;

            const int rows = 3;
            // rowGap 負値：隣接行のキャラが意図的に重なる前提のレイアウト（スカスカ解消・案A）。
            // ユニット画像が正方形（1024×1024）なので cellH 縛りで横幅を活かせない構造のため、
            // 縦に攻める方向でサイズアップする。描画順は row=0 → 2 で後列（row=2）が前面に来る。
            const float rowGap = -36f;
            // colGap 負値：前列と後列を意図的に重ねる（前列が後列を遮る奥行き演出）。
            const float colGap = -20f;
            float cellH = (area.height - rowGap * (rows - 1)) / rows;
            float cellW = (area.width - colGap) * 0.5f;

            for (int row = 0; row < rows; row++)
            {
                float y = area.y + row * (cellH + rowGap);
                // SlotIndex: 0/1/2=前列, 3/4/5=後列。row が前列／後列のレーン番号に対応
                int frontSlot = row;
                int backSlot  = row + 3;

                int leftSlot  = isAlly ? backSlot  : frontSlot;
                int rightSlot = isAlly ? frontSlot : backSlot;

                // 擬似 3D 奥行き演出：row=0=奥（小さく＋中央軸寄り）, row=2=手前（等倍＋外側）。
                // 消失点が画面中央奥に来る遠近表現（味方は奥が右＝中央軸／手前が左＝外側、敵は鏡像）。
                float depth     = row / (float)(rows - 1);          // 0.0 (奥) ～ 1.0 (手前)
                float scale     = Mathf.Lerp(0.92f, 1.0f, depth);
                float offsetMag = Mathf.Lerp(0.20f, 0f, depth) * cellW;
                float offsetX   = isAlly ? offsetMag : -offsetMag;

                DrawSlot(new Rect(area.x + offsetX,                  y, cellW, cellH), lineup, leftSlot,  isAlly, scale);
                DrawSlot(new Rect(area.x + cellW + colGap + offsetX, y, cellW, cellH), lineup, rightSlot, isAlly, scale);
            }
        }

        private void DrawSlot(Rect rect, List<RuntimeUnit> lineup, int slotIndex, bool isAlly, float scale = 1f)
        {
            RuntimeUnit unit = null;
            for (int i = 0; i < lineup.Count; i++)
                if (lineup[i].SlotIndex == slotIndex) { unit = lineup[i]; break; }

            // 空きスロット：何も描かない（戦場背景を素通し）。
            if (unit == null) return;

            bool alive = !_alive.TryGetValue(unit, out var a) || a;

            // 透過キャラ画像を戦場背景の上に直接描画する（スロット背景塗りは行わない）。
            // iconRect は cell より上下に拡張＝隣接行と意図的に重ねる前提（スカスカ解消）。
            // 死亡時は GUI.color で暗色化。
            var iconRect = new Rect(rect.x + 4, rect.y - 45f, rect.width - 8, rect.height + 90f);

            // 擬似 3D スケール：中心保持で縮小（奥のレーンほど小さく見せる）。
            if (scale < 0.999f)
            {
                float scaledW = iconRect.width  * scale;
                float scaledH = iconRect.height * scale;
                iconRect = new Rect(
                    iconRect.x + (iconRect.width  - scaledW) * 0.5f,
                    iconRect.y + (iconRect.height - scaledH) * 0.5f,
                    scaledW, scaledH);
            }

            // ボス（皇太子 2 形態）だけ画像をひと回り大きく見せる（プロト暫定）。
            // 他ユニットでも同様の要件が出たら UnitDefinition に IconScale: float（デフォルト 1.0）を
            // 追加してデータ駆動化する。現状は対象が 2 ID 固定なのでハードコード判定で済ます。
            if (unit.BaseUnit.Id == UniqueUnitIds.Prince || unit.BaseUnit.Id == UniqueUnitIds.PrinceDark)
            {
                const float bossScale = 1.2f;
                float bw = iconRect.width  * bossScale;
                float bh = iconRect.height * bossScale;
                iconRect = new Rect(
                    iconRect.x - (bw - iconRect.width)  * 0.5f,
                    iconRect.y - (bh - iconRect.height) * 0.5f,
                    bw, bh);
            }

            var prevIconColor = GUI.color;
            if (!alive) GUI.color = new Color(0.45f, 0.45f, 0.45f, 0.85f);
            IconRegistry.TryDrawIcon(iconRect, unit.BaseUnit.Id, flipHorizontal: !isAlly);
            GUI.color = prevIconColor;

            // 状態異常バッジ：味方は左上／敵は右上にユニットアイコン左右上端寄りに重畳。
            // 死亡時は描画スキップ（無意味なバッジを残さない）。
            if (alive) DrawStatusEffectBadges(iconRect, unit, isAlly);
        }

        // 下部 3 列（左=味方ステータス／中央=コントロール／右=敵ステータス）

        private void DrawBottomColumns(Rect area, BattleReport report)
        {
            const float gap = 8f;
            float colW = (area.width - gap * 2) / 3f;
            var leftCol   = new Rect(area.x,                     area.y, colW, area.height);
            var centerCol = new Rect(area.x + colW + gap,        area.y, colW, area.height);
            var rightCol  = new Rect(area.x + (colW + gap) * 2,  area.y, colW, area.height);

            DrawTeamStatusList(leftCol,  "── 味方 ──", report.AllyLineup);
            DrawControlPanel(centerCol);
            DrawTeamStatusList(rightCol, "── 敵 ──",   report.EnemyLineup);
        }

        private void DrawTeamStatusList(Rect area, string title, List<RuntimeUnit> lineup)
        {
            FillRect(area, ColorPanelBg);
            GUI.Label(new Rect(area.x + 10, area.y + 6, area.width - 20, 18), title, _mutedStyle);

            if (lineup == null) return;

            // 1 行 = テキスト 16px ＋ ミニ HP バー 6px ＋ 行間 4px ＝ 26px
            const float rowH = 26f;
            float rowY = area.y + 28f;
            // lineup は配置操作順（プレイヤーが配置した順）が保たれており、ここでは前列 →
            // 後列の直感に沿うよう SlotIndex 昇順で表示する（戦闘ロジック側の順序は不変）。
            foreach (var u in lineup.OrderBy(u => u.SlotIndex))
            {
                if (rowY + rowH > area.yMax) break;

                int hp = _hp.TryGetValue(u, out var h) ? h : u.MaxHP;
                bool alive = !_alive.TryGetValue(u, out var a) || a;
                float displayHp = _displayHp.TryGetValue(u, out var dhp) ? dhp : hp;
                string prefix = alive ? "" : "×";

                GUI.Label(new Rect(area.x + 12, rowY, area.width - 24, 16),
                    $"{prefix}{u.BaseUnit.Name}  HP {hp}/{u.MaxHP}",
                    _statusListNameStyle);

                var miniBar = new Rect(area.x + 12, rowY + 16, area.width - 24, 6);
                DrawHPBar(miniBar, hp, u.MaxHP, alive, displayHp, drawText: false);

                rowY += rowH;
            }
        }

        private void DrawControlPanel(Rect area)
        {
            FillRect(area, ColorPanelBg);
            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 8, area.width - 20, area.height - 16));
            GUILayout.Label("── 再生 ──", _mutedStyle);
            GUILayout.Space(6);

            // 進行ステータス表示（再生中／終了）
            int total = _activeSegment?.Report?.Events != null ? _activeSegment.Report.Events.Count : 0;
            string status = _isFinished ? "戦闘終了" : $"再生中  {_cursor}/{total}";
            GUILayout.Label(status, _bodyStyle);
            GUILayout.Space(4);

            // 速度モード切替（Step / x1 / x2 / x4）。現在モードはボタン名にマーカー付与で示す。
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModeLabel("Step", _mode == PlayMode.Step), GUILayout.Height(28)))
            {
                _mode = PlayMode.Step;
                StepOne();
            }
            if (GUILayout.Button(ModeLabel("x1", _mode == PlayMode.Auto && Mathf.Approximately(_secondsPerEvent, SecondsPerEventX1)), GUILayout.Height(28)))
                SetAutoMode(SecondsPerEventX1);
            if (GUILayout.Button(ModeLabel("x2", _mode == PlayMode.Auto && Mathf.Approximately(_secondsPerEvent, SecondsPerEventX2)), GUILayout.Height(28)))
                SetAutoMode(SecondsPerEventX2);
            if (GUILayout.Button(ModeLabel("x4", _mode == PlayMode.Auto && Mathf.Approximately(_secondsPerEvent, SecondsPerEventX4)), GUILayout.Height(28)))
                SetAutoMode(SecondsPerEventX4);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // メインボタン：状態でラベル＋動作を分岐
            //  再生中  → SkipToEnd（残りイベント消化＝戦闘終了状態へ）
            //  終了＆次あり → AdvanceToNextBattleSegment
            //  終了＆末尾   → FinishBattleReplay（Phase=Run 復帰）
            int totalSegments = _host.Segments != null ? _host.Segments.Count : 0;
            bool hasNext = _host.CurrentIndex + 1 < totalSegments;
            string mainLabel = !_isFinished ? "この戦闘をスキップ →"
                : (hasNext ? "次の戦闘へ →" : "ラン続行 →");
            if (GUILayout.Button(mainLabel, GUILayout.Height(30)))
            {
                if (!_isFinished) SkipToEnd();
                else
                {
                    // 次戦闘 or ラン続行へ進む前に、開いていたログモーダルを即時クリアする。
                    // これをしないと、同フレーム内で DrawLogModal が旧 segment.Report を引数に持ったまま
                    // 描画され、新戦闘なのに前戦闘のログが一瞬見える違和感（連戦時の FB）の原因になる。
                    CloseLogModal();
                    if (hasNext) _host.AdvanceToNext();
                    else _host.FinishAll();
                }
            }

            // ラウンド全戦闘スキップ（末尾の終端状態では意味がないので無効化）。
            // 復元は GUI.enabled = true ではなく prev 保存値で行う（モーダル中の全 disabled 状態を壊さないため）。
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && (!_isFinished || hasNext);
            if (GUILayout.Button("全戦闘をスキップ →", GUILayout.Height(30)))
            {
                CloseLogModal();
                _host.FinishAll();
            }
            GUI.enabled = prevEnabled;

            GUILayout.EndArea();
        }

        private void SetAutoMode(float secondsPerEvent)
        {
            _mode = PlayMode.Auto;
            _secondsPerEvent = secondsPerEvent;
            _accumDelay = 0f;
        }

        /// <summary>
        /// ログモーダル状態を即時クリアする（戦闘遷移ボタン押下時に呼ぶ）。
        /// 同フレーム内で旧 Report を引数に持った DrawLogModal が描画されるのを防ぐため、
        /// AdvanceToNextBattleSegment / FinishBattleReplay 呼び出し前に必ず実行する。
        /// </summary>
        private void CloseLogModal()
        {
            _showLogModal = false;
            _logScroll = Vector2.zero;
        }

        private static string ModeLabel(string text, bool active) => active ? "[" + text + "]" : text;

        // 最下部ログ 1 行

        private void DrawLogBar(Rect area, BattleReport report)
        {
            FillRect(area, ColorPanelBg);

            // 最新ログ：_cursor は「次に Apply する Events[_cursor]」を意味する。
            // 直前に Apply した Events[_cursor - 1] の LogLine を表示すると HP バー反映と
            // ログ表示が同期する。LogLine=null（Persistent 剥奪等）の場合は遡って直前の
            // 表示可能ログを連続表示する。
            string line = "戦闘ログ準備中…";
            int totalEv = report.Events != null ? report.Events.Count : 0;
            if (totalEv > 0)
            {
                int idx = Mathf.Min(_cursor - 1, totalEv - 1);
                while (idx >= 0 && string.IsNullOrEmpty(report.Events[idx].LogLine)) idx--;
                if (idx >= 0) line = report.Events[idx].LogLine;
            }
            // 1 行ログは折り返さず、はみ出した分はクリップ（_logLineStyle: wordWrap=false）。
            // 全文確認は「ログ」ボタンからモーダル参照（モーダル側は wordWrap=true で自動改行）。
            GUI.Label(new Rect(area.x + 12, area.y + 14, area.width - 120, area.height - 28), line, _logLineStyle);

            // 右端「ログ」ボタン → 全ログモーダル
            if (GUI.Button(new Rect(area.xMax - 100, area.y + 10, 84, 30), "ログ"))
                _showLogModal = true;
        }

        /// <summary>
        /// 全ログモーダル：戦闘ログを縦スクロールで全表示。
        /// 閉じる：右上「閉じる」ボタン or モーダル外 MouseDown（Event.Use でホット争奪回避）。
        /// </summary>
        private void DrawLogModal(BattleReport report)
        {
            var fullscreen = new Rect(0, 0, Screen.width, Screen.height);
            FillRect(fullscreen, new Color(0f, 0f, 0f, 0.65f));

            float modalW = Mathf.Min(Screen.width * 0.7f, 900f);
            float modalH = Mathf.Min(Screen.height * 0.8f, 700f);
            var modal = new Rect(
                (Screen.width - modalW) * 0.5f,
                (Screen.height - modalH) * 0.5f,
                modalW, modalH);

            // モーダル外 MouseDown で閉じる（後段の GUI に Event を渡さないよう Event.Use）
            if (Event.current.type == EventType.MouseDown
                && !modal.Contains(Event.current.mousePosition))
            {
                CloseLogModal();
                Event.current.Use();
                return;
            }

            FillRect(modal, ColorPanelBg);

            // 再生中はカーソル位置までのログだけ表示し、結末ネタバレを防ぐ。
            // _cursor は「次に Apply する Events 位置」＝ Events[0.._cursor-1] が Apply 済み。
            // 戦闘終了後（_isFinished）はカーソルが終端まで進んでいるので自然に全件見える。
            int total = report.Events != null ? report.Events.Count : 0;
            int shown = total > 0 ? Mathf.Clamp(_cursor, 0, total) : 0;

            // ヘッダ
            GUI.Label(new Rect(modal.x + 20, modal.y + 14, modal.width - 140, 28),
                $"戦闘ログ（{shown}/{total} 行）", _titleStyle);
            if (GUI.Button(new Rect(modal.xMax - 110, modal.y + 14, 90, 28), "閉じる"))
            {
                CloseLogModal();
                return;
            }

            // 本体：縦スクロール。Events[].LogLine を順に表示（LogLine 空はスキップ＝Persistent
            // 剥奪等の「Event 必要・ログ不要」を自然に省略）。
            var bodyRect = new Rect(modal.x + 16, modal.y + 56, modal.width - 32, modal.height - 72);
            GUILayout.BeginArea(bodyRect);
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            if (report.Events != null)
            {
                for (int i = 0; i < shown; i++)
                {
                    var ev = report.Events[i];
                    if (ev != null && !string.IsNullOrEmpty(ev.LogLine))
                        GUILayout.Label(ev.LogLine, _bodyStyle);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 描画ユーティリティ

        /// <summary>
        /// HP バーを描画する。緑→黄→赤グラデの実 HP バーの後ろに、displayHp 位置の黄色トレイルを
        /// 重ねて「失った HP が徐々に縮む」演出を作る。drawText=true で中央に「HP/MaxHP」を描画。
        /// </summary>
        private void DrawHPBar(Rect rect, int hpNow, int hpMax, bool alive, float displayHp, bool drawText)
        {
            float ratio = hpMax > 0 ? Mathf.Clamp01(hpNow / (float)hpMax) : 0f;
            float displayRatio = hpMax > 0 ? Mathf.Clamp01(displayHp / (float)hpMax) : 0f;
            var prev = GUI.color;

            // 枠＝半透明黒
            GUI.color = ColorHpBarFrame;
            GUI.Box(rect, "");

            // 黄バー：displayHp > 実 HP の差分が「黄色く残る」（先描き＝後ろ側）
            if (alive && displayRatio > ratio + 0.001f)
            {
                var trailRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * displayRatio, rect.height - 2);
                GUI.color = ColorHpBarTrail;
                GUI.Box(trailRect, "");
            }

            // 実 HP バー：緑→黄→赤グラデ
            if (alive && ratio > 0f)
            {
                var fillRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * ratio, rect.height - 2);
                Color fill;
                if (ratio > 0.5f) fill = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
                else              fill = Color.Lerp(Color.red,    Color.yellow, ratio * 2f);
                GUI.color = fill;
                GUI.Box(fillRect, "");
            }

            if (drawText)
            {
                GUI.color = Color.white;
                GUI.Label(rect, $"{hpNow}/{hpMax}", _hpTextStyle);
            }
            GUI.color = prev;
        }

        private static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // ── シナジーバッジ（アリーナ左上重畳・常時表示・ホバーで詳細） ──

        // 3 種類の属性（火/水/光）の発動段階を視覚化。味方陣営限定発動なので AllyLineup から
        // Element 体数を集計し、SynergyDefinitions の Tier から効果内容を読み出す。
        private void DrawSynergyBadges(Rect arenaRect, BattleReport report)
        {
            if (report?.AllyLineup == null) return;

            _synergyBadgeCounts[0] = CountElementInLineup(report.AllyLineup, Element.Fire);
            _synergyBadgeCounts[1] = CountElementInLineup(report.AllyLineup, Element.Water);
            _synergyBadgeCounts[2] = CountElementInLineup(report.AllyLineup, Element.Light);

            _hoveredSynergyIdx = -1;
            var mouse = Event.current != null ? Event.current.mousePosition : new Vector2(-1, -1);
            // モーダル表示中（GUI.enabled=false）はホバー反応もさせない
            bool hoverEnabled = GUI.enabled;

            float x = arenaRect.x + SynergyBadgePad;
            float y = arenaRect.y + SynergyBadgePad;
            for (int i = 0; i < 3; i++)
            {
                var rect = new Rect(x + i * (SynergyBadgeSize + SynergyBadgeGap), y,
                                    SynergyBadgeSize, SynergyBadgeSize);
                _synergyBadgeRects[i] = rect;
                DrawSingleSynergyBadge(rect, i, _synergyBadgeCounts[i]);
                if (hoverEnabled && rect.Contains(mouse)) _hoveredSynergyIdx = i;
            }
        }

        // 単一バッジの描画。画像があれば画像、無ければ属性カラー帯＋「F2/W4/L0」文字フォールバック。
        private void DrawSingleSynergyBadge(Rect rect, int slotIdx, int count)
        {
            bool active = IsSynergyActive(slotIdx, count);
            var element = SlotElement(slotIdx);
            var icon = LoadSynergyIcon(element);

            if (icon != null)
            {
                // 画像描画：未発動時は彩度を落とす（GUI.color で擬似グレースケール）
                var prev = GUI.color;
                GUI.color = active ? Color.white : new Color(0.55f, 0.55f, 0.6f, 0.85f);
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                GUI.color = prev;
                // 右下に Lv 数字を重ねる
                var lvBg = new Rect(rect.xMax - 14, rect.yMax - 14, 12, 12);
                FillRect(lvBg, new Color(0f, 0f, 0f, 0.7f));
                GUI.Label(lvBg, count.ToString(), _synergyBadgeStyle);
            }
            else
            {
                // 文字フォールバック：背景＋上端属性カラー帯＋中央「F2」
                FillRect(rect, ColorSynergyBadgeBg);
                var band = new Rect(rect.x, rect.y, rect.width, 3f);
                FillRect(band, active ? SlotActiveColor(slotIdx) : ColorSynergyInactive);
                GUI.Label(new Rect(rect.x, rect.y + 3f, rect.width, rect.height - 3f),
                    $"{SlotLetter(slotIdx)}{count}", _synergyBadgeStyle);
            }
        }

        // ホバー中バッジのツールチップ（OnGUI 末尾で描画・モーダル中は呼び出されない）。
        private void DrawSynergyTooltip()
        {
            if (_hoveredSynergyIdx < 0) return;

            int count = _synergyBadgeCounts[_hoveredSynergyIdx];
            var def = SlotDef(_hoveredSynergyIdx);
            int tierIdx = count >= def.Tiers.Count ? def.Tiers.Count - 1 : count;
            if (tierIdx < 0) tierIdx = 0;
            var tier = def.Tiers[tierIdx];

            string title = $"{def.SourceAbilityName} Lv{count}";
            string body = (tier == null || tier.Buffs == null || tier.Buffs.Count == 0)
                ? "未発動（Lv2 以上で発動）"
                : FormatSynergyBuffs(tier);

            var src = _synergyBadgeRects[_hoveredSynergyIdx];
            float x = src.x;
            float y = src.yMax + 4f;
            if (x + SynergyTooltipW > Screen.width - 8f) x = Screen.width - 8f - SynergyTooltipW;
            var rect = new Rect(x, y, SynergyTooltipW, SynergyTooltipH);
            FillRect(rect, ColorSynergyTooltipBg);
            // 縁取り（薄白 1px の上下左右）
            FillRect(new Rect(rect.x, rect.y, rect.width, 1f), ColorSynergyTooltipEdge);
            FillRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ColorSynergyTooltipEdge);
            FillRect(new Rect(rect.x, rect.y, 1f, rect.height), ColorSynergyTooltipEdge);
            FillRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), ColorSynergyTooltipEdge);

            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, 18), title, _synergyTooltipTitleStyle);
            GUI.Label(new Rect(rect.x + 8, rect.y + 22, rect.width - 16, 20), body, _synergyTooltipBodyStyle);
        }

        // ── ヘルパ ──

        private static int CountElementInLineup(List<RuntimeUnit> lineup, Element element)
        {
            int n = 0;
            for (int i = 0; i < lineup.Count; i++)
            {
                var u = lineup[i];
                if (u?.BaseUnit != null && u.BaseUnit.UnitElement == element) n++;
            }
            return n;
        }

        // Tier が空（SynergyTier.Empty）でなければ発動扱い。Lv0/1 は通常 Empty。
        private static bool IsSynergyActive(int slotIdx, int count)
        {
            var def = SlotDef(slotIdx);
            if (def.Tiers.Count == 0) return false;
            int idx = count >= def.Tiers.Count ? def.Tiers.Count - 1 : count;
            if (idx < 0) return false;
            var tier = def.Tiers[idx];
            return tier?.Buffs != null && tier.Buffs.Count > 0;
        }

        private static SynergyDefinition SlotDef(int slotIdx) =>
            slotIdx == 0 ? SynergyDefinitions.Fire :
            slotIdx == 1 ? SynergyDefinitions.Water :
                           SynergyDefinitions.Light;

        private static Element SlotElement(int slotIdx) =>
            slotIdx == 0 ? Element.Fire :
            slotIdx == 1 ? Element.Water :
                           Element.Light;

        private static string SlotLetter(int slotIdx) =>
            slotIdx == 0 ? "F" : slotIdx == 1 ? "W" : "L";

        private Color SlotActiveColor(int slotIdx) =>
            slotIdx == 0 ? ColorSynergyFireActive :
            slotIdx == 1 ? ColorSynergyWaterActive :
                           ColorSynergyLightActive;

        // 「攻撃+20 / 盾×3」のような複数 Buff を 1 行で要約。
        private static string FormatSynergyBuffs(SynergyTier tier)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < tier.Buffs.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                var b = tier.Buffs[i];
                sb.Append(FormatEffectKindLabel(b.Kind));
                if (b.Kind == EffectKind.Shield)
                {
                    int stacks = b.InitialStacks > 0 ? b.InitialStacks : 1;
                    sb.Append('×').Append(stacks);
                }
                else
                {
                    sb.Append('+').Append(b.Magnitude);
                }
            }
            return sb.ToString();
        }

        private static string FormatEffectKindLabel(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.OutgoingDamageUp: return "攻撃";
                case EffectKind.DefenseUp:        return "防御";
                case EffectKind.Shield:           return "盾";
                case EffectKind.HealOverTime:     return "継続回復";
                default:                          return kind.ToString();
            }
        }

        // 画像 → 文字フォールバック切替の単純な遅延ローダ。
        // Resources/Icons/Synergy/synergy_{fire|water|light}.png に画像を置けば自動採用される。
        private static Texture2D LoadSynergyIcon(Element element)
        {
            if (_synergyIconCache.TryGetValue(element, out var cached)) return cached;
            if (_synergyIconMissing.Contains(element)) return null;
            string fileName =
                element == Element.Fire  ? "synergy_fire"  :
                element == Element.Water ? "synergy_water" :
                element == Element.Light ? "synergy_light" : null;
            if (fileName == null) { _synergyIconMissing.Add(element); return null; }
            var tex = Resources.Load<Texture2D>($"Icons/Synergy/{fileName}");
            if (tex != null) { _synergyIconCache[element] = tex; return tex; }
            _synergyIconMissing.Add(element);
            return null;
        }

        // ── 状態異常バッジ（各ユニット画像上端の左／右に重畳・ホバーで詳細） ──

        private void DrawStatusEffectBadges(Rect iconRect, RuntimeUnit unit, bool isAlly)
        {
            if (unit == null) return;
            if (!_effectsSnapshot.TryGetValue(unit, out var effects)) return;
            if (effects == null || effects.Count == 0) return;

            int total = effects.Count;
            int shown = total > StatusBadgeMax ? StatusBadgeMax : total;
            // 上限超過時は最後尾を「+N」バッジで使うため shown-1 を実バッジに割り当てる
            int realBadges = total > StatusBadgeMax ? StatusBadgeMax - 1 : shown;
            int rowWidth = shown;

            // バッジ列の基準位置：ユニット画像の上端からやや下寄り（+50px）に配置。
            // iconRect は DrawSlot で rect.y - 45f に上拡張されているため、セル枠基準では 5px 下相当。
            // 前列がアリーナ上端ぎりぎりに来てもバッジが画面外に隠れない安全マージン。
            float baseY = iconRect.y + 50f;
            float rowW  = rowWidth * StatusBadgeW + (rowWidth - 1) * StatusBadgeGap;
            float startX = isAlly
                ? iconRect.x + 6f                          // 左上から右へ
                : iconRect.xMax - 6f - rowW;               // 右上から左へ

            var mouse = Event.current != null ? Event.current.mousePosition : new Vector2(-1, -1);
            bool hoverEnabled = GUI.enabled;

            for (int i = 0; i < realBadges; i++)
            {
                var eff = effects[i];
                if (eff == null) continue;
                var rect = new Rect(startX + i * (StatusBadgeW + StatusBadgeGap),
                                    baseY, StatusBadgeW, StatusBadgeH);
                DrawSingleStatusBadge(rect, eff);
                if (hoverEnabled && rect.Contains(mouse))
                {
                    _hoveredStatusEffect = eff;
                    _hoveredStatusEffectRect = rect;
                }
            }

            // 表示しきれない分は最後に「+N」バッジ
            if (total > StatusBadgeMax)
            {
                int hidden = total - (StatusBadgeMax - 1);
                var rect = new Rect(startX + (StatusBadgeMax - 1) * (StatusBadgeW + StatusBadgeGap),
                                    baseY, StatusBadgeW, StatusBadgeH);
                FillRect(rect, ColorStatusBadgeBg);
                DrawBadgeBorder(rect, ColorTextMuted);
                GUI.Label(rect, $"+{hidden}", _statusBadgeStyle);
            }
        }

        private void DrawSingleStatusBadge(Rect rect, EffectChange effect)
        {
            var icon = LoadStatusIcon(effect.Kind);
            if (icon != null)
            {
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                // 文字フォールバック：半透明黒背景＋カテゴリ色 1px 枠＋ 2 文字
                FillRect(rect, ColorStatusBadgeBg);
                DrawBadgeBorder(rect, GetCategoryBorderColor(effect));
                GUI.Label(rect, FormatKindShort(effect.Kind), _statusBadgeStyle);
            }

            // スタック数表示（2 以上のときのみ右下に小さく）
            if (effect.Stacks >= 2)
            {
                var st = new Rect(rect.xMax - 11f, rect.yMax - 10f, 11f, 10f);
                FillRect(st, ColorStatusStackBg);
                GUI.Label(st, effect.Stacks.ToString(), _statusStackStyle);
            }
        }

        private void DrawStatusEffectTooltip()
        {
            if (_hoveredStatusEffect == null) return;
            var eff = _hoveredStatusEffect;

            string title = $"{FormatKindLong(eff.Kind)}（{GetCategoryLabel(eff)}）";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(eff.SourceAbilityName))
            {
                sb.Append("源：").Append(eff.SourceAbilityName).Append('\n');
            }
            if (eff.Stacks >= 2) sb.Append("ｽﾀｯｸ ×").Append(eff.Stacks).Append(' ');
            if (eff.Lifetime == Lifetime.Triggered && eff.RemainingTurns >= 0)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append(" / ");
                sb.Append("残り ").Append(eff.RemainingTurns).Append('T');
            }
            else if (eff.Lifetime == Lifetime.Permanent)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append(" / ");
                sb.Append("永続");
            }
            string body = sb.ToString();
            if (string.IsNullOrWhiteSpace(body)) body = "（詳細なし）";

            var src = _hoveredStatusEffectRect;
            float x = src.x;
            float y = src.yMax + 4f;
            // 画面外はみ出し対策
            if (x + StatusTooltipW > Screen.width - 8f) x = Screen.width - 8f - StatusTooltipW;
            if (y + StatusTooltipH > Screen.height - 8f) y = src.y - StatusTooltipH - 4f;
            var rect = new Rect(x, y, StatusTooltipW, StatusTooltipH);
            FillRect(rect, ColorSynergyTooltipBg);
            FillRect(new Rect(rect.x, rect.y, rect.width, 1f), ColorSynergyTooltipEdge);
            FillRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ColorSynergyTooltipEdge);
            FillRect(new Rect(rect.x, rect.y, 1f, rect.height), ColorSynergyTooltipEdge);
            FillRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), ColorSynergyTooltipEdge);

            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, 18), title, _statusTooltipTitleStyle);
            GUI.Label(new Rect(rect.x + 8, rect.y + 22, rect.width - 16, rect.height - 26), body, _statusTooltipBodyStyle);
        }

        // ── 状態異常バッジヘルパ ──

        private static void DrawBadgeBorder(Rect rect, Color color)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            FillRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            FillRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            FillRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        // Cleansable 優先 → Permanent → Triggered の順でカテゴリ判定。
        // 同じカテゴリでも IsUndispellable は別軸（Persistent の中でも消せる/消せない）だが、
        // バッジ視覚化では Persistent 1 色にまとめる。
        private static Color GetCategoryBorderColor(EffectChange e)
        {
            if (e.IsCleansable) return ColorStatusCleansable;
            if (e.Lifetime == Lifetime.Permanent) return ColorStatusPersistent;
            return ColorStatusTriggered;
        }

        private static string GetCategoryLabel(EffectChange e)
        {
            if (e.IsCleansable) return "Cleansable";
            if (e.Lifetime == Lifetime.Permanent) return "Persistent";
            return "Triggered";
        }

        // 英字 2 文字短縮（DefenseUp と OutgoingDamageUp が衝突しないよう注意）
        private static string FormatKindShort(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.AttackUp:           return "AU";
                case EffectKind.AttackDown:         return "AD";
                case EffectKind.DefenseUp:          return "DU";
                case EffectKind.DefenseDown:        return "DD";
                case EffectKind.EvasionUp:          return "EU";
                case EffectKind.EvasionDown:        return "ED";
                case EffectKind.OutgoingDamageUp:   return "OD";
                case EffectKind.IncomingDamageDown: return "ID";
                case EffectKind.CriticalRateUp:     return "CR";
                case EffectKind.CounterDamageUp:   return "CN";
                case EffectKind.SearingWound:       return "SW";
                case EffectKind.Burn:               return "Br";
                case EffectKind.HealOverTime:       return "HT";
                case EffectKind.Freeze:             return "Fz";
                case EffectKind.Paralysis:          return "Pa";
                case EffectKind.Curse:              return "Cs";
                case EffectKind.Shield:             return "Sh";
                case EffectKind.SilencedCounter:    return "SC";
                case EffectKind.ReviveInvalid:      return "RI";
                case EffectKind.IgnoreCounter:      return "IC";
                case EffectKind.SelfDefenseGuard:   return "SG";
                default:                            return "??";
            }
        }

        private static string FormatKindLong(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.AttackUp:           return "攻撃 UP";
                case EffectKind.AttackDown:         return "攻撃 DOWN";
                case EffectKind.DefenseUp:          return "防御 UP";
                case EffectKind.DefenseDown:        return "防御 DOWN";
                case EffectKind.EvasionUp:          return "回避 UP";
                case EffectKind.EvasionDown:        return "回避 DOWN";
                case EffectKind.OutgoingDamageUp:   return "与ダメ UP";
                case EffectKind.IncomingDamageDown: return "被ダメ DOWN";
                case EffectKind.CriticalRateUp:     return "クリ率 UP";
                case EffectKind.CounterDamageUp:    return "反撃 UP";
                case EffectKind.SearingWound:       return "熱傷";
                case EffectKind.Burn:               return "燃焼";
                case EffectKind.HealOverTime:       return "継続回復";
                case EffectKind.Freeze:             return "凍結";
                case EffectKind.Paralysis:          return "麻痺";
                case EffectKind.Curse:              return "呪い";
                case EffectKind.Shield:             return "盾";
                case EffectKind.SilencedCounter:    return "反撃封印";
                case EffectKind.ReviveInvalid:      return "蘇生不可";
                case EffectKind.IgnoreCounter:      return "無反撃";
                case EffectKind.SelfDefenseGuard:   return "防御態勢";
                default:                            return kind.ToString();
            }
        }

        // 画像 → 文字フォールバック切替の単純な遅延ローダ。
        // Resources/Icons/StatusEffects/status_{kind}.png に画像を置けば自動採用される。
        private static Texture2D LoadStatusIcon(EffectKind kind)
        {
            if (_statusIconCache.TryGetValue(kind, out var cached)) return cached;
            if (_statusIconMissing.Contains(kind)) return null;
            string fileName = $"status_{kind.ToString().ToLowerInvariant()}";
            var tex = Resources.Load<Texture2D>($"Icons/StatusEffects/{fileName}");
            if (tex != null) { _statusIconCache[kind] = tex; return tex; }
            _statusIconMissing.Add(kind);
            return null;
        }

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            GuiTheme.EnsureJapaneseFont();

            _titleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 22, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _bodyStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft, wordWrap = true,
            };
            // 戦闘ステータスリストのユニット名行専用：fontSize 小さめ＋ wordWrap 無効＋クリップで枠超え抑止。
            _statusListNameStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 12, normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft, wordWrap = false,
                clipping = TextClipping.Clip,
            };
            // 1 行ログ専用：wordWrap=false で折り返さず、Rect 外ははみ出さない（自動クリップ）。
            _logLineStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft, wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _mutedStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14, normal = { textColor = ColorTextMuted },
                alignment = TextAnchor.MiddleLeft,
            };
            _hpTextStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 12, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter,
            };
            _synergyBadgeStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter,
            };
            _synergyTooltipTitleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _synergyTooltipBodyStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14,
                normal = { textColor = new Color(0.85f, 0.85f, 0.88f) },
                alignment = TextAnchor.MiddleLeft, wordWrap = true,
            };
            _statusBadgeStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter,
            };
            _statusStackStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleCenter,
            };
            _statusTooltipTitleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorText },
                alignment = TextAnchor.MiddleLeft,
            };
            _statusTooltipBodyStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14,
                normal = { textColor = new Color(0.85f, 0.85f, 0.88f) },
                alignment = TextAnchor.MiddleLeft, wordWrap = true,
            };

            _stylesBuilt = true;
        }
    }
}
