using System.Collections.Generic;
using NUnit.Framework;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class SynergyApplierTests
    {
        private const string FireName = "炎の共鳴";
        private const string WaterName = "水の共鳴";
        private const string LightName = "光の共鳴";

        private static RuntimeUnit Make(Element el, int slot, int atk = 50, int hp = 100, int def = 0)
        {
            var u = new Unit($"{el}_{slot}", $"{el}_{slot}", el)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                DEF = def,
                AttackKind = AttackKind.Melee,
                State = UnitState.Active,
            };
            return new RuntimeUnit(u, slot);
        }

        private static BattleContext MakeContext(
            List<RuntimeUnit> allies = null, List<RuntimeUnit> enemies = null)
        {
            return new BattleContext(10)
            {
                AllyUnits = allies ?? new List<RuntimeUnit>(),
                EnemyUnits = enemies ?? new List<RuntimeUnit>(),
            };
        }

        // SourceAbilityName は SynergyApplier が「{def.SourceAbilityName} Lv{count}」形式で
        // 埋めるため、テストは先頭一致（StartsWith）で同 Source を識別する。
        private static int SumMagnitudeBy(RuntimeUnit ru, EffectKind kind, string sourceName)
        {
            int total = 0;
            foreach (var e in ru.ActiveEffects)
            {
                if (e == null) continue;
                if (e.Kind != kind) continue;
                if (e.SourceAbilityName == null || !e.SourceAbilityName.StartsWith(sourceName)) continue;
                total += (int)TestEff.MagnitudeOf(e) * e.Stacks;
            }
            return total;
        }

        private static int SumStacksBy(RuntimeUnit ru, EffectKind kind, string sourceName)
        {
            int total = 0;
            foreach (var e in ru.ActiveEffects)
            {
                if (e == null) continue;
                if (e.Kind != kind) continue;
                if (e.SourceAbilityName == null || !e.SourceAbilityName.StartsWith(sourceName)) continue;
                total += e.Stacks;
            }
            return total;
        }

        private static int CountBy(RuntimeUnit ru, EffectKind kind, string sourceName)
        {
            int n = 0;
            foreach (var e in ru.ActiveEffects)
            {
                if (e == null) continue;
                if (e.Kind == kind
                    && e.SourceAbilityName != null
                    && e.SourceAbilityName.StartsWith(sourceName)) n++;
            }
            return n;
        }

        // ───── 共通 ─────

        [Test]
        public void ApplyAll_null安全_例外を投げない()
        {
            Assert.DoesNotThrow(() => SynergyApplier.ApplyAll(null, SynergyDefinitions.All));
            Assert.DoesNotThrow(() => SynergyApplier.ApplyAll(MakeContext(), null));
            Assert.DoesNotThrow(() => SynergyApplier.ApplyAll(null, null));
        }

        [Test]
        public void SourceAbilityNameに発動段階Lvが含まれる()
        {
            // 火 3 体染め → 最 ATK 1 体に OutgoingDamageUp +10%。Source は「炎の共鳴 Lv3」。
            var allies = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0, atk: 50),
                Make(Element.Fire, 1, atk: 80),
                Make(Element.Fire, 2, atk: 70),
            };
            SynergyApplier.ApplyAll(MakeContext(allies), SynergyDefinitions.All);

            string actualSource = null;
            foreach (var e in allies[1].ActiveEffects)
            {
                if (e.Kind == EffectKind.OutgoingDamageUp)
                {
                    actualSource = e.SourceAbilityName;
                    break;
                }
            }
            Assert.AreEqual($"{FireName} Lv3", actualSource);
        }

        [Test]
        public void Persistent付与_解除不能フラグとCategoryが整合()
        {
            var allies = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0, atk: 80),
                Make(Element.Fire, 1, atk: 60),
            };
            SynergyApplier.ApplyAll(MakeContext(allies), SynergyDefinitions.All);

            IEffect found = null;
            foreach (var e in allies[0].ActiveEffects)
                if (e.SourceAbilityName != null && e.SourceAbilityName.StartsWith(FireName)) { found = e; break; }
            Assert.IsNotNull(found, "火属性シナジー由来効果が付与されていること");
            Assert.AreEqual(Lifetime.Permanent, found.Lifetime);
            Assert.IsTrue(found.IsUndispellable);
            Assert.AreEqual(-1, found.RemainingTurns);
        }

        // ───── 火属性 ─────

        [Test]
        public void 火0体_バフ無し()
        {
            var allies = new List<RuntimeUnit>
            {
                Make(Element.Water, 0),
                Make(Element.Water, 1),
            };
            SynergyApplier.ApplyAll(MakeContext(allies), SynergyDefinitions.All);
            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.OutgoingDamageUp, FireName));
        }

        [Test]
        public void 火1体_バフ無し()
        {
            var allies = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0, atk: 80),
                Make(Element.Water, 1),
            };
            SynergyApplier.ApplyAll(MakeContext(allies), SynergyDefinitions.All);
            foreach (var u in allies)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.OutgoingDamageUp, FireName));
        }

        [Test]
        public void 火2体_最ATK1体に5パーセント付与()
        {
            var a1 = Make(Element.Fire, 0, atk: 50);
            var a2 = Make(Element.Fire, 1, atk: 80);
            SynergyApplier.ApplyAll(
                MakeContext(new List<RuntimeUnit> { a1, a2 }), SynergyDefinitions.All);
            Assert.AreEqual(0, SumMagnitudeBy(a1, EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(5, SumMagnitudeBy(a2, EffectKind.OutgoingDamageUp, FireName));
        }

        [Test]
        public void 火4体_ATK上位2体に20パーセント付与()
        {
            var us = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0, atk: 50),
                Make(Element.Fire, 1, atk: 60),
                Make(Element.Fire, 2, atk: 70),
                Make(Element.Fire, 3, atk: 80),
            };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            Assert.AreEqual(0, SumMagnitudeBy(us[0], EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(0, SumMagnitudeBy(us[1], EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(20, SumMagnitudeBy(us[2], EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(20, SumMagnitudeBy(us[3], EffectKind.OutgoingDamageUp, FireName));
        }

        [Test]
        public void 火6体_ATK上位2体に70パーセント付与()
        {
            var us = new List<RuntimeUnit>();
            for (int i = 0; i < 6; i++) us.Add(Make(Element.Fire, i, atk: 50 + i * 10));
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(0, SumMagnitudeBy(us[i], EffectKind.OutgoingDamageUp, FireName), $"i={i}");
            Assert.AreEqual(70, SumMagnitudeBy(us[4], EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(70, SumMagnitudeBy(us[5], EffectKind.OutgoingDamageUp, FireName));
        }

        [Test]
        public void 同ATKはSlotIndex昇順タイブレーク()
        {
            var a1 = Make(Element.Fire, 0, atk: 80);
            var a2 = Make(Element.Fire, 1, atk: 80);
            SynergyApplier.ApplyAll(
                MakeContext(new List<RuntimeUnit> { a1, a2 }), SynergyDefinitions.All);
            Assert.AreEqual(5, SumMagnitudeBy(a1, EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(0, SumMagnitudeBy(a2, EffectKind.OutgoingDamageUp, FireName));
        }

        // ───── 水属性 ─────

        [Test]
        public void 水0体_バフ無し()
        {
            var us = new List<RuntimeUnit> { Make(Element.Fire, 0), Make(Element.Fire, 1) };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
            {
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.DefenseUp, WaterName));
                Assert.AreEqual(0, SumStacksBy(u, EffectKind.Shield, WaterName));
            }
        }

        [Test]
        public void 水2体_全員にDEF10_Shieldなし()
        {
            var us = new List<RuntimeUnit> { Make(Element.Water, 0), Make(Element.Water, 1) };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
            {
                Assert.AreEqual(10, SumMagnitudeBy(u, EffectKind.DefenseUp, WaterName));
                Assert.AreEqual(0, SumStacksBy(u, EffectKind.Shield, WaterName));
            }
        }

        [Test]
        public void 水4体_全員にDEF20_Shield1()
        {
            var us = new List<RuntimeUnit>();
            for (int i = 0; i < 4; i++) us.Add(Make(Element.Water, i));
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
            {
                Assert.AreEqual(20, SumMagnitudeBy(u, EffectKind.DefenseUp, WaterName));
                Assert.AreEqual(1, SumStacksBy(u, EffectKind.Shield, WaterName));
            }
        }

        [Test]
        public void 水6体_全員にDEF30_Shield3()
        {
            var us = new List<RuntimeUnit>();
            for (int i = 0; i < 6; i++) us.Add(Make(Element.Water, i));
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
            {
                Assert.AreEqual(30, SumMagnitudeBy(u, EffectKind.DefenseUp, WaterName));
                Assert.AreEqual(3, SumStacksBy(u, EffectKind.Shield, WaterName));
            }
        }

        [Test]
        public void 水属性以外も自陣営なら恩恵を受ける()
        {
            var w1 = Make(Element.Water, 0);
            var w2 = Make(Element.Water, 1);
            var fire = Make(Element.Fire, 2);
            SynergyApplier.ApplyAll(
                MakeContext(new List<RuntimeUnit> { w1, w2, fire }), SynergyDefinitions.All);
            Assert.AreEqual(10, SumMagnitudeBy(w1, EffectKind.DefenseUp, WaterName));
            Assert.AreEqual(10, SumMagnitudeBy(w2, EffectKind.DefenseUp, WaterName));
            Assert.AreEqual(10, SumMagnitudeBy(fire, EffectKind.DefenseUp, WaterName));
        }

        // ───── 光属性 ─────

        [Test]
        public void 光0体_バフ無し()
        {
            var us = new List<RuntimeUnit> { Make(Element.Fire, 0), Make(Element.Fire, 1) };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.HealOverTime, LightName));
        }

        [Test]
        public void 光2体_全員にHealOverTime3パーセント()
        {
            var us = new List<RuntimeUnit>
            {
                Make(Element.Light, 0),
                Make(Element.Light, 1),
                Make(Element.Fire, 2),
            };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
                Assert.AreEqual(3, SumMagnitudeBy(u, EffectKind.HealOverTime, LightName));
        }

        [Test]
        public void 光4体_全員にHealOverTime8パーセント()
        {
            var us = new List<RuntimeUnit>();
            for (int i = 0; i < 4; i++) us.Add(Make(Element.Light, i));
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
                Assert.AreEqual(8, SumMagnitudeBy(u, EffectKind.HealOverTime, LightName));
        }

        // ───── 敵側不発動・死亡除外 ─────

        [Test]
        public void 敵側不発動_自陣のみシナジー適用_敵陣火4でも何も付かない()
        {
            var allies = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0, atk: 80),
                Make(Element.Fire, 1, atk: 60),
            };
            var enemies = new List<RuntimeUnit>();
            for (int i = 0; i < 4; i++) enemies.Add(Make(Element.Fire, i, atk: 50 + i * 10));

            SynergyApplier.ApplyAll(MakeContext(allies, enemies), SynergyDefinitions.All);

            // 自陣火 2 体 → 最 ATK 1 体に +5%
            Assert.AreEqual(5, SumMagnitudeBy(allies[0], EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(0, SumMagnitudeBy(allies[1], EffectKind.OutgoingDamageUp, FireName));

            // 敵陣は火 4 体でも一切付与されない
            for (int i = 0; i < enemies.Count; i++)
                Assert.AreEqual(0, SumMagnitudeBy(enemies[i], EffectKind.OutgoingDamageUp, FireName),
                    $"enemies[{i}] は不発動");
        }

        [Test]
        public void 戦闘開始時に死亡してるユニットはカウント対象外()
        {
            var dead = Make(Element.Light, 0);
            dead.BaseUnit.CurrentHP = 0;
            var us = new List<RuntimeUnit>
            {
                dead,
                Make(Element.Light, 1),
                Make(Element.Fire, 2),
            };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            foreach (var u in us)
                Assert.AreEqual(0, SumMagnitudeBy(u, EffectKind.HealOverTime, LightName));
        }

        // ───── 動的再評価しない（再呼出ししないこと前提・呼ぶと重複する仕様） ─────

        [Test]
        public void ApplyAllは戦闘中の体数変動で再評価しない_1回呼び切りが正しい運用()
        {
            var us = new List<RuntimeUnit>
            {
                Make(Element.Fire, 0, atk: 50),
                Make(Element.Fire, 1, atk: 80),
            };
            SynergyApplier.ApplyAll(MakeContext(us), SynergyDefinitions.All);
            Assert.AreEqual(5, SumMagnitudeBy(us[1], EffectKind.OutgoingDamageUp, FireName));
            Assert.AreEqual(1, CountBy(us[1], EffectKind.OutgoingDamageUp, FireName));

            // 仕様：途中で 1 体死亡しても戦闘開始時のバフは残り続ける（解除されない）。
            us[1].BaseUnit.CurrentHP = 0;
            Assert.AreEqual(5, SumMagnitudeBy(us[1], EffectKind.OutgoingDamageUp, FireName));
        }
    }
}
