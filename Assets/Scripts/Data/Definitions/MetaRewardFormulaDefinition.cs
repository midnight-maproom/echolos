// メタ通貨「王国の記憶」獲得式の定義データ（純 POCO）。
//
// 【役割】
// - ScriptableObject（MetaRewardFormulaSO）と Domain ロジック（MetaRewardFormulaRegistry）の中間 POCO。
// - SO からロードされ、IMetaRewardFormulaCatalog 経由で UseCase 層に渡される。
//
// 【設計方針】
// - フィールドは public + [System.Serializable] で SO シリアライズ可能化（auto-property 禁止）。
// - Dictionary 型は使わず List<FormulaParam>（WazaDefinition と同パターン）。
using System.Collections.Generic;

namespace Echolos.Data.Definitions
{
    /// <summary>メタ通貨獲得式の定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class MetaRewardFormulaDefinition
    {
        /// <summary>SO アセットの一意ID（カタログ主キー）。</summary>
        public string Id;

        /// <summary>表示名（Inspector 識別用・実行時参照なし）。</summary>
        public string Name;

        /// <summary>
        /// 計算式の ID（MetaRewardFormulaRegistry で実装解決）。
        /// 例：「vsproto_standard_v1」。
        /// </summary>
        public string FormulaId;

        /// <summary>式パラメタ（例：perRound=10, reachedBoss=50）。</summary>
        public List<FormulaParam> Params = new List<FormulaParam>();
    }
}
