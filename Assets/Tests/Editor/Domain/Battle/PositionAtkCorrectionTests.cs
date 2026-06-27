// 配置 ATK 補正カーブの単体テスト。
//
// 距離 0..5 → 100/100/95/85/75/65%
// 近接：内部スロット 0 からの距離
// 遠隔：最後尾の内部スロットからの距離
// 少人数編成では遠隔の最後尾基準がシフトして補正がほぼ消える（3 体編成で最大 -5%）
using NUnit.Framework;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Tests.Domain
{
    [TestFixture]
    public class PositionAtkCorrectionTests
    {
        // ── 近接 6 体編成（slot 0..5 → 1.0/1.0/0.95/0.85/0.75/0.65） ──

        [TestCase(0, 1.0)]
        [TestCase(1, 1.0)]
        [TestCase(2, 0.95)]
        [TestCase(3, 0.85)]
        [TestCase(4, 0.75)]
        [TestCase(5, 0.65)]
        public void 近接6体_slot別補正(int internalSlot, double expected)
        {
            double c = PositionAtkCorrection.GetCorrection(internalSlot, 6, AttackKind.Melee);
            Assert.AreEqual(expected, c, 1e-9);
        }

        // ── 遠隔 6 体編成（最後尾 slot 5 から距離計算・対称） ──

        [Test]
        public void 遠隔6体_最後尾は100パーセント()
        {
            double c = PositionAtkCorrection.GetCorrection(5, 6, AttackKind.Ranged);
            Assert.AreEqual(1.0, c, 1e-9);
        }

        [Test]
        public void 遠隔6体_最前は65パーセント()
        {
            double c = PositionAtkCorrection.GetCorrection(0, 6, AttackKind.Ranged);
            Assert.AreEqual(0.65, c, 1e-9);
        }

        // ── 少人数編成（仕様 320 §2.4 の核） ──

        [Test]
        public void 近接3体_slot0は100パーセント()
        {
            double c = PositionAtkCorrection.GetCorrection(0, 3, AttackKind.Melee);
            Assert.AreEqual(1.0, c, 1e-9);
        }

        [Test]
        public void 近接3体_slot2は95パーセント_補正がほぼ消える()
        {
            double c = PositionAtkCorrection.GetCorrection(2, 3, AttackKind.Melee);
            Assert.AreEqual(0.95, c, 1e-9);
        }

        [Test]
        public void 遠隔3体_最後尾slot2は100パーセント()
        {
            double c = PositionAtkCorrection.GetCorrection(2, 3, AttackKind.Ranged);
            Assert.AreEqual(1.0, c, 1e-9);
        }

        [Test]
        public void 遠隔3体_最前slot0は95パーセント_補正がほぼ消える()
        {
            double c = PositionAtkCorrection.GetCorrection(0, 3, AttackKind.Ranged);
            Assert.AreEqual(0.95, c, 1e-9);
        }

        // ── 異常入力のクランプ（カーブ末尾値で続行・例外なし） ──

        [Test]
        public void 距離6以上は末尾値65パーセントにクランプ()
        {
            double c = PositionAtkCorrection.GetCorrectionByDistance(99);
            Assert.AreEqual(0.65, c, 1e-9);
        }

        [Test]
        public void 距離マイナスは先頭値100パーセントにクランプ()
        {
            double c = PositionAtkCorrection.GetCorrectionByDistance(-1);
            Assert.AreEqual(1.0, c, 1e-9);
        }
    }
}
