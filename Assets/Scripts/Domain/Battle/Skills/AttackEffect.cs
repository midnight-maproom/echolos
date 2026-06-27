using System;
using System.Collections.Generic;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Skills
{
    // 攻撃チェーンを 1 つの Effect に凝集したクラス。
    // 命中判定 → ダメ計算（配置 ATK 補正・地形補正を BattleContext から動的算出）
    // → 与/被ダメ% → クリ判定 → Shield 消費 → ダメ適用 → 付帯（Rider）→ 反撃発動
    // の依存連鎖を内部にカプセル化し、Effect 間の暗黙結合を排除する。
    //
    // 付帯効果は onHitRiders として渡される IActionEffect リスト。命中時のみ、同 target に
    // 対して順次 Apply される。回避ヒットでは Rider は発動しない。
    //
    // 反撃発動条件（§3.2）：攻撃側＆被弾側ともに AttackKind=Melee で、両者生存。
    // 反撃は本クラスを isCounterAttack=true で内部呼び出しすることで実現し、
    // 反撃の反撃は発動しない（無限ループ回避）。
    // 反撃時のみ CounterDamageUp（炎の大盾兵パッシブ等）が乗算される。
    //
    // バフ/回復のみの Waza は AttackEffect を使わず、ApplyStatusEffectEffect / HealEffect 等を
    // Effects に直接持つ。
    public sealed class AttackEffect : IActionEffect
    {
        // EvasionUp の合算キャップ（仕様 320 §1.4.2）。どんなに積んでも 50% が上限。
        public const int EvasionCapPercent = 50;

        private readonly double _wazaMultiplier;
        private readonly bool _isSureHit;
        private readonly bool _isCounterAttack;
        private readonly IReadOnlyList<IActionEffect> _onHitRiders;

        public AttackEffect(
            double wazaMultiplier = 1.0,
            bool isSureHit = false,
            IReadOnlyList<IActionEffect> onHitRiders = null)
            : this(wazaMultiplier, isSureHit, isCounterAttack: false, onHitRiders) { }

        private AttackEffect(
            double wazaMultiplier,
            bool isSureHit,
            bool isCounterAttack,
            IReadOnlyList<IActionEffect> onHitRiders)
        {
            _wazaMultiplier = wazaMultiplier;
            _isSureHit = isSureHit;
            _isCounterAttack = isCounterAttack;
            _onHitRiders = onHitRiders;
        }

        public void Apply(IActionContext context)
        {
            if (context == null) return;
            var actor = context.Actor;
            var targets = context.Targets;
            if (actor == null || targets == null) return;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;
                if (!actor.IsAlive) break;

                bool isHit = _isSureHit || !IsEvaded(actor, target, context.Random0To99);

                if (!isHit)
                {
                    context.Outcomes.Add(new HitOutcome(
                        target: target,
                        damage: 0,
                        wasEvaded: true,
                        targetHPAfter: target.BaseUnit.CurrentHP,
                        isCounterAttack: _isCounterAttack));
                    continue;
                }

                double posAtk = ComputePositionAtkCorrection(context, actor);
                int terrainBonus = ComputeTerrainBonus(context, target);

                int raw = DamageFormula.ComputeBaseDamage(
                    attackerAtk: actor.EffectiveATK,
                    wazaMultiplier: _wazaMultiplier,
                    defenderCurrentHp: target.CurrentHP,
                    defenderDef: target.EffectiveDEF,
                    positionAtkCorrection: posAtk,
                    terrainBonus: terrainBonus);

                int withOutgoing = DamageModifier.ApplyOutgoingMultiplier(raw, actor);
                int withCounter = _isCounterAttack
                    ? DamageModifier.ApplyCounterMultiplier(withOutgoing, actor)
                    : withOutgoing;
                int withIncoming = DamageModifier.ApplyIncomingMultiplier(withCounter, target);

                var (afterCrit, isCritical) = DamageModifier.ApplyCritical(withIncoming, actor, context.Random0To99);

                var shieldResult = ShieldConsumer.Consume(afterCrit, target);
                int finalDmg = shieldResult.FinalDamage;
                bool wasShielded = shieldResult.ShieldConsumed;

                if (finalDmg > 0)
                {
                    int after = target.BaseUnit.CurrentHP - finalDmg;
                    if (after < 0) after = 0;
                    target.BaseUnit.CurrentHP = after;
                }

                context.Outcomes.Add(new HitOutcome(
                    target: target,
                    damage: finalDmg,
                    wasEvaded: false,
                    isCritical: isCritical,
                    resultedInDeath: !target.IsAlive,
                    targetHPAfter: target.BaseUnit.CurrentHP,
                    isCounterAttack: _isCounterAttack,
                    wasShielded: wasShielded));

                // 命中時のみ Rider 発動（反撃には Rider なし）。Shield 吸収時は付帯付与も無効化。
                if (!_isCounterAttack && !wasShielded && _onHitRiders != null && _onHitRiders.Count > 0)
                {
                    var subCtx = new SubContext
                    {
                        Battle = context.Battle,
                        Actor = actor,
                        Targets = new List<RuntimeUnit> { target },
                        Random0To99 = context.Random0To99,
                        Outcomes = context.Outcomes,
                        CurrentWazaId = context.CurrentWazaId,
                    };
                    foreach (var rider in _onHitRiders)
                        rider?.Apply(subCtx);
                }

                // 反撃発動（通常攻撃のみ・反撃の反撃なし・両者 Melee・両者生存時）
                if (!_isCounterAttack && CounterAttackResolver.CanCounterAttack(actor, target))
                {
                    ExecuteCounterAttack(context, counterAttacker: target, counterTarget: actor);
                }
            }
        }

        // 反撃判定：プロト範囲では「攻撃側 AttackKind=Ranged」のときのみ発火（仕様 320 §1.4.2）。
        // target の EvasionUp 合計（Magnitude × Stacks）を % として扱い、キャップ 50% で頭打ち。
        // EvasionDown は減算で打ち消し可能（負値は 0 でクランプ）。
        private static bool IsEvaded(RuntimeUnit actor, RuntimeUnit target, Func<int> random0To99)
        {
            if (actor?.BaseUnit == null || target == null) return false;
            if (actor.BaseUnit.AttackKind != AttackKind.Ranged) return false;

            int evasion = ComputeEvasionPercent(target);
            if (evasion <= 0) return false;
            if (evasion >= 100) return true;
            if (random0To99 == null) return false;
            return random0To99() < evasion;
        }

        private static int ComputeEvasionPercent(RuntimeUnit target)
        {
            int total = 0;
            foreach (var e in target.ActiveEffects)
            {
                if (e is EvasionModifier mod)
                {
                    if (mod.IsBuff) total += (int)mod.Magnitude * mod.Stacks;
                    else            total -= (int)mod.Magnitude * mod.Stacks;
                }
            }
            if (total < 0) total = 0;
            if (total > EvasionCapPercent) total = EvasionCapPercent;
            return total;
        }

        // 配置 ATK 補正を BattleContext から動的算出。actor の所属陣営を判定し、その陣営の
        // 生存ユニット数と actor の内部スロット番号から PositionAtkCorrection を適用。
        // BattleContext が無い／actor が両陣営に居ない場合は補正なし（1.0）。
        private static double ComputePositionAtkCorrection(IActionContext context, RuntimeUnit actor)
        {
            if (context?.Battle == null || actor?.BaseUnit == null) return 1.0;
            var side = ResolveSideOf(context.Battle, actor);
            if (side == null) return 1.0;

            int internalSlot = InternalSlotResolver.GetInternalSlotIndex(side, actor);
            if (internalSlot < 0) return 1.0;
            int aliveCount = InternalSlotResolver.GetAliveCount(side);
            return PositionAtkCorrection.GetCorrection(internalSlot, aliveCount, actor.BaseUnit.AttackKind);
        }

        // 地形補正は target.Element と BattleContext.Terrain / TerrainStrength から動的算出。
        // 環境項はダメージ式の分母（被弾側）に作用するため target を参照する（仕様 §5.3）。
        private static int ComputeTerrainBonus(IActionContext context, RuntimeUnit target)
        {
            if (context?.Battle == null || target?.BaseUnit == null) return 0;
            return TerrainBonusCalculator.GetTerrainBonus(
                target.BaseUnit.UnitElement,
                context.Battle.Terrain,
                context.Battle.TerrainStrength);
        }

        // actor が所属する陣営リストを返す（味方 or 敵）。
        // 参照同値比較で判定（List.Contains の既定動作）。
        private static List<RuntimeUnit> ResolveSideOf(BattleContext battle, RuntimeUnit actor)
        {
            if (battle.AllyUnits != null && battle.AllyUnits.Contains(actor)) return battle.AllyUnits;
            if (battle.EnemyUnits != null && battle.EnemyUnits.Contains(actor)) return battle.EnemyUnits;
            return null;
        }

        // 反撃発動。反撃の反撃なし（isCounterAttack=true）。Rider なし。
        // 反撃 Waza として Waza.DefaultCounter を使う（個別の反撃 Waza は Phase E で対応）。
        private static void ExecuteCounterAttack(
            IActionContext context, RuntimeUnit counterAttacker, RuntimeUnit counterTarget)
        {
            var counterEffect = new AttackEffect(
                wazaMultiplier: 1.0,
                isSureHit: false,
                isCounterAttack: true,
                onHitRiders: null);
            var subCtx = new SubContext
            {
                Battle = context.Battle,
                Actor = counterAttacker,
                Targets = new List<RuntimeUnit> { counterTarget },
                Random0To99 = context.Random0To99,
                Outcomes = context.Outcomes,
                CurrentWazaId = Waza.DefaultCounter.Id,
            };
            counterEffect.Apply(subCtx);
        }

        private sealed class SubContext : IActionContext
        {
            public BattleContext Battle { get; set; }
            public RuntimeUnit Actor { get; set; }
            public IList<RuntimeUnit> Targets { get; set; }
            public Func<int> Random0To99 { get; set; }
            public IList<HitOutcome> Outcomes { get; set; }
            public string CurrentWazaId { get; set; }
        }
    }
}
