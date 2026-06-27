// 戦場背景・マップ背景・拠点アイコンの実行時ロード共通ヘルパ。
//
// 設計方針：
// - `Resources/Images/VSPrototype/{key}.png` を遅延ロード＋ Dictionary キャッシュ＋ミッシングキャッシュ。
//   IconRegistry と同パターンで「無いものは無い」もキャッシュして毎フレーム Resources.Load を叩かない。
// - 描画 API は 2 種類：
//   - TryDrawCover：背景フィル用（ScaleAndCrop で rect 全体を埋める）
//   - TryDrawFit  ：拠点アイコン用（ScaleToFit で内接表示・α 制御可）
// - アセット未配置でも null 返却で済むため、呼び出し側は「成功なら描画／失敗なら従来描画」のフォールバックを
//   組みやすい。アセットを後から配置するだけで自動反映される並行作業性を担保。
using System.Collections.Generic;
using UnityEngine;

namespace Echolos.Presentation.Common
{
    /// <summary>VSプロト背景画像・拠点アイコンの遅延ロード共通ヘルパ。</summary>
    public static class BackgroundRegistry
    {
        private const string ResourcePath = "Images/VSPrototype/";

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>
        /// 指定キーの Texture2D を返す。見つからなければ null。
        /// 呼び出し側は null チェックで従来描画にフォールバックする。
        /// </summary>
        public static Texture2D Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_cache.TryGetValue(key, out var cached)) return cached;
            if (_missing.Contains(key)) return null;

            var tex = Resources.Load<Texture2D>(ResourcePath + key);
            if (tex == null)
            {
                _missing.Add(key);
                return null;
            }
            _cache[key] = tex;
            return tex;
        }

        /// <summary>
        /// 背景フィル用：指定 Rect に画像を ScaleAndCrop で全体描画する（アスペクト維持・はみ出しはトリミング）。
        /// アセット無しなら false（呼び出し側で従来単色塗りにフォールバック）。
        /// </summary>
        public static bool TryDrawCover(Rect rect, string key)
        {
            var tex = Get(key);
            if (tex == null) return false;
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleAndCrop);
            return true;
        }

        /// <summary>
        /// 拠点アイコン用：指定 Rect に画像を ScaleToFit で内接描画する（アスペクト維持・余白あり）。
        /// alpha で透過率指定可能（デフォルト 1.0）。アセット無しなら false。
        /// </summary>
        public static bool TryDrawFit(Rect rect, string key, float alpha = 1f)
        {
            var tex = Get(key);
            if (tex == null) return false;

            var prev = GUI.color;
            if (alpha < 0.999f) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
            GUI.color = prev;
            return true;
        }
    }
}
