// アクション 1 回で 1 ターゲットに発生した結果の値オブジェクト。
// ActionExecutor.ExecuteAction がターゲットごとに作って蓄積し、
// 完了時に ActionResolved イベントの Outcomes として束ねて発火する。
//
// 【スコープ】
// - 1 ターゲット × 1 ヒット = 1 HitOutcome。
// - 多段ヒット技は同ターゲットに複数 HitOutcome が並ぶ（演出側で段を表現できる余地）。
// - アクション「外」で発生する Aura / BurnTick / オーラ等はこの構造に含めない（個別 Event）。
using System.Collections.Generic;
using Echolos.Domain.Effects;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    /// <summary>1 ターゲットへの 1 ヒット結果。不変。</summary>
    public sealed class HitOutcome
    {
        /// <summary>対象ユニット。</summary>
        public RuntimeUnit Target { get; }

        /// <summary>与ダメージ（攻撃でなければ 0）。</summary>
        public int Damage { get; }

        /// <summary>回復量（回復でなければ 0）。</summary>
        public int HealAmount { get; }

        /// <summary>回避された場合 true（このとき Damage は 0）。</summary>
        public bool WasEvaded { get; }

        /// <summary>クリティカルヒットの場合 true（演出・ログ表示用）。</summary>
        public bool IsCritical { get; }

        /// <summary>このヒットで対象が戦闘不能になった場合 true。</summary>
        public bool ResultedInDeath { get; }

        /// <summary>付帯付与された状態効果の値スナップショット（攻撃の付帯バフ・支援技の主効果も含む）。
        /// Stacks / RemainingTurns を含むため観戦ビューはバッジ表示＋ホバー詳細を 1 件で構築できる。</summary>
        public IReadOnlyList<EffectChange> AppliedEffects { get; }

        /// <summary>解除された状態効果の値スナップショット（Dispel/Cleanse 系 Effect の主効果）。</summary>
        public IReadOnlyList<EffectChange> RemovedEffects { get; }

        /// <summary>処理後の対象 HP（HP バー更新用）。</summary>
        public int TargetHPAfter { get; }

        /// <summary>反撃由来の Outcome なら true。</summary>
        public bool IsCounterAttack { get; }

        /// <summary>攻撃が Shield で完全吸収された場合 true。</summary>
        public bool WasShielded { get; }

        public HitOutcome(
            RuntimeUnit target,
            int damage = 0,
            int healAmount = 0,
            bool wasEvaded = false,
            bool isCritical = false,
            bool resultedInDeath = false,
            IReadOnlyList<EffectChange> appliedEffects = null,
            int targetHPAfter = 0,
            bool isCounterAttack = false,
            bool wasShielded = false,
            IReadOnlyList<EffectChange> removedEffects = null)
        {
            Target = target;
            Damage = damage;
            HealAmount = healAmount;
            WasEvaded = wasEvaded;
            IsCritical = isCritical;
            ResultedInDeath = resultedInDeath;
            AppliedEffects = appliedEffects ?? System.Array.Empty<EffectChange>();
            TargetHPAfter = targetHPAfter;
            IsCounterAttack = isCounterAttack;
            WasShielded = wasShielded;
            RemovedEffects = removedEffects ?? System.Array.Empty<EffectChange>();
        }
    }
}
