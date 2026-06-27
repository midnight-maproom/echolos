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

        /// <summary>能力ラベル（UI 表示用・「専守」「暗殺」「多段攻撃」等）。</summary>
        public List<string> AbilityLabels = new List<string>();

        /// <summary>内部処理タグ（UI 非公開のフラグ群）。</summary>
        public List<string> Tags = new List<string>();

        // 基礎ステータス
        public int MaxHP;
        public int BaseATK;
        public int DEF;
        public int BaseSPD;
        public int BaseEvasion;

        /// <summary>状態異常無効（燃焼/毒・凍結・麻痺・呪いを弾く）。</summary>
        public bool ImmuneToStatusAilments = false;

        // 兵種強化
        public int EnhancementHPPerLevel = 0;
        public int EnhancementATKPerLevel = 0;
        public int EnhancementDEFPerLevel = 0;
        public int EnhancementEvasionPerLevel = 0;
        public int EnhancementHealPerLevel = 0;
        public int EnhancementMagnitudePerLevel = 0;

        // 関連

        /// <summary>所有 Waza の ID リスト（WazaCatalog から引く）。</summary>
        public List<string> WazaIds = new List<string>();

        /// <summary>反撃 Waza の ID（空文字なら共通フォールバック反撃を使用）。</summary>
        public string CounterWazaId;

        /// <summary>
        /// 置物オーラ（戦闘開始時に同陣営全員へ付与・本体死亡で剥奪）。
        /// null = オーラなし。
        /// </summary>
        public StatusEffect AuraEffect;
    }
}
