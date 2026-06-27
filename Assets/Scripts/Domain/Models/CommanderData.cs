using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Items;

namespace Echolos.Domain.Models
{
    /// <summary>
    /// 指揮官（プレイヤー）データ。
    /// ランを通じて維持されるゲージ・アイテム・装備インベントリ等を管理する。
    /// MonoBehaviourを継承しない純粋なPOCO。
    /// </summary>
    public class CommanderData
    {
        public string CommanderId { get; set; }
        public string Name { get; set; }

        // 偵察システム

        /// <summary>
        /// 基礎偵察値。1ポイントにつき次の戦闘で敵編成が1体明らかになる（最大6体）。
        /// レリックやユニットスキルで加算される。
        /// </summary>
        public int BaseScoutingValue { get; set; }

        // 指揮官ゲージ・必殺技

        /// <summary>現在の指揮官ゲージ</summary>
        public int CurrentGauge { get; set; }

        /// <summary>指揮官ゲージの最大値</summary>
        public int MaxGauge { get; set; }

        /// <summary>
        /// 指揮官固有の必殺技。
        /// ゲージがMAXの時、戦闘中のプレイヤー介入タイミングで発動可能。
        /// 使用後はゲージがリセットされる。
        /// </summary>
        public Waza UltimateSkill { get; set; }

        // アイテム管理

        /// <summary>
        /// 消費アイテムスロット（デフォルト最大3）。
        /// 戦闘中のプレイヤー介入タイミングで使用可能な使い切りアイテム。
        /// </summary>
        public List<Item> Consumables { get; set; } = new List<Item>();

        /// <summary>消費アイテムの最大スロット数。指揮官の能力により増減する（デフォルト3）</summary>
        public int MaxConsumableSlots { get; set; } = 3;

        /// <summary>
        /// 所持している装備品のインベントリ。
        /// ユニットがロスト（完全死亡）した際の装備品返還先になる。
        /// </summary>
        public List<Equipment> EquipmentInventory { get; set; } = new List<Equipment>();

        // 経験値・ランリソース

        /// <summary>所持している経験値アイテムの個数</summary>
        public int TotalExpItems { get; set; }

        /// <summary>
        /// ランを通じた敗北・撤退の累計回数。
        /// 3回に達した時点でゲームオーバーとなる。
        /// </summary>
        public int AccumulatedFailures { get; set; }

        // 編成

        /// <summary>
        /// 現在リーダーに指定されているユニットのID。
        /// 戦闘開始時にRuntimeUnitのIsLeaderフラグ設定に使用する。
        /// </summary>
        public string SelectedLeaderUnitId { get; set; }

        public CommanderData(string commanderId, string name)
        {
            CommanderId = commanderId;
            Name = name;
            MaxGauge = 100;
        }
    }
}
