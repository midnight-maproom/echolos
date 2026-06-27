// メタ拠点強化 1 項目の定義データ（純 POCO）。
//
// 【役割】
// - ScriptableObject（MetaUpgradeDefinitionSO）と Domain クラス（MetaUpgrade）の中間 POCO。
// - SO からロードされ、IMetaUpgradeCatalog 経由で Domain 型に変換される。
//
// 【設計方針】
// - フィールドは public + [System.Serializable] で SO シリアライズ可能化（auto-property 禁止）。
using System.Collections.Generic;
using UnityEngine;

namespace Echolos.Data.Definitions
{
    /// <summary>メタ拠点強化の定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class MetaUpgradeDefinition
    {
        /// <summary>SO 主キー（MetaUpgradeIds の const string と一致させる）。</summary>
        public string Id;

        /// <summary>UI 表示名。</summary>
        public string DisplayName;

        /// <summary>UI 効果説明文（1〜2 行）。</summary>
        [TextArea(1, 3)]
        public string EffectText;

        /// <summary>段階別購入コスト（王国の記憶）。長さは Cap と一致させる。Costs[N]=N 回目購入のコスト。</summary>
        public List<int> Costs = new List<int>();

        /// <summary>強化上限 Lv。</summary>
        public int Cap;
    }
}
