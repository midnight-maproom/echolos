using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // Waza の不変テンプレート。
    // 戦闘中の状態（CD・使用回数）は RuntimeWaza に分離する。
    //
    // 
    // - WazaCategory enum 廃止（Strategy パターン化により WazaCategory による分岐が不要）
    // - CalculateBaseDamage / CalculateHealAmount Func 廃止（DamageEffect / HealEffect の
    //   コンストラクタ引数で表現）
    // - AppliedEffects 廃止（ApplyStatusEffectEffect のコンストラクタ引数で表現）
    // - DispelsBuffs / DispelsDebuffs / CleansesStatusAilments フラグ廃止
    //   （DispelBuffsEffect / DispelDebuffsEffect / CleanseStatusAilmentsEffect を Effects に追加）
    // - 全効果は Effects: IList<IActionEffect> に一本化
    // - TargetCount プロパティ追加（DirectionalEnemies 用）
    public sealed class Waza
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>行動速度。値が大きいほどメインフェーズで先に行動する</summary>
        public int SPD { get; set; }

        /// <summary>クールダウンの最大値（0 = 毎ターン使用可）</summary>
        public int Cooldown { get; set; }

        /// <summary>
        /// バトル開始時の初期クールダウン（チャージ用）。
        /// RuntimeWaza のコンストラクタで CurrentCooldown の初期値に使う。
        /// </summary>
        public int InitialCooldown { get; set; }

        /// <summary>
        /// 優先発動フラグ。trueの場合、この技がCD・使用回数・有効ターゲットの点で使用可能に
        /// なったターンに、AIのスコア評価を介さず確定的に発動する。
        /// </summary>
        public bool IsForcedWhenReady { get; set; }

        /// <summary>多段攻撃のヒット数（1 = 単発攻撃）。各ヒットで独立した命中/反撃判定。</summary>
        public int HitCount { get; set; } = 1;

        /// <summary>1バトル中の最大使用回数。-1 = 無制限</summary>
        public int MaxUsesPerBattle { get; set; } = -1;

        /// <summary>ターゲット指定タイプ</summary>
        public TargetingType TargetingType { get; set; }

        /// <summary>
        /// 範囲攻撃時の対象数（TargetingType=DirectionalEnemies で参照）。
        /// SingleEnemy / SingleAlly では常に 1。AllEnemies / AllAllies では無関係。
        /// </summary>
        public int TargetCount { get; set; } = 1;

        /// <summary>
        /// 単体対象の選定戦略（SingleEnemy / SingleAlly で参照）。
        /// Default は TargetingType ごとの既定挙動。それ以外は全生存対象から戦略に従って 1 体選ぶ。
        /// </summary>
        public TargetSelection TargetSelection { get; set; } = TargetSelection.Default;

        /// <summary>
        /// AI用ターゲット選択条件。nullなら常に有効。対象のRuntimeUnitを受け取りboolを返す。
        /// 例：(target) => target.CurrentHP / (float)target.MaxHP <= 0.5f
        /// </summary>
        public Func<RuntimeUnit, bool> TargetingCondition { get; set; }

        /// <summary>
        /// Waza の効果リスト（Strategy パターン）。ActionExecutor が順次 Apply する。
        /// 例：
        /// - 通常攻撃：[DamageEffect]
        /// - 攻撃＋付帯バフ：[DamageEffect, ApplyStatusEffectEffect]
        /// - バフ＋デバフ解除：[ApplyStatusEffectEffect, DispelDebuffsEffect]
        /// - 回復＋デバフ解除：[HealEffect, DispelDebuffsEffect]
        /// </summary>
        public IList<IActionEffect> Effects { get; set; } = new List<IActionEffect>();

        public Waza(string id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// 共通フォールバック反撃 Waza（倍率 0.5・属性なし・付帯効果なし）。
        /// Unit.CounterWaza が null のとき反撃発動時に使われる。
        /// 配置 ATK 補正・属性バフ・環境項は通常攻撃と同じく ActionExecutor で適用される。
        /// 倍率は前列同士の殴り合いが反撃で過剰に削れすぎないよう半減で運用する。
        /// </summary>
        public static readonly Waza DefaultCounter = new Waza("counter:default", "反撃")
        {
            SPD = 0,
            HitCount = 1,
            TargetingType = TargetingType.SingleEnemy,
            Effects = new List<IActionEffect> { new AttackEffect(wazaMultiplier: 0.5) },
        };
    }
}
