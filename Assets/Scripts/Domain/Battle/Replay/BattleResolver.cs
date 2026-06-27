// 1 戦線の戦闘解決を担うデリゲート（テスト時の差替＋本実装は BattleRunner.Run のラッパー）。
using System;
using System.Collections.Generic;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>
    /// 1 戦線の戦闘解決を担うデリゲート。
    /// 攻め守り（IsAttackingSide）と地形強度（TerrainStrength）は呼び出し側が
    /// MapNode 種別から判定して渡す。地形種別（TerrainKind）は現状 Neutral 固定のため引数化していない。
    /// </summary>
    public delegate BattleReport BattleResolver(
        List<RuntimeUnit> allies, List<RuntimeUnit> enemies,
        int maxTurns, Func<int> rng,
        bool isAttackingSide, TerrainStrength terrainStrength);
}
