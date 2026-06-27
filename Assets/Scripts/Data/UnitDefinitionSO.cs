// UnitDefinition の ScriptableObject ラッパー。
//
// 役割：Unity Inspector 上で UnitDefinition POCO を編集可能にする薄いラッパー。
// 配置先：Assets/Resources/Data/Units/*.asset（Resources.LoadAll で一括ロード）。
using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>UnitDefinition の SO ラッパー（Unity Inspector 編集用）。</summary>
    [CreateAssetMenu(
        fileName = "unit_new",
        menuName = "Echolos/Unit Definition",
        order = 100)]
    public sealed class UnitDefinitionSO : ScriptableObject
    {
        [SerializeField]
        private UnitDefinition _definition = new UnitDefinition();

        /// <summary>定義データ（Catalog が読み取って Unit を構築する）。</summary>
        public UnitDefinition Definition => _definition;
    }
}
