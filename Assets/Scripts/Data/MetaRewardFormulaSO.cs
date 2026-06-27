// MetaRewardFormulaDefinition の ScriptableObject ラッパー。
// Unity Inspector 上で POCO を編集可能にする薄いラッパー。
// 配置先：Assets/Resources/Data/MetaReward/*.asset。
using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>MetaRewardFormulaDefinition の SO ラッパー（Unity Inspector 編集用）。</summary>
    [CreateAssetMenu(
        fileName = "meta_reward_formula_new",
        menuName = "Echolos/Meta Reward Formula",
        order = 102)]
    public sealed class MetaRewardFormulaSO : ScriptableObject
    {
        [SerializeField]
        private MetaRewardFormulaDefinition _definition = new MetaRewardFormulaDefinition();

        /// <summary>定義データ（Catalog が読み取って Registry に渡す）。</summary>
        public MetaRewardFormulaDefinition Definition => _definition;
    }
}
