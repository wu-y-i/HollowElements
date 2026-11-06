using System;
using System.Collections.Generic;

namespace HollowElements.Rules
{
    public enum Element { None, Pyro, Hydro, Electro, Cryo }
    public enum AttackSlot { Nail, Wave, Dive, Shriek, GreatSlash, DashSlash, Cyclone }
    public enum AttackSource
    {
        Nail, WhiteWave, BlackWave, WhiteDiveBody, WhiteDiveShock, BlackDiveBody,
        BlackDiveShock, WhiteShriek, BlackShriek, GreatSlash, DashSlash, Cyclone, NativeAuxiliary
    }
    public enum Reaction { None, VaporizeForward, VaporizeReverse, MeltForward, MeltReverse, Overload, ElectroCharged, Freeze, Superconduct, Shatter }
    public enum DamageOrigin { Direct, Overload, ElectroCharged, Shatter, Superconduct }

    // These records are independent of Unity. A game hit is committed only after it is accepted.
    public sealed class AttackContext
    {
        public AttackSource Source;
        public long CastId;
        public Element Element;
        public double Damage;
        public int BaseNailDamage;
        public bool TargetCanRecoil;
        public bool CanApplyAura = true;
        public bool SkipSuperconduct;
        public bool AnySuperconductTarget = true;
        public DamageOrigin Origin;
        public AttackContext Copy() => (AttackContext)MemberwiseClone();
    }

    public sealed class SecondaryDamage
    {
        public DamageOrigin Origin;
        public Element Element;
        public int Damage;
        public double Radius;
        public bool HeavyRecoil;
        public double RecoilMultiplier;
        public double Delay;
        public bool Sparks;
        public bool Soul;
        public SecondaryDamage Copy() => (SecondaryDamage)MemberwiseClone();
    }

    public sealed class HitResult
    {
        public TargetState NextState;
        public double PreciseDamage;
        // Keep precision through reactions and Boss modifiers; round only at native submission.
        public int Damage { get => Reactions.FinalDamage(PreciseDamage); set => PreciseDamage=value; }
        public Reaction Reaction;
        public bool AppliedAura;
        public double AppliedUnits, ConsumptionPerUnit, PriorAuraUnits, AppliedSeconds;
        public Element ResolvedElement;
        public CharmState NextCharms;
        public bool Empowered, UsedSpecialFire, UsedFrostAura, SuperconductConsumed, ConsumedFrost;
        public readonly List<Reaction> PreludeReactions = new List<Reaction>();
        public readonly List<SecondaryDamage> Secondary = new List<SecondaryDamage>();
    }

    public sealed class IcdState
    {
        public double WindowStart;
        public int Hits;
        public IcdState Copy() { return (IcdState)MemberwiseClone(); }
    }

    public sealed class CastBudget
    {
        public double LastUsed;
        public int Attachments;
        public int AttachmentHits;
        public int SuperconductHits;
        public double SuperconductDamage;
        public CastBudget Copy() { return (CastBudget)MemberwiseClone(); }
    }

    public sealed class PeriodicState
    {
        public double NextTick;
        public double Interval;
        public int RemainingTicks;
        public int TotalTicks;
        public int Damage;
        public int OriginalDamage;
        public int Cashouts;
        public PeriodicState Copy() { return (PeriodicState)MemberwiseClone(); }
    }

    public sealed class TargetState
    {
        // Intrinsic aura is an inexhaustible encounter mechanic, not a very large elemental gauge.
        public Element IntrinsicAura;
        public Element Aura;
        public double AuraUnits;
        public double AuraUntil;
        public double AuraDuration;
        public double FrozenUntil;
        public double FrozenDuration;
        public double FrozenSpeed = 1;
        public bool HardFrozen;
        public double HardFrozenUntil, FrostAfterSpeed = 1;
        public double FreezeDecayRate=1, FreezeDecayAt=-1, FreezeDecayAcceleration=.25, FreezeDecayRecovery=.5, FreezeDecayFrostRecovery=.05;
        public int SuperconductHalfHits;
        public double SuperconductUntil;
        public double SuperconductDuration;
        public int SuperconductStacks;
        public double SuperconductDamagePerLayer;
        public double SuperconductNeutralMultiplier;
        public double SuperconductLastConsumedAt = -1000;
        public PeriodicState ElectroCharged;
        public TargetCharmState Charms = new TargetCharmState();
        public readonly Dictionary<string, IcdState> Icd = new Dictionary<string, IcdState>();
        public readonly Dictionary<string, CastBudget> Budgets = new Dictionary<string, CastBudget>();

        public TargetState Copy()
        {
            var result = new TargetState {
                IntrinsicAura = IntrinsicAura, Aura = Aura, AuraUnits = AuraUnits, AuraUntil = AuraUntil, AuraDuration = AuraDuration,
                FrozenUntil = FrozenUntil, FrozenDuration = FrozenDuration, FrozenSpeed = FrozenSpeed, HardFrozen = HardFrozen,
                FreezeDecayRate=FreezeDecayRate,FreezeDecayAt=FreezeDecayAt,FreezeDecayAcceleration=FreezeDecayAcceleration,FreezeDecayRecovery=FreezeDecayRecovery,FreezeDecayFrostRecovery=FreezeDecayFrostRecovery,
                HardFrozenUntil=HardFrozenUntil, FrostAfterSpeed=FrostAfterSpeed, SuperconductHalfHits=SuperconductHalfHits,
                SuperconductUntil = SuperconductUntil, SuperconductDuration = SuperconductDuration, SuperconductStacks = SuperconductStacks,
                SuperconductDamagePerLayer=SuperconductDamagePerLayer,SuperconductNeutralMultiplier=SuperconductNeutralMultiplier,SuperconductLastConsumedAt=SuperconductLastConsumedAt,
                ElectroCharged = ElectroCharged == null ? null : ElectroCharged.Copy(), Charms = Charms.Copy()
            };
            foreach (var pair in Icd) result.Icd.Add(pair.Key, pair.Value.Copy());
            foreach (var pair in Budgets) result.Budgets.Add(pair.Key, pair.Value.Copy());
            return result;
        }

        public void AdvanceFreezeDecay(double now)
        {
            if(FreezeDecayAt<0 || now<FreezeDecayAt) {FreezeDecayAt=now;return;}
            double dt=now-FreezeDecayAt;
            double hard=HardFrozen?Math.Max(0,Math.Min(now,HardFrozenUntil)-FreezeDecayAt):0;
            double soft=Math.Max(0,Math.Min(now,FrozenUntil)-FreezeDecayAt-hard);
            FreezeDecayRate=Math.Max(1,FreezeDecayRate+hard*FreezeDecayAcceleration-soft*FreezeDecayFrostRecovery-(dt-hard-soft)*FreezeDecayRecovery);
            FreezeDecayAt=now;
        }
        public double FreezeDuration(double baseline,ReactionProfile profile)
        {
            FreezeDecayAcceleration=profile.FreezeDecayAcceleration;FreezeDecayRecovery=profile.FreezeDecayRecovery;FreezeDecayFrostRecovery=profile.FreezeDecayFrostRecovery;
            double budget=baseline+FreezeDecayAcceleration*baseline*baseline*.5;
            return 2*budget/(Math.Sqrt(FreezeDecayRate*FreezeDecayRate+2*FreezeDecayAcceleration*budget)+FreezeDecayRate);
        }
        public void EndFrost() { FrozenUntil=0;FrozenDuration=0;FrozenSpeed=1;HardFrozen=false;HardFrozenUntil=0;FrostAfterSpeed=1; }

        public void ClearAura() { Aura = Element.None; AuraUnits = 0; AuraUntil = 0; AuraDuration = 0; }

        public void SetIntrinsicAura(Element element)
        {
            IntrinsicAura = element;
            if (element == Element.None) ClearAura(); else RestoreIntrinsicAura();
        }

        public void RestoreIntrinsicAura()
        {
            if (IntrinsicAura == Element.None) return;
            Aura = IntrinsicAura; AuraUnits = 1; AuraUntil = double.MaxValue; AuraDuration = double.MaxValue;
        }

        public void Expire(double now)
        {
            AdvanceFreezeDecay(now);
            if (now >= AuraUntil) ClearAura();
            RestoreIntrinsicAura();
            if(now>=Charms.FireUntil) {Charms.FireUnits=0;Charms.FireUntil=0;}
            if (HardFrozen && now >= HardFrozenUntil) { HardFrozen=false;FrozenSpeed=FrostAfterSpeed; }
            if (now >= FrozenUntil) EndFrost();
            if (now >= SuperconductUntil) { SuperconductUntil = 0; SuperconductDuration=0;SuperconductStacks=0;SuperconductDamagePerLayer=0; }
            var obsolete = new List<string>();
            foreach (var pair in Budgets) if (now - pair.Value.LastUsed > 30) obsolete.Add(pair.Key);
            foreach (var key in obsolete) Budgets.Remove(key);
        }
    }

    public static class Reactions
    {
        public static Reaction Identify(Element aura, Element incoming)
        {
            if (aura == Element.None || incoming == Element.None || aura == incoming) return Reaction.None;
            if (aura == Element.Pyro && incoming == Element.Hydro) return Reaction.VaporizeForward;
            if (aura == Element.Hydro && incoming == Element.Pyro) return Reaction.VaporizeReverse;
            if (aura == Element.Cryo && incoming == Element.Pyro) return Reaction.MeltForward;
            if (aura == Element.Pyro && incoming == Element.Cryo) return Reaction.MeltReverse;
            if (Pair(aura, incoming, Element.Pyro, Element.Electro)) return Reaction.Overload;
            if (Pair(aura, incoming, Element.Hydro, Element.Electro)) return Reaction.ElectroCharged;
            if (Pair(aura, incoming, Element.Hydro, Element.Cryo)) return Reaction.Freeze;
            return Reaction.Superconduct;
        }
        private static bool Pair(Element a, Element b, Element x, Element y) { return a == x && b == y || a == y && b == x; }
        public static bool Forward(Reaction reaction) { return reaction == Reaction.VaporizeForward || reaction == Reaction.MeltForward; }
        public static bool Amplifying(Reaction reaction) { return Forward(reaction) || reaction == Reaction.VaporizeReverse || reaction == Reaction.MeltReverse; }
        public static int Damage(double value) { return Math.Max(1, (int)Math.Floor(value + 1e-9)); }
        public static int FinalDamage(double value) => value<=0 ? 0 : Damage(value);
    }
}
