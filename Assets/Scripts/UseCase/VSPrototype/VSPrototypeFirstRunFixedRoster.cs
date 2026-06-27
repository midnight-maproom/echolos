// 初期ドラフトをスキップする際に与える固定構成（1 周目スキップ／テスト起動の両用）。
//
// 【利用箇所】
// - VSPrototypeBootstrap.StartNewRun() の 1 周目分岐
// - VSPrototypeBootstrap.StartNewRunWithDefaultRoster()（MetaHubGUI「[テスト] 固定ユニットでラン開始」ボタン）
//
// 【設計意図】
// プレイヤーの 1 周目体験は「与えられたユニットの配置を考える」ことに集中させる。
// ドラフトという編成判断ゲームは召集コマンド（行動力 1 で 3 択）でいつでも体験できるため、
// 1 周目から初期ドラフト 5 連発で詰む or 不安定なスタートを避け、配置と戦闘の手応えに
// 専念してもらう狙い。テスト起動も同じ構成を使うことで両ルートの挙動が一致し、検証コストが下がる。
//
// 2 周目以降は通常通り 3 択ドラフト 5 回（メタ強化「初期所持ユニット +1」分追加）に戻る。
//
// 【可変性】
// 構成変更時は本ファイル内の UnitIds リストだけ書き換えればよい。体数は固定 5 ではなく
// 可変（前衛 3＋後衛 3 や 4 体構成等にも対応可）。
// ただし兵種 ID は IUnitCatalog で解決可能なものに限る（誤 ID は実機起動時に NullRef）。
using System.Collections.Generic;

namespace Echolos.UseCase.VSPrototype
{
    /// <summary>1 周目限定で初期ドラフトをスキップして投入する固定構成。</summary>
    public static class VSPrototypeFirstRunFixedRoster
    {
        /// <summary>
        /// 1 周目で王女に加えて初期投入する兵種 ID リスト。
        /// 火・水・光の 3 属性 × 剣士／タンク／ヒーラーの最小バランス構成で、王女（光属性）と
        /// 合わせて火 2／水 2／光 2 の全シナジー 2 体段階を成立させる。
        /// 変更したい場合は本リストだけ書き換える。メタ強化「初期所持ユニット+1」は適用しない
        /// （固定構成の不可侵性を保つ・2 周目以降のドラフト枠で吸収される設計）。
        /// </summary>
        public static readonly IReadOnlyList<string> UnitIds = new[]
        {
            "fire_swordsman",  // 炎の双剣士：火属性 前衛アタッカー
            "fire_tank",       // 炎の大盾兵：火属性 前衛タンク
            "water_swordsman", // 水の剣士：水属性 前衛アタッカー（ATK デバフ付帯）
            "water_tank",      // 水の大盾兵：水属性 前衛専守タンク
            "light_priest",    // 光の司祭：光属性 ヒーラー
        };
    }
}
