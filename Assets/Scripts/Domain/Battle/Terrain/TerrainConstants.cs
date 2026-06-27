using System.Collections.Generic;

namespace Echolos.Domain.Battle.Terrain
{
    // 地形補正の暫定数値テーブル。インデックス = TerrainStrength enum 値。
    // 自属性ユニットには +α、逆属性には -α として TerrainBonusCalculator が適用する。
    // 数値はバランス調整フェーズで再調整する。
    public static class TerrainConstants
    {
        public static readonly IReadOnlyList<int> AlphaByStrength =
            new[] { 5, 10, 15 };
    }
}
