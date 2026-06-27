// マス種別ごとの敵編成ファクトリ。
//
// 動作：
// - マス種別 → プール（弱／中／強）対応表で選択し、プールから N 体抽選する。
// - 自領防衛のみ「ラウンド進行で徐々に厳しくなる」設計：R1-2=弱／R3-5=中／R6=強。
//   敵領／敵拠点は常に中／強（攻め込む側の難度はマス固有・反転攻勢のタイミング判断を促す）。
// - 抽選は「Required 確定枠（各グループから 1 体）＋ Random 抽選枠（残り体数を Fisher–Yates）」の二段。
// - 抽選後、Unit.SortOrder の昇順で stable sort して SlotIndex 0 から順に並べる。
//   これで「弓が最前列に出る」事故を排除し、編成の強さ下限を担保する。
// - 各エントリの Level に応じて AvailableUpgrades の先頭から AppliedUpgrades に積む。
//
// 編成数：自領 R1-2=2／R3-5=3／R6=5 / 敵領 4 / 敵拠点 5 / 本拠地戦 自領防衛と同じ / R7 ボス 6。
// 敵編成は属性混在で良い。
using System;
using System.Collections.Generic;
using Echolos.Domain.Catalog;
using Echolos.Domain.Models;

namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロトのマス別敵編成ファクトリ（プール抽選方式）。</summary>
    public sealed class VSPrototypeEnemyPatterns
    {
        // 自領防衛の体数は R 帯で分岐（プール切替と同じ境界）。
        // R1-2 弱は 2 体（プレイヤーの初期手札 2 体と数を揃え、立ち上がりの難度を下げる）。
        // R3-5 中は 3 体（中盤の圧力をプール強化で表現）。
        // R6 強は 5 体（敵拠点と完全一致＝R6 は強プール × 5 体で本格的な圧）。
        private const int WeakFriendlyCount = 2;
        private const int MidFriendlyCount = 3;
        private const int StrongFriendlyCount = 5;
        private const int EnemyTerritoryCount = 4;
        private const int EnemyStrongholdCount = 5;

        private readonly IUnitCatalog _unitCatalog;
        private readonly Func<int> _rng;

        public VSPrototypeEnemyPatterns(IUnitCatalog unitCatalog, Func<int> rng = null)
        {
            _unitCatalog = unitCatalog ?? throw new ArgumentNullException(nameof(unitCatalog));
            _rng = rng ?? (() => 0); // null は決定論的フォールバック（テスト用）
        }

        /// <summary>
        /// 自領防衛戦の敵編成。プール・体数とも R 帯で切替：R1-2=弱2／R3-5=中3／R6=強3。
        /// 「防衛だけで徐々に厳しくなる」圧でプレイヤーに反転攻勢のタイミング判断を迫る設計。
        /// </summary>
        public List<RuntimeUnit> CreateFriendlyDefenseEnemies(int round)
        {
            var (pool, count) = SelectFriendlyDefense(round);
            return BuildFromPool(pool, count);
        }

        /// <summary>敵領反転攻勢戦の敵編成（中プールから抽選・4 体・ラウンド不問）。</summary>
        public List<RuntimeUnit> CreateEnemyTerritoryEnemies() =>
            BuildFromPool(VSPrototypeEnemyPools.Mid(), EnemyTerritoryCount);

        /// <summary>敵拠点攻略戦の敵編成（強プールから抽選・5 体・ラウンド不問）。</summary>
        public List<RuntimeUnit> CreateEnemyStrongholdEnemies() =>
            BuildFromPool(VSPrototypeEnemyPools.Strong(), EnemyStrongholdCount);

        /// <summary>
        /// 本拠地戦の侵攻軍編成。プール・体数とも自領防衛と同じ R 帯切替：
        /// R1-2=弱2／R3-5=中3／R6=強3。本拠地戦は連戦ではなく 1 ラウンド 1 回
        /// （自領陥落が 1 つ以上残っているラウンドで発生）。
        /// </summary>
        public List<RuntimeUnit> CreateHomeInvasionEnemies(int round)
        {
            var (pool, count) = SelectFriendlyDefense(round);
            return BuildFromPool(pool, count);
        }

        /// <summary>
        /// R7 本拠地ラスボス戦の編成。HasNotedPendantPower で経路分岐：
        /// - true（A-c2 浄化版）：通常皇太子＋取り巻き 5 体の固定 6 体編成
        /// - false（A-c1 必敗版）：闇皇太子単騎（HP/DEF 9999/999 ＋自己 AttackUp 永続スタックで詰み）
        /// 単騎にしているのは「皇太子を倒せなくても他敵を全滅させて辛勝」を防ぐため。
        /// SortOrder ソートは bypass し、Lineup の List 順をそのまま SlotIndex に割り付ける。
        /// </summary>
        public List<RuntimeUnit> CreateBossPattern(bool hasNotedPendantPower) =>
            BuildFromLineup(hasNotedPendantPower
                ? VSPrototypeBossLineups.R7FinalBoss()
                : VSPrototypeBossLineups.R7FinalBossDark());

        /// <summary>マス種別から該当する敵編成を生成する（R7 ボス戦は呼ばない・別経路）。</summary>
        public List<RuntimeUnit> CreateForNode(MapNodeKind kind, int round)
        {
            switch (kind)
            {
                case MapNodeKind.Friendly:        return CreateFriendlyDefenseEnemies(round);
                case MapNodeKind.EnemyTerritory:  return CreateEnemyTerritoryEnemies();
                case MapNodeKind.EnemyStronghold: return CreateEnemyStrongholdEnemies();
                case MapNodeKind.Home:
                    throw new InvalidOperationException(
                        "Home の敵編成は CreateBossPattern / CreateHomeInvasionEnemies を直接呼ぶこと");
                default:
                    return new List<RuntimeUnit>();
            }
        }

        // 自領防衛のラウンド別プール＋体数対応：R1-2=弱 2／R3-5=中 3／R6=強 3。
        // R7 は自領防衛が発生しない（本拠地ボス戦のみ）ため対象外。
        // 本拠地戦の侵攻軍も同じプール切替を使う（体数は呼び出し側で別途指定）。
        private static (VSPrototypeEnemyPool pool, int count) SelectFriendlyDefense(int round)
        {
            if (round >= 6) return (VSPrototypeEnemyPools.Strong(), StrongFriendlyCount);
            if (round >= 3) return (VSPrototypeEnemyPools.Mid(), MidFriendlyCount);
            return (VSPrototypeEnemyPools.Weak(), WeakFriendlyCount);
        }

        // 固定編成リストを List 順そのまま SlotIndex に割り付ける（SortOrder ソート bypass）。
        // ラスボス編成のように戦術意図を持った順番を保ちたい用途。
        private List<RuntimeUnit> BuildFromLineup(IList<VSPrototypeEnemyPoolEntry> lineup)
        {
            var list = new List<RuntimeUnit>(lineup.Count);
            for (int slot = 0; slot < lineup.Count; slot++)
            {
                var unit = LoadAndLevelUp(lineup[slot]);
                list.Add(new RuntimeUnit(unit, slot));
            }
            return list;
        }

        // プールから指定数を抽選し、Unit.SortOrder の昇順で並べた RuntimeUnit リストを返す。
        // 内訳：Required 確定枠（各グループから 1 体）＋ Random 抽選枠（残数を重複なく抽選）。
        private List<RuntimeUnit> BuildFromPool(VSPrototypeEnemyPool pool, int targetCount)
        {
            var picked = new List<VSPrototypeEnemyPoolEntry>();

            // Required: 各グループから 1 体ずつ抽選（目標数を超えない範囲で）
            foreach (var group in pool.Required)
            {
                if (picked.Count >= targetCount) break;
                if (group.Candidates.Count == 0) continue;
                int idx = Mod(_rng(), group.Candidates.Count);
                picked.Add(group.Candidates[idx]);
            }

            // Random: 残数を Fisher–Yates で重複なく抽選
            int randomCount = Math.Max(0, targetCount - picked.Count);
            if (randomCount > 0 && pool.Random.Count > 0)
            {
                var randomPicks = PickIndices(pool.Random.Count, randomCount);
                foreach (var idx in randomPicks)
                    picked.Add(pool.Random[idx]);
            }

            // Unit 化＋ SortOrder で並べ替え（stable sort・同 SortOrder は抽選順そのまま）
            var withUnits = new List<(Unit unit, VSPrototypeEnemyPoolEntry entry)>(picked.Count);
            foreach (var entry in picked)
            {
                var unit = LoadAndLevelUp(entry);
                withUnits.Add((unit, entry));
            }
            withUnits.Sort((a, b) => a.unit.SortOrder.CompareTo(b.unit.SortOrder));

            var list = new List<RuntimeUnit>(withUnits.Count);
            for (int slot = 0; slot < withUnits.Count; slot++)
                list.Add(new RuntimeUnit(withUnits[slot].unit, slot));
            return list;
        }

        // Fisher–Yates の先頭 N 個シャッフルで重複なく N 個のインデックスを返す。
        private List<int> PickIndices(int total, int count)
        {
            int take = Math.Min(count, total);
            var indices = new List<int>(total);
            for (int i = 0; i < total; i++) indices.Add(i);
            for (int i = 0; i < take; i++)
            {
                int swap = i + Mod(_rng(), total - i);
                int tmp = indices[i];
                indices[i] = indices[swap];
                indices[swap] = tmp;
            }
            var result = new List<int>(take);
            for (int i = 0; i < take; i++) result.Add(indices[i]);
            return result;
        }

        // 負値を返す rng でも index out-of-range にならないよう、非負剰余で整える。
        private static int Mod(int value, int modulus)
        {
            int m = value % modulus;
            return m < 0 ? m + modulus : m;
        }

        // エントリの Unit ID を Catalog で解決し、目標 Lv まで先頭 Upgrade から順に適用する。
        private Unit LoadAndLevelUp(VSPrototypeEnemyPoolEntry entry)
        {
            var unit = _unitCatalog.Get(entry.UnitId);
            int targetLevel = Math.Min(entry.Level, VSPrototypeInteriorState.MaxUnitLevel);
            while (unit.Level < targetLevel && unit.AvailableUpgrades.Count > 0)
            {
                var pick = unit.AvailableUpgrades[0];
                unit.AvailableUpgrades.RemoveAt(0);
                unit.AppliedUpgrades.Add(pick);
                unit.Level++;
            }
            return unit;
        }
    }
}
