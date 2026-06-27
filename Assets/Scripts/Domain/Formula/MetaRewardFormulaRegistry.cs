// メタ通貨「王国の記憶」獲得式のレジストリ（ID → 実装）。
//
// 【役割】
// - SO（MetaRewardFormulaSO）から FormulaId と Params を持ち、本 Registry で実装を解決する。
// - 標準セット（VSプロト範囲）：vsproto_standard_v1 の 1 種類。
//
// 【設計方針】
// - 純関数群・noEngineReferences=true 維持（DamageFormulaRegistry と同パターン）。
// - パラメタは IReadOnlyDictionary<string, float> で受ける（FormulaParam.ToDictionary 経由）。
using System;
using System.Collections.Generic;

namespace Echolos.Domain.Formula
{
    /// <summary>メタ通貨獲得式のレジストリ（純関数群）。</summary>
    public static class MetaRewardFormulaRegistry
    {
        /// <summary>メタ通貨獲得式の共通シグネチャ。</summary>
        public delegate int Formula(
            MetaRewardContext ctx,
            IReadOnlyDictionary<string, float> p);

        // 標準セット
        private static readonly Dictionary<string, Formula> _map =
            new Dictionary<string, Formula>
            {
                // vsproto_standard_v1：
                //   reward = RoundsCompleted * perRound
                //         + (ReachedBossRound ? reachedBoss : 0)
                //         + (BridgetRescued   ? bridgetRescue : 0)
                //         + (BossDefeated     ? bossDefeated : 0)
                //         + (TrueEnd          ? trueEnd : 0)
                ["vsproto_standard_v1"] = (ctx, p) =>
                {
                    int reward = ctx.RoundsCompleted * (int)GetParam(p, "perRound", 10f);
                    if (ctx.ReachedBossRound) reward += (int)GetParam(p, "reachedBoss", 50f);
                    if (ctx.BridgetRescued)   reward += (int)GetParam(p, "bridgetRescue", 100f);
                    if (ctx.BossDefeated)     reward += (int)GetParam(p, "bossDefeated", 200f);
                    if (ctx.TrueEnd)          reward += (int)GetParam(p, "trueEnd", 150f);
                    return reward;
                },
            };

        /// <summary>ID から計算式を引く。未登録 ID は例外。</summary>
        public static Formula Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("MetaRewardFormulaId must not be null/empty", nameof(id));
            if (!_map.TryGetValue(id, out var f))
                throw new KeyNotFoundException($"MetaRewardFormulaId not registered: {id}");
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
