// ストーリー演出の描画層。
// StoryProgress（純ロジック・Domain 側）が保持するページとフェード値を読み、
// フルスクリーン Rect にスチルと暗幕を描く。
//
// 【方針】
// - 静的メソッドのみ。状態は持たない（呼び出し側が StoryProgress を保持する）。
// - 画像は Resources.Load<Texture2D>(page.ImagePath) を IconRegistry 同様にキャッシュ。
//   見つからないパスもキャッシュして毎フレーム再ロードを抑止する。
// - ナレ文字／Skip ボタン／「次へ」ボタンの描画は呼び出し側 GUI が直接行う。
//   理由：レイアウトはフェーズごとに変えたい（タイトルとエンディングではボタン位置が違う）
//   一方で「暗幕＋画像＋フェード」は共通化価値が高いのでここに集約する。
using System.Collections.Generic;
using UnityEngine;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.Story
{
    /// <summary>ストーリーオーバーレイ用の描画ヘルパー（状態を持たない静的クラス）。</summary>
    public static class StoryOverlay
    {
        /// <summary>ナレ帯の高さが画面に占める比率（呼び出し側が「次へ」ボタンをナレ帯外に置く座標計算で参照）。</summary>
        public const float NarrationBandHeightRatio = 0.28f;

        // Resources.Load の結果をキャッシュ。null も入れて未配置パスの再試行を抑止する。
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// 暗幕＋スチル画像を Rect 全体に描画する。
        /// CurrentAlpha 値で全体のフェードを表現する。
        /// ナレ・ボタンは別途呼び出し側で描画する。
        /// </summary>
        public static void DrawBackground(Rect area, StoryProgress progress)
        {
            if (progress == null || progress.IsFinished) return;

            var page = progress.CurrentPage;
            if (page == null) return;

            float alpha = progress.CurrentAlpha;

            // 1) 暗幕（黒・常時アルファ最大に近い・後ろの GUI を覆い隠す目的）
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.85f, 1f, alpha));
            GUI.DrawTexture(area, Texture2D.whiteTexture);

            // 2) スチル画像（alpha でフェード・ScaleToFit でアスペクト維持）
            //    メインが未配置なら FallbackImagePath を試す（GPT 生成途中の素材を吸収）。
            var tex = ResolveTexture(page);
            if (tex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(area, tex, ScaleMode.ScaleToFit);
            }

            GUI.color = prevColor;
        }

        private static Texture2D ResolveTexture(StoryPage page)
        {
            if (!string.IsNullOrEmpty(page.ImagePath))
            {
                var tex = LoadCached(page.ImagePath);
                if (tex != null) return tex;
            }
            if (!string.IsNullOrEmpty(page.FallbackImagePath))
            {
                var tex = LoadCached(page.FallbackImagePath);
                if (tex != null) return tex;
            }
            return null;
        }

        /// <summary>
        /// Rect 下部 1/4 にナレーション文字列を描画する。背景にも薄い半透明帯を敷いて可読性を確保。
        /// </summary>
        public static void DrawNarration(Rect area, StoryProgress progress, GUIStyle style)
        {
            if (progress == null || progress.IsFinished) return;
            var page = progress.CurrentPage;
            if (page == null || string.IsNullOrEmpty(page.NarrationText)) return;

            float alpha = progress.CurrentAlpha;
            var band = new Rect(
                area.x,
                area.yMax - area.height * NarrationBandHeightRatio,
                area.width,
                area.height * NarrationBandHeightRatio);

            // ナレ背景の半透明帯
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(band, Texture2D.whiteTexture);

            // ナレ文字
            GUI.color = new Color(1f, 1f, 1f, alpha);
            const float padding = 40f;
            var textRect = new Rect(band.x + padding, band.y + padding * 0.4f,
                                    band.width - padding * 2f, band.height - padding * 0.8f);
            GUI.Label(textRect, page.NarrationText, style);
            GUI.color = prev;
        }

        private static Texture2D LoadCached(string path)
        {
            if (_textureCache.TryGetValue(path, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>(path);
            _textureCache[path] = tex; // null も入れる
            return tex;
        }
    }
}
