// UnitCatalog（SO ローダ）の整合性テスト。
//
// 検証方針：
// - 本物 SO + Resources.LoadAll で動く統合テスト（Stub Catalog ではない）。
// - 仕様準拠の件数・Id・各 Unit の主要プロパティ・PersistentEffects の整合性を担保。
// - 数値検証は Roster.cs と SO 値が一致することのみ確認（個別バランスはバランス調整側で扱う）。
using System.Linq;
using NUnit.Framework;
using Echolos.Data;
using Echolos.Data.Roster;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class UnitCatalogIntegrationTests
    {
        private WazaCatalog _wazaCatalog;
        private UnitCatalog _unitCatalog;

        [SetUp]
        public void SetUp()
        {
            _wazaCatalog = new WazaCatalog();
            _unitCatalog = new UnitCatalog(_wazaCatalog, new UnitUpgradeCatalog());
        }

        [Test]
        public void Roster_AlliesAllUnits_17体()
        {
            int count = AlliesRoster.AllUnits().Count();
            Assert.AreEqual(17, count, "Roster は固有 2＋ Normal 10＋ Rare 5＝ 17 体");
        }

        [Test]
        public void Roster_EnemiesAllUnits_11体_通常プール10体プラス皇太子戦専用1体()
        {
            int count = EnemiesRoster.AllUnits().Count();
            Assert.AreEqual(11, count,
                "通常プール 10 体（剣士/弓兵/大盾兵/補助兵 × 火水 ＋ 騎士／司祭）" +
                "＋皇太子戦専用 1 体（帝国大魔導士）。レア相当 4 体は削除済（900 §7.10）");
        }

        [Test]
        public void SO_AlliesEnemiesBoss_全ユニットロード可能()
        {
            foreach (var def in AlliesRoster.AllUnits())
                Assert.IsTrue(_unitCatalog.IsRegistered(def.Id), $"味方 {def.Id} の SO が無い");
            foreach (var def in EnemiesRoster.AllUnits())
                Assert.IsTrue(_unitCatalog.IsRegistered(def.Id), $"帝国 {def.Id} の SO が無い");
            foreach (var def in BossRoster.AllUnits())
                Assert.IsTrue(_unitCatalog.IsRegistered(def.Id), $"ボス {def.Id} の SO が無い");
        }

        [Test]
        public void SO_GetAllIds_30件以上()
        {
            int ids = _unitCatalog.GetAllIds().Count();
            Assert.GreaterOrEqual(ids, 30,
                $"Allies 17＋ Enemies 11＋ Boss 2（皇太子＋闇皇太子）で 30 以上のはず（実測 {ids}）");
        }

        [Test]
        public void SO_Get_BridgetPrincess_固有Id整合()
        {
            var bridget = _unitCatalog.Get(UniqueUnitIds.Bridget);
            var princess = _unitCatalog.Get(UniqueUnitIds.Princess);
            Assert.AreEqual(UniqueUnitIds.Bridget, bridget.Id);
            Assert.AreEqual(UniqueUnitIds.Princess, princess.Id);
            Assert.AreEqual(Element.Fire, bridget.UnitElement);
            Assert.AreEqual(Element.Light, princess.UnitElement);
        }

        [Test]
        public void SO_Get_数値がRosterと一致_炎の大盾兵()
        {
            var rosterDef = AlliesRoster.FireTank();
            var unit = _unitCatalog.Get(rosterDef.Id);
            Assert.AreEqual(rosterDef.MaxHP, unit.MaxHP);
            Assert.AreEqual(rosterDef.BaseATK, unit.BaseATK);
            Assert.AreEqual(rosterDef.DEF, unit.DEF);
            Assert.AreEqual(rosterDef.BaseSPD, unit.BaseSPD);
        }

        [Test]
        public void SO_Get_BaseWazas_Waza型_要素数1()
        {
            var unit = _unitCatalog.Get("fire_swordsman");
            Assert.IsNotNull(unit.BaseWazas);
            Assert.AreEqual(1, unit.BaseWazas.Count);
            Assert.AreEqual("waza_double_strike", unit.BaseWazas[0].Id);
        }

        [Test]
        public void SO_Get_PersistentEffects_焔影の暗殺者_CriticalRateUp10()
        {
            var unit = _unitCatalog.Get("fire_assassin");
            Assert.AreEqual(1, unit.PersistentEffects.Count);
            var e = unit.PersistentEffects[0];
            Assert.AreEqual(EffectKind.CriticalRateUp, e.Kind);
            Assert.AreEqual(10f, e.Magnitude);
            Assert.IsTrue(e.IsUndispellable, "Persistent は IsUndispellable=true");
        }

        [Test]
        public void SO_Get_PersistentEffects_水の大盾兵_2件_SilencedCounterと被ダメ低下()
        {
            var unit = _unitCatalog.Get("water_tank");
            Assert.AreEqual(2, unit.PersistentEffects.Count);
            var types = unit.PersistentEffects.Select(e => e.Kind).ToList();
            CollectionAssert.Contains(types, EffectKind.SilencedCounter);
            CollectionAssert.Contains(types, EffectKind.IncomingDamageDown);
            foreach (var e in unit.PersistentEffects)
                Assert.IsTrue(e.IsUndispellable, "全 Persistent は IsUndispellable=true");
        }

        [Test]
        public void SO_Get_帝国コピー_味方と基礎ステ同値かつElementNone()
        {
            var ally = _unitCatalog.Get("fire_swordsman");
            var imperial = _unitCatalog.Get("imperial_fire_swordsman");
            Assert.AreEqual(ally.MaxHP, imperial.MaxHP);
            Assert.AreEqual(ally.BaseATK, imperial.BaseATK);
            Assert.AreEqual(ally.DEF, imperial.DEF);
            Assert.AreEqual(ally.BaseSPD, imperial.BaseSPD);
            Assert.AreEqual(Element.None, imperial.UnitElement,
                "帝国軍は一律 Element.None（VSプロト範囲では地形効果も属性シナジーも敵不関与）");
        }

        [Test]
        public void SO_Get_呼ぶたびに別インスタンス_永続データ破壊防止()
        {
            var a = _unitCatalog.Get("fire_swordsman");
            var b = _unitCatalog.Get("fire_swordsman");
            Assert.AreNotSame(a, b, "Get は呼ぶたび新しい Unit を返す（戦闘で CurrentHP を触っても SO が壊れない）");
        }
    }
}
