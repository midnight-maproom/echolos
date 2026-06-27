using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    // ターゲット評価ロジック。
    //
    // 責務：
    // - DeclareAction：行動不能判定 → 優先発動 → 通常評価 → def_guard フォールバック
    // - GetValidTargets：Waza の TargetingType に従って有効ターゲット群を返す
    // - TargetingDirection の解決（actor の BaseUnit.TargetingDirection から取得）
    // - 生存ユニットのみ対象（死亡ユニットは自動除外）
    // - TargetingCondition / TargetSelection で対象を絞り込み
    //
    // 設計方針（[project_combat_randomness_policy]）：
    // - AI スコア評価・支援優先順位判断は実装しない（推測容易性の原則）
    // - 複数 Waza の使い分けは BattleWazas のリスト順 + IsForcedWhenReady + CD で表現
    public static class TargetEvaluator
    {
        // 防御フォールバック用 Waza のテンプレート。
        // ApplyStatusEffectEffect が template を Clone するため、複数ユニットで
        // 共有しても各ターン独立に効く（残ターン共有リスクなし）。
        // SelfGuard 派生クラスで「次の自分の行動順」に StatusEffectProcessor が削除する。
        private const int SelfGuardDefenseMagnitude = 3;

        private static readonly Waza _selfGuardTemplate = CreateSelfGuardTemplate();

        private static Waza CreateSelfGuardTemplate()
        {
            return new Waza("def_guard", "防御")
            {
                SPD = 5,
                TargetingType = TargetingType.Self,
                Effects = new List<IActionEffect>
                {
                    new ApplyStatusEffectEffect(new List<Echolos.Domain.Effects.IEffect>
                    {
                        new Echolos.Domain.Effects.SelfGuard(SelfGuardDefenseMagnitude)
                        {
                            SourceAbilityName = "防御",
                        },
                    }),
                },
            };
        }

        /// <summary>
        /// この actor の行動を宣言する。
        /// 1. 行動不能（麻痺・凍結）→ 待機
        /// 2. IsForcedWhenReady の Waza を順に評価 → 使用可能 ＆ 対象ありなら採用
        /// 3. 通常 Waza を順に評価 → 使用可能 ＆ 対象ありなら採用
        /// 4. すべて空振り → def_guard フォールバック
        /// </summary>
        public static ActionDeclaration DeclareAction(
            RuntimeUnit actor,
            IList<RuntimeWaza> battleWazas,
            IList<RuntimeUnit> allies,
            IList<RuntimeUnit> enemies)
        {
            if (actor == null) return BuildWaiting(null, 0);

            int spd = ComputeEffectiveSpd(actor);

            if (IsActionImpaired(actor))
                return BuildWaiting(actor, spd);

            if (battleWazas != null)
            {
                for (int i = 0; i < battleWazas.Count; i++)
                {
                    var waza = battleWazas[i];
                    if (waza == null) continue;
                    if (!waza.IsForcedWhenReady) continue;
                    if (!IsUsable(waza)) continue;
                    var targets = GetValidTargets(actor, waza, allies, enemies);
                    if (targets.Count == 0) continue;
                    return Build(actor, waza, targets, spd);
                }
            }

            if (battleWazas != null)
            {
                for (int i = 0; i < battleWazas.Count; i++)
                {
                    var waza = battleWazas[i];
                    if (waza == null) continue;
                    if (waza.IsForcedWhenReady) continue;
                    if (!IsUsable(waza)) continue;
                    var targets = GetValidTargets(actor, waza, allies, enemies);
                    if (targets.Count == 0) continue;
                    return Build(actor, waza, targets, spd);
                }
            }

            return BuildSelfGuard(actor, spd);
        }

        private static bool IsUsable(RuntimeWaza waza)
        {
            if (waza.CurrentCooldown > 0) return false;
            if (waza.MaxUsesPerBattle >= 0 && waza.CurrentUses >= waza.MaxUsesPerBattle) return false;
            return true;
        }

        private static bool IsActionImpaired(RuntimeUnit actor)
        {
            if (actor == null) return false;
            return actor.IsParalyzed || actor.IsFullyFrozen;
        }

        // Freeze スタック × 10% で SPD 減衰（上限 90% 減衰）。
        // IsFullyFrozen（Stacks >= 10）は IsActionImpaired で行動不能扱いになる。
        // public：動的順序方式の SpdOrderResolver.SelectNext から行動順番選定時に呼ばれる。
        public static int ComputeEffectiveSpd(RuntimeUnit actor)
        {
            if (actor?.BaseUnit == null) return 0;
            int baseSpd = actor.BaseUnit.BaseSPD;
            int freezeStacks = actor.ActiveEffects
                .Where(e => e is Echolos.Domain.Effects.FreezeEffect)
                .Sum(e => e.Stacks);
            double reduction = freezeStacks * 0.1;
            if (reduction > 0.9) reduction = 0.9;
            int effective = (int)(baseSpd * (1.0 - reduction));
            return effective < 0 ? 0 : effective;
        }

        private static ActionDeclaration Build(
            RuntimeUnit actor, RuntimeWaza waza, List<RuntimeUnit> targets, int spd)
        {
            return new ActionDeclaration(
                actor: actor,
                declaredWaza: waza,
                targets: targets,
                effectiveSPD: spd,
                isWaiting: false);
        }

        private static ActionDeclaration BuildWaiting(RuntimeUnit actor, int spd)
        {
            return new ActionDeclaration(
                actor: actor,
                declaredWaza: null,
                targets: new List<RuntimeUnit>(),
                effectiveSPD: spd,
                isWaiting: true);
        }

        private static ActionDeclaration BuildSelfGuard(RuntimeUnit actor, int spd)
        {
            var runtimeWaza = new RuntimeWaza(_selfGuardTemplate);
            return new ActionDeclaration(
                actor: actor,
                declaredWaza: runtimeWaza,
                targets: new List<RuntimeUnit> { actor },
                effectiveSPD: spd,
                isWaiting: false);
        }

        /// <summary>
        /// Waza に従って有効ターゲット群を返す。
        /// TargetingCondition が設定されていればフィルタも適用する。
        /// </summary>
        public static List<RuntimeUnit> GetValidTargets(
            RuntimeUnit actor,
            RuntimeWaza waza,
            IList<RuntimeUnit> allies,
            IList<RuntimeUnit> enemies)
        {
            if (waza == null) return new List<RuntimeUnit>();

            List<RuntimeUnit> result;
            switch (waza.TargetingType)
            {
                case TargetingType.Self:
                    result = actor != null && actor.IsAlive
                        ? new List<RuntimeUnit> { actor }
                        : new List<RuntimeUnit>();
                    break;

                case TargetingType.AllAllies:
                    result = GetAliveUnits(allies);
                    break;

                case TargetingType.AllEnemies:
                    result = GetAliveUnits(enemies);
                    break;

                case TargetingType.SingleEnemy:
                    result = waza.TargetSelection != TargetSelection.Default
                        ? SelectSingleByStrategy(enemies, waza.TargetSelection)
                        : SelectSingleByDirection(enemies, actor, count: 1);
                    break;

                case TargetingType.SingleAlly:
                    result = waza.TargetSelection != TargetSelection.Default
                        ? SelectSingleByStrategy(allies, waza.TargetSelection)
                        : SelectSingleByStrategy(allies, TargetSelection.LowestHpRatio);
                    break;

                case TargetingType.DirectionalEnemies:
                    int count = waza.TargetCount > 0 ? waza.TargetCount : 1;
                    result = SelectSingleByDirection(enemies, actor, count: count);
                    break;

                default:
                    result = new List<RuntimeUnit>();
                    break;
            }

            if (waza.TargetingCondition != null)
                result = result.FindAll(t => waza.TargetingCondition(t));

            return result;
        }

        /// <summary>生存ユニットのみ抽出（順序は入力順を維持）。</summary>
        private static List<RuntimeUnit> GetAliveUnits(IList<RuntimeUnit> side)
        {
            var result = new List<RuntimeUnit>();
            if (side == null) return result;
            foreach (var u in side)
                if (u != null && u.IsAlive) result.Add(u);
            return result;
        }

        /// <summary>
        /// TargetingDirection に従って敵側から 1 体を選定する。
        /// FromFront = 内部スロット最前（slot 昇順の先頭）。
        /// FromBack = 内部スロット最後尾（slot 昇順の末尾）。
        /// </summary>
        private static List<RuntimeUnit> SelectSingleByDirection(
            IList<RuntimeUnit> side, RuntimeUnit actor, int count)
        {
            var result = new List<RuntimeUnit>();
            var alive = GetAliveUnits(side);
            if (alive.Count == 0) return result;
            alive.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            var direction = actor?.BaseUnit != null
                ? actor.BaseUnit.TargetingDirection
                : TargetingDirection.FromFront;

            int take = count < alive.Count ? count : alive.Count;
            if (direction == TargetingDirection.FromFront)
            {
                for (int i = 0; i < take; i++) result.Add(alive[i]);
            }
            else
            {
                for (int i = alive.Count - take; i < alive.Count; i++) result.Add(alive[i]);
            }
            return result;
        }

        /// <summary>
        /// 戦略に従って全生存対象から 1 体選定する（TargetingDirection は無視）。
        /// タイブレークは入力順（呼び出し側が slot 昇順で渡す前提）。
        /// </summary>
        private static List<RuntimeUnit> SelectSingleByStrategy(
            IList<RuntimeUnit> side, TargetSelection strategy)
        {
            var alive = GetAliveUnits(side);
            if (alive.Count == 0) return alive;

            RuntimeUnit best = null;
            switch (strategy)
            {
                case TargetSelection.LowestHpRatio:
                {
                    float bestVal = float.MaxValue;
                    foreach (var u in alive)
                    {
                        float ratio = u.MaxHP > 0 ? (float)u.CurrentHP / u.MaxHP : 0f;
                        if (ratio < bestVal) { bestVal = ratio; best = u; }
                    }
                    break;
                }
                case TargetSelection.HighestAtk:
                {
                    int bestVal = int.MinValue;
                    foreach (var u in alive)
                    {
                        int atk = u.EffectiveATK;
                        if (atk > bestVal) { bestVal = atk; best = u; }
                    }
                    break;
                }
                case TargetSelection.HighestDef:
                {
                    int bestVal = int.MinValue;
                    foreach (var u in alive)
                    {
                        int def = u.EffectiveDEF;
                        if (def > bestVal) { bestVal = def; best = u; }
                    }
                    break;
                }
                default:
                    best = alive[0];
                    break;
            }
            return best != null ? new List<RuntimeUnit> { best } : new List<RuntimeUnit>();
        }
    }
}
