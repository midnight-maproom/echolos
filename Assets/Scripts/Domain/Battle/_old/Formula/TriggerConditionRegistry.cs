// 技の発動条件レジストリ（ID → 実装）。
//
// 【役割】
// - Waza.TargetingCondition クロージャを「ID + パラメタ」に置換した先で、
//   ID から条件判定デリゲートを引くレジストリ。
// - 標準セット（プロト範囲）：always_true / self_hp_below_ratio の2種。
using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Formula
{
    /// <summary>技の発動条件レジストリ（純関数群）。</summary>
    public static class TriggerConditionRegistry
    {
        /// <summary>発動条件の共通シグネチャ（subject = 判定対象＝呼び出し側が指定する）。</summary>
        public delegate bool TriggerCondition(
            RuntimeUnit subject, IReadOnlyDictionary<string, float> p);

        // 標準セット
        private static readonly Dictionary<string, TriggerCondition> _map =
            new Dictionary<string, TriggerCondition>
            {
                // 無条件で発動可能（既定）
                ["always_true"] = (s, p) => true,

                // 自身の HP が指定割合以下のときに発動（VSプロト範囲では現状未使用・汎用ロジックとして保持）
                ["self_hp_below_ratio"] = (s, p) =>
                {
                    if (s == null) return false;
                    float ratio = GetParam(p, "ratio", 0.5f);
                    return s.BaseUnit.CurrentHP <= s.MaxHP * ratio;
                },
            };

        public static TriggerCondition Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("TriggerConditionId must not be null/empty", nameof(id));
            if (!_map.TryGetValue(id, out var f))
                throw new KeyNotFoundException($"TriggerConditionId not registered: {id}");
            return f;
        }

        public static bool IsRegistered(string id) =>
            !string.IsNullOrEmpty(id) && _map.ContainsKey(id);

        public static IEnumerable<string> GetAllIds() => _map.Keys;

        private static float GetParam(IReadOnlyDictionary<string, float> p, string key, float fallback)
            => p != null && p.TryGetValue(key, out var v) ? v : fallback;
    }
}
