// 技の定義データ（純 POCO）。
//
// 【データ駆動化の核心】
// - 既存 Waza.CalculateBaseDamage (Func<RuntimeUnit, RuntimeUnit, int>) はクロージャを
//   埋め込むため SO シリアライズ不可。
// - これを「ID + パラメタ」に置換し、DamageFormulaRegistry / TriggerConditionRegistry
//   で実装を解決する。
//
// 【FormulaParam について】
// - Unity SO シリアライズは Dictionary<string, float> をサポートしないため、
//   KVP リストで持って、Registry に渡す段階で Dictionary に変換する。
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Data.Definitions
{
    /// <summary>技定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class WazaDefinition
    {
        // 識別
        public string Id;
        public string Name;

        // 分類
        public WazaCategory Category = WazaCategory.Attack;

        // 行動パラメタ
        public int SPD;
        public int Cooldown;
        public int InitialCooldown;
        public int HitCount = 1;
        public int MaxUsesPerBattle = -1;
        public bool IsSureHit = false;
        public bool IsForcedWhenReady = false;
        public TargetingType TargetingType = TargetingType.SingleEnemy;

        // ダメージ計算（クロージャ→ID 化）

        /// <summary>
        /// ダメージ計算式の ID（DamageFormulaRegistry で実装解決）。
        /// 例：「standard_attack」「heal_flat」「buff_only」。
        /// 既定値は "buff_only"（攻撃しない）。
        /// </summary>
        public string DamageFormulaId = "buff_only";

        /// <summary>ダメージ式パラメタ（例：mult=1.0）。Dictionary 不可のため KVP リスト。</summary>
        public List<FormulaParam> DamageFormulaParams = new List<FormulaParam>();

        // 発動条件（クロージャ→ID 化）

        /// <summary>
        /// 発動条件 ID（TriggerConditionRegistry で実装解決）。
        /// 例：「always_true」「self_hp_below_ratio」。
        /// 既定値は "always_true"（無条件）。
        /// </summary>
        public string TriggerConditionId = "always_true";

        /// <summary>発動条件パラメタ（例：ratio=0.5）。</summary>
        public List<FormulaParam> TriggerConditionParams = new List<FormulaParam>();

        // 付帯効果

        /// <summary>技ヒット時に対象へ付与する状態効果テンプレ（Burn / DefenseDown 等）。</summary>
        public List<StatusEffect> AppliedEffects = new List<StatusEffect>();

        /// <summary>治療系：味方の状態異常（燃焼/凍結/麻痺/呪い）を解除。</summary>
        public bool CleansesStatusAilments = false;
    }

    /// <summary>
    /// 数式・条件パラメタの key-value（SO Serialize 不可な Dictionary の代替）。
    /// Registry に渡す前に Dictionary に変換する。
    /// </summary>
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
