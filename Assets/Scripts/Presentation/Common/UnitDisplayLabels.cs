// Unit のメタ情報（役割／配置ヒント／攻撃種別）を GUI 表示用の短ラベルに整形する静的ユーティリティ。
// ドラフトカード／配置モーダル一覧／強化リスト等で共通利用。
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Echolos.Domain.Models;

namespace Echolos.Presentation.Common
{
    /// <summary>
    /// Unit の戦術的役割・配置ヒント・主攻撃種別を GUI 表示用の短ラベルに変換する。
    /// 文字列はプレイヤー視認性を優先した日本語短縮形（1〜2 文字 + 角括弧）。
    /// </summary>
    public static class UnitDisplayLabels
    {
        /// <summary>
        /// 戦術的役割（UnitRole）を「[盾/攻]」のような短ラベルに整形する。
        /// 空リスト or null は空文字を返す（呼び出し側で表示スキップ）。
        /// </summary>
        public static string RoleTagsLabel(IList<UnitRole> roles)
        {
            if (roles == null || roles.Count == 0) return "";
            var sb = new StringBuilder("[");
            for (int i = 0; i < roles.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(RoleShort(roles[i]));
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>単一役割の 1 文字短縮形。</summary>
        public static string RoleShort(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Tank:     return "盾";
                case UnitRole.Attacker: return "攻";
                case UnitRole.Support:  return "補";
                case UnitRole.Healer:   return "癒";
                default: return "?";
            }
        }

        /// <summary>配置ヒントの短ラベル。Any は空文字（推奨なし＝表示しない）。</summary>
        public static string PlacementHintLabel(PlacementHint hint)
        {
            switch (hint)
            {
                case PlacementHint.Front: return "前列推奨";
                case PlacementHint.Back:  return "後列推奨";
                default: return "";
            }
        }

        /// <summary>攻撃種別の短ラベル（Melee/Ranged/None）。</summary>
        public static string AttackKindLabel(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.Melee:  return "近接";
                case AttackKind.Ranged: return "遠隔";
                case AttackKind.None:   return "補助";
                default: return "";
            }
        }

        /// <summary>属性の漢字ラベル。None は空文字（無属性＝表示しない）。</summary>
        public static string ElementLabel(Element element)
        {
            switch (element)
            {
                case Element.Fire:      return "火";
                case Element.Water:     return "水";
                case Element.Ice:       return "氷";
                case Element.Lightning: return "雷";
                case Element.Wind:      return "風";
                case Element.Earth:     return "地";
                case Element.Light:     return "光";
                case Element.Dark:      return "闇";
                default: return ""; // None
            }
        }

        // 属性色（属性ラベル表示用）。シナジー Tips とロスター行・ドラフトカードで共通使用。
        // 火＝橙、水＝青、光＝淡黄を基調に、未使用属性も将来増設時の調和を保てるよう中庸トーンで定義。
        private static readonly Color ColorElementNeutral   = new Color(0.75f, 0.75f, 0.80f);
        private static readonly Color ColorElementFire      = new Color(1.00f, 0.55f, 0.35f);
        private static readonly Color ColorElementWater     = new Color(0.45f, 0.75f, 1.00f);
        private static readonly Color ColorElementIce       = new Color(0.70f, 0.90f, 1.00f);
        private static readonly Color ColorElementLightning = new Color(1.00f, 0.85f, 0.40f);
        private static readonly Color ColorElementWind      = new Color(0.65f, 0.95f, 0.75f);
        private static readonly Color ColorElementEarth     = new Color(0.85f, 0.70f, 0.50f);
        private static readonly Color ColorElementLight     = new Color(1.00f, 0.95f, 0.55f);
        private static readonly Color ColorElementDark      = new Color(0.75f, 0.60f, 0.90f);

        /// <summary>属性に対応する表示色。None は中庸色（無属性ユニットでも判読可能なラベル色）。</summary>
        public static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Fire:      return ColorElementFire;
                case Element.Water:     return ColorElementWater;
                case Element.Ice:       return ColorElementIce;
                case Element.Lightning: return ColorElementLightning;
                case Element.Wind:      return ColorElementWind;
                case Element.Earth:     return ColorElementEarth;
                case Element.Light:     return ColorElementLight;
                case Element.Dark:      return ColorElementDark;
                default: return ColorElementNeutral; // None
            }
        }
    }
}
