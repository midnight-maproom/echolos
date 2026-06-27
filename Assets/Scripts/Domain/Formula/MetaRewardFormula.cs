// メタ通貨「王国の記憶」獲得式の Domain 完成品クラス。
// Catalog（Data 層）が SO/POCO から組み立てて UseCase 層に渡す Domain 型。
// UnitCatalog → Unit と同パターン（Data の Definition を Domain の完成品に変換）。
using System;
using System.Collections.Generic;

namespace Echolos.Domain.Formula
{
    /// <summary>メタ通貨獲得式の Domain 完成品（Catalog 経由で組み立てられる不変オブジェクト）。</summary>
    public sealed class MetaRewardFormula
    {
        /// <summary>SO アセットの一意 ID（カタログ主キー）。</summary>
        public string Id { get; }

        /// <summary>計算式の ID（MetaRewardFormulaRegistry で実装解決）。</summary>
        public string FormulaId { get; }

        /// <summary>式パラメタ（perRound=10 等）。</summary>
        public IReadOnlyDictionary<string, float> Params { get; }

        public MetaRewardFormula(string id, string formulaId, IReadOnlyDictionary<string, float> parameters)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id は必須", nameof(id));
            if (string.IsNullOrEmpty(formulaId))
                throw new ArgumentException("formulaId は必須", nameof(formulaId));
            Id = id;
            FormulaId = formulaId;
            Params = parameters ?? new Dictionary<string, float>();
        }

        /// <summary>このコンテキストでの獲得量を計算する。</summary>
        public int Calculate(MetaRewardContext ctx)
        {
            var formula = MetaRewardFormulaRegistry.Get(FormulaId);
            return formula(ctx, Params);
        }
    }
}
