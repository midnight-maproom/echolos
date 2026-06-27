// IStorySceneCatalog の Resources.LoadAll 実装。
//
// 【役割】
// - Resources/Data/StoryScenes 配下の StorySceneDefinitionSO を lazy init で一括ロード。
// - SO の Definition（POCO）から Domain 完成品 StoryScene に変換して返す
//   （UnitCatalog → Unit と同パターン）。
//
// 【設計方針】
// - DI 抽象なし（依存ゼロ）。Composition Root（Bootstrap）で new StorySceneCatalog() するだけ。
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data.Definitions;
using Echolos.Domain.Catalog;
using Echolos.Domain.Story;

namespace Echolos.Data
{
    /// <summary>IStorySceneCatalog の Resources.LoadAll 実装。</summary>
    public sealed class StorySceneCatalog : IStorySceneCatalog
    {
        private const string ResourcesPath = "Data/StoryScenes";

        private Dictionary<string, StoryScene> _cache;
        private List<StoryScene> _list;

        public StoryScene Get(string sceneId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(sceneId, out var s))
                throw new KeyNotFoundException($"StoryScene not found: {sceneId}");
            return s;
        }

        public bool IsRegistered(string sceneId)
        {
            EnsureLoaded();
            return _cache.ContainsKey(sceneId);
        }

        public IReadOnlyList<StoryScene> GetAll()
        {
            EnsureLoaded();
            return _list;
        }

        private void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, StoryScene>();
            _list = new List<StoryScene>();
            var sos = Resources.LoadAll<StorySceneDefinitionSO>(ResourcesPath);
            foreach (var so in sos)
            {
                if (so == null || so.Definition == null) continue;
                var def = so.Definition;
                if (string.IsNullOrEmpty(def.Id)) continue;
                var scene = BuildScene(def);
                _cache[def.Id] = scene;
                _list.Add(scene);
            }
        }

        private static StoryScene BuildScene(StorySceneDefinition def)
        {
            var pages = new List<StoryPage>();
            if (def.Pages != null)
            {
                foreach (var pageDef in def.Pages)
                {
                    if (pageDef == null) continue;
                    pages.Add(new StoryPage(
                        imagePath: pageDef.ImagePath,
                        narrationText: pageDef.NarrationText,
                        fadeInSeconds: pageDef.FadeInSeconds,
                        displaySeconds: pageDef.DisplaySeconds,
                        fadeOutSeconds: pageDef.FadeOutSeconds,
                        fallbackImagePath: pageDef.FallbackImagePath));
                }
            }
            return new StoryScene(def.Id, pages, def.RepeatNarration);
        }
    }
}
