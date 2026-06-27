// 戦闘可視化のためにバトル進行を構造化イベント列として記録する。
//
// 【設計】
// - 既存の BattleRunner はテキストログのみを残しており、UI で再生するには情報が足りない。
//   このイベント列を別経路で記録しておけば、戦闘ロジックを変更せず可視化レイヤーが再生できる。
// - 1イベントは「画面で何が起きたか」を最小限に伝えるために必要なフィールドだけを持つ。
//   差分（誰のHPがいくつになった等）のみを記録し、初期スナップショットからの再構築を観戦ビュー側で行う。
// - 録画→再生方式にすることで、倍速・スキップ・自動進行が再生制御だけで実現できる。
using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;
using Echolos.Domain.Battle;

namespace Echolos.Domain.Battle.Replay
{
    /// <summary>戦闘イベントの種類。観戦ビューの状態遷移を駆動する。</summary>
    public enum BattleEventKind
    {
        /// <summary>新しいターンが始まった。</summary>
        TurnStart,
        /// <summary>ユニットが行動を宣言した（技名・対象つき）。攻撃線・行動者ハイライトに使う。</summary>
        ActionDeclared,
        /// <summary>命中（ダメージ）。対象のHP表示更新・ヒットエフェクト発火に使う。</summary>
        HitLanded,
        /// <summary>回復。対象のHP表示更新・回復ポップアップ発火に使う。</summary>
        Healed,
        /// <summary>回避。対象上に「回避」テキスト等を出す。</summary>
        Evaded,
        /// <summary>戦闘不能。対象スロットをグレーアウトに切り替える。</summary>
        Died,
        /// <summary>毒/燃焼の継続ダメージ。HP更新と継続ダメージのバッジ表示に使う。</summary>
        BurnTick,
        /// <summary>行動スキップ（待機/麻痺/凍結）。</summary>
        ActionSkipped,
        /// <summary>
        /// 状態効果（バフ/デバフ/状態異常）が対象ユニットに付与された。
        /// Target にユニット、EffectType に効果種別を入れる。観戦ビュー側の HashSet に追加して
        /// バフデバフバッジを表示するために使う。
        /// </summary>
        StatusEffectApplied,
        /// <summary>
        /// 状態効果が対象ユニットから剥がれた。
        /// Target にユニット、EffectType に効果種別を入れる。観戦ビュー側の HashSet から
        /// 取り除いてバフデバフバッジを消すために使う。
        /// </summary>
        StatusEffectExpired,
        /// <summary>戦闘終了。Result を確定表示に使う。</summary>
        BattleEnd,
        /// <summary>
        /// 1 アクションが完結した。Actor / WazaName / Outcomes に集約結果が乗る。
        /// 観戦ビューはこれ 1 件でアクション 1 回分の HP 更新・死亡反映・付帯効果バッジを
        /// まとめて適用する。アクション内の HitLanded / Healed / Evaded / Died は
        /// このイベントに集約されるため、Events リストには個別追加されない。
        /// </summary>
        ActionResolved,
        /// <summary>
        /// ターン終了時 HealOverTime の陣営単位集約。HealTicks に全対象分の回復結果が乗る。
        /// 観戦ビューはこれ 1 件で全 unit の HP を一括反映し、LogLine 1 行を表示する
        /// （ActionResolved と同じく 1 Event = 1 Tick = 1 ログ）。
        /// </summary>
        HealOverTimePhase,
    }

    /// <summary>
    /// 戦闘進行中の1イベント。BattleRunner が記録し、観戦ビューが時間軸で再生する。
    /// すべてのフィールドはイベント種別ごとに必要なものだけ使い、不要なフィールドは null/0 のままで良い。
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; set; }

        /// <summary>イベント発生時のターン番号（1始まり）。</summary>
        public int Turn { get; set; }

        /// <summary>行動者・継続ダメージ等の主体ユニット（ActionDeclared/ActionSkipped/BurnTick）。</summary>
        public RuntimeUnit Actor { get; set; }

        /// <summary>命中・回避・死亡など単一対象の対象ユニット（HitLanded/Evaded/Died）。</summary>
        public RuntimeUnit Target { get; set; }

        /// <summary>行動宣言時の対象リスト（複数対象技で使う）。</summary>
        public List<RuntimeUnit> Targets { get; set; }

        /// <summary>技名（ActionDeclared）。通常攻撃フォールバックなら "通常攻撃"。</summary>
        public string WazaName { get; set; }

        /// <summary>与えたダメージ量（HitLanded/BurnTick）。</summary>
        public int Damage { get; set; }

        /// <summary>回復した量（Healed）。観戦ビューは緑色 "+X" として表示する。</summary>
        public int HealAmount { get; set; }

        /// <summary>対象の被弾後/被回復後HP（HitLanded/BurnTick/Healed）。観戦ビューはこの値でHPバーを更新する。</summary>
        public int TargetHPAfter { get; set; }

        /// <summary>戦闘結果（BattleEnd）。</summary>
        public BattleResult Result { get; set; }

        /// <summary>スキップ理由表示用（ActionSkipped）。"待機"/"麻痺"/"凍結"など。</summary>
        public string SkipReason { get; set; }

        /// <summary>
        /// 付与・剥がれの対象となった状態効果の値スナップショット
        /// （StatusEffectApplied / StatusEffectExpired）。
        /// 観戦ビューは Target ユニットのバッジ集合を更新し、ホバー時のスタック数／残ターン数を
        /// この値から復元する（戦闘終了後の IEffect ライブ状態に依存しない）。
        /// </summary>
        public EffectChange EffectChange { get; set; }

        /// <summary>
        /// 戦闘開始時のシナジー/オーラ/ユニット固有 PersistentEffects のように、複数 unit × 複数 effect が
        /// 同時に付与される一括スナップショット（StatusEffectApplied 集約 Event 用）。
        /// 観戦ビューは BulkEffectChanges を順に snapshot に反映し、ログは LogLine 1 行に集約する。
        /// </summary>
        public IReadOnlyList<EffectApplication> BulkEffectChanges { get; set; }

        /// <summary>
        /// アクション完結時の集約結果（ActionResolved）。
        /// 各 HitOutcome が「1 ターゲットへの 1 ヒット結果」を表し、観戦ビューはこれを順次適用するか
        /// 全件まとめて演出するかを選べる。
        /// </summary>
        public IReadOnlyList<HitOutcome> Outcomes { get; set; }

        /// <summary>
        /// ターン終了 HealOverTime の集約結果（HealOverTimePhase）。
        /// 観戦ビューはこの 1 件で全 unit の HP を一括反映する。
        /// </summary>
        public IReadOnlyList<StatusEffectProcessor.HealOverTimeTick> HealTicks { get; set; }

        /// <summary>
        /// このイベントに対応する 1 行ログ。null/空ならログ表示はスキップ（HP 更新等の Apply は通常通り
        /// 走る）。Recorder の各 Add 箇所で初期化し、観戦ビューの cursor 進行と必ず 1:1 対応する。
        /// BattleReport.Log の重複保持は段階移行で残存（最終的には本フィールドに一本化予定）。
        /// </summary>
        public string LogLine { get; set; }
    }
}
