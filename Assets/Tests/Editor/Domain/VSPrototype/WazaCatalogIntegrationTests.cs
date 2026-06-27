// WazaCatalog（SO ローダ）の整合性テスト。
//
// 検証方針：
// - 本物 SO + Resources.LoadAll で動く統合テスト。
// - WazaPattern enum に応じて Effects に正しい IActionEffect インスタンスが
//   組まれているかを構造的に担保。
using System.Linq;
using NUnit.Framework;
using Echolos.Data;
using Echolos.Data.Roster;
using Echolos.Domain.Battle.Skills;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class WazaCatalogIntegrationTests
    {
        private WazaCatalog _wazaCatalog;

        [SetUp]
        public void SetUp() => _wazaCatalog = new WazaCatalog();

        [Test]
        public void Roster_AllWazas_17件()
        {
            // 通常皇太子 2 種（waza_dark_blade / waza_dark_wave）＋
            // 闇皇太子 1 種（waza_dark_sweep・A-c1 必敗形態専用）。
            int count = WazaRoster.AllWazas().Count();
            Assert.AreEqual(17, count);
        }

        [Test]
        public void SO_全Waza_ロード可能()
        {
            foreach (var def in WazaRoster.AllWazas())
                Assert.IsTrue(_wazaCatalog.IsRegistered(def.Id), $"Waza {def.Id} の SO が無い");
        }

        [Test]
        public void Get_Attack系_AttackEffect1件()
        {
            var w = _wazaCatalog.Get("waza_attack_basic_melee");
            Assert.AreEqual(1, w.Effects.Count);
            Assert.IsInstanceOf<AttackEffect>(w.Effects[0]);
        }

        [Test]
        public void Get_AttackWithStatusRider_炎の弓兵_AttackEffectのみ_Riderは内部保持()
        {
            // Pattern=AttackWithStatusRider は Effects に AttackEffect 1 件で、
            // onHitRiders が AttackEffect の内部リストとして組まれる。
            var w = _wazaCatalog.Get("waza_fire_arrow");
            Assert.AreEqual(1, w.Effects.Count);
            Assert.IsInstanceOf<AttackEffect>(w.Effects[0]);
        }

        [Test]
        public void Get_Heal_HealEffect1件()
        {
            var w = _wazaCatalog.Get("waza_lesser_heal");
            Assert.AreEqual(1, w.Effects.Count);
            Assert.IsInstanceOf<HealEffect>(w.Effects[0]);
        }

        [Test]
        public void Get_HealAndDispelDebuffs_2件_順序はHeal先Dispel後()
        {
            var w = _wazaCatalog.Get("waza_purify_heal");
            Assert.AreEqual(2, w.Effects.Count);
            Assert.IsInstanceOf<HealEffect>(w.Effects[0]);
            Assert.IsInstanceOf<DispelDebuffsEffect>(w.Effects[1]);
        }

        [Test]
        public void Get_ApplyStatusEffect_鼓舞_ApplyStatusEffectEffect1件()
        {
            var w = _wazaCatalog.Get("waza_battle_cry");
            Assert.AreEqual(1, w.Effects.Count);
            Assert.IsInstanceOf<ApplyStatusEffectEffect>(w.Effects[0]);
        }

        [Test]
        public void Get_DispelEnemyBuffs_看破_DispelBuffsEffect1件()
        {
            var w = _wazaCatalog.Get("waza_dispel_aura");
            Assert.AreEqual(1, w.Effects.Count);
            Assert.IsInstanceOf<DispelBuffsEffect>(w.Effects[0]);
        }

        [Test]
        public void Get_呼ぶたびに別インスタンス()
        {
            var a = _wazaCatalog.Get("waza_attack_basic_melee");
            var b = _wazaCatalog.Get("waza_attack_basic_melee");
            Assert.AreNotSame(a, b, "Get は呼ぶたび新しい Waza を返す");
        }
    }
}
