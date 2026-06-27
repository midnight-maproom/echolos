// ユニットの永続定義データ（純 POCO）。
//
// 【役割】
// - ScriptableObject（UnitDefinitionSO）と Unit クラスの中間 POCO。
// - SO からロードされ、UnitCatalog 経由で Unit インスタンスに変換される。
// - Editor テストでは POCO を直接構築可能（SO 介さず・実行速度／依存性最小化）。
//
// 【設計方針】
// - フィールドは public + [System.Serializable] で SO シリアライズ可能化。
// - Dictionary 型は使わない（Unity SO Serialize 不可）。
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Data.Definitions
{
    /// <summary>ユニット定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class UnitDefinition
    {
        // 識別
        public string Id;
        public string Name;

        /// <summary>UI 表示用の説明文（ドラフトカード等で表示・複数行可・自動改行）。</summary>
        [TextArea(2, 6)]
        public string Description = "";

        // 分類軸

        /// <summary>属性（シナジー軸と地形補正のみに作用）。</summary>
        public Element UnitElement = Element.None;

        /// <summary>推奨配置（UI ヒント・システム制約なし）。</summary>
        public PlacementHint PlacementHint = PlacementHint.Any;

        /// <summary>攻撃種別（近接 Melee / 遠隔 Ranged）。反撃可否と配置 ATK 補正カーブを決める。</summary>
        public AttackKind AttackKind = AttackKind.Melee;

        /// <summary>通常攻撃のターゲット選定方向（前から / 後ろから）。</summary>
        public TargetingDirection TargetingDirection = TargetingDirection.FromFront;

        /// <summary>戦術的役割（Tank/Attacker/Support/Healer・複数可）。</summary>
        public List<UnitRole> CombatRoles = new List<UnitRole>();

        /// <summary>
        /// 敵編成抽選後の並び順優先度（小さいほど前列・敵編成生成時のみ使用）。
        /// 1=メインタンク／2=サブタンク／3=近接アタッカー／4=後衛支援／5=遠隔。
        /// </summary>
        public int SortOrder = 99;

        /// <summary>能力ラベル（UI 表示用・「専守」「暗殺」「多段攻撃」等）。</summary>
        public List<string> AbilityLabels = new List<string>();

        /// <summary>内部処理タグ（UI 非公開のフラグ群）。</summary>
        public List<string> Tags = new List<string>();

        // 基礎ステータス
        public int MaxHP;
        public int BaseATK;
        public int DEF;
        public int BaseSPD;

        /// <summary>付与時に弾く EffectKind 集合（個別 Kind 指定）。</summary>
        public List<EffectKind> ImmunityKinds = new List<EffectKind>();

        /// <summary>麻痺スタック許容量の初期値（既定 1・耐麻痺ユニットは 2 以上）。</summary>
        public int BaseParalysisTolerance = 1;

        // 関連

        /// <summary>所有 Waza の ID リスト（WazaCatalog から引く）。</summary>
        public List<string> WazaIds = new List<string>();

        /// <summary>戦闘開始時に自身に付与する永続効果テンプレ（パッシブ）。</summary>
        public List<EffectDefinition> PersistentEffects = new List<EffectDefinition>();

        /// <summary>
        /// このユニットが Lv アップ時に提示する強化選択肢の ID リスト（UnitUpgradeCatalog から引く）。
        /// 通常 3 件。Lv2/Lv3 でいずれか 1 件ずつ選択して AppliedUpgrades に追加される。
        /// </summary>
        public List<string> AvailableUpgradeIds = new List<string>();
    }
}
