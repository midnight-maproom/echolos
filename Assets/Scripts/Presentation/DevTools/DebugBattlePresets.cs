// Debug シーン用：味方／敵プリセット編成定義。
// 各 List に new() {...} を 1 件追記するだけで増やせる。
// UnitIds の順序＝SlotIndex（0,1,2 が前列／3,4,5 が後列）。長さ 1〜6 で可変。
// 値の解決は IUnitCatalog 経由なので、typo は実行時にエラー（未登録 Id）で早期発覚する。
using System.Collections.Generic;

namespace Echolos.Presentation.DevTools
{
    /// <summary>プリセット 1 件。SlotIndex は UnitIds のリスト順（0,1,2 が前列／3,4,5 が後列）。</summary>
    public sealed class DebugBattlePreset
    {
        public string Name;
        public string Description;
        public List<string> UnitIds;
    }

    /// <summary>
    /// 味方／敵の初期プリセット集。GUI 上で陣営別にドロップダウンから選択する。
    /// 具体的なプリセットは利用者がこの List に new() {...} を追加していく。
    /// </summary>
    public static class DebugBattlePresets
    {
        public static readonly List<DebugBattlePreset> Allies = new()
        {
            new()
            {
                Name = "（空）",
                Description = "全スロット空（手動配置用）",
                UnitIds = new(),
            },
            new()
            {
                Name = "火 6 体",
                Description = "盾／剣士／刺客／鼓舞／魔導士／弓",
                UnitIds = new()
                {
                    "fire_tank", "fire_swordsman", "fire_assassin",
                    "fire_buffer", "fire_mage", "fire_archer",
                },
            },
            new()
            {
                Name = "水 6 体",
                Description = "守護／剣士／護術／巫女／弓／水鏡の幻盾兵",
                UnitIds = new()
                {
                    "water_tank", "water_swordsman", "water_buffer",
                    "water_healer", "water_archer", "water_dispel_tank",
                },
            },
            new()
            {
                Name = "光 4 火 2",
                Description = "光の騎士／王女／炎の鼓舞師／光の司祭／暁光の大魔導士／炎の弓兵",
                UnitIds = new()
                {
                    "light_paladin", "princess", "fire_buffer",
                    "light_priest", "light_mage", "fire_archer",
                },
            },
            new()
            {
                Name = "光 4 水 2",
                Description = "水の大盾兵／王女／癒水の巫女／光の司祭／暁光の大魔導士／光の騎士",
                UnitIds = new()
                {
                    "water_tank", "princess", "water_healer",
                    "light_priest", "light_mage", "light_paladin",
                },
            },
        };

        // 敵プリセットは味方プリセットと同じ並び順。
        // 王女は敵側に存在しないため炎の双剣士（imperial_fire_swordsman）で代用する。
        public static readonly List<DebugBattlePreset> Enemies = new()
        {
            new()
            {
                Name = "（空）",
                Description = "全スロット空（手動配置用）",
                UnitIds = new(),
            },
            new()
            {
                Name = "火 6 体",
                Description = "大盾／剣士／剣士／補助／魔導士／弓",
                UnitIds = new()
                {
                    "imperial_fire_tank", "imperial_fire_swordsman", "imperial_fire_swordsman",
                    "imperial_fire_buffer", "imperial_fire_mage", "imperial_fire_archer",
                },
            },
            new()
            {
                Name = "水 6 体",
                Description = "大盾／剣士／補助／弓／大盾／剣士",
                UnitIds = new()
                {
                    "imperial_water_tank", "imperial_water_swordsman", "imperial_water_buffer",
                    "imperial_water_archer", "imperial_water_tank", "imperial_water_swordsman",
                },
            },
            new()
            {
                Name = "光 4 火 2",
                Description = "騎士／騎士／司祭／司祭／炎補助／炎弓",
                UnitIds = new()
                {
                    "imperial_light_paladin", "imperial_light_paladin",
                    "imperial_light_priest",  "imperial_light_priest",
                    "imperial_fire_buffer",   "imperial_fire_archer",
                },
            },
            new()
            {
                Name = "光 4 水 2",
                Description = "騎士／騎士／司祭／司祭／水大盾／水弓",
                UnitIds = new()
                {
                    "imperial_light_paladin", "imperial_light_paladin",
                    "imperial_light_priest",  "imperial_light_priest",
                    "imperial_water_tank",    "imperial_water_archer",
                },
            },
            new()
            {
                // R7 通常皇太子戦と同じ編成（VSPrototypeBossLineups.R7FinalBoss）。
                // 戦闘デバッグは Lv 設定機能なし＝全員 Lv1 になる（本番 R7 は取り巻き Lv3 ＋皇太子 Lv1）。
                Name = "R7 皇太子戦（通常）",
                Description = "水大盾／皇太子／炎補助／光司祭／炎魔導士／光騎士（全員 Lv1）",
                UnitIds = new()
                {
                    "imperial_water_tank",    "imperial_prince",        "imperial_fire_buffer",
                    "imperial_light_priest",  "imperial_fire_mage",     "imperial_light_paladin",
                },
            },
        };
    }
}
