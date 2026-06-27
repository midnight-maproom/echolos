// 既存 StorySceneDefinitionSO に対して短縮ナレーション（RepeatNarration）の初期値を
// 一括書き込みする Editor メニュー。シーン ID ごとの既定文を持つ辞書を Single Source of
// Truth として保持し、ID マッチした SO アセットに反映する。
//
// 使い方（ユーザー作業）：
// 1. Unity Editor を開く
// 2. メニュー [Echolos > Data > 短縮ナレーション既定値を書き込み]
// 3. 内容を確認したうえで個別 SO を Inspector で調整
//
// 既存値が空でない SO は上書きしない（手動編集を尊重・冪等再実行可能）。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Echolos.Data.Editor
{
    /// <summary>StorySceneDefinitionSO の RepeatNarration 既定値を一括書き込みする Editor ツール。</summary>
    public static class StorySceneRepeatNarrationMigrator
    {
        // シーン ID → 短縮ナレーション既定値（暫定・ユーザーが Inspector で校正想定）。
        // エンディング系（ending_*）は除外（毎ランで本文を流す方針）。
        private static readonly Dictionary<string, string> DefaultRepeatNarrations = new Dictionary<string, string>
        {
            { "opening",            "滅亡した王国の最後の希望、王女が立ち上がった。" },
            { "b_a_balduin",        "バルドゥインが籠城を始めた。" },
            { "b_b1_letter",        "バルドゥインからの救援要請は、王宮で握りつぶされた。" },
            { "b_b2_surrender",     "バルドゥインは、ついに降伏した。" },
            { "b_c_girl",           "謎の少女が王女の演説をじっと見つめ、立ち去った。" },
            { "balduin_rescue",     "バルドゥインは王女にペンダントを託し、ブリジットを預けた。" },
            { "b_e_sword",          "王女はペンダントの力に気づき、聖剣は真の輝きを取り戻した。" },
            { "a_c1_attack",        "魔王のオーラに侵された皇太子が、王女に襲いかかった。" },
            { "a_c2_purify",        "王女は聖剣を掲げ、皇太子のオーラを祓った。" },
        };

        [MenuItem("Echolos/Data/短縮ナレーション既定値を書き込み")]
        public static void ApplyDefaults()
        {
            var sos = Resources.LoadAll<StorySceneDefinitionSO>("Data/StoryScenes");
            int written = 0, skipped = 0, unknown = 0;

            foreach (var so in sos)
            {
                if (so == null || so.Definition == null || string.IsNullOrEmpty(so.Definition.Id))
                    continue;

                var id = so.Definition.Id;
                if (!DefaultRepeatNarrations.TryGetValue(id, out var defaultText))
                {
                    unknown++;
                    continue;
                }

                if (!string.IsNullOrEmpty(so.Definition.RepeatNarration))
                {
                    skipped++;
                    continue;
                }

                var serialized = new SerializedObject(so);
                var repeatProp = serialized.FindProperty("_definition.RepeatNarration");
                if (repeatProp != null)
                {
                    repeatProp.stringValue = defaultText;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so);
                    written++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[StorySceneRepeatNarrationMigrator] 書き込み {written} 件／既存維持 {skipped} 件／対象外 {unknown} 件");
        }
    }
}
