namespace Echolos.Domain.Battle.Terrain
{
    // 地形強度。マップ層別に固定（自領=Light / 敵領=Medium / 敵拠点=Heavy）。
    // 同列内（自領・敵領・敵拠点）は同じ TerrainKind を共有しつつ強度だけが層別に変化する。
    public enum TerrainStrength
    {
        Light = 0,   // 軽微（自領）
        Medium = 1,  // 中（敵領）
        Heavy = 2,   // 重（敵拠点）
    }
}
