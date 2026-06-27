// MetaProgressState の JSON 文字列⇔State 変換（純C#・UnityEngine 非依存）。
//
// 【設計方針】
// - asmdef noEngineReferences=true のため UnityEngine.JsonUtility は使えない。
// - スキーマが極めて単純（5フィールド・1配列・1辞書）なので、手書きの軽量
//   シリアライザ／パーサーで十分。.NET 外部ライブラリ依存なし。
// - 不正・欠損フィールドは無視して既定値を使う（互換性重視）。
// - フィールド ID 規約：英数字＋アンダースコアのみ。文字列内エスケープは
//   最小限（\" と \\）で済む前提。
using System;
using System.Collections.Generic;
using System.Text;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>MetaProgressState を JSON 文字列に変換／復元する純関数。</summary>
    public static class MetaProgressSerializer
    {
        /// <summary>State を JSON 文字列に変換する。フィールド順は固定（互換性確保）。</summary>
        public static string ToJson(MetaProgressState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"memories\":").Append(state.Memories);
            sb.Append(",\"runCount\":").Append(state.RunCount);
            sb.Append(",\"hasReachedTrueEnd\":").Append(state.HasReachedTrueEnd ? "true" : "false");
            sb.Append(",\"hasFirstReachedBoss\":").Append(state.HasFirstReachedBoss ? "true" : "false");
            sb.Append(",\"hasRescuedBalduin\":").Append(state.HasRescuedBalduin ? "true" : "false");
            sb.Append(",\"hasNotedPendantPower\":").Append(state.HasNotedPendantPower ? "true" : "false");

            sb.Append(",\"unlockedUnits\":[");
            bool first = true;
            foreach (var u in state.UnlockedUnits)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeString(u)).Append('"');
            }
            sb.Append(']');

            sb.Append(",\"appliedUpgrades\":{");
            first = true;
            foreach (var kv in state.AppliedUpgrades)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeString(kv.Key)).Append("\":").Append(kv.Value);
            }
            sb.Append('}');

            sb.Append(",\"appliedUpgradeChoices\":{");
            first = true;
            foreach (var kv in state.AppliedUpgradeChoices)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeString(kv.Key)).Append("\":[");
                bool firstId = true;
                foreach (var id in kv.Value)
                {
                    if (!firstId) sb.Append(',');
                    firstId = false;
                    sb.Append('"').Append(EscapeString(id)).Append('"');
                }
                sb.Append(']');
            }
            sb.Append('}');

            sb.Append(",\"seenStorySceneIds\":[");
            first = true;
            foreach (var id in state.SeenStorySceneIds)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeString(id)).Append('"');
            }
            sb.Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// JSON 文字列から既存 State にロードする。
        /// 戻り値：成功＝true、空文字／パース失敗＝false（State は触らない）。
        /// 欠損フィールドは既定値（数値は 0、bool は false、コレクションは空）で扱う。
        /// </summary>
        public static bool ApplyJson(string json, MetaProgressState target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                int memories = ExtractInt(json, "memories");
                int runCount = ExtractInt(json, "runCount");
                bool trueEnd = ExtractBool(json, "hasReachedTrueEnd");
                var unlocks = ExtractStringArray(json, "unlockedUnits");
                var upgrades = ExtractIntDict(json, "appliedUpgrades");
                // 後から追加された 3 フラグ。旧スキーマには存在しないため、欠損時は false 既定（後方互換）。
                bool firstBoss = ExtractBool(json, "hasFirstReachedBoss");
                bool rescuedBalduin = ExtractBool(json, "hasRescuedBalduin");
                bool pendantNoted = ExtractBool(json, "hasNotedPendantPower");
                // 固有ユニット Lv 強化の選択結果（key=unit_id, value=upgrade_id 配列）。
                // 旧スキーマには存在しないため、欠損時は空辞書既定（後方互換）。
                var upgradeChoices = ExtractStringArrayDict(json, "appliedUpgradeChoices");
                // ストーリー既見集合：旧スキーマには存在しないため、欠損時は空既定（既見なし＝
                // 全シーン初見扱いで通常本文再生）。
                var seenStoryScenes = ExtractStringArray(json, "seenStorySceneIds");

                target.LoadFromSerializedState(memories, runCount, trueEnd, unlocks, upgrades,
                    firstBoss, rescuedBalduin, pendantNoted, upgradeChoices, seenStoryScenes);
                return true;
            }
            catch
            {
                // パース失敗時は target を触らない（呼び出し側で初期状態のまま使う）
                return false;
            }
        }

        // 内部：軽量 JSON 抽出ユーティリティ

        /// <summary>"fieldName" を探し、その値の開始位置（":" の次の非空白文字）を返す。見つからなければ -1。</summary>
        private static int FindFieldValueStart(string json, string fieldName)
        {
            string token = "\"" + fieldName + "\"";
            int idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return -1;
            int colon = json.IndexOf(':', idx + token.Length);
            if (colon < 0) return -1;
            int valStart = colon + 1;
            while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;
            return valStart;
        }

        private static int ExtractInt(string json, string fieldName)
        {
            int start = FindFieldValueStart(json, fieldName);
            if (start < 0) return 0;
            int end = start;
            // 先頭の '-' および数字を読む
            if (end < json.Length && json[end] == '-') end++;
            while (end < json.Length && json[end] >= '0' && json[end] <= '9') end++;
            int len = end - start;
            return (len > 0 && int.TryParse(json.Substring(start, len), out var v)) ? v : 0;
        }

        private static bool ExtractBool(string json, string fieldName)
        {
            int start = FindFieldValueStart(json, fieldName);
            if (start < 0) return false;
            // "true" で始まるかだけ判定（"false" / null 等は false 扱い）
            if (start + 4 <= json.Length
                && string.CompareOrdinal(json, start, "true", 0, 4) == 0)
                return true;
            return false;
        }

        private static List<string> ExtractStringArray(string json, string fieldName)
        {
            var result = new List<string>();
            int start = FindFieldValueStart(json, fieldName);
            if (start < 0 || start >= json.Length || json[start] != '[') return result;
            int end = json.IndexOf(']', start);
            if (end < 0) return result;

            string contents = json.Substring(start + 1, end - start - 1);
            int i = 0;
            while (i < contents.Length)
            {
                int qOpen = contents.IndexOf('"', i);
                if (qOpen < 0) break;
                int qClose = FindStringEnd(contents, qOpen + 1);
                if (qClose < 0) break;
                result.Add(UnescapeString(contents.Substring(qOpen + 1, qClose - qOpen - 1)));
                i = qClose + 1;
            }
            return result;
        }

        private static Dictionary<string, int> ExtractIntDict(string json, string fieldName)
        {
            var result = new Dictionary<string, int>();
            int start = FindFieldValueStart(json, fieldName);
            if (start < 0 || start >= json.Length || json[start] != '{') return result;
            int end = json.IndexOf('}', start);
            if (end < 0) return result;

            string contents = json.Substring(start + 1, end - start - 1);
            int i = 0;
            while (i < contents.Length)
            {
                int qOpen = contents.IndexOf('"', i);
                if (qOpen < 0) break;
                int qClose = FindStringEnd(contents, qOpen + 1);
                if (qClose < 0) break;
                string key = UnescapeString(contents.Substring(qOpen + 1, qClose - qOpen - 1));

                int colon = contents.IndexOf(':', qClose);
                if (colon < 0) break;
                int valStart = colon + 1;
                while (valStart < contents.Length && char.IsWhiteSpace(contents[valStart])) valStart++;
                int valEnd = valStart;
                if (valEnd < contents.Length && contents[valEnd] == '-') valEnd++;
                while (valEnd < contents.Length && contents[valEnd] >= '0' && contents[valEnd] <= '9') valEnd++;
                int len = valEnd - valStart;
                if (len > 0 && int.TryParse(contents.Substring(valStart, len), out var v))
                    result[key] = v;

                i = valEnd;
            }
            return result;
        }

        private static Dictionary<string, List<string>> ExtractStringArrayDict(string json, string fieldName)
        {
            var result = new Dictionary<string, List<string>>();
            int start = FindFieldValueStart(json, fieldName);
            if (start < 0 || start >= json.Length || json[start] != '{') return result;

            // ネストした [...] と {...} を考慮して対応する '}' を探す。
            int depth = 1;
            int end = start + 1;
            while (end < json.Length && depth > 0)
            {
                char c = json[end];
                if (c == '"')
                {
                    int strEnd = FindStringEnd(json, end + 1);
                    if (strEnd < 0) break;
                    end = strEnd + 1;
                    continue;
                }
                if (c == '{') depth++;
                else if (c == '}') depth--;
                end++;
            }
            if (depth != 0) return result;

            string contents = json.Substring(start + 1, end - start - 2);
            int i = 0;
            while (i < contents.Length)
            {
                int qOpen = contents.IndexOf('"', i);
                if (qOpen < 0) break;
                int qClose = FindStringEnd(contents, qOpen + 1);
                if (qClose < 0) break;
                string key = UnescapeString(contents.Substring(qOpen + 1, qClose - qOpen - 1));

                int colon = contents.IndexOf(':', qClose);
                if (colon < 0) break;
                int arrStart = colon + 1;
                while (arrStart < contents.Length && char.IsWhiteSpace(contents[arrStart])) arrStart++;
                if (arrStart >= contents.Length || contents[arrStart] != '[') break;
                int arrEnd = contents.IndexOf(']', arrStart);
                if (arrEnd < 0) break;

                var list = new List<string>();
                string inner = contents.Substring(arrStart + 1, arrEnd - arrStart - 1);
                int j = 0;
                while (j < inner.Length)
                {
                    int sOpen = inner.IndexOf('"', j);
                    if (sOpen < 0) break;
                    int sClose = FindStringEnd(inner, sOpen + 1);
                    if (sClose < 0) break;
                    list.Add(UnescapeString(inner.Substring(sOpen + 1, sClose - sOpen - 1)));
                    j = sClose + 1;
                }
                if (list.Count > 0) result[key] = list;

                i = arrEnd + 1;
            }
            return result;
        }

        /// <summary>fromIndex から始まる文字列の終端（エスケープを考慮した次の "）の位置を返す。</summary>
        private static int FindStringEnd(string s, int fromIndex)
        {
            int i = fromIndex;
            while (i < s.Length)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i += 2; // エスケープシーケンスをスキップ
                    continue;
                }
                if (s[i] == '"') return i;
                i++;
            }
            return -1;
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // 最小限のエスケープ：\\ と \" のみ。
            // ID 規約（英数字＋アンダースコア）の値しか書かれない前提なので、
            // 制御文字や Unicode エスケープは省略している。
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string UnescapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
