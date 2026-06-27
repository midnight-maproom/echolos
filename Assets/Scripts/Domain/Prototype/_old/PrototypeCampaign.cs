// Assets/Scripts/Core/Prototype/PrototypeCampaign.cs
// プロト 段階2-Inc3: 多戦線ミニループ（H2）のプリセット編成ファクトリ（純C#・MonoBehaviour非依存）
//
// H2の非対称性（仕様 210_prototype_spec.md §5.2）を最小構成で成立させる:
//   - 手駒6 < 「全戦線を強く守る」のに必要な数 → 集中と取捨が要る
//   - 戦線ごとに敵強度が違う（弱/中/強）→ 偵察しないと分からない
//   - 捨てた戦線は突破ダメージで本拠地HPを削る（強い戦線ほど痛い）
using System.Collections.Generic;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
namespace Echolos.Domain.Prototype
{
    /// <summary>多戦線ミニループの初期状態（手駒6＋強度差のある3戦線）を構築するファクトリ。</summary>
    public static class PrototypeCampaign
    {
        /// <summary>本拠地耐久。3ターン制で、賢く配分しないと守り切れない暫定値。</summary>
        public const int HomeBaseHp = 60;

        /// <summary>規定ターン数（これを守り切ればクリア）。</summary>
        public const int MaxTurns = 3;

        /// <summary>キャンペーン初期状態を生成する。手駒は戦闘スライスの6役を流用。</summary>
        public static CampaignState Build()
        {
            var roster = PrototypeRoster.BuildAllyParty(); // 前衛=重装/機動/物理、後衛=魔法/回復/軍師

            var fronts = new List<FrontState>
            {
                // 弱：捨ててもダメージは小さい
                new FrontState("北の関門", BuildWeakEnemies(), baseBreakthroughDamage: 8),
                // 中：そこそこ痛い
                new FrontState("東の街道", BuildMediumEnemies(), baseBreakthroughDamage: 14),
                // 強：捨て続けると本拠地が保たない（3回突破で66 > 60）
                new FrontState("南の砦", BuildStrongEnemies(), baseBreakthroughDamage: 22),
            };

            return new CampaignState(HomeBaseHp, MaxTurns, roster, fronts);
        }

        // ── 戦線ごとの敵編成（強度差をつける）──

        private static List<RuntimeUnit> BuildWeakEnemies()
        {
            return new List<RuntimeUnit>
            {
                Enemy("w_grunt1", "野盗",   hp: 60, atk: 18, pdef: 4, mdef: 2, spd: 7, slot: 0),
                Enemy("w_grunt2", "野盗",   hp: 55, atk: 16, pdef: 3, mdef: 2, spd: 6, slot: 1),
            };
        }

        private static List<RuntimeUnit> BuildMediumEnemies()
        {
            return new List<RuntimeUnit>
            {
                Enemy("m_sword", "敵剣兵", hp: 95, atk: 24, pdef: 8, mdef: 4, spd: 7, slot: 0),
                Enemy("m_lance", "敵槍兵", hp: 85, atk: 22, pdef: 7, mdef: 4, spd: 6, slot: 1),
                Enemy("m_arch",  "敵弓兵", hp: 70, atk: 26, pdef: 3, mdef: 4, spd: 9, slot: 2),
            };
        }

        private static List<RuntimeUnit> BuildStrongEnemies()
        {
            return new List<RuntimeUnit>
            {
                Enemy("s_knight", "敵重騎士", hp: 130, atk: 28, pdef: 12, mdef: 6, spd: 5, slot: 0),
                Enemy("s_vet1",   "敵精兵",   hp: 110, atk: 30, pdef: 9,  mdef: 5, spd: 7, slot: 1),
                Enemy("s_vet2",   "敵精兵",   hp: 100, atk: 28, pdef: 8,  mdef: 5, spd: 7, slot: 2),
                Enemy("s_mage",   "敵魔導",   hp: 80,  atk: 32, pdef: 3,  mdef: 8, spd: 8, slot: 3),
            };
        }

        private static RuntimeUnit Enemy(string id, string name,
            int hp, int atk, int pdef, int mdef, int spd, int slot)
        {
            var unit = new Unit(id, name, Element.None)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                PDEF = pdef,
                MDEF = mdef,
                BaseSPD = spd,
                // 前列(0-2)は近接、後列(3-5)は中射程（後列からでも敵前列を攻撃でき、置物化しない）
                Range = slot >= 3 ? AttackRange.Mid : AttackRange.Melee,
                State = UnitState.Active
            };
            // 技は付けず通常攻撃フォールバック（基礎ATK）で戦わせる。戦線の強度差は素のステータスで表現。
            return new RuntimeUnit(unit, slot);
        }
    }
}
