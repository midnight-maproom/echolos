// WazaDefinition の ScriptableObject ラッパー。
// Unity Inspector 上で WazaDefinition POCO を編集可能にする薄いラッパー。
// 配置先：Assets/Resources/Data/Wazas/*.asset。
using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>WazaDefinition の SO ラッパー。</summary>
    [CreateAssetMenu(
        fileName = "waza_new",
        menuName = "Echolos/Waza Definition",
        order = 101)]
    public sealed class WazaDefinitionSO : ScriptableObject
    {
        [SerializeField]
        private WazaDefinition _definition = new WazaDefinition();

        public WazaDefinition Definition => _definition;
    }
}
