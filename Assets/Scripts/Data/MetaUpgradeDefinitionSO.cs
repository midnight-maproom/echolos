// MetaUpgradeDefinition の ScriptableObject ラッパー。
// Unity Inspector 上で POCO を編集可能にする薄いラッパー。
// 配置先：Assets/Resources/Data/MetaUpgrades/*.asset。
using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>MetaUpgradeDefinition の SO ラッパー（Unity Inspector 編集用）。</summary>
    [CreateAssetMenu(
        fileName = "meta_upgrade_new",
        menuName = "Echolos/Meta Upgrade",
        order = 103)]
    public sealed class MetaUpgradeDefinitionSO : ScriptableObject
    {
        [SerializeField]
        private MetaUpgradeDefinition _definition = new MetaUpgradeDefinition();

        /// <summary>定義データ（Catalog が読み取って Domain 型に変換する）。</summary>
        public MetaUpgradeDefinition Definition => _definition;
    }
}
