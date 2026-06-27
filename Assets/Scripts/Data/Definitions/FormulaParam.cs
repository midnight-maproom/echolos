// 数式・条件パラメタの key-value（SO シリアライズ不可な Dictionary の代替）。
// Registry に渡す前に Dictionary に変換するヘルパー付き。
//
// 利用箇所：MetaRewardFormulaDefinition.Params 等の Data.Definitions POCO 群。
using System.Collections.Generic;

namespace Echolos.Data.Definitions
{
    /// <summary>SO シリアライズ可能な key-value（List 化で Dictionary の代替）。</summary>
    [System.Serializable]
    public struct FormulaParam
    {
        public string Key;
        public float Value;

        public FormulaParam(string key, float value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>KVP リストを Dictionary に変換するヘルパー。</summary>
        public static IReadOnlyDictionary<string, float> ToDictionary(IList<FormulaParam> list)
        {
            var dict = new Dictionary<string, float>();
            if (list == null) return dict;
            foreach (var p in list) dict[p.Key] = p.Value;
            return dict;
        }
    }
}
