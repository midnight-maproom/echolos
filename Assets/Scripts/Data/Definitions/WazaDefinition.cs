// 技の定義データ（純 POCO・SO シリアライズ可能）。
//
// 【データ駆動化の核心】
// 新戦闘の Waza は Effects: IList<IActionEffect> を持つが、interface 型は
// Unity SO シリアライズ不可（[SerializeReference] は Domain 層の noEngineReferences=true
// 制約で使えない）。よって POCO 側では「Pattern enum + 各種パラメタ」で表現し、
// WazaCatalog.Get が Pattern に応じて Effects を構築する 2 段設計とする。
//
// 新しい Pattern が必要になったら WazaPattern enum に列挙＋ WazaCatalog 側 switch に
// case を追加する形（拡張時のコストは最小化）。
using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Data.Definitions
{
    /// <summary>
    /// 技の効果パターン。Waza.Effects への変換ルールを Catalog 側で持つ。
    /// </summary>
    public enum WazaPattern
    {
        /// <summary>単純攻撃（AttackEffect 1 つ）。</summary>
        Attack,

        /// <summary>攻撃＋ヒット時に StatusEffect 付与（AttackEffect onHitRiders=ApplyStatusEffectEffect）。</summary>
        AttackWithStatusRider,

        /// <summary>
        /// 攻撃＋ caster 自身に StatusEffect 付与（AttackEffect ＋ ApplyStatusEffectToActorEffect）。
        /// 攻撃対象とは別経路で actor 自身にバフ／デバフを積む用途。
        /// 用例：闇皇太子「闇槍の薙ぎ」（全体物理＋自己 AttackUp 永続スタックで必敗化）。
        /// </summary>
        AttackWithSelfStatusRider,

        /// <summary>回復（HealEffect 1 つ）。</summary>
        Heal,

        /// <summary>全体回復＋味方デバフ解除（HealEffect + DispelDebuffsEffect）。</summary>
        HealAndDispelDebuffs,

        /// <summary>味方に StatusEffect 付与（ApplyStatusEffectEffect 1 つ）。</summary>
        ApplyStatusEffect,

        /// <summary>敵バフ全体解除（DispelBuffsEffect 1 つ）。</summary>
        DispelEnemyBuffs,

        /// <summary>味方デバフ全体解除（DispelDebuffsEffect 1 つ）。</summary>
        DispelAllyDebuffs,

        /// <summary>味方状態異常解除（CleanseStatusAilmentsEffect 1 つ）。</summary>
        CleanseAllyStatusAilments,

        /// <summary>チャージ（Effects 空・IsForcedWhenReady=true でターン待機）。</summary>
        Charge,
    }

    /// <summary>技定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class WazaDefinition
    {
        // 識別
        public string Id;
        public string Name;

        // 行動パラメタ（Waza に直接転送）
        public int SPD;
        public int Cooldown;
        public int InitialCooldown;
        public int HitCount = 1;
        public int MaxUsesPerBattle = -1;
        public bool IsForcedWhenReady = false;
        public TargetingType TargetingType = TargetingType.SingleEnemy;
        public int TargetCount = 1;
        public TargetSelection TargetSelection = TargetSelection.Default;

        // 効果パターン（WazaCatalog が Pattern に応じて Effects を組み立てる）
        public WazaPattern Pattern = WazaPattern.Attack;

        // AttackEffect / AttackWithStatusRider 用
        public double WazaMultiplier = 1.0;
        public bool IsSureHit = false;

        // HealEffect 用（mult / 暫定）。HealEffect の実装側で √HP × wazaPower を計算。
        // double 化済（バランス調整で 1.5 等の小数値を持たせるため）。
        public double WazaPower = 0;

        // AttackWithStatusRider / ApplyStatusEffect 用付帯効果テンプレ。
        // 単発を想定（複数同時付与が必要な Waza が出てきたら List 化）。
        public EffectDefinition RiderEffect;
    }
}
