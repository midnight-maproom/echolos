using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Formula
{
    /// <summary>
    /// ダメージ計算式のレジストリ（純関数群）。
    /// Waza.CalculateBaseDamage クロージャを「ID + パラメタ」に置換した先で ID から実装デリゲートを引く。
    /// パラメタは IReadOnlyDictionary&lt;string, float&gt; で受け、WazaCatalog 側で
    /// WazaDefinition.DamageFormulaParams を FormulaParam.ToDictionary で変換して渡す。
    /// </summary>
    public static class DamageFormulaRegistry
    {
        /// <summary>ダメージ計算式の共通シグネチャ。</summary>
        public delegate int DamageFormula(
            RuntimeUnit attacker, RuntimeUnit target,
            IReadOnlyDictionary<string, float> p);

        private static readonly Dictionary<string, DamageFormula> _map =
            new Dictionary<string, DamageFormula>
            {
                // 通常攻撃：a.EffectiveATK × mult（防御適用は BattleManager.ComputeBaseDamage 側）
                ["standard_attack"] = (a, t, p) =>
                    (int)(a.EffectiveATK * GetParam(p, "mult", 1.0f)),

                // 旧 ID は新戦闘ダメージ式実装まで暫定的に standard_attack 同等で残す
                ["standard_physical"] = (a, t, p) =>
                    (int)(a.EffectiveATK * GetParam(p, "mult", 1.0f)),
                ["standard_magical"] = (a, t, p) =>
                    (int)(a.EffectiveATK * GetParam(p, "mult", 1.0f)),
                ["multi_hit_physical"] = (a, t, p) =>
                    (int)(a.EffectiveATK * GetParam(p, "mult", 1.0f)),

                // 固定値回復：amount + 兵種強化
                ["heal_flat"] = (a, t, p) =>
                    (int)(GetParam(p, "amount", 0f)
                        + a.BaseUnit.EnhancementHealPerLevel * a.BaseUnit.EnhancementLevel),

                // 攻撃しない：バフ・支援専門技用
                ["buff_only"] = (a, t, p) => 0,
            };

        /// <summary>ID から計算式を引く。未登録 ID は例外。</summary>
        public static DamageFormula Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("DamageFormulaId must not be null/empty", nameof(id));
            if (!_map.TryGetValue(id, out var f))
                throw new KeyNotFoundException($"DamageFormulaId not registered: {id}");
            return f;
        }

        /// <summary>登録済みか確認（Catalog ロード時のバリデーション用）。</summary>
        public static bool IsRegistered(string id) =>
            !string.IsNullOrEmpty(id) && _map.ContainsKey(id);

        /// <summary>登録 ID 全列挙（テスト・デバッグ用）。</summary>
        public static IEnumerable<string> GetAllIds() => _map.Keys;

        private static float GetParam(IReadOnlyDictionary<string, float> p, string key, float fallback)
            => p != null && p.TryGetValue(key, out var v) ? v : fallback;
    }
}
