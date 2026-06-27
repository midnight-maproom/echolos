using System;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;

namespace Echolos.Domain.Skills
{
    /// <summary>
    /// パッシブスキル。特定のトリガー条件で自動発動する効果の定義。
    /// スキル発動のフックはイベント駆動（Action/Func）で実装し、クラス間を疎結合に保つ。
    /// </summary>
    public class Skill
    {
        public string SkillId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>このスキルが発動するタイミング</summary>
        public TriggerType TriggerType { get; set; }

        /// <summary>
        /// 発動の追加条件。nullなら常にトリガー時に発動。
        /// 例：「自身がリーダーの時のみ」「自身のHPが50%以下の時のみ」
        /// </summary>
        public Func<BattleContext, RuntimeUnit, bool> Condition { get; set; }

        /// <summary>
        /// スキルの効果処理。
        /// 第1引数：バトルコンテキスト、第2引数：スキル保持者のRuntimeUnit。
        /// 状態異常の付与・ReactionStackへの追加・ステータス変更などを行う。
        /// </summary>
        public Action<BattleContext, RuntimeUnit> Effect { get; set; }

        public Skill(string skillId, string name, TriggerType triggerType)
        {
            SkillId = skillId;
            Name = name;
            TriggerType = triggerType;
        }
    }
}
