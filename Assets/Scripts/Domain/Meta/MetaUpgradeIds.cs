namespace Echolos.Domain.Meta
{
    /// <summary>
    /// メタ強化項目のID 定数集。Data 層 Roster や Presentation 層 GUI から typo なく参照するために Domain.Meta に置く。
    /// </summary>
    public static class MetaUpgradeIds
    {
        /// <summary>王女 Lv 強化（ラン開始時の王女初期 Lv +1・購入時 3 択選択）。</summary>
        public const string PrincessLevel = "princess_level";
        /// <summary>ブリジット Lv 強化（ラン開始時のブリジット初期 Lv +1・購入時 3 択選択）。</summary>
        public const string BridgetLevel = "bridget_level";
        /// <summary>毎ラウンドの内政行動力 +1（2→3）。</summary>
        public const string ActionPoints = "action_points";
        /// <summary>初期所持ユニット +1（ランダム抽選）。</summary>
        public const string InitialUnit = "initial_unit";
    }
}
