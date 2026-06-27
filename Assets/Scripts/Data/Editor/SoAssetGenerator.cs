// Roster から SO アセットを生成する Editor メニュー。
//
// 動作：
// - AlliesRoster + EnemiesRoster + WazaRoster + UpgradeRoster + MetaUpgradeRoster の Definition 列挙を読み、
//   Resources/Data/Units/{id}.asset / Wazas/{id}.asset / Upgrades/{id}.asset / MetaUpgrades/meta_upgrade_{id}.asset
//   を一括生成。
// - 既存アセットは上書き（冪等再実行可能）。
//
// 使い方（ユーザー作業）：
// 1. Unity Editor を開く
// 2. メニュー [Echolos > Data > SO アセットを生成]
using System.IO;
using UnityEditor;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Data.Roster;

namespace Echolos.Data.Editor
{
    /// <summary>Roster から Unit/Waza SO アセットを生成する Editor ツール。</summary>
    public static class SoAssetGenerator
    {
        private const string UnitOutputDir        = "Assets/Resources/Data/Units";
        private const string WazaOutputDir        = "Assets/Resources/Data/Wazas";
        private const string DraftPoolOutputDir   = "Assets/Resources/Data/DraftPools";
        private const string UpgradeOutputDir     = "Assets/Resources/Data/Upgrades";
        private const string MetaUpgradeOutputDir = "Assets/Resources/Data/MetaUpgrades";

        [MenuItem("Echolos/Data/SO アセットを生成")]
        public static void GenerateAll()
        {
            EnsureDirectory(UnitOutputDir);
            EnsureDirectory(WazaOutputDir);
            EnsureDirectory(DraftPoolOutputDir);
            EnsureDirectory(UpgradeOutputDir);
            EnsureDirectory(MetaUpgradeOutputDir);

            int wazaCount = 0;
            foreach (var def in WazaRoster.AllWazas())
            {
                CreateOrUpdateWazaSo(def);
                wazaCount++;
            }

            int upgradeCount = 0;
            foreach (var def in UpgradeRoster.AllUpgrades())
            {
                CreateOrUpdateUpgradeSo(def);
                upgradeCount++;
            }

            int metaUpgradeCount = 0;
            foreach (var def in MetaUpgradeRoster.AllUpgrades())
            {
                CreateOrUpdateMetaUpgradeSo(def);
                metaUpgradeCount++;
            }

            int unitCount = 0;
            foreach (var def in AlliesRoster.AllUnits())
            {
                CreateOrUpdateUnitSo(def);
                unitCount++;
            }
            foreach (var def in EnemiesRoster.AllUnits())
            {
                CreateOrUpdateUnitSo(def);
                unitCount++;
            }
            foreach (var def in BossRoster.AllUnits())
            {
                CreateOrUpdateUnitSo(def);
                unitCount++;
            }

            CreateOrUpdateDraftPoolSo(BuildStandardDraftPool());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SoAssetGenerator] Waza {wazaCount} 件 / Upgrade {upgradeCount} 件 / MetaUpgrade {metaUpgradeCount} 件 / Unit {unitCount} 件 / DraftPool 1 件 生成完了");
        }

        // VSプロト標準ドラフトプール（Normal 10 / Rare 5・固有 2 体は除外）。
        private static DraftPoolDefinition BuildStandardDraftPool()
        {
            var pool = new DraftPoolDefinition
            {
                Id = "vsproto_standard_pool",
                Name = "VSプロト標準プール",
                AllRareSpecialProbability = 0.03f,
                CandidatesPerOffer = 3,
            };
            pool.RarePerSlotProbabilities.Clear();
            pool.RarePerSlotProbabilities.AddRange(new[] { 0.15f, 0.15f, 0.15f });
            pool.NormalUnitIds.AddRange(new[]
            {
                "fire_swordsman", "fire_archer", "fire_tank", "fire_buffer",
                "water_swordsman", "water_archer", "water_tank", "water_buffer",
                "light_paladin", "light_priest",
            });
            pool.RareUnitIds.AddRange(new[]
            {
                "fire_mage", "fire_assassin",
                "water_dispel_tank", "water_healer",
                "light_mage",
            });
            return pool;
        }

        private static void CreateOrUpdateDraftPoolSo(DraftPoolDefinition def)
        {
            string path = $"{DraftPoolOutputDir}/draft_pool_{def.Id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<DraftPoolDefinitionSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<DraftPoolDefinitionSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            var serialized = new SerializedObject(so);
            var defProp = serialized.FindProperty("_definition");
            ApplyDefinition(defProp, def);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
        }

        private static void CreateOrUpdateWazaSo(WazaDefinition def)
        {
            string path = $"{WazaOutputDir}/{def.Id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<WazaDefinitionSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<WazaDefinitionSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            var serialized = new SerializedObject(so);
            var defProp = serialized.FindProperty("_definition");
            ApplyDefinition(defProp, def);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
        }

        private static void CreateOrUpdateUnitSo(UnitDefinition def)
        {
            string path = $"{UnitOutputDir}/{def.Id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<UnitDefinitionSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<UnitDefinitionSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            var serialized = new SerializedObject(so);
            var defProp = serialized.FindProperty("_definition");
            ApplyDefinition(defProp, def);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
        }

        private static void CreateOrUpdateUpgradeSo(UnitUpgradeDefinition def)
        {
            string path = $"{UpgradeOutputDir}/{def.Id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<UnitUpgradeDefinitionSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<UnitUpgradeDefinitionSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            var serialized = new SerializedObject(so);
            var defProp = serialized.FindProperty("_definition");
            ApplyDefinition(defProp, def);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
        }

        // MetaUpgrade SO のファイル名は既存命名規約に合わせ "meta_upgrade_{id}.asset" とする。
        private static void CreateOrUpdateMetaUpgradeSo(MetaUpgradeDefinition def)
        {
            string path = $"{MetaUpgradeOutputDir}/meta_upgrade_{def.Id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<MetaUpgradeDefinitionSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<MetaUpgradeDefinitionSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            var serialized = new SerializedObject(so);
            var defProp = serialized.FindProperty("_definition");
            ApplyDefinition(defProp, def);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
        }

        // POCO の各フィールドを SerializedProperty 経由で SO に書き込む。
        // Unity のシリアライザは public フィールド名で照合する（既定の場合）。
        // ApplyModifiedPropertiesWithoutUndo の後に SetDirty で保存対象化。
        private static void ApplyDefinition(SerializedProperty so, object src)
        {
            if (so == null || src == null) return;
            var srcType = src.GetType();
            var iter = so.Copy();
            var end = iter.GetEndProperty();
            iter.Next(true);
            while (!SerializedProperty.EqualContents(iter, end))
            {
                var field = srcType.GetField(iter.name,
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                    AssignProperty(iter, field.GetValue(src));
                if (!iter.Next(false)) break;
            }
        }

        private static void AssignProperty(SerializedProperty prop, object value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = System.Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = (string)value ?? string.Empty;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = (int)value;
                    break;
                case SerializedPropertyType.Generic:
                    // List<T> や入れ子 POCO は再帰で処理
                    AssignGeneric(prop, value);
                    break;
            }
        }

        private static void AssignGeneric(SerializedProperty prop, object value)
        {
            if (value == null)
            {
                if (prop.isArray) prop.arraySize = 0;
                return;
            }

            // List<T> / Array
            if (prop.isArray && value is System.Collections.IList list)
            {
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    var elem = prop.GetArrayElementAtIndex(i);
                    var item = list[i];
                    if (elem.propertyType == SerializedPropertyType.String)
                        elem.stringValue = (string)item ?? string.Empty;
                    else if (elem.propertyType == SerializedPropertyType.Integer)
                        elem.intValue = System.Convert.ToInt32(item);
                    else if (elem.propertyType == SerializedPropertyType.Enum)
                        elem.enumValueIndex = (int)item;
                    else if (elem.propertyType == SerializedPropertyType.Generic)
                        ApplyDefinition(elem, item);
                }
                return;
            }

            // 単一の構造体／クラス
            ApplyDefinition(prop, value);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
