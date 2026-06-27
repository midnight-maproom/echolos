// StorySceneDefinition の ScriptableObject ラッパー。
// 配置先：Assets/Resources/Data/StoryScenes/*.asset。
using UnityEngine;
using Echolos.Data.Definitions;

namespace Echolos.Data
{
    /// <summary>StorySceneDefinition の SO ラッパー（Unity Inspector 編集用）。</summary>
    [CreateAssetMenu(
        fileName = "story_scene_new",
        menuName = "Echolos/Story Scene",
        order = 105)]
    public sealed class StorySceneDefinitionSO : ScriptableObject
    {
        [SerializeField]
        private StorySceneDefinition _definition = new StorySceneDefinition();

        /// <summary>定義データ（Catalog が読み取って Domain 型に変換する）。</summary>
        public StorySceneDefinition Definition => _definition;
    }
}
