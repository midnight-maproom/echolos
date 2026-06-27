// バフ・デバフ・状態異常アイコンの描画ユーティリティ。
//
// 【目的】
// - 観戦中の戦線スロットで、ユニットが現在受けている状態効果（バフ/デバフ/状態異常）を
//   常時アイコン表示し、「何が起きているか」をログを開かずに把握できるようにする。
// - UnitBadgeOverlay（射程/かばう/役割の永続バッジ）とは責務を分離：
//   本クラスは戦闘中に動的に増減する効果のみを扱う。
//
// 【表示方針】
// - 配置中は表示しない（戦闘前は効果無し）。観戦中のみ呼ばれる前提。
// - アイコン領域の下端寄りに横並びで描画する。
// - 最大 5 個まで・以降は「+N」省略表示で過密化を防ぐ。
// - 死亡ユニットには描画しない。
//
// 【画像方針】
// 画像が `Assets/Resources/Icons/Badges/badge_status_xxx.png` にあれば自動で使用。
// 画像未配置の効果はテキストプレースホルダー（色付き枠＋半透明黒下敷き＋1〜2文字）にフォールバック。
//
// 【IMGUI 注意】
// 絶対座標 API のみ使用する（GUILayout 系は Layout/Repaint パス不整合の原因）。
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.Battle
{
    /// <summary>戦闘中のバフ／デバフ／状態異常のアイコン描画。</summary>
    public static class StatusEffectOverlay
    {
        // バッジサイズはアイコン親矩形の短辺に対する比率で算出する（2026-06-04 ピクセル固定→相対化）。
        // UnitBadgeOverlay と同じ比率で統一感を保つ（基準：iconSize=96px のとき BadgeSize=18px）。
        private const float BadgeSizeRatio   = 18f / 96f; // 0.1875
        private const float BadgeGapRatio    =  1f / 96f; // 0.0104
        private const float HorizontalMarginRatio = 2f / 96f;
        private const float VerticalMarginRatio   = 2f / 96f;
        private const float LabelFontRatio   = 10f / 18f;
        private const float MinBadgeSize     = 8f;
        private const int   MinFontSize      = 8;
        private const int MaxBadges = 5;            // 以降は省略

        // 色：用途別。
        // バフ＝緑系 / 能力デバフ＝赤系 / 状態異常＝紫系 / 省略＝灰
        private static readonly Color BuffColor      = new Color(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color DebuffColor    = new Color(0.94f, 0.27f, 0.27f, 1f);
        private static readonly Color AilmentColor   = new Color(0.58f, 0.30f, 0.83f, 1f);
        private static readonly Color OverflowColor  = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color BgColor        = new Color(0f, 0f, 0f, 0.78f);

        // 画像キャッシュ：Resources.Load は同一パス再呼び出しでもコストが乗るためここで持つ。
        // 見つからなかったパスも記録して再ロードを避ける（プレースホルダー確定→以降は色枠＋テキスト）。
        // UnitBadgeOverlay と同じ構造（命名規則だけ揃えて統一感を保つ）。
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _missingPaths = new HashSet<string>();

        private static Texture2D LoadBadgeTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_textureCache.TryGetValue(name, out var cached)) return cached;
            if (_missingPaths.Contains(name)) return null;

            // 「Resources/Icons/Badges/」配下を相対パスで指定。拡張子は付けない（Unity 規約）。
            var tex = Resources.Load<Texture2D>("Icons/Badges/" + name);
            if (tex == null)
            {
                _missingPaths.Add(name);
                return null;
            }
            _textureCache[name] = tex;
            return tex;
        }

        // 文字スタイルキャッシュ。fontSize 別にキャッシュ（画面リサイズで動的に変わるため）。
        private static readonly Dictionary<int, GUIStyle> _labelStyleByFont = new Dictionary<int, GUIStyle>();
        private static GUIStyle EnsureLabelStyle(int fontSize)
        {
            if (_labelStyleByFont.TryGetValue(fontSize, out var cached)) return cached;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
            };
            _labelStyleByFont[fontSize] = style;
            return style;
        }

        /// <summary>
        /// アイコン領域に対して、状態効果バッジを下端寄りに横並びで描画する。
        /// 観戦ビューから「現在この瞬間に表示すべき状態効果」を時系列再生で集計したリストを
        /// 直接渡してもらう前提（RuntimeUnit.ActiveEffects 直参照だと戦闘終了時のスナップショットしか
        /// 見えず観戦中の動的変化を表現できないため）。
        /// </summary>
        /// <param name="iconRect">ユニットアイコンの矩形（バッジは下端基準）</param>
        /// <param name="activeTypes">現時点で対象ユニットに付与されている効果タイプ（付与順推奨・null/空なら描画なし）</param>
        public static void Draw(Rect iconRect, IReadOnlyList<EffectKind> activeTypes)
        {
            if (activeTypes == null || activeTypes.Count == 0) return;

            // 表示対象だけ集める（Cover/ReviveInvalid/Generic は除外・別経路で表現済 or プロト対象外）。
            var visible = new List<EffectKind>();
            for (int i = 0; i < activeTypes.Count; i++)
            {
                var t = activeTypes[i];
                if (IsVisibleType(t)) visible.Add(t);
            }
            if (visible.Count == 0) return;

            int displayCount = visible.Count;
            bool overflow = false;
            if (displayCount > MaxBadges)
            {
                displayCount = MaxBadges;
                overflow = true;
            }

            // 親矩形短辺からバッジサイズを算出（画面リサイズ追従）。
            float iconShortSide = Mathf.Min(iconRect.width, iconRect.height);
            float badgeSize = Mathf.Max(MinBadgeSize, iconShortSide * BadgeSizeRatio);
            float gap = iconShortSide * BadgeGapRatio;
            float hMargin = iconShortSide * HorizontalMarginRatio;
            float vMargin = iconShortSide * VerticalMarginRatio;
            int fontSize = Mathf.Max(MinFontSize, Mathf.RoundToInt(badgeSize * LabelFontRatio));

            // 描画位置：アイコン下端寄りの横並び。アイコン左端から余白を取り、右へ流す。
            float y = iconRect.yMax - badgeSize - vMargin;
            float x = iconRect.x + hMargin;

            for (int i = 0; i < displayCount; i++)
            {
                var type = visible[i];
                DrawBadge(new Rect(x, y, badgeSize, badgeSize),
                    EffectLabel(type), EffectColor(type), EffectTextureName(type), fontSize);
                x += badgeSize + gap;
            }

            if (overflow)
            {
                int more = visible.Count - MaxBadges;
                // 省略表示「+N」は画像化しない（数値依存・常に色枠＋テキストで描画）
                DrawBadge(new Rect(x, y, badgeSize, badgeSize), "+" + more, OverflowColor, null, fontSize);
            }
        }

        // ─────────────────────────────────
        // 内部
        // ─────────────────────────────────

        /// <summary>
        /// バッジ1個を描画。画像があれば画像優先（下敷きなし）、無ければ色枠＋テキストの
        /// プレースホルダーに自動フォールバック（テキスト視認性確保のため下敷きあり）。
        /// 画像版で下敷きを敷くと GPT 画像の白縁取りと干渉して「白い四角」が悪目立ちするため撤去
        /// （UnitBadgeOverlay と同じ方針・2026-06-04）。
        /// </summary>
        private static void DrawBadge(Rect rect, string label, Color accentColor, string textureName, int fontSize)
        {
            var prev = GUI.color;

            var tex = LoadBadgeTexture(textureName);
            if (tex != null)
            {
                // 画像版：下敷きなしで透過 PNG をそのままフィット描画。
                // alphaBlend: true を明示（透過 PNG の白塗り問題対策）。
                GUI.color = Color.white;
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // プレースホルダー版（Phase 1 と同じ見た目・段階的差し替え可）：
                // 背景の半透明黒下敷き＋色枠＋1〜2文字テキスト。
                GUI.color = BgColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                // アクセント枠（4 辺の 1px 線）
                GUI.color = accentColor;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);

                // ラベル（中央寄せ）
                GUI.color = Color.white;
                GUI.Label(rect, label, EnsureLabelStyle(fontSize));
            }

            GUI.color = prev;
        }

        /// <summary>
        /// EffectKind → 画像ファイル名（`Resources/Icons/Badges/` 配下・拡張子なし）。
        /// 配置されていない名前は LoadBadgeTexture でフォールバックされるので
        /// 段階的に置き換えていけば良い（A10 Phase 2 着手中・2026-06-04）。
        /// </summary>
        private static string EffectTextureName(EffectKind type)
        {
            switch (type)
            {
                // 状態異常
                case EffectKind.Burn:        return "badge_status_burn";
                case EffectKind.Freeze:      return "badge_status_freeze";       // プロト範囲外（画像生成スキップ）
                case EffectKind.Paralysis:   return "badge_status_paralysis";
                case EffectKind.Curse:       return "badge_status_curse";        // プロト範囲外（画像生成スキップ）
                // バフ
                case EffectKind.AttackUp:    return "badge_status_atk_up";
                case EffectKind.DefenseUp:   return "badge_status_def_up";
                case EffectKind.EvasionUp:   return "badge_status_eva_up";
                // デバフ
                case EffectKind.AttackDown:  return "badge_status_atk_down";
                case EffectKind.DefenseDown: return "badge_status_def_down";
                case EffectKind.EvasionDown: return "badge_status_eva_down";
                default: return null;
            }
        }

        /// <summary>表示対象の EffectKind か（Cover/ReviveInvalid/Generic は除外）。</summary>
        private static bool IsVisibleType(EffectKind type)
        {
            switch (type)
            {
                case EffectKind.Burn:
                case EffectKind.Freeze:
                case EffectKind.Paralysis:
                case EffectKind.Curse:
                case EffectKind.AttackUp:
                case EffectKind.AttackDown:
                case EffectKind.DefenseUp:
                case EffectKind.DefenseDown:
                case EffectKind.EvasionUp:
                case EffectKind.EvasionDown:
                    return true;
                default:
                    return false;
            }
        }

        private static string EffectLabel(EffectKind type)
        {
            switch (type)
            {
                // 状態異常（1文字）
                case EffectKind.Burn:        return "燃";
                case EffectKind.Freeze:      return "凍";
                case EffectKind.Paralysis:   return "麻";
                case EffectKind.Curse:       return "呪";
                // バフ・デバフ（2文字＋矢印）
                case EffectKind.AttackUp:    return "攻↑";
                case EffectKind.AttackDown:  return "攻↓";
                case EffectKind.DefenseUp:   return "防↑";
                case EffectKind.DefenseDown: return "防↓";
                case EffectKind.EvasionUp:   return "回↑";
                case EffectKind.EvasionDown: return "回↓";
                default: return "?";
            }
        }

        private static Color EffectColor(EffectKind type)
        {
            switch (type)
            {
                case EffectKind.Burn:
                case EffectKind.Freeze:
                case EffectKind.Paralysis:
                case EffectKind.Curse:
                    return AilmentColor;
                case EffectKind.AttackUp:
                case EffectKind.DefenseUp:
                case EffectKind.EvasionUp:
                    return BuffColor;
                case EffectKind.AttackDown:
                case EffectKind.DefenseDown:
                case EffectKind.EvasionDown:
                    return DebuffColor;
                default:
                    return OverflowColor;
            }
        }
    }
}
