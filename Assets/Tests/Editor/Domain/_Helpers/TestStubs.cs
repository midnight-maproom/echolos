// テスト用：Domain 抽象の Stub 実装。
//
// 【役割】
// - IMetaRewardFormulaCatalog 等の Domain.Catalog 抽象を、テスト独立性のため
//   本物の Resources.LoadAll 実装ではなく固定値を返す Stub で差し替える。
// - Catalog 本体の動作検証は本物使用の *CatalogTests で別途行う。
//
// 【方針】
// - Stub の数値・パラメタは仕様準拠
//   （perRound=10 / reachedBoss=50 / bridgetRescue=100 / bossDefeated=200 / trueEnd=150）。
// - 数値が SO 由来の本物アセットとズレるとテストが意味を失うので、SO 生成ツールと同じ値を使う。
using System.Collections.Generic;
using Echolos.Domain.Catalog;
using Echolos.Domain.Draft;
using Echolos.Domain.Formula;
using Echolos.Domain.Meta;
using Echolos.Domain.Save;
using Echolos.Domain.Story;

namespace Echolos.Tests.Domain.Helpers
{
    /// <summary>VSプロト標準獲得式 1 件のみ持つ Stub。</summary>
    public sealed class StubMetaRewardFormulaCatalog : IMetaRewardFormulaCatalog
    {
        private readonly MetaRewardFormula _formula;
        private readonly IReadOnlyList<MetaRewardFormula> _list;

        public StubMetaRewardFormulaCatalog()
        {
            var paramDict = new Dictionary<string, float>
            {
                ["perRound"]      = 10f,
                ["reachedBoss"]   = 50f,
                ["bridgetRescue"] = 100f,
                ["bossDefeated"]  = 200f,
                ["trueEnd"]       = 150f,
            };
            _formula = new MetaRewardFormula("vsproto_standard_v1", "vsproto_standard_v1", paramDict);
            _list = new[] { _formula };
        }

        public MetaRewardFormula Get(string formulaId)
        {
            if (formulaId == _formula.Id) return _formula;
            throw new KeyNotFoundException($"StubMetaRewardFormulaCatalog: unknown id '{formulaId}'");
        }

        public bool IsRegistered(string formulaId) => formulaId == _formula.Id;

        public IReadOnlyList<MetaRewardFormula> GetAll() => _list;
    }

    /// <summary>メタ拠点強化 3 項目を持つ Stub。</summary>
    public sealed class StubMetaUpgradeCatalog : IMetaUpgradeCatalog
    {
        private readonly Dictionary<string, MetaUpgrade> _map;
        private readonly IReadOnlyList<MetaUpgrade> _list;

        public StubMetaUpgradeCatalog()
        {
            var list = new List<MetaUpgrade>
            {
                new MetaUpgrade("princess_level", "王女 Lv 強化",        "ラン開始時の王女初期 Lv +1（購入時 3 択選択）", new[] { 30, 60 }, 2),
                new MetaUpgrade("bridget_level",  "ブリジット Lv 強化",  "ラン開始時のブリジット初期 Lv +1（購入時 3 択選択）", new[] { 30, 60 }, 2),
                new MetaUpgrade("action_points",  "行動力 +1",            "毎ラウンドの内政枠 2 → 3",                       new[] { 100 }, 1),
                new MetaUpgrade("initial_unit",   "初期所持ユニット +1", "ラン開始時にランダムで1体追加（最大3回）", new[] { 50, 80, 100 }, 3),
            };
            _list = list;
            _map = new Dictionary<string, MetaUpgrade>();
            foreach (var u in list) _map[u.Id] = u;
        }

        public MetaUpgrade Get(string upgradeId)
        {
            if (_map.TryGetValue(upgradeId, out var u)) return u;
            throw new KeyNotFoundException($"StubMetaUpgradeCatalog: unknown id '{upgradeId}'");
        }

        public bool IsRegistered(string upgradeId) => _map.ContainsKey(upgradeId);

        public IReadOnlyList<MetaUpgrade> GetAll() => _list;
    }

    /// <summary>VSプロト標準ドラフトプール 1 件を持つ Stub。</summary>
    public sealed class StubDraftPoolCatalog : IDraftPoolCatalog
    {
        // SO 生成側と同じ構成（順序含む）。SO ↔ Stub の同期は
        // DraftPoolCatalogTests で本物 Catalog 経由の検証により担保する。
        public static readonly string[] NormalIds = new[]
        {
            "tank_def", "paladin", "atk_multi", "debuffer", "mercenary",
            "archer", "firemage", "healer", "buffer",
        };
        public static readonly string[] RareIds = new[]
        {
            "samurai", "ninja", "tactician", "aoemage", "medic",
        };

        private readonly DraftPool _pool;
        private readonly IReadOnlyList<DraftPool> _list;

        public StubDraftPoolCatalog()
        {
            _pool = new DraftPool(
                id: "vsproto_standard_pool",
                normalUnitIds: NormalIds,
                rareUnitIds: RareIds,
                allRareSpecialProbability: 0.03f,
                rarePerSlotProbabilities: new[] { 0.15f, 0.15f, 0.15f },
                candidatesPerOffer: 3);
            _list = new[] { _pool };
        }

        public DraftPool Get(string poolId)
        {
            if (poolId == _pool.Id) return _pool;
            throw new KeyNotFoundException($"StubDraftPoolCatalog: unknown id '{poolId}'");
        }

        public bool IsRegistered(string poolId) => poolId == _pool.Id;

        public IReadOnlyList<DraftPool> GetAll() => _list;
    }

    /// <summary>VSプロト 13 シーンを軽量に持つ Stub。</summary>
    public sealed class StubStorySceneCatalog : IStorySceneCatalog
    {
        // VSPrototypeStorySceneIds 相当の 13 シーン ID（SO ↔ Stub の同期は StorySceneCatalogTests で担保）。
        public const string IdOpening                 = "opening";
        public const string IdBalduinIntro            = "b_a_balduin";
        public const string IdBalduinLetter           = "b_b1_letter";
        public const string IdBalduinSurrender        = "b_b2_surrender";
        public const string IdMysteriousGirl          = "b_c_girl";
        public const string IdBalduinRescue           = "balduin_rescue";
        public const string IdSwordEmpowered          = "b_e_sword";
        public const string IdBossAttack              = "a_c1_attack";
        public const string IdBossPurify              = "a_c2_purify";
        public const string IdEndingDefeatFirst       = "ending_defeat_first";
        public const string IdEndingDefeatNormalClear = "ending_defeat_normal_clear";
        public const string IdEndingDefeatRepeated    = "ending_defeat_repeated";
        public const string IdEndingTrue              = "ending_true";

        private readonly Dictionary<string, StoryScene> _map;
        private readonly IReadOnlyList<StoryScene> _list;

        public StubStorySceneCatalog()
        {
            // ページ件数は SO 生成側と同じ。
            // SO ↔ Stub の整合は StorySceneCatalogTests（本物 Catalog）で別途検証。
            var list = new List<StoryScene>
            {
                MakeStub(IdOpening,                 6),
                MakeStub(IdBalduinIntro,            3),
                MakeStub(IdBalduinLetter,           3),
                MakeStub(IdBalduinSurrender,        3),
                MakeStub(IdMysteriousGirl,          4),
                MakeStub(IdBalduinRescue,           4),
                MakeStub(IdSwordEmpowered,          3),
                MakeStub(IdBossAttack,              3),
                MakeStub(IdBossPurify,              3),
                MakeStub(IdEndingDefeatFirst,       9),
                MakeStub(IdEndingDefeatNormalClear, 6),
                MakeStub(IdEndingDefeatRepeated,    5),
                MakeStub(IdEndingTrue,              5),
            };
            _list = list;
            _map = new Dictionary<string, StoryScene>();
            foreach (var s in list) _map[s.Id] = s;
        }

        private static StoryScene MakeStub(string id, int pageCount)
        {
            var pages = new List<StoryPage>(pageCount);
            for (int i = 0; i < pageCount; i++)
                pages.Add(new StoryPage($"images/stub/{id}_{i}", $"{id} page {i}"));
            return new StoryScene(id, pages);
        }

        public StoryScene Get(string sceneId)
        {
            if (_map.TryGetValue(sceneId, out var s)) return s;
            throw new KeyNotFoundException($"StubStorySceneCatalog: unknown id '{sceneId}'");
        }

        public bool IsRegistered(string sceneId) => _map.ContainsKey(sceneId);

        public IReadOnlyList<StoryScene> GetAll() => _list;
    }

    /// <summary>
    /// IMetaProgressView の Stub。各フラグ・ラン数を public field で直接設定可能。
    /// </summary>
    public sealed class StubMetaProgressView : IMetaProgressView
    {
        public bool HasReachedTrueEnd { get; set; }
        public bool HasFirstReachedBoss { get; set; }
        public bool HasRescuedBalduin { get; set; }
        public bool HasNotedPendantPower { get; set; }
        public int RunCount { get; set; }

        private readonly Dictionary<string, int> _upgrades = new Dictionary<string, int>();
        public StubMetaProgressView SetUpgradeLevel(string id, int level)
        {
            _upgrades[id] = level;
            return this;
        }
        public int GetUpgradeLevel(string upgradeId)
            => _upgrades.TryGetValue(upgradeId, out var v) ? v : 0;

        private readonly HashSet<string> _seenStorySceneIds = new HashSet<string>();
        public StubMetaProgressView MarkStorySceneSeen(string sceneId)
        {
            _seenStorySceneIds.Add(sceneId);
            return this;
        }
        public bool HasSeenStoryScene(string sceneId)
            => !string.IsNullOrEmpty(sceneId) && _seenStorySceneIds.Contains(sceneId);
    }

    /// <summary>インメモリ Dictionary ベースの ISaveStore Stub。</summary>
    public sealed class StubSaveStore : ISaveStore
    {
        private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

        public string Load(string key)
            => _store.TryGetValue(key, out var v) ? v : string.Empty;

        public void Save(string key, string content)
            => _store[key] = content ?? string.Empty;

        public bool Has(string key) => _store.ContainsKey(key);

        public void Delete(string key) => _store.Remove(key);
    }
}
