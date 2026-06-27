using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Effects;

namespace Echolos.Domain.Models
{
    /// <summary>
    /// バトル用のランタイムユニット。
    /// 永続データ（Unit）への参照を保持しつつ、戦闘中のみ有効な状態を管理する。
    ///
    /// 【設計方針：状態の独立性】
    /// - 永続データ（MaxHP・DEF 等）はBaseUnit経由で参照する。
    /// - 戦闘中のみ有効な状態（シールド・状態異常・行動フラグ）はこのクラスで管理する。
    /// - 戦闘終了時にRuntimeUnitは破棄され、次の戦闘で再生成される。
    /// </summary>
    public class RuntimeUnit
    {
        // 永続データへの参照

        /// <summary>
        /// 永続ユニットデータへの参照（コピーではなく同一オブジェクト）。
        /// 戦闘中のHP変化はBaseUnit.CurrentHPを直接変更することで、
        /// 戦闘結果（生存HPの維持）が永続データに反映される。
        /// </summary>
        public Unit BaseUnit { get; }

        // 配置情報

        /// <summary>
        /// スロットインデックス（0〜5）。
        /// 0 が最前、5 が最後尾の内部スロット番号。配置数が 6 未満でも詰めて連番。
        /// </summary>
        public int SlotIndex { get; set; }

        /// <summary>
        /// このユニットがリーダーかどうか。
        /// 戦闘開始時にCommanderData.SelectedLeaderUnitIdと照合して決定する。
        /// 戦闘中にリーダーが死亡しても他ユニットへの移譲は行わない（リーダー不在扱い）。
        /// </summary>
        public bool IsLeader { get; set; }

        // バトル用の技リスト（CDがこのバトル用に管理される）

        /// <summary>
        /// バトル用の技リスト（戦闘中状態を保持する RuntimeWaza のリスト）。
        /// 戦闘開始時に BaseUnit.BaseWazas から生成される。
        /// このリストへの変更（CD 更新等）は BaseUnit の永続データに影響しない。
        /// </summary>
        public List<RuntimeWaza> BattleWazas { get; set; } = new List<RuntimeWaza>();

        // 戦闘中のみ有効な状態（戦闘終了時にリセット）

        /// <summary>このターンのメインフェーズで行動済みかどうかのフラグ</summary>
        public bool HasActedThisTurn { get; set; }

        /// <summary>
        /// 麻痺スタック許容量。
        /// 自分の行動順に麻痺スタックがこの値以上なら行動不能になり、解除と同時にこの値が倍化する（1→2→4→8…）。
        /// 戦闘開始時の初期値は <see cref="Unit.BaseParalysisTolerance"/>（既定 1・耐麻痺ユニットは 2 以上）。
        /// </summary>
        public int ParalysisTolerance { get; set; }

        /// <summary>
        /// 残りの復活可能回数。
        /// アンデッドスキル等で初期値を設定し、HP0到達時に消費してHP回復する。
        /// </summary>
        public int CurrentReviveCount { get; set; }

        // 状態異常・バフ・デバフ（API 経由でのみ変更可能）
        //
        // public List 直公開を避け、IReadOnlyList の読み取り専用ビュー + API メソッド経由に統一する。
        // AddEffect / RemoveEffect / RemoveEffectsWhere / ClearAllEffects 内で OnEffectAdded /
        // OnEffectRemoved を発火することで、UI 層への通知漏れを構造的に防ぐ。
        private readonly List<IEffect> _activeEffects = new List<IEffect>();

        /// <summary>現在付与されている状態異常・バフ・デバフ(読み取り専用ビュー)。</summary>
        public IReadOnlyList<IEffect> ActiveEffects => _activeEffects;

        /// <summary>
        /// 状態効果が追加された時に発火する（AddEffect 呼び出しごと・1効果につき1回）。
        /// </summary>
        public event Action<IEffect> OnEffectAdded;

        /// <summary>
        /// 状態効果が削除された時に発火する（RemoveEffect / RemoveEffectsWhere / ClearAllEffects 経由・
        /// 削除1件につき1回）。
        /// </summary>
        public event Action<IEffect> OnEffectRemoved;

        /// <summary>状態効果を1つ追加し、OnEffectAdded を発火する。null は無視。</summary>
        public void AddEffect(IEffect effect)
        {
            if (effect == null) return;
            _activeEffects.Add(effect);
            OnEffectAdded?.Invoke(effect);
        }

        /// <summary>
        /// 状態効果を1つ削除し、OnEffectRemoved を発火する。
        /// 削除に成功したら true、リストに存在しなかったら false。
        /// </summary>
        public bool RemoveEffect(IEffect effect)
        {
            if (effect == null) return false;
            bool removed = _activeEffects.Remove(effect);
            if (removed) OnEffectRemoved?.Invoke(effect);
            return removed;
        }

        /// <summary>
        /// 述語にマッチする全ての状態効果を削除し、削除した各効果について OnEffectRemoved を発火する。
        /// 削除した件数を返す。
        /// </summary>
        public int RemoveEffectsWhere(Predicate<IEffect> match)
        {
            if (match == null) return 0;
            int count = 0;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                if (!match(effect)) continue;
                _activeEffects.RemoveAt(i);
                OnEffectRemoved?.Invoke(effect);
                count++;
            }
            return count;
        }

        /// <summary>全状態効果を削除し、削除した各効果について OnEffectRemoved を発火する。</summary>
        public int ClearAllEffects() => RemoveEffectsWhere(_ => true);

        /// <summary>
        /// 述語にマッチする最初の状態効果を返す。なければ null。
        /// </summary>
        public IEffect FindEffect(Predicate<IEffect> match)
        {
            if (match == null) return null;
            foreach (var e in _activeEffects)
                if (match(e)) return e;
            return null;
        }

        /// <summary>特定 Kind の状態効果を返す(最初の1件・なければ null)。</summary>
        public IEffect FindEffect(EffectKind kind) =>
            FindEffect(e => e.Kind == kind);

        /// <summary>
        /// 述語にマッチする全ての状態効果を新しいリストで返す。
        /// </summary>
        public List<IEffect> FindEffects(Predicate<IEffect> match)
        {
            var result = new List<IEffect>();
            if (match == null) return result;
            foreach (var e in _activeEffects)
                if (match(e)) result.Add(e);
            return result;
        }

        // 便利プロパティ（BaseUnitへのプロキシ・計算値）

        /// <summary>現在HP（BaseUnit.CurrentHPへのプロキシ）</summary>
        public int CurrentHP => BaseUnit.CurrentHP;

        /// <summary>最大HP（強化反映済の Unit.EffectiveMaxHP を委譲）。</summary>
        public int MaxHP => BaseUnit.EffectiveMaxHP;

        /// <summary>ユニットが生存しているか（HPが1以上かつDead状態でない）</summary>
        public bool IsAlive => BaseUnit.CurrentHP > 0 && BaseUnit.State != UnitState.Dead;

        /// <summary>
        /// このユニットが何らかの攻撃手段を持つか（永続データのみで判定・全体バフの影響を受けない）。
        /// </summary>
        public bool HasAttackingMeans
        {
            get
            {
                if (BaseUnit.BaseWazas == null) return false;
                foreach (var waza in BaseUnit.BaseWazas)
                {
                    if (waza?.Effects == null) continue;
                    foreach (var eff in waza.Effects)
                        if (eff is AttackEffect) return true;
                }
                return false;
            }
        }

        /// <summary>実効攻撃力（Unit.EffectiveATK + AttackUp - AttackDown。最低0）。</summary>
        public int EffectiveATK => ApplyAbilityModifiers(BaseUnit.EffectiveATK, AbilityStat.Attack);

        /// <summary>実効防御力（Unit.EffectiveDEF + DefenseUp - DefenseDown。最低0）。</summary>
        public int EffectiveDEF => ApplyAbilityModifiers(BaseUnit.EffectiveDEF, AbilityStat.Defense);

        private int ApplyAbilityModifiers(int baseValue, AbilityStat stat)
        {
            int value = baseValue;
            foreach (var effect in _activeEffects)
            {
                if (effect is AbilityModifier mod && mod.Stat == stat)
                {
                    if (mod.IsBuff) value += (int)mod.Magnitude * mod.Stacks;
                    else             value -= (int)mod.Magnitude * mod.Stacks;
                }
            }
            return Math.Max(0, value);
        }

        /// <summary>
        /// 麻痺状態かどうか（麻痺スタック合計が <see cref="ParalysisTolerance"/> 以上で行動不能）。
        /// </summary>
        public bool IsParalyzed
        {
            get
            {
                int totalStacks = 0;
                foreach (var e in _activeEffects)
                    if (e is ParalysisEffect) totalStacks += e.Stacks;
                return totalStacks > 0 && totalStacks >= ParalysisTolerance;
            }
        }

        /// <summary>完全凍結状態かどうか（凍結スタック合計が10以上で行動不能）</summary>
        public bool IsFullyFrozen =>
            _activeEffects.Where(e => e is FreezeEffect).Sum(e => e.Stacks) >= 10;

        /// <summary>復活無効化デバフが付与されているかどうか</summary>
        public bool HasReviveInvalid =>
            _activeEffects.Any(e => e is ReviveInvalidFlag);

        /// <summary>
        /// 現在の Shield 残数（合計スタック）。被弾時に 1 ヒット単位で消費される。0 以下なら Shield なし。
        /// </summary>
        public int ShieldStacks =>
            _activeEffects.Where(e => e is ShieldEffect).Sum(e => e.Stacks);

        /// <summary>
        /// 永続ユニットデータからバトル用インスタンスを生成する。
        /// </summary>
        public RuntimeUnit(Unit baseUnit, int slotIndex, bool isLeader = false)
        {
            BaseUnit = baseUnit;
            SlotIndex = slotIndex;
            IsLeader = isLeader;

            HasActedThisTurn = false;
            ParalysisTolerance = baseUnit.BaseParalysisTolerance;
            CurrentReviveCount = 0;
            BattleWazas = new List<RuntimeWaza>();
        }
    }
}
