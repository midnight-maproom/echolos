// 攻撃種別アイコンの統一描画ユーティリティ。
//
// 【目的】
// - 配置中の手駒アイコンと観戦中の戦線スロットアイコンの**両方で同じ見た目**のバッジを出す。
// - ユーザーが「配置で見たアイコン」と「観戦で見るアイコン」を同一視できるようにする。
//
// 【描画方針】
// - ユニットアイコンの**左上隅**に縦積みで小バッジを並べる。
// - サイズ 14×14 px・背景半透明黒の上に画像をフィット描画。
// - 画像が見つからない場合はプレースホルダー（色枠＋1文字）にフォールバックする。
//
// 【画像配置】
// Assets/Resources/Icons/Badges/ 配下：
//   badge_attack_melee.png / badge_attack_ranged.png / badge_role_healer.png
//
// 【IMGUI 注意】
// 絶対座標 API（GUI.Box / GUI.Label / GUI.DrawTexture）のみ使用する。
// GUILayout 系は Layout/Repaint パス間の制御数不整合で例外が出るので不可。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Presentation.Battle
{
    /// <summary>ユニットアイコンに重ねる攻撃種別バッジの描画。</summary>
    public static class UnitBadgeOverlay
    {
        // バッジサイズはアイコン親矩形の短辺に対する比率で算出する。
        private const float BadgeSizeRatio   = 18f / 96f; // 0.1875
        private const float BadgeMarginRatio =  2f / 96f; // 0.0208
        private const float BadgeGapRatio    =  1f / 96f; // 0.0104
        private const float LabelFontRatio   = 10f / 18f; // 0.555
        private const float MinBadgeSize     = 8f;
        private const int   MinFontSize      = 8;

        private static readonly Color MeleeColor   = new Color(0.88f, 0.27f, 0.27f, 1f);
        private static readonly Color RangedColor  = new Color(0.19f, 0.44f, 0.82f, 1f);
        private static readonly Color HealerColor  = new Color(0.37f, 0.80f, 0.37f, 1f);
        private static readonly Color BgColor      = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color BorderColor  = new Color(1f, 1f, 1f, 0.85f);

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

        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _missingPaths = new HashSet<string>();

        private static Texture2D LoadBadgeTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_textureCache.TryGetValue(name, out var cached)) return cached;
            if (_missingPaths.Contains(name)) return null;

            var tex = Resources.Load<Texture2D>("Icons/Badges/" + name);
            if (tex == null)
            {
                _missingPaths.Add(name);
                return null;
            }
            _textureCache[name] = tex;
            return tex;
        }

        /// <summary>
        /// ユニットアイコンの左上隅にバッジを縦積みで描画する。
        /// </summary>
        public static void Draw(Rect iconRect, Unit unit)
        {
            if (unit == null) return;

            float iconShortSide = Mathf.Min(iconRect.width, iconRect.height);
            float badgeSize = Mathf.Max(MinBadgeSize, iconShortSide * BadgeSizeRatio);
            float margin = iconShortSide * BadgeMarginRatio;
            float gap = iconShortSide * BadgeGapRatio;
            int fontSize = Mathf.Max(MinFontSize, Mathf.RoundToInt(badgeSize * LabelFontRatio));

            float x = iconRect.x + margin;
            float y = iconRect.y + margin;

            bool isHealer = unit.BaseWazas != null && unit.BaseWazas.Any(w => w != null && w.Effects != null && w.Effects.Any(e => e is HealEffect));
            bool hasAttackWaza = unit.BaseWazas != null && unit.BaseWazas.Any(w => w != null && w.Effects != null && w.Effects.Any(e => e is AttackEffect));

            // 回復役バッジ（杖）：射程より優先で先頭に出す
            if (isHealer)
            {
                DrawBadge(new Rect(x, y, badgeSize, badgeSize), "治", HealerColor, "badge_role_healer", fontSize);
                y += badgeSize + gap;
            }

            // 攻撃種別バッジ：攻撃 Waza を持つかつ回復役でない場合のみ
            if (!isHealer && hasAttackWaza)
            {
                DrawBadge(new Rect(x, y, badgeSize, badgeSize),
                    AttackKindLabel(unit.AttackKind), AttackKindColor(unit.AttackKind), AttackKindTextureName(unit.AttackKind), fontSize);
                y += badgeSize + gap;
            }
        }

        /// <summary>
        /// 戦闘中の RuntimeUnit 用のオーバーロード。BaseUnit を参照して同じ判定を行う。
        /// </summary>
        public static void Draw(Rect iconRect, RuntimeUnit runtime)
        {
            if (runtime == null) return;
            Draw(iconRect, runtime.BaseUnit);
        }

        private static void DrawBadge(Rect rect, string label, Color accentColor, string textureName, int fontSize)
        {
            var prev = GUI.color;

            var tex = LoadBadgeTexture(textureName);
            if (tex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.color = BgColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                GUI.color = accentColor;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(rect, label, EnsureLabelStyle(fontSize));
            }

            GUI.color = prev;
        }

        private static string AttackKindLabel(AttackKind k)
        {
            switch (k)
            {
                case AttackKind.Melee:  return "近";
                case AttackKind.Ranged: return "遠";
                default: return "?";
            }
        }

        private static Color AttackKindColor(AttackKind k)
        {
            switch (k)
            {
                case AttackKind.Melee:  return MeleeColor;
                case AttackKind.Ranged: return RangedColor;
                default: return BorderColor;
            }
        }

        private static string AttackKindTextureName(AttackKind k)
        {
            switch (k)
            {
                case AttackKind.Melee:  return "badge_attack_melee";
                case AttackKind.Ranged: return "badge_attack_ranged";
                default: return null;
            }
        }
    }
}
