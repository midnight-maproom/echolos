// ユニットアイコンの実行時ロード。
//
// 使い方：
// - Unity プロジェクトに `Assets/Resources/Icons/Battlers/{Camp}/{unitId}.png` を置くとアイコンが反映される。
//   - 味方は固有／汎用でサブ階層分離：
//     - 例：Resources/Icons/Battlers/Allies/Unique/princess.png → 王女
//     - 例：Resources/Icons/Battlers/Allies/Generic/fire_archer.png → 炎の弓兵
//   - 例：Resources/Icons/Battlers/Enemies/imperial_fire_archer.png → 帝国弓兵（火）
//   - 例：Resources/Icons/Battlers/Bosses/imperial_prince.png → 皇太子
// - 陣営判定は Unit.Id プレフィックスで自動：
//   - `imperial_*` → Enemies／`boss_*` → Bosses／それ以外（無印） → Allies（Unique → Generic の順で探索）
// - 全て無い場合はプレースホルダ（陣営色矩形＋名前テキスト）にフォールバック。
// - 一度ロードを試みた結果（ヒット／ミス）は静的キャッシュ。次フレーム以降は Resources.Load を叩かない。
//
// 設計上のポイント：
// - 「描画側はアイコンが有るか無いか」だけを判定すれば良く、ビューコードはパス階層を意識しない。
// - 検索キーは Unit.Id（"fire_archer" / "imperial_fire_archer" など）に統一。
// - 固有／汎用の判別は Id ハードコードではなく Unique → Generic のフォールバック探索で行う。
//   新固有キャラ追加時はアセットを Unique/ に置くだけでコード変更不要。
using System.Collections.Generic;
using UnityEngine;

namespace Echolos.Presentation.Common
{
    /// <summary>ユニット ID → アイコン Texture の解決。Resources/Icons/Battlers/{Camp}/{unitId}.png を遅延ロードする。</summary>
    public static class IconRegistry
    {
        // 新パス（陣営別階層）。
        private const string IconsBattlersRoot = "Icons/Battlers/";
        // 旧パス（フラット）：アセット投入完了までの過渡期フォールバック。
        private const string IconsLegacyRoot = "Icons/";

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>
        /// 指定ユニット ID のアイコンを返す。見つからなければ null。
        /// 呼び出し側は null チェックでプレースホルダ描画にフォールバックする。
        /// </summary>
        public static Texture2D Get(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            if (_cache.TryGetValue(unitId, out var cached)) return cached;
            if (_missing.Contains(unitId)) return null;

            Texture2D tex = LoadFor(unitId);
            if (tex == null)
            {
                _missing.Add(unitId);
                return null;
            }
            _cache[unitId] = tex;
            return tex;
        }

        // 指定 ID で陣営別パス→旧フラットパスの順に Resources を試行（マップ解決前後で共通）。
        private static Texture2D LoadFor(string unitId)
        {
            Texture2D tex = null;
            foreach (var subPath in EnumerateCampSubPaths(unitId))
            {
                tex = Resources.Load<Texture2D>(IconsBattlersRoot + subPath + unitId);
                if (tex != null) return tex;
            }
            return Resources.Load<Texture2D>(IconsLegacyRoot + unitId);
        }

        /// <summary>
        /// Unit.Id プレフィックスから探索する陣営サブパス候補を順に返す。
        /// 味方は Unique → Generic → 直下（旧構成互換）の 3 段、敵・ボスは 1 段。
        /// </summary>
        private static IEnumerable<string> EnumerateCampSubPaths(string unitId)
        {
            if (unitId.StartsWith("imperial_"))
            {
                // 帝国軍 11 体は Enemies/ で確実にヒット。
                // ラスボス系（imperial_prince / imperial_prince_dark）は Bosses/ にあるので
                // フォールバック探索する。_missing キャッシュで 2 回目以降はスキップされる。
                yield return "Enemies/";
                yield return "Bosses/";
                yield break;
            }
            if (unitId.StartsWith("boss_"))
            {
                yield return "Bosses/";
                yield break;
            }
            yield return "Allies/Unique/";
            yield return "Allies/Generic/";
            yield return "Allies/";
        }

        /// <summary>
        /// 指定 Rect にアイコンを描く。アイコンが無ければ何もしない（false を返す）。
        /// アスペクト比を保ちつつ Rect 内に内接させる（縦長/横長のどちらでも崩れない）。
        /// flipHorizontal=true で左右反転（右向き素材を敵スロットに描く際に使う）。
        /// </summary>
        public static bool TryDrawIcon(Rect rect, string unitId, bool flipHorizontal = false)
        {
            var tex = Get(unitId);
            if (tex == null) return false;

            float texAspect = tex.width / (float)tex.height;
            float rectAspect = rect.width / rect.height;

            Rect drawRect;
            if (texAspect > rectAspect)
            {
                // 横長：幅基準で内接
                float h = rect.width / texAspect;
                drawRect = new Rect(rect.x, rect.y + (rect.height - h) * 0.5f, rect.width, h);
            }
            else
            {
                // 縦長or同じ：高さ基準で内接
                float w = rect.height * texAspect;
                drawRect = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
            }

            if (flipHorizontal)
            {
                // GUI.matrix を一時的に書き換えて drawRect の中心軸で水平反転する。
                // 元素材は全て右向きを想定しているので、敵スロット側はこれで対峙構図になる。
                var prev = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), drawRect.center);
                GUI.DrawTexture(drawRect, tex, ScaleMode.ScaleToFit);
                GUI.matrix = prev;
            }
            else
            {
                GUI.DrawTexture(drawRect, tex, ScaleMode.ScaleToFit);
            }
            return true;
        }
    }
}
