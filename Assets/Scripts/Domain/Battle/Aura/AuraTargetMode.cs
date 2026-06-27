namespace Echolos.Domain.Battle.Aura
{
    // オーラ効果を配布する範囲。
    // AllAllies      ：陣営の生存全員（既存・王家の加護）
    // SelfAndPartners：SourceUnit と RequiredPartnerUnitIds に列挙されたパートナーのみ
    //                  （連携系・2 体が同時に出撃しているときだけバフが乗る）
    public enum AuraTargetMode
    {
        AllAllies,
        SelfAndPartners,
    }
}
