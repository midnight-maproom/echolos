using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Skills
{
    /// <summary>
    /// 技（アクティブスキル）の不変テンプレート定義。
    /// 戦闘中の状態（CD・使用回数）は RuntimeWaza に分離する。
    /// </summary>
    public class Waza
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>行動速度。値が大きいほどメインフェーズで先に行動する</summary>
        public int SPD { get; set; }

        /// <summary>クールダウンの最大値（0 = 毎ターン使用可）</summary>
        public int Cooldown { get; set; }

        /// <summary>
        /// バトル開始時の初期クールダウン（チャージ用）。
        /// RuntimeWaza のコンストラクタで CurrentCooldown の初期値に使う。初撃の発動ターンを揃える
        /// ために使用する（例：3 ターン目に初撃 → InitialCooldown=2）。既定 0。
        /// </summary>
        public int InitialCooldown { get; set; }

        /// <summary>
        /// 優先発動フラグ（チャージ用）。
        /// trueの場合、この技がCD・使用回数・有効ターゲットの点で使用可能になったターンに、
        /// AIのスコア評価を介さず確定的に発動する（台本的な行動切替）。
        /// </summary>
        public bool IsForcedWhenReady { get; set; }

        /// <summary>多段攻撃のヒット数（1 = 単発攻撃）</summary>
        public int HitCount { get; set; } = 1;

        /// <summary>1バトル中の最大使用回数。-1 = 無制限</summary>
        public int MaxUsesPerBattle { get; set; } = -1;

        /// <summary>ターゲット指定タイプ（単体・全体・自身）</summary>
        public TargetingType TargetingType { get; set; }

        /// <summary>技の行動カテゴリ（攻撃・回復・強化・弱体・反撃）。既定は攻撃。</summary>
        public WazaCategory Category { get; set; } = WazaCategory.Attack;

        /// <summary>
        /// Heal時の回復量計算式（Category=Healのとき使用）。
        /// 第1引数：使用者、第2引数：回復対象。返り値：回復HP量。
        /// </summary>
        public Func<RuntimeUnit, RuntimeUnit, int> CalculateHealAmount { get; set; }

        /// <summary>
        /// Buff/Debuff時に対象へ付与する状態異常テンプレートのリスト。
        /// 適用時に複製され、同種効果が既にあれば強度・残ターンを更新する（多重スタック防止）。
        /// </summary>
        public List<StatusEffect> AppliedEffects { get; set; } = new List<StatusEffect>();

        /// <summary>必中フラグ。trueの場合、回避判定を完全にスキップする</summary>
        public bool IsSureHit { get; set; }

        /// <summary>
        /// AI用ターゲット選択条件。
        /// nullなら常に有効。対象のRuntimeUnitを受け取りboolを返す。
        /// 例：(target) =&gt; target.CurrentHP / (float)target.MaxHP &lt;= 0.5f
        /// </summary>
        public Func<RuntimeUnit, bool> TargetingCondition { get; set; }

        /// <summary>
        /// 基礎ダメージ計算式。
        /// 第1引数：使用者のRuntimeUnit、第2引数：対象のRuntimeUnit。
        /// 返り値：ダメージ式に投入する基礎値（防御適用・配置補正・環境項は BattleManager 側で処理）。
        /// </summary>
        public Func<RuntimeUnit, RuntimeUnit, int> CalculateBaseDamage { get; set; }

        /// <summary>
        /// 状態異常治癒（cleanse）。trueの場合、対象（味方）の状態異常
        /// （燃焼/毒・凍結・麻痺・呪い）を解除する。回復役 2 に使用。
        /// </summary>
        public bool CleansesStatusAilments { get; set; }

        public Waza(string id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// 共通フォールバック反撃 Waza（倍率 1.0・属性なし・付帯効果なし）。
        /// <see cref="Unit.CounterWaza"/> が null のとき反撃発動時に使われる。
        /// 配置 ATK 補正・属性バフ・環境項は通常攻撃と同じく BattleManager 側で適用される。
        /// </summary>
        public static readonly Waza DefaultCounter = new Waza("counter:default", "反撃")
        {
            Category = WazaCategory.Counter,
            SPD = 0,
            HitCount = 1,
            TargetingType = TargetingType.SingleEnemy,
            CalculateBaseDamage = (a, t) => a.EffectiveATK,
        };
    }
}
