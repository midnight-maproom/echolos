namespace Echolos.Domain.Battle.Terrain
{
    // 地形種別。列ごとに 1 種を割り当て、層別強度（TerrainStrength）と組み合わせて
    // ダメージ式の環境項を決める。属性同士の直接相性は持たず、地形バイアスのみ。
    public enum TerrainKind
    {
        Neutral,         // 中立：補正なし
        FireAdvantage,   // 火有利：火属性に +α / 水属性に -α
        WaterAdvantage,  // 水有利：水属性に +α / 火属性に -α
    }
}
