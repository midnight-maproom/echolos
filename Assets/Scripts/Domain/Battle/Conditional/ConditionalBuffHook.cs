namespace Echolos.Domain.Battle.Conditional
{
    /// <summary>
    /// Conditional バフの再評価フック種別。
    /// 各 ConditionalBuffProcessor が購読フックを宣言し、BattleManager が
    /// 該当する Processor のみフック発火時に Refresh する。
    /// 新しいフックが必要になったら enum 追加 + 発火位置を追加する。
    /// </summary>
    public enum ConditionalBuffHook
    {
        BattleStart, // 戦闘開始時（InitializeBattle 末尾）
        TurnStart,   // 各ターン開始時（StartPhase 移行時）
        UnitDied,    // ユニット死亡時（陣営構成変化）
        BuffApplied, // 任意の RuntimeUnit に状態効果が付与されたとき（OnEffectAdded 契機）
        BuffRemoved, // 任意の RuntimeUnit から状態効果が剥奪されたとき（OnEffectRemoved 契機）
    }
}
