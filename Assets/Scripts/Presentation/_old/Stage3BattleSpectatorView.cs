// Assets/Scripts/UnityView/Stage3BattleSpectatorView.cs
// 段階4 §11.3：1戦線のバトル進行を観戦するための OnGUI ビュー（プレースホルダー版）。
//
// 設計方針：
// - BattleReport.Events を時間軸で再生し、HPバー・行動者ハイライト・被弾フラッシュ・死亡グレーアウトを描画する。
// - MonoBehaviour ではなくプレーンクラスにし、Tick(deltaTime)/DrawGUI(area) を Stage3CampaignGUI から呼ぶ統合形を採る。
//   これで OnGUI のレイヤリング問題を回避し、観戦中はキャンペーンUIの一部分として描画できる。
// - ユニット表示は現状プレースホルダー矩形＋名前テキスト。Step 4-1 でアイコンが揃ったら DrawUnitIcon() の中身を差し替える。
// - 倍速・スキップは Step 4-3 で外から SecondsPerEvent / SkipToEnd を制御する想定。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Models;
using Echolos.Domain.Prototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>1戦線の戦闘進行を再生する OnGUI ビュー。プレースホルダー描画版。</summary>
    public sealed class Stage3BattleSpectatorView
    {
        // ══════════════════════════════════════════════
        // 設定
        // ══════════════════════════════════════════════

        /// <summary>1イベント表示あたりの秒数。倍速時は短くする。</summary>
        public float SecondsPerEvent { get; set; } = 0.5f;

        /// <summary>戦線名（"平原"等）。タイトルバーに表示。</summary>
        public string Title { get; set; }

        /// <summary>再生完了したか。Tick 後にこれを確認して次戦線に進める。</summary>
        public bool IsFinished { get; private set; }

        // ══════════════════════════════════════════════
        // 内部状態
        // ══════════════════════════════════════════════

        private BattleReport _report;
        private int _cursor;
        private float _accumDelay;

        // 表示用ステート（イベント列を進めるたびに更新）。RuntimeUnit 参照キーで保持。
        private readonly Dictionary<RuntimeUnit, int> _hp = new Dictionary<RuntimeUnit, int>();
        private readonly Dictionary<RuntimeUnit, bool> _alive = new Dictionary<RuntimeUnit, bool>();

        // 直近の行動表示用：行動者・対象・技名・フラッシュ残時間
        private RuntimeUnit _lastActor;
        private List<RuntimeUnit> _lastTargets;
        private string _lastWazaName;
        private RuntimeUnit _lastHitTarget;
        private string _lastHitText;
        // §11.5 Step 4-13a A7：ダメージ色分け（赤=被ダメ／緑=回復／白=回避）。
        // _lastHitText と対で更新し、描画時にこの色を文字色として使う。
        private Color _lastHitColor = Color.white;

        // 暗いアイコン上でも視認できるよう原色より少し明るめに寄せた赤と緑。
        private static readonly Color DamageRed = new Color(1f, 0.35f, 0.35f);
        private static readonly Color HealGreen = new Color(0.4f, 1f, 0.45f);

        // §11.5 Step 4-13a A4：ターゲット線
        // ShowTargetLines が true のとき、_lastActor から _lastTargets[*] への線を描く。
        // コマ送り or ×1 のときだけ ON にする想定（呼び出し側で制御）。
        public bool ShowTargetLines { get; set; } = false;
        // 各スロット描画時に rect を記録しておき、ターゲット線描画時に「中心→中心」の座標として使う。
        private readonly Dictionary<RuntimeUnit, Rect> _slotRectCache = new Dictionary<RuntimeUnit, Rect>();
        // ターゲット線の色（攻撃=赤・支援=緑・A7 のポップアップ色と同系統で配色一貫性を確保）。
        private static readonly Color AttackLineColor  = new Color(1f, 0.35f, 0.35f, 0.85f);
        private static readonly Color SupportLineColor = new Color(0.4f, 1f, 0.45f, 0.85f);
        private const float TargetLineThickness = 3f;
        private const float ArrowHeadSize = 12f;        // 矢印ヘッド（target 側・三角形のサイズ）
        private const float ActorDotSize   = 8f;        // actor 側起点マーク（小四角）
        // スロット中心からの内接半径比率（min(width, height) に掛ける）。
        // 0.4 でスロット内のアイコン外周あたり。ここから線を発着させて「アイコン同士を結ぶ」見た目にする。
        private const float SlotRadiusRatio = 0.4f;

        // §11.5 Step 4-13a A10 Phase 1.5-b（2026-06-04）：
        // バフ/デバフ/状態異常の動的表示。RuntimeUnit.ActiveEffects を直接参照すると
        // 戦闘終了時のスナップショットしか見えないため、BattleEvent の
        // StatusEffectApplied / StatusEffectExpired を再生して各ユニットの「現在表示すべき効果リスト」を
        // 時系列で組み立てる。
        //
        // List<StatusEffectType> を採用（HashSet ではなく）：付与順を維持して
        // バッジ並びの視覚的安定性を確保するため。同種効果のスタック重ねがけは
        // 重複追加しない（Contains チェック）。
        private readonly Dictionary<RuntimeUnit, List<StatusEffectType>> _activeEffects =
            new Dictionary<RuntimeUnit, List<StatusEffectType>>();

        // §11.5 Step 4-13a A6：HPバー減少視覚化（黄バー残し補間）。
        // _hp（実HP）に対し _displayHp は表示用の補間値。減少時のみゆっくり追従させて
        // 「ダメージで失った分」を黄色いバーとして残し、徐々に縮ませる演出を作る。
        // 増加（回復）は瞬時追従＝黄バー演出は出さない。
        private readonly Dictionary<RuntimeUnit, float> _displayHp = new Dictionary<RuntimeUnit, float>();

        // 黄バー（残HP演出）の色。視認性のため少しオレンジ寄りの明るい黄。
        private static readonly Color HpBarTrailYellow = new Color(1f, 0.85f, 0.2f, 0.85f);

        // 補間時間 = SecondsPerEvent × この係数。1イベント内で確実に追いつくよう 0.7（70%）に設定。
        // 倍速時は SecondsPerEvent 自体が縮むので、補間も自然にスケールする。
        private const float HpBarCatchupRatio = 0.7f;
        private float _flashTimer;

        private int _currentTurn;
        private BattleResult _resultIfEnded = BattleResult.None;

        // 毎フレーム new していた一時 GUIStyle のキャッシュ。OnGUI の最初のアクセスで遅延構築する。
        private GUIStyle _slotNameStyle;
        private GUIStyle _slotHitStyle;
        private GUIStyle _hpTextStyle;
        private void EnsureSlotStyles()
        {
            if (_slotNameStyle == null)
            {
                _slotNameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };
                _slotNameStyle.normal.textColor = Color.white;
            }
            if (_slotHitStyle == null)
            {
                _slotHitStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold,
                    fontSize = 14,
                };
                _slotHitStyle.normal.textColor = Color.white;
            }
            if (_hpTextStyle == null)
            {
                _hpTextStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                };
                _hpTextStyle.normal.textColor = Color.white;
            }
        }

        // ══════════════════════════════════════════════
        // 公開 API
        // ══════════════════════════════════════════════

        /// <summary>新しいレポートで再生を開始する。</summary>
        public void Initialize(BattleReport report, string title)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            Title = title;
            _cursor = 0;
            _accumDelay = 0f;
            IsFinished = false;
            _flashTimer = 0f;
            _currentTurn = 0;
            _resultIfEnded = BattleResult.None;
            _lastActor = null;
            _lastTargets = null;
            _lastWazaName = null;
            _lastHitTarget = null;
            _lastHitText = null;
            _lastHitColor = Color.white;

            _hp.Clear();
            _alive.Clear();
            _displayHp.Clear();
            // §11.5 Step 4-13a A10 Phase 1.5-b：状態効果リストも初期化（空からスタート）。
            // BattleRunner が Turn=0 の StatusEffectApplied イベントとして既存付与
            // （Cover タグ持ちの永続 Cover など）を report.Events 冒頭に記録しているので、
            // 再生開始すぐに正しい初期状態へ巻き戻る。
            _activeEffects.Clear();
            // 編成スナップショットから初期HP/生存状態を構築。MaxHPは兵種強化込みで RuntimeUnit が計算する。
            // §11.5 Step 4-13a A6：displayHp も初期 MaxHP で同期させておく（最初は黄バー無し）。
            if (report.AllyLineup != null)
                foreach (var u in report.AllyLineup)
                {
                    _hp[u] = u.MaxHP; _alive[u] = true; _displayHp[u] = u.MaxHP;
                    _activeEffects[u] = new List<StatusEffectType>();
                }
            if (report.EnemyLineup != null)
                foreach (var u in report.EnemyLineup)
                {
                    _hp[u] = u.MaxHP; _alive[u] = true; _displayHp[u] = u.MaxHP;
                    _activeEffects[u] = new List<StatusEffectType>();
                }

            // 編成不在（味方放置 等）は最初から終了扱いに。
            if (report.Events == null || report.Events.Count == 0)
                IsFinished = true;
        }

        /// <summary>Update から呼ぶ。deltaTime を蓄積して、イベントを順次適用する。</summary>
        public void Tick(float deltaTime)
        {
            if (IsFinished || _report == null) return;

            if (_flashTimer > 0f) _flashTimer = Mathf.Max(0f, _flashTimer - deltaTime);

            _accumDelay += deltaTime;
            // 倍速設定が極端なら複数イベントを一度に進める
            while (_accumDelay >= SecondsPerEvent && _cursor < _report.Events.Count)
            {
                ApplyEvent(_report.Events[_cursor++]);
                _accumDelay -= SecondsPerEvent;
            }
            if (_cursor >= _report.Events.Count) IsFinished = true;

            // §11.5 Step 4-13a A6：黄バー補間更新（時間ベース・倍速時は SecondsPerEvent が縮むので自然にスケール）。
            UpdateDisplayHp(deltaTime);
        }

        /// <summary>スキップ：残りイベントを一気に適用して最終状態にする。</summary>
        public void SkipToEnd()
        {
            if (_report == null) return;
            while (_cursor < _report.Events.Count)
                ApplyEvent(_report.Events[_cursor++]);
            IsFinished = true;

            // §11.5 Step 4-13a A6：一気にスキップした場合は黄バーも即時追従させる（演出残しの違和感を避ける）。
            SnapDisplayHpToReal();
        }

        /// <summary>
        /// 手動ステップ：1イベントだけ進める。ステップモード（自動Tick停止中）で使う。
        /// 進めたあと _flashTimer をフラッシュ寿命にセットしておくので、Tick が呼ばれなくても
        /// 「直近に起きたこと」が画面に残る。
        /// </summary>
        public void StepOne()
        {
            if (IsFinished || _report == null) return;
            if (_cursor < _report.Events.Count)
            {
                ApplyEvent(_report.Events[_cursor++]);
                if (_cursor >= _report.Events.Count) IsFinished = true;
            }
        }

        // ══════════════════════════════════════════════
        // イベント適用
        // ══════════════════════════════════════════════

        private void ApplyEvent(BattleEvent ev)
        {
            _currentTurn = ev.Turn;
            switch (ev.Kind)
            {
                case BattleEventKind.ActionDeclared:
                    _lastActor = ev.Actor;
                    _lastTargets = ev.Targets;
                    _lastWazaName = ev.WazaName;
                    _lastHitTarget = null;
                    _lastHitText = null;
                    // 新しい行動が始まったので前の被弾フラッシュは終了させる（ステップモード時にも次の行動者
                    // ハイライトが即出るようにするため）。
                    _flashTimer = 0f;
                    break;
                case BattleEventKind.HitLanded:
                    if (_hp.ContainsKey(ev.Target)) _hp[ev.Target] = ev.TargetHPAfter;
                    _lastHitTarget = ev.Target;
                    _lastHitText = $"-{ev.Damage}";
                    _lastHitColor = DamageRed;
                    _flashTimer = SecondsPerEvent * 1.5f;
                    break;
                case BattleEventKind.Healed:
                    // §11.5 Step 4-13a A7：回復は緑色 "+X" で表示。HitLanded と対の経路。
                    if (_hp.ContainsKey(ev.Target)) _hp[ev.Target] = ev.TargetHPAfter;
                    _lastHitTarget = ev.Target;
                    _lastHitText = $"+{ev.HealAmount}";
                    _lastHitColor = HealGreen;
                    _flashTimer = SecondsPerEvent * 1.5f;
                    break;
                case BattleEventKind.Evaded:
                    _lastHitTarget = ev.Target;
                    _lastHitText = "回避";
                    _lastHitColor = Color.white;
                    _flashTimer = SecondsPerEvent * 1.5f;
                    break;
                case BattleEventKind.Died:
                    if (_alive.ContainsKey(ev.Target)) _alive[ev.Target] = false;
                    break;
                case BattleEventKind.BurnTick:
                    if (_hp.ContainsKey(ev.Target)) _hp[ev.Target] = ev.TargetHPAfter;
                    _lastHitTarget = ev.Target;
                    _lastHitText = $"☠{ev.Damage}";
                    _lastHitColor = DamageRed;
                    _flashTimer = SecondsPerEvent * 1.5f;
                    break;
                case BattleEventKind.ActionSkipped:
                    _lastActor = ev.Actor;
                    _lastTargets = null;
                    _lastWazaName = ev.SkipReason;
                    _lastHitTarget = null;
                    _lastHitText = null;
                    break;
                case BattleEventKind.StatusEffectApplied:
                    // §11.5 Step 4-13a A10 Phase 1.5-b：効果付与を表示リストに反映。
                    // 既に同種が入っている場合（スタック重ねがけ等）は重複追加しない。
                    if (ev.Target != null && _activeEffects.TryGetValue(ev.Target, out var addList))
                    {
                        if (!addList.Contains(ev.EffectType))
                            addList.Add(ev.EffectType);
                    }
                    break;
                case BattleEventKind.StatusEffectExpired:
                    // 効果剥がれを表示リストから除外。スタック減算で消えるパターンも単一 Remove で OK
                    // （ロジック層は同種重ね追加を抑止しないがビュー層は重複追加していないため）。
                    if (ev.Target != null && _activeEffects.TryGetValue(ev.Target, out var rmList))
                        rmList.Remove(ev.EffectType);
                    break;
                case BattleEventKind.BattleEnd:
                    _resultIfEnded = ev.Result;
                    break;
                // TurnStart は _currentTurn 更新のみで描画ステート変更なし
            }
        }

        /// <summary>
        /// §11.5 Step 4-13a A10 Phase 1.5-b：指定ユニットに現在表示すべき状態効果タイプ列を返す。
        /// _activeEffects に登録があればその List を返し、なければ空 List（毎回 new はキャッシュで回避）。
        /// </summary>
        private static readonly List<StatusEffectType> _emptyEffectList = new List<StatusEffectType>();
        private IReadOnlyList<StatusEffectType> GetActiveEffectTypes(RuntimeUnit unit)
        {
            if (unit == null) return _emptyEffectList;
            return _activeEffects.TryGetValue(unit, out var list) ? list : _emptyEffectList;
        }

        // ══════════════════════════════════════════════
        // 描画
        // ══════════════════════════════════════════════

        /// <summary>OnGUI 内から呼ぶ。area の範囲に描画する。</summary>
        public void DrawGUI(Rect area)
        {
            if (_report == null) return;

            // WebGL でも日本語表示できるよう、フォントを毎フレーム適用する
            // （観戦中はキャンペーンGUI側 OnGUI ではなくここから描画される場合があるため、
            //  防御的に二重で呼んでも参照比較なので追加コストはほぼゼロ）。
            GuiTheme.EnsureJapaneseFont();

            EnsureSlotStyles();
            GuiTheme.FillRect(area, GuiTheme.PanelBg);
            GUILayout.BeginArea(area);

            DrawHeader();
            DrawArena(area);
            DrawActionFooter();

            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            string titleText = string.IsNullOrEmpty(Title) ? "戦闘" : Title;

            GUILayout.BeginHorizontal(GuiTheme.Panel, GUILayout.Height(34));
            // 戦線名（中見出し）
            GUILayout.Label($"【{titleText}】", GuiTheme.Subtitle, GUILayout.Width(140));

            if (IsFinished)
            {
                // 結果チップ：完勝/辛勝/惜敗/完敗 で色変え
                GuiTheme.DrawChip(ResultLabel(_resultIfEnded), ResultColor(_resultIfEnded), 100f, 26f);
            }
            else
            {
                // 進行中のターン表示
                GUILayout.Label($"T{_currentTurn}", GuiTheme.Stat, GUILayout.Width(48));
                GUILayout.Label($"{_cursor}/{_report.Events.Count}",
                    GuiTheme.Muted, GUILayout.Width(80));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>戦闘結果ごとのアクセント色。チップやオーバーレイで使う。</summary>
        private static Color ResultColor(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory:      return GuiTheme.AccentSuccess;
                case BattleResult.AdvantageousVictory: return GuiTheme.AccentWarning;
                case BattleResult.MarginalDefeat:      return GuiTheme.AccentDanger;
                case BattleResult.CrushingDefeat:      return GuiTheme.AccentDanger;
                default:                               return GuiTheme.TextMuted;
            }
        }

        private void DrawArena(Rect outer)
        {
            // ヘッダ・フッタを引いた中央の戦闘描画領域を計算する。
            const float headerH = 32f;
            const float footerH = 56f;
            float arenaTop = headerH + 6f;
            float arenaBottom = footerH + 6f;
            float arenaHeight = outer.height - arenaTop - arenaBottom;
            float arenaWidth = outer.width - 12f;

            // 左右で2分割：左=味方、右=敵
            float halfWidth = (arenaWidth - 16f) * 0.5f;
            var allyArea = new Rect(6f, arenaTop, halfWidth, arenaHeight);
            var enemyArea = new Rect(6f + halfWidth + 16f, arenaTop, halfWidth, arenaHeight);

            DrawTeamGrid(allyArea, _report.AllyLineup, isAlly: true);
            DrawTeamGrid(enemyArea, _report.EnemyLineup, isAlly: false);

            // §11.5 Step 4-13a A4：両陣営のスロット位置キャッシュが揃ったので、最前面にターゲット線を重ねる。
            DrawTargetLines();
        }

        /// <summary>
        /// §11.5 Step 4-13a A4：行動者→対象（複数可）への線を描画する。
        /// ShowTargetLines がオフ、または行動データが未設定なら何もしない。
        /// 攻撃線は赤・支援線（同陣営対象）は緑。配色は A7 ポップアップと一貫。
        /// </summary>
        private void DrawTargetLines()
        {
            if (!ShowTargetLines) return;
            if (_lastActor == null || _lastTargets == null || _lastTargets.Count == 0) return;
            if (!_slotRectCache.TryGetValue(_lastActor, out var actorRect)) return;

            bool actorIsAlly = _report?.AllyLineup != null && _report.AllyLineup.Contains(_lastActor);
            Vector2 actorCenter = actorRect.center;
            // スロットの内接半径相当（≒アイコン外周距離）。線はこの分内側から出して
            // ユニット絵を突き抜けず「アイコン同士を結ぶ」見た目にする。
            float actorRadius = Mathf.Min(actorRect.width, actorRect.height) * SlotRadiusRatio;

            // 起点マークの描画位置（actor 側）：actor アイコン外周ではなく中心に置く方が
            // 「ここが行動者」として目立つ。1 回だけ・最初の有効対象の色で。
            Color? actorMarkerColor = null;

            foreach (var target in _lastTargets)
            {
                if (target == null) continue;
                if (!_slotRectCache.TryGetValue(target, out var targetRect)) continue;

                bool targetIsAlly = _report?.AllyLineup != null && _report.AllyLineup.Contains(target);
                Color lineColor = (actorIsAlly == targetIsAlly) ? SupportLineColor : AttackLineColor;
                Vector2 targetCenter = targetRect.center;
                float targetRadius = Mathf.Min(targetRect.width, targetRect.height) * SlotRadiusRatio;

                // 方向ベクトル
                Vector2 diff = targetCenter - actorCenter;
                float dist = diff.magnitude;
                // 起点・終点・矢印頭が重なって潰れる距離なら描画しない（同陣営の隣接スロット同士など）。
                if (dist <= actorRadius + targetRadius + ArrowHeadSize) continue;
                Vector2 dir = diff / dist;

                // 線の起点は actor 外周、終点は target 外周より矢印頭の分さらに手前。
                Vector2 from = actorCenter + dir * actorRadius;
                Vector2 to   = targetCenter - dir * (targetRadius + ArrowHeadSize * 0.6f);

                DrawLine(from, to, TargetLineThickness, lineColor);
                DrawArrowHead(to, from, ArrowHeadSize, lineColor);

                if (!actorMarkerColor.HasValue) actorMarkerColor = lineColor;
            }

            // 起点マーク（actor 側に小四角・最後に描いて線の上に重ねる）
            if (actorMarkerColor.HasValue)
                DrawDot(actorCenter, ActorDotSize, actorMarkerColor.Value);
        }

        /// <summary>
        /// 2 点間に細長矩形を回転描画して線を引く。
        /// GUI.matrix を一時的に回転 → 描画 → 戻す方式。GL.Lines は OnGUI 内で使えないため代用する。
        /// </summary>
        private static void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
        {
            var prevColor = GUI.color;
            var prevMatrix = GUI.matrix;

            Vector2 diff = to - from;
            float length = diff.magnitude;
            if (length <= 0.01f) return;
            float angleDeg = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            GUIUtility.RotateAroundPivot(angleDeg, from);
            GUI.color = color;
            // from を矩形の左端中心に配置。thickness の半分だけ上にずらすことで線の中心軸が from-to を通る。
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        /// <summary>actor 側の起点マーク（小四角）。</summary>
        private static void DrawDot(Vector2 center, float size, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>
        /// 矢印ヘッド：tip を頂点に、from 方向に開いた三角形を 2 本の斜辺として描く。
        /// 厚みのある三角形は GUI 系では難しいため、線2本での疑似表現とする。
        /// </summary>
        private static void DrawArrowHead(Vector2 tip, Vector2 from, float size, Color color)
        {
            Vector2 diff = tip - from;
            float length = diff.magnitude;
            if (length <= 0.01f) return;
            Vector2 dir = diff / length;

            // tip から逆方向に size だけ戻った位置を「翼の基準点」、垂直方向に開く。
            Vector2 backCenter = tip - dir * size;
            Vector2 perp = new Vector2(-dir.y, dir.x); // 90 度回転（左手系）
            Vector2 leftWing  = backCenter + perp * (size * 0.6f);
            Vector2 rightWing = backCenter - perp * (size * 0.6f);

            // 矢印の 2 本の斜辺。線厚は本体線より少し太く（視認性のため）。
            DrawLine(leftWing,  tip, TargetLineThickness, color);
            DrawLine(rightWing, tip, TargetLineThickness, color);
        }

        /// <summary>
        /// 編成グリッドを縦3行×横2列で描く。各行は1レーン分（前列スロットと後列スロットのペア）。
        /// 味方陣営は「左=後列、右=前列」、敵陣営は「左=前列、右=後列」と鏡像配置にすることで、
        /// 画面中央軸で両陣営の前列がレーン同士で向き合う JRPG 風レイアウトになる。
        ///
        /// レーン対応：行0=(前0,後3) 行1=(前1,後4) 行2=(前2,後5)
        /// </summary>
        private void DrawTeamGrid(Rect area, List<RuntimeUnit> lineup, bool isAlly)
        {
            if (lineup == null) return;

            const int rows = 3;
            const float gap = 6f;
            float cellH = (area.height - gap * (rows - 1)) / rows;
            float cellW = (area.width - gap) * 0.5f;

            // 中央軸に「前列」を寄せる：味方は右が中央、敵は左が中央。
            int innerOffset = 0; // 中央側列のスロットオフセット（前列＝0）
            int outerOffset = 3; // 外側列のスロットオフセット（後列＝3）

            for (int row = 0; row < rows; row++)
            {
                float y = area.y + row * (cellH + gap);
                int innerSlot = innerOffset + row;
                int outerSlot = outerOffset + row;

                // 味方：左=外側(後列)、右=内側(前列)
                // 敵　：左=内側(前列)、右=外側(後列)
                int leftSlot = isAlly ? outerSlot : innerSlot;
                int rightSlot = isAlly ? innerSlot : outerSlot;

                var leftRect = new Rect(area.x, y, cellW, cellH);
                var rightRect = new Rect(area.x + cellW + gap, y, cellW, cellH);
                DrawSlot(leftRect, lineup, leftSlot, isAlly);
                DrawSlot(rightRect, lineup, rightSlot, isAlly);
            }
        }

        private void DrawSlot(Rect rect, List<RuntimeUnit> lineup, int slotIndex, bool isAlly)
        {
            RuntimeUnit unit = null;
            for (int i = 0; i < lineup.Count; i++)
                if (lineup[i].SlotIndex == slotIndex) { unit = lineup[i]; break; }

            // §11.5 Step 4-13a A4：ターゲット線描画用にスロット位置を記録（Repaint パス時のみ更新で十分）。
            if (unit != null && Event.current.type == EventType.Repaint)
                _slotRectCache[unit] = rect;

            // 空きスロット
            if (unit == null)
            {
                var prevBg = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.15f);
                GUI.Box(rect, "");
                GUI.color = prevBg;
                return;
            }

            bool alive = _alive.TryGetValue(unit, out var a) ? a : true;
            int hpNow = _hp.TryGetValue(unit, out var h) ? h : unit.MaxHP;
            bool isActor = _lastActor == unit && _flashTimer <= 0f; // 行動中ハイライト（被弾フラッシュ中は対象側を優先）
            bool isTargetHit = _lastHitTarget == unit && _flashTimer > 0f;
            bool isTargetDeclared = !isTargetHit && _lastTargets != null && _lastTargets.Contains(unit);

            bool hasIcon = IconRegistry.Get(unit.BaseUnit.Id) != null;

            // スロット背景：チビ素材は素材自身が背景グラデを持つので、アイコンありの平常時は団色を薄くして
            // 素材の見栄えを優先する。状態演出（被弾フラッシュ・行動者ハイライト・宣言対象・死亡）は
            // 視認性を保つため強めの色のままにする。
            Color bg;
            if (!alive) bg = new Color(0.25f, 0.25f, 0.25f, 0.7f);
            else if (isTargetHit) bg = new Color(0.9f, 0.3f, 0.3f, 0.85f);
            else if (isActor) bg = new Color(0.95f, 0.85f, 0.25f, 0.85f);
            else if (isTargetDeclared) bg = new Color(0.95f, 0.55f, 0.25f, 0.6f);
            else if (hasIcon) bg = isAlly
                ? new Color(0.25f, 0.45f, 0.85f, 0.18f)  // アイコンあり：素材を主役にするため薄め
                : new Color(0.85f, 0.4f, 0.4f, 0.18f);
            else bg = isAlly
                ? new Color(0.25f, 0.45f, 0.85f, 0.6f)   // アイコンなし：従来どおりの団色
                : new Color(0.85f, 0.4f, 0.4f, 0.6f);

            var prev = GUI.color;
            GUI.color = bg;
            GUI.Box(rect, "");
            GUI.color = prev;

            // §11.5 Step 4-1：アイコンがあれば上半分に表示、無ければ従来通りユニット名を中央に。
            // アイコンは Resources/Icons/{unitId}.png から自動ロード（IconRegistry）。
            // 右向き素材で統一しているため、敵スロット (isAlly=false) は左右反転して対峙構図にする。
            string nameText = $"{unit.BaseUnit.Name}";
            if (unit.BaseUnit.EnhancementLevel > 0)
                nameText += $"+{unit.BaseUnit.EnhancementLevel}";
            if (!alive) nameText = $"×{nameText}";

            if (hasIcon)
            {
                // 上 約70%：アイコン領域、下 約30%：名前
                float iconBottom = rect.yMax - 18 - 14;
                var iconRect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, iconBottom - (rect.y + 2));
                IconRegistry.TryDrawIcon(iconRect, unit.BaseUnit.Id, flipHorizontal: !isAlly);
                var nameRect = new Rect(rect.x + 2, iconBottom, rect.width - 4, 14);
                GUI.Label(nameRect, nameText, _slotNameStyle);

                // §11.5 Step 4-13a A11+B1：射程／かばうバッジをアイコン左上に重ねる（配置中と完全に同じ見た目）。
                UnitBadgeOverlay.Draw(iconRect, unit);
                // §11.5 Step 4-13a A10：バフ／デバフ／状態異常をアイコン下端寄りに横並びで重ねる。
                // Phase 1.5-b：観戦ビューが時系列追跡した効果リストを直接渡す（戦闘終了時スナップショット参照のバグ修正）。
                StatusEffectOverlay.Draw(iconRect, GetActiveEffectTypes(unit));
            }
            else
            {
                // アイコン未配置のフォールバック：従来のテキストのみ
                var nameRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 22);
                GUI.Label(nameRect, nameText, _slotNameStyle);

                // アイコンが無くてもバッジは情報源として有用なので、スロット左上に重ねる。
                UnitBadgeOverlay.Draw(rect, unit);
                // バフデバフも同様にスロット下端寄りに描画。
                StatusEffectOverlay.Draw(rect, GetActiveEffectTypes(unit));
            }

            // HPバー：下端18px帯
            var hpFrame = new Rect(rect.x + 4, rect.yMax - 16, rect.width - 8, 12);
            // §11.5 Step 4-13a A6：黄バー位置（_displayHp）。未登録の場合は実HPに落ちる＝黄バーは出ない。
            float displayHpForBar = _displayHp.TryGetValue(unit, out var dhp) ? dhp : hpNow;
            DrawHPBar(hpFrame, hpNow, unit.MaxHP, alive, displayHpForBar);

            // ヒット表示：対象なら右上にダメージ／回復文字（§11.5 Step 4-13a A7 色分け）
            if (isTargetHit && !string.IsNullOrEmpty(_lastHitText))
            {
                var hitRect = new Rect(rect.x + rect.width - 50, rect.y + 2, 48, 18);
                GUI.color = _lastHitColor;
                GUI.Label(hitRect, _lastHitText, _slotHitStyle);
                GUI.color = prev;
            }
        }

        /// <summary>
        /// §11.5 Step 4-13a A6：黄バーの補間更新。
        /// _displayHp を _hp に向けて減少時のみ時間ベースで追従させる。増加（回復）は瞬時追従＝黄バー演出なし。
        /// 補間時間は SecondsPerEvent × HpBarCatchupRatio（既定 0.7）。倍速時は SecondsPerEvent が縮むため自動でスケール。
        /// </summary>
        private void UpdateDisplayHp(float deltaTime)
        {
            if (_displayHp.Count == 0) return;

            float catchupSec = SecondsPerEvent * HpBarCatchupRatio;
            if (catchupSec <= 0f) catchupSec = 0.01f;

            // foreach 中にコレクション変更を避けるため、キーをスナップショットしてから走査する。
            var keys = new List<RuntimeUnit>(_displayHp.Keys);
            foreach (var key in keys)
            {
                float target = _hp.TryGetValue(key, out var realHp) ? realHp : 0f;
                float current = _displayHp[key];

                if (current > target)
                {
                    // 減少：ユニットの MaxHP を1イベント時間で渡り切る速度で追従。
                    float rate = key.MaxHP / catchupSec;
                    float next = current - rate * deltaTime;
                    _displayHp[key] = Mathf.Max(next, target);
                }
                else if (current < target)
                {
                    // 増加：瞬時追従（回復時の演出は緑バーが伸びるのみ・黄バーは出さない）
                    _displayHp[key] = target;
                }
            }
        }

        /// <summary>スキップ時の即時同期。黄バーの取り残しを起こさない。</summary>
        private void SnapDisplayHpToReal()
        {
            var keys = new List<RuntimeUnit>(_displayHp.Keys);
            foreach (var key in keys)
            {
                if (_hp.TryGetValue(key, out var realHp))
                    _displayHp[key] = realHp;
            }
        }

        /// <summary>
        /// HPバー描画。緑/赤の実HPバーの後ろに、黄バー（displayHp 位置）を重ねて
        /// 「失ったHPが徐々に縮む」演出を作る（§11.5 Step 4-13a A6）。
        /// </summary>
        private void DrawHPBar(Rect rect, int hpNow, int hpMax, bool alive, float displayHp)
        {
            float ratio = hpMax > 0 ? Mathf.Clamp01(hpNow / (float)hpMax) : 0f;
            float displayRatio = hpMax > 0 ? Mathf.Clamp01(displayHp / (float)hpMax) : 0f;
            var prev = GUI.color;

            // 枠＝半透明黒
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.Box(rect, "");

            // 黄バー（残HP演出）：displayHp が実HPより大きい時のみ、その差分が「黄色く残る」見た目になる。
            // 描画順は「先に displayHp 幅で黄、後に実HP幅で緑/赤」とすることで、緑/赤が前面・黄が後ろに残る。
            if (alive && displayRatio > ratio + 0.001f)
            {
                var trailRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * displayRatio, rect.height - 2);
                GUI.color = HpBarTrailYellow;
                GUI.Box(trailRect, "");
            }

            if (alive && ratio > 0f)
            {
                var fillRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * ratio, rect.height - 2);
                // 緑→黄→赤のグラデーション
                Color fill;
                if (ratio > 0.5f) fill = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
                else fill = Color.Lerp(Color.red, Color.yellow, ratio * 2f);
                GUI.color = fill;
                GUI.Box(fillRect, "");
            }

            // HP テキスト（キャッシュ済みスタイル）
            GUI.color = Color.white;
            GUI.Label(rect, $"{hpNow}/{hpMax}", _hpTextStyle);

            GUI.color = prev;
        }

        private void DrawActionFooter()
        {
            string line;
            GUIStyle style = GuiTheme.Subtitle;
            if (IsFinished)
            {
                line = $"戦闘終了：{ResultLabel(_resultIfEnded)}";
            }
            else if (_lastActor != null && _lastTargets != null)
            {
                line = $"{_lastActor.BaseUnit.Name} → {_lastWazaName} → {TargetsText(_lastTargets)}";
            }
            else if (_lastActor != null && !string.IsNullOrEmpty(_lastWazaName))
            {
                line = $"{_lastActor.BaseUnit.Name}：{_lastWazaName}";
            }
            else
            {
                line = "戦闘開始…";
                style = GuiTheme.Muted;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(line, style);
        }

        private static string TargetsText(List<RuntimeUnit> targets)
        {
            if (targets == null || targets.Count == 0) return "—";
            var names = new List<string>();
            foreach (var t in targets) names.Add(t.BaseUnit.Name);
            return string.Join(", ", names);
        }

        private static string ResultLabel(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory:      return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat:      return "惜敗";
                case BattleResult.CrushingDefeat:      return "完敗";
                default:                               return "—";
            }
        }
    }
}
