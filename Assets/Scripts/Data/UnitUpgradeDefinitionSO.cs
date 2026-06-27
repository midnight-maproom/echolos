using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>UnitUpgradeDefinition の SO ラッパー。配置先：Assets/Resources/Data/Upgrades/*.asset。</summary>
    [CreateAssetMenu(
        fileName = "upgrade_new",
        menuName = "Echolos/Unit Upgrade Definition",
        order = 103)]
    public sealed class UnitUpgradeDefinitionSO : ScriptableObject
    {
        [SerializeField]
        private UnitUpgradeDefinition _definition = new UnitUpgradeDefinition();

        public UnitUpgradeDefinition Definition => _definition;
    }
}
