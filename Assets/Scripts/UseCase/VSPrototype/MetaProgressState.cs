// ラン外で永続するメタ進行状態（メタ通貨／周回数／解禁ユニット／適用済み強化）。
//
// 【設計方針】
// - PlayerPrefs / JSON シリアライズへの依存はこのクラスには持たせない（純Coreドメインモデル）。
//   永続化は MetaProgressStore（Presentation 側）と MetaProgressSerializer で分離実装する。
// - 状態変化は全て API メソッド経由（EarnMemories / SpendMemories / UnlockUnit / ApplyUpgrade）。
//   public set は不可。これにより本番設計の柱（状態変化イベント通知）の前提を継承する。
using System;
using System.Collections.Generic;
using Echolos.Domain.Meta;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>固有ユニットの解禁ID。</summary>
    public static class MetaUnitIds
    {
        /// <summary>ブリジット（バルドゥイン拠点救出で解禁）。</summary>
        public const string Bridget = "bridget";
    }

    /// <summary>メタ進行状態（ラン外永続）。Domain 層には <see cref="IMetaProgressView"/> として読み取り専用で提供する。</summary>
    public sealed class MetaProgressState : IMetaProgressView
    {
        // 強化項目の上限は MetaUpgradeDefinitionSO 側に保持。
        // ApplyUpgrade(id, cap) の cap 引数として呼び出し側（MetaHubGUI が IMetaUpgradeCatalog 経由）が SO 値を渡す。

        private readonly List<string> _unlockedUnits = new List<string>();
        private readonly Dictionary<string, int> _appliedUpgrades = new Dictionary<string, int>();
        // 固有ユニット Lv 強化で選択された Upgrade ID リスト（key=unit_id "princess"/"bridget"、
        // value=購入順に並んだ upgrade_id 列）。StartNewRunCore で先頭から順次 AppliedUpgrades に適用される。
        private readonly Dictionary<string, List<string>> _appliedUpgradeChoices
            = new Dictionary<string, List<string>>();
        // 過去のラン含めて 1 回以上再生した StorySceneId 集合（既見短縮判定に使用）。
        private readonly HashSet<string> _seenStorySceneIds = new HashSet<string>();

        /// <summary>メタ通貨「王国の記憶」の残高。</summary>
        public int Memories { get; private set; }

        /// <summary>完了したラン数(ビター/トゥルー/敗北いずれかの完了で +1)。</summary>
        public int RunCount { get; private set; }

        /// <summary>トゥルーエンドに少なくとも1回到達したか。</summary>
        public bool HasReachedTrueEnd { get; private set; }

        /// <summary>R7 ボス戦に少なくとも1回到達したか（A-b3 ノーマルエンド演出条件）。</summary>
        public bool HasFirstReachedBoss { get; private set; }

        /// <summary>バルドゥインを救出済か（マップ左列救援済化＋ B 系列スキップ条件）。</summary>
        public bool HasRescuedBalduin { get; private set; }

        /// <summary>ペンダント気づきイベントを完了したか（A-c2 経路解禁＋ B-e 発火条件）。</summary>
        public bool HasNotedPendantPower { get; private set; }

        /// <summary>解禁済の固有ユニットID（読み取り専用ビュー）。</summary>
        public IReadOnlyList<string> UnlockedUnits => _unlockedUnits;

        /// <summary>適用済み強化（key=強化ID, value=現在Lv）の読み取り専用ビュー。</summary>
        public IReadOnlyDictionary<string, int> AppliedUpgrades => _appliedUpgrades;

        /// <summary>再生済 StorySceneId 集合（読み取り専用ビュー・シリアライザから列挙する用）。</summary>
        public IReadOnlyCollection<string> SeenStorySceneIds => _seenStorySceneIds;

        /// <summary>
        /// 固有ユニット Lv 強化で選択された Upgrade ID リストの読み取り専用ビュー
        /// （key=unit_id, value=購入順に並んだ upgrade_id 列）。
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> AppliedUpgradeChoices
        {
            get
            {
                var view = new Dictionary<string, IReadOnlyList<string>>();
                foreach (var kv in _appliedUpgradeChoices) view[kv.Key] = kv.Value;
                return view;
            }
        }

        // メタ通貨

        /// <summary>メタ通貨を獲得する。負値は不正。</summary>
        public void EarnMemories(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "獲得量は0以上");
            Memories += amount;
        }

        /// <summary>
        /// メタ通貨を消費する。残高不足の場合は false を返し、状態は変更しない。
        /// 成功時のみ Memories を減らす。
        /// </summary>
        public bool SpendMemories(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "消費量は0以上");
            if (Memories < amount) return false;
            Memories -= amount;
            return true;
        }

        // 周回数

        /// <summary>ラン完了時にカウンタを進める。</summary>
        public void IncrementRunCount()
        {
            RunCount++;
        }

        // 固有ユニットの解禁

        /// <summary>
        /// 固有ユニットを解禁する。既に解禁済なら何もせず false を返す（冪等）。
        /// </summary>
        public bool UnlockUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                throw new ArgumentException("unitId は必須", nameof(unitId));
            if (_unlockedUnits.Contains(unitId)) return false;
            _unlockedUnits.Add(unitId);
            return true;
        }

        /// <summary>指定ユニットが解禁済か。</summary>
        public bool IsUnitUnlocked(string unitId)
        {
            return _unlockedUnits.Contains(unitId);
        }

        // メタ強化の適用

        /// <summary>
        /// 指定強化を1段階適用する（上限は呼び出し側で渡す）。
        /// 上限到達済なら false。それ以外は新Lvを上書きして true。
        /// </summary>
        public bool ApplyUpgrade(string upgradeId, int cap)
        {
            if (string.IsNullOrEmpty(upgradeId))
                throw new ArgumentException("upgradeId は必須", nameof(upgradeId));
            if (cap <= 0)
                throw new ArgumentOutOfRangeException(nameof(cap), "cap は1以上");

            int current = GetUpgradeLevel(upgradeId);
            if (current >= cap) return false;
            _appliedUpgrades[upgradeId] = current + 1;
            return true;
        }

        /// <summary>指定強化の現在Lv（未適用は0）。</summary>
        public int GetUpgradeLevel(string upgradeId)
        {
            return _appliedUpgrades.TryGetValue(upgradeId, out var lv) ? lv : 0;
        }

        // 固有ユニット Lv 強化の選択結果

        /// <summary>
        /// 固有ユニットの Lv 強化購入時に選んだ Upgrade ID を末尾追加する。
        /// 既存リストの末尾に積むことで「購入順 = 適用順」を保証する。
        /// </summary>
        public void ApplyUpgradeChoice(string unitId, string upgradeId)
        {
            if (string.IsNullOrEmpty(unitId))
                throw new ArgumentException("unitId は必須", nameof(unitId));
            if (string.IsNullOrEmpty(upgradeId))
                throw new ArgumentException("upgradeId は必須", nameof(upgradeId));

            if (!_appliedUpgradeChoices.TryGetValue(unitId, out var list))
                _appliedUpgradeChoices[unitId] = list = new List<string>();
            list.Add(upgradeId);
        }

        /// <summary>指定ユニットの選択済 Upgrade ID リスト（未選択は空リスト）。</summary>
        public IReadOnlyList<string> GetUpgradeChoices(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return System.Array.Empty<string>();
            return _appliedUpgradeChoices.TryGetValue(unitId, out var list)
                ? list
                : (IReadOnlyList<string>)System.Array.Empty<string>();
        }

        // トゥルーエンド到達フラグ

        /// <summary>トゥルーエンドに到達したことを記録する（冪等）。</summary>
        public void MarkTrueEndReached()
        {
            HasReachedTrueEnd = true;
        }

        /// <summary>R7 ボス戦到達を記録する（冪等・A-b3 ノーマルエンド演出を一度だけ出すため）。</summary>
        public void MarkFirstReachedBoss()
        {
            HasFirstReachedBoss = true;
        }

        /// <summary>バルドゥイン救出を記録する（冪等・マップ動的化＋ B 系列スキップを永続化）。</summary>
        public void MarkBalduinRescued()
        {
            HasRescuedBalduin = true;
        }

        /// <summary>ペンダント気づきを記録する（冪等・A-c2 経路解禁＋ B-e 発火条件）。</summary>
        public void MarkPendantPowerNoted()
        {
            HasNotedPendantPower = true;
        }

        // ストーリー既見管理

        /// <summary>指定 StorySceneId を既見としてマークする（冪等）。</summary>
        public void MarkStorySceneSeen(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
                throw new ArgumentException("sceneId は必須", nameof(sceneId));
            _seenStorySceneIds.Add(sceneId);
        }

        /// <summary>指定 StorySceneId を過去のラン含めて 1 回以上再生済か。</summary>
        public bool HasSeenStoryScene(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return false;
            return _seenStorySceneIds.Contains(sceneId);
        }

        // シリアライザ専用 API：状態の一括復元

        /// <summary>
        /// 永続化された状態から一括復元する（MetaProgressSerializer から呼ぶ用）。
        /// 通常のアプリコードからは呼ばない。Earn/Spend/Unlock/Apply の通常 API を使うこと。
        /// 不正値（負数）は ArgumentOutOfRangeException、null コレクションは空として許容する。
        /// </summary>
        public void LoadFromSerializedState(
            int memories, int runCount, bool hasReachedTrueEnd,
            IEnumerable<string> unlockedUnits,
            IDictionary<string, int> appliedUpgrades,
            bool hasFirstReachedBoss = false,
            bool hasRescuedBalduin = false,
            bool hasNotedPendantPower = false,
            IDictionary<string, List<string>> appliedUpgradeChoices = null,
            IEnumerable<string> seenStorySceneIds = null)
        {
            if (memories < 0) throw new ArgumentOutOfRangeException(nameof(memories));
            if (runCount < 0) throw new ArgumentOutOfRangeException(nameof(runCount));

            Memories = memories;
            RunCount = runCount;
            HasReachedTrueEnd = hasReachedTrueEnd;
            HasFirstReachedBoss = hasFirstReachedBoss;
            HasRescuedBalduin = hasRescuedBalduin;
            HasNotedPendantPower = hasNotedPendantPower;

            _unlockedUnits.Clear();
            if (unlockedUnits != null)
                foreach (var u in unlockedUnits)
                    if (!string.IsNullOrEmpty(u) && !_unlockedUnits.Contains(u))
                        _unlockedUnits.Add(u);

            _appliedUpgrades.Clear();
            if (appliedUpgrades != null)
                foreach (var kv in appliedUpgrades)
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0)
                        _appliedUpgrades[kv.Key] = kv.Value;

            _appliedUpgradeChoices.Clear();
            if (appliedUpgradeChoices != null)
                foreach (var kv in appliedUpgradeChoices)
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null && kv.Value.Count > 0)
                        _appliedUpgradeChoices[kv.Key] = new List<string>(kv.Value);

            _seenStorySceneIds.Clear();
            if (seenStorySceneIds != null)
                foreach (var id in seenStorySceneIds)
                    if (!string.IsNullOrEmpty(id))
                        _seenStorySceneIds.Add(id);
        }
    }
}
