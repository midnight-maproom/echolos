// ラスボス（R7 本拠地戦）の UnitDefinition ファクトリ集。
//
// 役割：
// - 帝国軍 15 体（味方コピー）とは別軸の専用 Unit を定義する。
// - 現状は皇太子 1 体のみ。プロト範囲の R7 本拠地戦の固定編成 Slot 1 として使用。
//
// 設計方針：
// - 属性：None（闇属性は既存軸にないため）。シナジー対象外。
// - Lv 強化：持たない（AvailableUpgradeIds 空・固定強度）。
// - ATK は編成内最高に設定（味方 ATK バフ（炎の鼓舞師の鼓舞・HighestAtk 対象）が
//   皇太子に確実に乗るようにする）。
// - 通常攻撃は持たず、CD=2 ＋ InitialCD ずらしで闇剣・闇波を交互発動する 2 Waza 構成。
using System.Collections.Generic;
using Echolos.Data.Definitions;
using Echolos.Domain.Models;

namespace Echolos.Data.Roster
{
    /// <summary>ラスボスの UnitDefinition ファクトリ集（現状：皇太子 1 体）。</summary>
    public static class BossRoster
    {
        public static UnitDefinition Prince() => new UnitDefinition
        {
            Id = UniqueUnitIds.Prince, Name = "皇太子",
            UnitElement = Element.None,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            // 編成内最高 ATK（敵 焦熱の大魔導士 Lv3 EffectiveATK=45、敵 光の騎士 Lv3=30+α より高い 55）。
            MaxHP = 250, BaseATK = 55, DEF = 5, BaseSPD = 12,
            WazaIds = { "waza_dark_blade", "waza_dark_wave" },
            // Lv 強化は持たない（固定強度・ラスボスとして Lv1 固定運用）。
        };

        /// <summary>皇太子（闇）：A-c1 必敗形態。HasNotedPendantPower=false で R7 に出現する単騎ボス。
        /// HP/DEF=9999/999 で実質無敵＋「闇槍の薙ぎ」（全体物理＋自己 AttackUp 永続スタック）で
        /// T3 までに全滅させる必敗演出専用ユニット。</summary>
        public static UnitDefinition PrinceDark() => new UnitDefinition
        {
            Id = UniqueUnitIds.PrinceDark, Name = "皇太子",
            UnitElement = Element.Dark,
            AttackKind = AttackKind.Melee, TargetingDirection = TargetingDirection.FromFront,
            PlacementHint = PlacementHint.Front,
            CombatRoles = { UnitRole.Attacker },
            SortOrder = 3,
            // HP/DEF 9999/999 で実質無敵。ATK は通常皇太子準拠（55）＋自己 AttackUp +20 毎ターン累積。
            MaxHP = 9999, BaseATK = 55, DEF = 999, BaseSPD = 12,
            WazaIds = { "waza_dark_sweep" },
            // Lv 強化なし／状態異常無効化は ImmunityKinds 未設定でも HP/DEF 9999/999 と
            // 闇のオーラ IsUndispellable で詰みは保証される（最小設定優先）。
        };

        /// <summary>全ラスボス Unit の UnitDefinition を列挙（SoAssetGenerator 用）。</summary>
        public static IEnumerable<UnitDefinition> AllUnits()
        {
            yield return Prince();
            yield return PrinceDark();
        }
    }
}
