using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;
using Echolos.Domain.Items;

namespace Echolos.Domain.Models
{
    /// <summary>
    /// ユニットの永続データ（非戦闘時の実体）。
    /// ランを通じて維持されるHP・ステータス・装備等を保持する。
    /// 戦闘中の一時的な状態（シールド・バフ・デバフ）はRuntimeUnitで管理する。
    /// MonoBehaviourを継承しない純粋なPOCO。
    /// </summary>
    public class Unit
    {
        // 基本識別情報

        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>UI 表示用の説明文（ドラフトカード等で表示）。</summary>
        public string Description { get; set; } = "";

        /// <summary>ユニットの属性（火・水・補助 None 等）</summary>
        public Element UnitElement { get; set; }

        /// <summary>
        /// 攻撃種別（近接 Melee / 遠隔 Ranged）。
        /// 反撃可否（Melee のみ反撃を受ける／発動できる）と配置 ATK 補正カーブを決める。
        /// </summary>
        public AttackKind AttackKind { get; set; } = AttackKind.Melee;

        /// <summary>
        /// 通常攻撃のターゲット選定方向（前から狙う FromFront / 後ろから狙う FromBack）。
        /// AttackKind とは独立した軸。
        /// </summary>
        public TargetingDirection TargetingDirection { get; set; } = TargetingDirection.FromFront;

        // データ駆動リアーキで追加

        /// <summary>推奨配置（プレイヤー向け UI ヒント・システム制約ではない）。</summary>
        public PlacementHint PlacementHint { get; set; } = PlacementHint.Any;

        /// <summary>戦術的役割（Tank/Attacker/Support/Healer・複数指定可）。</summary>
        public List<UnitRole> CombatRoles { get; set; } = new List<UnitRole>();

        /// <summary>
        /// 敵編成抽選後の並び順優先度（小さいほど前列）。
        /// 1=メインタンク／2=サブタンク／3=近接アタッカー／4=後衛支援／5=遠隔。
        /// 味方陣営では手動配置なので参照されない（敵編成生成時のみ使用）。
        /// </summary>
        public int SortOrder { get; set; } = 99;

        /// <summary>能力ラベル（UI 表示用・「専守」「暗殺」「多段攻撃」等）。</summary>
        public List<string> AbilityLabels { get; set; } = new List<string>();

        /// <summary>ユニットの現在の状態（出撃中・控え・死亡）</summary>
        public UnitState State { get; set; }

        // HP（ランを通じて維持される）

        /// <summary>最大HP。戦闘中に変化しない永続値</summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// 現在HP。ランを通じて維持され、戦闘中も直接変更される。
        /// 0になるとロスト（完全死亡）扱い。
        /// </summary>
        public int CurrentHP { get; set; }

        // 攻撃・防御・速度・回避（永続値）

        /// <summary>基礎攻撃力。Waza の AttackEffect が ATK × 倍率で参照する。</summary>
        public int BaseATK { get; set; }

        /// <summary>基礎速度。Waza.SPD が未設定時のフォールバック・行動順計算に使用する。</summary>
        public int BaseSPD { get; set; }

        /// <summary>防御力（物理・魔法統合）。ダメージ式の分母として作用する。</summary>
        public int DEF { get; set; }

        /// <summary>
        /// 付与時に弾く EffectKind の集合。
        /// 「燃焼無効」「凍結無効」を個別 Kind の集合として表現する。
        /// </summary>
        public HashSet<EffectKind> ImmunityKinds { get; set; } = new HashSet<EffectKind>();

        // 装備・技

        /// <summary>装備スロット（最大1つ。未装備の場合はnull）</summary>
        public Equipment EquippedGear { get; set; }

        /// <summary>
        /// 内部処理用のタグリスト。戦闘ロジックの起点フラグ（カスタム属性等）を保持する。
        /// UIへの公開は想定しない。戦術的役割（UI 公開）は CombatRoles で管理する。
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 麻痺スタック許容量の初期値。戦闘開始時にこの値が RuntimeUnit.ParalysisTolerance
        /// に複製され、麻痺で行動不能になるたびに倍々で増加する（1→2→4→8…）。
        /// 既定 1。耐麻痺ユニットはこの値を 2 以上に設定する。
        /// </summary>
        public int BaseParalysisTolerance { get; set; } = 1;

        /// <summary>ベースの技リスト（装備によるオーバーライド前）。Waza ベース。</summary>
        public List<Waza> BaseWazas { get; set; } = new List<Waza>();

        /// <summary>
        /// 戦闘開始時に自身へ付与する永続効果テンプレ（パッシブ）。
        /// 戦闘開始時に ToEffect() で派生クラスに変換され、RuntimeUnit に付与される。
        /// </summary>
        public List<EffectDefinition> PersistentEffects { get; set; } = new List<EffectDefinition>();

        // 個別 Lv 強化システム（敵味方共通の Unit.Level 軸）

        /// <summary>
        /// 現在のレベル（1〜3）。Lv1 が初期値、Lv2/Lv3 で AvailableUpgrades から 1 つずつ選択して
        /// AppliedUpgrades に移し、EffectiveXxx に Kind 別 Magnitude を集計反映する。
        /// </summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// このユニット固有の強化選択肢（Roster で 3 件を埋め、Lv 上昇のたびに 1 件を AppliedUpgrades へ）。
        /// </summary>
        public List<UnitUpgrade> AvailableUpgrades { get; set; } = new List<UnitUpgrade>();

        /// <summary>適用済みの強化選択肢。Kind 別に Magnitude を集計して EffectiveXxx に反映する。</summary>
        public List<UnitUpgrade> AppliedUpgrades { get; set; } = new List<UnitUpgrade>();

        // 実効値（AppliedUpgrades の Kind 別 Magnitude を基礎値に加算した値）。
        // 戦闘中効果（バフ・デバフ・Shield 等）は RuntimeUnit 側で別途加味する。

        /// <summary>強化反映後の最大 HP（MaxHP + HpBoost 合計）。</summary>
        public int EffectiveMaxHP => MaxHP + SumUpgrade(UpgradeKind.HpBoost);

        /// <summary>強化反映後の基礎 ATK（BaseATK + AtkBoost 合計）。</summary>
        public int EffectiveATK => BaseATK + SumUpgrade(UpgradeKind.AtkBoost);

        /// <summary>強化反映後の DEF（DEF + DefBoost 合計）。</summary>
        public int EffectiveDEF => DEF + SumUpgrade(UpgradeKind.DefBoost);

        private int SumUpgrade(UpgradeKind kind)
        {
            if (AppliedUpgrades == null) return 0;
            int sum = 0;
            foreach (var up in AppliedUpgrades)
                if (up != null && up.Kind == kind) sum += up.Magnitude;
            return sum;
        }

        public Unit(string id, string name, Element element = Element.None)
        {
            Id = id;
            Name = name;
            UnitElement = element;
            State = UnitState.Reserve; // 新規作成時はデフォルトで控え
        }
    }
}
