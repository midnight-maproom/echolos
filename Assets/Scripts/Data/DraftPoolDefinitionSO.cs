// DraftPoolDefinition の ScriptableObject ラッパー。
// 配置先：Assets/Resources/Data/DraftPools/*.asset。
using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>DraftPoolDefinition の SO ラッパー（Unity Inspector 編集用）。</summary>
    [CreateAssetMenu(
        fileName = "draft_pool_new",
        menuName = "Echolos/Draft Pool",
        order = 104)]
    public sealed class DraftPoolDefinitionSO : ScriptableObject
    {
        [SerializeField]
        private DraftPoolDefinition _definition = new DraftPoolDefinition();

        /// <summary>定義データ（Catalog が読み取って Domain 型に変換する）。</summary>
        public DraftPoolDefinition Definition => _definition;
    }
}
