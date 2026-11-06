using System;
using System.Collections.Generic;

namespace HollowElements.Rules
{
    public sealed partial class EnemyRules
    {
        private readonly RuleConfig config;
        public EnemyRules(RuleConfig snapshot) { snapshot.Validate(); config = snapshot.Copy(); }
        private ReactionProfile ReactionProfile()
        {
            var r=config.Reactions.Copy();double scale=config.ReactionDamageMultiplier;
            r.OverloadNailScale*=scale;r.EnemyElectroNailScale*=scale;r.ShatterNailScale*=scale;r.SuperconductNailScale*=scale;
            return r;
        }

        // Transactional: rejected/blocked game hits discard NextState, including counters and budgets.
        public HitResult Prepare(TargetState current, AttackContext hit, double now, IRuleModifier modifier = null, CharmState charms = null)
        {
            hit=hit.Copy();charms=charms?.Copy();charms?.Expire(now);
            if(current.Charms.FragileUntil>now) hit.Damage*=1.5;
            if(charms!=null) PrepareCharmHit(charms,current,hit,now);
            var result = PrepareCore(current, hit, now, modifier,charms);
            result.NextCharms=charms;result.ResolvedElement=hit.Element;
            if(charms!=null) FinishCharms(charms,current,hit,result,now);
            result.NextState.RestoreIntrinsicAura();
            return result;
        }

        private HitResult PrepareCore(TargetState current, AttackContext hit, double now, IRuleModifier modifier,CharmState charms)
        {
            if (hit.Damage <= 0 || double.IsNaN(hit.Damage) || double.IsInfinity(hit.Damage) || hit.BaseNailDamage < 1 || double.IsNaN(now) || double.IsInfinity(now)) throw new ArgumentException("命中数据无效。");
            var state = current.Copy();
            state.Expire(now);
            var result = new HitResult { NextState = state, PreciseDamage = hit.Damage };
            if (hit.Origin != DamageOrigin.Direct) return result;
            var p = config.Attacks[hit.Source].Copy();
            var r = ReactionProfile();
            if (modifier != null) modifier.Modify(hit, p, r);
            if(charms!=null) CharmProfiles(charms,state,hit,p,r,result,now);
            result.PreciseDamage=hit.Damage*config.DirectDamageMultiplier;

            bool swordArt = p.Slot == AttackSlot.GreatSlash || p.Slot == AttackSlot.DashSlash || p.Slot == AttackSlot.Cyclone;
            if (state.FrozenUntil > now && (hit.Element == Element.None || swordArt))
            {
                result.Reaction = Reaction.Shatter;result.ConsumedFrost=true;
                state.EndFrost();
                result.Secondary.Add(new SecondaryDamage { Origin = DamageOrigin.Shatter, Element = Element.None, Damage = Reactions.Damage(hit.BaseNailDamage * r.ShatterNailScale) });
                // The triggering neutral direct hit can still receive superconduct; shatter damage cannot.
                if(hit.Element==Element.None) {ApplySuperconduct(state,hit,p,result,now,charms);return result;}
            }

            ApplySuperconduct(state, hit, p, result, now,charms);
            if (!hit.CanApplyAura || hit.Element == Element.None || !CanApply(state, hit, p, now)) return result;
            if(charms!=null && charms.Has(CharmId.C28) && state.Charms.FrostCharges>0 && hit.Element==Element.Hydro)
            {
                state.Charms.FrostCharges--;
                var pre=new HitResult {NextState=state};var cold=hit.Copy();cold.Element=Element.Cryo;cold.Damage=0;
                ApplyElement(pre,cold,1,r,now);result.Secondary.AddRange(pre.Secondary);
                if(pre.Reaction!=Reaction.None) result.PreludeReactions.Add(pre.Reaction);
            }
            if(state.FrozenUntil>now && swordArt)
            {
                result.Reaction=Reaction.Shatter;result.ConsumedFrost=true;
                state.EndFrost();
                result.Secondary.Add(new SecondaryDamage {Origin=DamageOrigin.Shatter,Element=Element.None,Damage=Reactions.Damage(hit.BaseNailDamage*r.ShatterNailScale)});
            }
            if(result.Reaction==Reaction.Shatter) {result.PreludeReactions.Add(Reaction.Shatter);result.Reaction=Reaction.None;}
            return ApplyCharmElement(result,hit,p.AuraUnits,r,now);
        }

        // A self-application has no direct hit, ICD, shatter trigger, or synthetic chip damage.
        public HitResult Attach(TargetState current, Element element, double units, int baseNailDamage, bool canRecoil, double now)
        {
            if (element == Element.None || units <= 0 || double.IsNaN(units) || double.IsInfinity(units) || baseNailDamage < 1) throw new ArgumentException("Invalid attachment.");
            var state = current.Copy(); state.Expire(now);
            var result = new HitResult { NextState = state, Damage = 0 };
            var hit = new AttackContext { Element = element, Damage = 0, BaseNailDamage = baseNailDamage, TargetCanRecoil = canRecoil };
            ApplyElement(result, hit, units, ReactionProfile(), now);
            result.NextState.RestoreIntrinsicAura();
            return result;
        }

        private static HitResult ApplyElement(HitResult result, AttackContext hit, double units, ReactionProfile r, double now)
        {
            var state = result.NextState;
            result.AppliedAura = true;
            result.AppliedUnits=units;result.PriorAuraUnits=state.AuraUnits;result.AppliedSeconds=r.AuraSeconds;
            // The control state supplies virtual Cryo only against fire/lightning.
            // Never write it into the ordinary aura: even an empowered zero-consumption
            // reaction must not leave ice that a later Hydro hit can reuse.
            bool virtualIce=state.FrozenUntil>now && (hit.Element==Element.Pyro || hit.Element==Element.Electro);
            result.UsedFrostAura=virtualIce;
            Reaction reaction = Reactions.Identify(virtualIce?Element.Cryo:state.Aura, hit.Element);
            result.Reaction = reaction;
            if (reaction == Reaction.None)
            {
                state.Aura = hit.Element;
                state.AuraUnits = Math.Max(state.AuraUnits, units);
                state.AuraUntil = now + r.AuraSeconds;
                state.AuraDuration = r.AuraSeconds;
                return result;
            }

            if(!result.UsedSpecialFire && reaction!=Reaction.Freeze && state.FrozenUntil>now) {state.EndFrost();result.ConsumedFrost=true;}
            double consumption = Reactions.Forward(reaction) ? r.ForwardConsumption : Reactions.Amplifying(reaction) ? r.ReverseConsumption : 1;
            result.ConsumptionPerUnit=consumption;
            if(!virtualIce)
            {
                state.AuraUnits = Math.Max(0, state.AuraUnits - (result.Empowered?0:units * consumption));
                if (state.AuraUnits <= 1e-9) state.ClearAura();
            }
            switch (reaction)
            {
                case Reaction.VaporizeForward: case Reaction.MeltForward:
                    result.PreciseDamage *= r.ForwardMultiplier; break;
                case Reaction.VaporizeReverse: case Reaction.MeltReverse:
                    result.PreciseDamage *= r.ReverseMultiplier; break;
                case Reaction.Overload:
                    result.Secondary.Add(new SecondaryDamage { Origin = DamageOrigin.Overload, Element = Element.Pyro,
                        Damage = Reactions.Damage(hit.BaseNailDamage * r.OverloadNailScale), Radius = r.OverloadRadius, HeavyRecoil = true, RecoilMultiplier = r.HeavyRecoilMultiplier }); break;
                case Reaction.ElectroCharged:
                    var nextTick=state.ElectroCharged?.NextTick ?? now+r.EnemyElectroInterval;
                    state.ElectroCharged = new PeriodicState { NextTick = nextTick,
                        Interval = r.EnemyElectroInterval, TotalTicks = (int)Math.Round(r.EnemyElectroSeconds / r.EnemyElectroInterval),
                        Damage = Reactions.Damage(hit.BaseNailDamage * r.EnemyElectroNailScale),OriginalDamage=Reactions.Damage(hit.BaseNailDamage*r.EnemyElectroNailScale) };
                    // Refresh resets damage/cashout reductions, without delaying the next pulse.
                    state.ElectroCharged.RemainingTicks = state.ElectroCharged.TotalTicks; break;
                case Reaction.Freeze:
                    // Freeze is a control state, never a persistent Cryo application.
                    if(state.Aura==Element.Cryo) state.ClearAura();
                    {
                        state.HardFrozen = hit.TargetCanRecoil;
                        double baseline=Math.Min(r.FreezeSeconds,r.FrostSeconds),hard=state.HardFrozen?state.FreezeDuration(baseline,r):0;
                        state.FrozenDuration=state.HardFrozen?hard+Math.Max(0,r.FrostSeconds-baseline):r.FrostSeconds;
                        state.HardFrozenUntil=state.HardFrozen?now+hard:0;state.FrostAfterSpeed=r.FrostSpeed;
                        state.FrozenSpeed = state.HardFrozen ? 0 : r.FrostSpeed; state.FrozenUntil = now + state.FrozenDuration;
                    }
                    break;
                case Reaction.Superconduct:
                    state.SuperconductUntil = now + r.SuperconductSeconds; state.SuperconductDuration = r.SuperconductSeconds;
                    state.SuperconductStacks=r.SuperconductLayers;state.SuperconductHalfHits=0;state.SuperconductDamagePerLayer=hit.BaseNailDamage*r.SuperconductNailScale;
                    state.SuperconductNeutralMultiplier=r.SuperconductNeutralMultiplier;break;
            }
            return result;
        }

        private static CastBudget Budget(TargetState state, string group, long castId, double now)
        {
            string key = group + ":" + castId;
            CastBudget budget;
            if (!state.Budgets.TryGetValue(key, out budget)) { budget = new CastBudget(); state.Budgets.Add(key, budget); }
            budget.LastUsed = now;
            return budget;
        }

        private static bool CanApply(TargetState state, AttackContext hit, AttackProfile p, double now)
        {
            var budget = Budget(state, "A/" + p.AttachmentGroup, hit.CastId, now);
            if (budget.Attachments >= p.MaxApplicationsPerCast) return false;
            if(budget.AttachmentHits++ % p.AttachmentHitStride != 0) return false;
            IcdState icd;
            if (!state.Icd.TryGetValue(p.IcdGroup, out icd))
            {
                icd = new IcdState { WindowStart = now }; state.Icd.Add(p.IcdGroup, icd);
            }
            else if (p.IcdSeconds <= 0 || now - icd.WindowStart >= p.IcdSeconds)
            {
                icd.WindowStart = now; icd.Hits = 0;
            }
            int sequence = icd.Hits++;
            if (sequence % p.HitStride != 0) return false;
            budget.Attachments++;
            return true;
        }

        private static void ApplySuperconduct(TargetState state, AttackContext hit, AttackProfile p, HitResult result, double now,CharmState charms)
        {
            if (hit.SkipSuperconduct || state.SuperconductStacks<=0 || state.SuperconductUntil <= now || p.SuperconductCoefficient <= 0 || now-state.SuperconductLastConsumedAt+1e-9<p.SuperconductMinInterval) return;
            var budget = Budget(state, "S/" + p.SuperconductGroup, hit.CastId, now);
            if (budget.SuperconductHits >= p.SuperconductHitsPerCast) return;
            bool half=charms!=null && charms.Has(CharmId.C03) && charms.InsulatedUntil>now && hit.Element!=Element.None;
            double factor=half?.5:1;
            if(charms!=null && charms.Has(CharmId.C21) && charms.ScarUntil>now) factor*=hit.Element==Element.None?2.5:2;
            double extra = factor * state.SuperconductDamagePerLayer * (hit.Element==Element.None?state.SuperconductNeutralMultiplier:1) * p.SuperconductCoefficient;
            if (p.SuperconductDamageCap > 0)
            {
                double remaining=p.SuperconductDamageCap-budget.SuperconductDamage;
                if(remaining<1) return;
                extra=Math.Min(extra,remaining);
            }
            if(extra<=0) return;
            int amount=Reactions.Damage(extra);
            result.Secondary.Add(new SecondaryDamage {Origin=DamageOrigin.Superconduct,Element=Element.None,Damage=amount});
            budget.SuperconductHits++;
            budget.SuperconductDamage += amount;
            if(!half || ++state.SuperconductHalfHits>=2) {state.SuperconductStacks--;state.SuperconductHalfHits=0;result.SuperconductConsumed=true;}
            state.SuperconductLastConsumedAt=now;
            if(state.SuperconductStacks>0) state.SuperconductUntil=now+state.SuperconductDuration;
            else {state.SuperconductUntil=0;state.SuperconductDuration=0;}
        }

        public List<SecondaryDamage> Tick(TargetState state, double now)
        {
            state.Expire(now);
            var result = new List<SecondaryDamage>();
            var ec = state.ElectroCharged;
            if (ec == null || now + 1e-9 < ec.NextTick) return result;
            result.Add(new SecondaryDamage { Origin = DamageOrigin.ElectroCharged, Element = Element.Electro, Damage = ec.Damage });
            ec.RemainingTicks--;
            // No burst catch-up after invulnerability or a long frame; preserve all remaining pulses.
            ec.NextTick = Math.Max(ec.NextTick + ec.Interval, now + ec.Interval);
            if (ec.RemainingTicks <= 0) state.ElectroCharged = null;
            return result;
        }
    }
}
