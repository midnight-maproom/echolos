namespace Echolos.Domain.Models
{
    /// <summary>
    /// 固有ユニットの ID 定数（兵種強化対象外・救出システム対象等の特別扱いに使う）。
    /// SSoT は本クラス。Roster / UseCase / Presentation 各層はここを参照する。
    /// </summary>
    public static class UniqueUnitIds
    {
        public const string Princess = "princess";
        public const string Bridget  = "bridget";

        // ラスボス（敵専用・R7 本拠地戦の固定編成 Slot 1）
        public const string Prince     = "imperial_prince";
        // ラスボス（A-c1 必敗形態・HasNotedPendantPower=false 時に R7 で出現する闇皇太子）
        public const string PrinceDark = "imperial_prince_dark";
    }
}
