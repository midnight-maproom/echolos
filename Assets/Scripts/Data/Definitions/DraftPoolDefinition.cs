// ドラフト候補プールの定義データ（純 POCO）。
//
// 【役割】
// - ScriptableObject（DraftPoolDefinitionSO）と Domain クラス（DraftPool）の中間 POCO。
// - SO からロードされ、IDraftPoolCatalog 経由で Domain 型に変換される。
//
// 【設計方針】
// - フィールドは public + [System.Serializable] で SO シリアライズ可能化（auto-property 禁止）。
// - List<string> で兵種 ID リストを保持（IUnitCatalog で Unit に変換）。
using System.Collections.Generic;
using UnityEngine;

namespace Echolos.Data.Definitions
{
    /// <summary>ドラフト候補プールの定義データ（純 POCO・SO シリアライズ可能）。</summary>
    [System.Serializable]
    public class DraftPoolDefinition
    {
        /// <summary>SO 主キー（VSプロト範囲では "vsproto_standard_pool"）。</summary>
        public string Id;

        /// <summary>表示名（Inspector 識別用・実行時参照なし）。</summary>
        public string Name;

        /// <summary>通常プールの兵種 ID リスト（初期ドラフト＋召集の非レア枠）。</summary>
        public List<string> NormalUnitIds = new List<string>();

        /// <summary>レアプールの兵種 ID リスト（召集ドラフトの Rare 枠／全 Rare スペシャル）。</summary>
        public List<string> RareUnitIds = new List<string>();

        /// <summary>
        /// 召集ドラフトの「★全 Rare スペシャル」確率（0.0〜1.0）。
        /// 当選時は 3 枠全てが Rare プールから抽出される。
        /// </summary>
        [Range(0f, 1f)]
        public float AllRareSpecialProbability = 0.03f;

        /// <summary>
        /// 通常モード時の枠別 Rare 抽選確率（要素数は CandidatesPerOffer と一致させる）。
        /// 各枠ごとに独立判定。中央枠だけ高めにする等の微調整はここで行う。
        /// </summary>
        public List<float> RarePerSlotProbabilities = new List<float> { 0.15f, 0.15f, 0.15f };

        /// <summary>1 ドラフトの提示数（VSプロトは 3 択固定）。</summary>
        public int CandidatesPerOffer = 3;
    }
}
