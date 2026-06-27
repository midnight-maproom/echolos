using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;
using Echolos.Domain.Skills;

namespace Echolos.Domain.Prototype
{
    /// <summary>Roster 共通のビルダーヘルパ（internal・3 Roster 内部のみで使用）。</summary>
    internal static class RosterHelpers
    {
        internal static Unit Make(string id, string name, Element element,
            int hp, int atk, int pdef, int mdef, int spd, AttackRange range,
            int eva = 0, bool immune = false, string[] tags = null, string[] roles = null)
        {
            var u = new Unit(id, name, element)
            {
                MaxHP = hp,
                CurrentHP = hp,
                BaseATK = atk,
                PDEF = pdef,
                MDEF = mdef,
                BaseSPD = spd,
                BaseEvasion = eva,
                Range = range,
                ImmuneToStatusAilments = immune,
                State = UnitState.Active
            };
            if (tags != null) u.Tags.AddRange(tags);
            if (roles != null) u.TargetTags.AddRange(roles);
            return u;
        }

        internal static Waza Phys(string id, string name, int spd, float mult,
            int hits = 1, bool sureHit = false, float defIgnore = 0f,
            TargetingType targeting = TargetingType.SingleEnemy,
            float rowSplash = 0f,
            bool ignoresCover = false,
            bool ignoresFrontRowGuard = false,
            List<StatusEffect> riders = null)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Attack,
                IsPhysical = true,
                WazaElement = Element.None,
                SPD = spd,
                HitCount = hits,
                IsSureHit = sureHit,
                DefenseIgnoreRatio = defIgnore,
                TargetingType = targeting,
                SameRowSplashMultiplier = rowSplash,
                IgnoresCover = ignoresCover,
                IgnoresFrontRowGuard = ignoresFrontRowGuard,
                CalculateBaseDamage = (a, t) => (int)(a.EffectiveATK * mult),
                AppliedEffects = riders ?? new List<StatusEffect>()
            };
        }

        internal static Waza Magic(string id, string name, Element element, int spd, float mult,
            TargetingType targeting = TargetingType.SingleEnemy,
            List<StatusEffect> riders = null)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Attack,
                IsPhysical = false,
                WazaElement = element,
                SPD = spd,
                TargetingType = targeting,
                CalculateBaseDamage = (a, t) => (int)(a.EffectiveATK * mult),
                AppliedEffects = riders ?? new List<StatusEffect>()
            };
        }

        internal static Waza HealW(string id, string name, int spd, int amount,
            TargetingType targeting = TargetingType.SingleAlly, int cooldown = 0,
            bool cleanse = false, List<StatusEffect> riders = null)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Heal,
                SPD = spd,
                Cooldown = cooldown,
                TargetingType = targeting,
                CalculateHealAmount = (a, t) => amount
                    + a.BaseUnit.EnhancementHealPerLevel * a.BaseUnit.EnhancementLevel,
                CleansesStatusAilments = cleanse,
                AppliedEffects = riders ?? new List<StatusEffect>()
            };
        }

        internal static Waza BuffW(string id, string name, int spd, List<StatusEffect> riders)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Buff,
                SPD = spd,
                TargetingType = TargetingType.SingleAlly,
                AppliedEffects = riders ?? new List<StatusEffect>()
            };
        }

        /// <summary>
        /// 何もしない自己対象アクション（チャージ中の「詠唱」等）。
        /// IsForcedWhenReady=true + Cooldown=0 で「他に強制発動できる Waza が無いターンは必ず選ばれる」を実現する。
        /// AppliedEffects 空・Buff カテゴリのため TryDeclareSupportAction の buffWaza 評価でも除外される
        /// （AppliedEffects.Count &gt; 0 ガード）。ログには技名が表示される。
        /// </summary>
        internal static Waza Idle(string id, string name, int spd)
        {
            return new Waza(id, name)
            {
                Category = WazaCategory.Buff,
                SPD = spd,
                TargetingType = TargetingType.Self,
                IsForcedWhenReady = true,
                AppliedEffects = new List<StatusEffect>(),
            };
        }
    }
}
