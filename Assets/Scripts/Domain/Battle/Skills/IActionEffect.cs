namespace Echolos.Domain.Battle.Skills
{
    // Waza の効果を Strategy パターンで表現するインタフェース。
    // 1 つの Waza は IActionEffect を順序付きで複数持ち、ActionExecutor が順次 Apply する。
    // 各 Effect は context.Targets リストに対して 1 段の処理を実行し、
    // 結果を context.Outcomes に追加する。多段ループ・命中/回避判定・反撃結線は
    // 呼び出し元（ActionExecutor）の責務であり、本インタフェースの範囲外。
    public interface IActionEffect
    {
        void Apply(IActionContext context);
    }
}
