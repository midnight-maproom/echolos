using System;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;

namespace Echolos.Domain.Items
{
    /// <summary>
    /// 消費アイテム。戦闘中のプレイヤー介入タイミングで使用する使い切りアイテム。
    /// 回復・バフ付与・直接ダメージなど様々な効果を持つ。
    /// CommanderDataのConsumablesスロット（デフォルト最大3）で管理される。
    /// </summary>
    public class Item
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// 使用時の効果処理。
        /// 第1引数：バトルコンテキスト、第2引数：使用対象のRuntimeUnit。
        /// </summary>
        public Action<BattleContext, RuntimeUnit> UseEffect { get; set; }

        public Item(string itemId, string name, string description = "")
        {
            ItemId = itemId;
            Name = name;
            Description = description;
        }
    }
}
