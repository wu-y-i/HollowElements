using System;

namespace HollowElements.Rules
{
    public sealed partial class EnemyRules
    {
        private static bool Sword(AttackSource source) => source==AttackSource.GreatSlash || source==AttackSource.DashSlash || source==AttackSource.Cyclone;
        private static int Kind(AttackSource source) => source==AttackSource.Nail?0:Sword(source)?2:source==AttackSource.NativeAuxiliary?-1:1;
        private static void PrepareCharmHit(CharmState charms,TargetState state,AttackContext hit,double now)
        {
            if(charms.Has(CharmId.C03) && charms.InsulatedUntil>now && Sword(hit.Source) && !hit.AnySuperconductTarget) charms.InsulatedUntil=0;
            if(charms.Has(CharmId.C03) && charms.InsulatedUntil>now && state.SuperconductStacks>0 && state.SuperconductUntil>now && Sword(hit.Source)) hit.Element=Element.None;
            if(charms.Has(CharmId.C06) && charms.FourUntil>now && hit.Element!=Element.None) hit.Damage*=1+charms.Bonuses[0]*.05;
        }
        private static void CharmProfiles(CharmState charms,TargetState state,AttackContext hit,AttackProfile p,ReactionProfile r,HitResult result,double now)
        {
            if(charms.Has(CharmId.C01) && hit.Source==AttackSource.Nail) {p.IcdSeconds=2;p.HitStride=2;}
            if(charms.Has(CharmId.C07) && hit.Element==Element.Pyro) {p.AuraUnits*=2;r.AuraSeconds*=1.5;}
            if(charms.Has(CharmId.C08)) r.ReverseConsumption*=.5;
            if(charms.Has(CharmId.C09)) {r.ForwardMultiplier=4.5;r.ReverseMultiplier=3;}
            if(charms.Has(CharmId.C13)) {r.OverloadRadius*=3;r.OverloadNailScale*=2;}
            if(charms.Has(CharmId.C27)) {r.FreezeSeconds*=1.5;r.FrostSpeed=Math.Max(0,1-(1-r.FrostSpeed)*1.5);}
            bool frostReaction=state.FrozenUntil>now && (hit.Element==Element.Pyro || hit.Element==Element.Electro);
            bool power=charms.Has(CharmId.C31) && Kind(hit.Source)==1 && (charms.SpellCasts.ContainsKey(hit.CastId) || charms.SpellCharges>0 && (frostReaction || Reactions.Identify(state.Aura,hit.Element)!=Reaction.None));
            if(power)
            {
                p.IcdSeconds=0;p.HitStride=1;p.AttachmentHitStride=1;p.MaxApplicationsPerCast=int.MaxValue;result.Empowered=true;
            }
            if(charms.Has(CharmId.C03) && charms.InsulatedUntil>now && hit.Element==Element.None && Sword(hit.Source)) p.SuperconductCoefficient*=2;
            if(charms.Has(CharmId.C17) && Sword(hit.Source) && state.ElectroCharged!=null)
            {
                var ec=state.ElectroCharged;
                result.Secondary.Add(new SecondaryDamage {Origin=DamageOrigin.ElectroCharged,Element=Element.Electro,Damage=ec.Damage*ec.RemainingTicks});
                if(ec.OriginalDamage<=0) ec.OriginalDamage=ec.Damage;
                ec.Cashouts++;
                ec.Damage=charms.Has(CharmId.C19)?Reactions.Damage(ec.OriginalDamage*(1-Math.Min(.6,ec.Cashouts*.2))):Math.Max(1,ec.Damage/2);
            }
        }
        private static HitResult ApplyCharmElement(HitResult result,AttackContext hit,double units,ReactionProfile r,double now)
        {
            var state=result.NextState;
            if(state.Charms.FireUnits<=0 || state.Charms.FireUntil<=now || hit.Element!=Element.Hydro && hit.Element!=Element.Cryo)
                return ApplyElement(result,hit,units,r,now);
            var aura=state.Aura;double amount=state.AuraUnits,until=state.AuraUntil,duration=state.AuraDuration;
            state.Aura=Element.Pyro;state.AuraUnits=state.Charms.FireUnits;state.AuraUntil=state.Charms.FireUntil;state.AuraDuration=state.Charms.FireDuration;
            var special=r.Copy();
            if(hit.Element==Element.Cryo) special.ReverseMultiplier=r.ForwardMultiplier;
            else special.ForwardConsumption=r.ReverseConsumption;
            result.UsedSpecialFire=true;ApplyElement(result,hit,units,special,now);
            state.Charms.FireUnits=state.AuraUnits;if(state.Aura==Element.None) state.Charms.FireUntil=0;
            state.Aura=aura;state.AuraUnits=amount;state.AuraUntil=until;state.AuraDuration=duration;
            return result;
        }
        private void FinishCharms(CharmState charms,TargetState previous,AttackContext hit,HitResult result,double now)
        {
            var state=result.NextState;var target=state.Charms;bool amplify=Reactions.Amplifying(result.Reaction);
            if(charms.Has(CharmId.C09) && amplify && !result.UsedFrostAura && state.IntrinsicAura==Element.None) state.ClearAura();
            if(charms.Has(CharmId.C27) && result.ConsumedFrost) target.FragileUntil=now+5;
            if(charms.Has(CharmId.C05) && result.AppliedAura && result.Reaction!=Reaction.None && !result.UsedSpecialFire && !result.UsedFrostAura && target.LastKind>=0 && target.LastKind!=Kind(hit.Source) && state.Aura==Element.None && state.IntrinsicAura==Element.None)
            {
                state.Aura=hit.Element;state.AuraUnits=.5;state.AuraUntil=now+result.AppliedSeconds;state.AuraDuration=result.AppliedSeconds;
            }
            if(Kind(hit.Source)>=0) target.LastKind=Kind(hit.Source);

            if(charms.Has(CharmId.C12) && Sword(hit.Source) && amplify && !result.UsedSpecialFire)
            {
                target.FireUnits=Math.Max(target.FireUnits,1);
                target.FireDuration=config.Reactions.AuraSeconds;target.FireUntil=now+target.FireDuration;
            }
            if(result.Reaction==Reaction.ElectroCharged && state.ElectroCharged!=null)
            {
                var ec=state.ElectroCharged;
                if(charms.Has(CharmId.C17)) {ec.Damage=ec.OriginalDamage>0?ec.OriginalDamage:ec.Damage;ec.Cashouts=0;}
                if(charms.Has(CharmId.C35)) {ec.RemainingTicks=1;ec.TotalTicks=1;}
            }
            if(charms.Has(CharmId.C19) && result.Reaction!=Reaction.ElectroCharged && (hit.Element==Element.Hydro || hit.Element==Element.Electro) && state.ElectroCharged!=null)
            {
                var ec=state.ElectroCharged;ec.RemainingTicks=Math.Min(charms.Has(CharmId.C35)?4:6,ec.RemainingTicks+1);ec.TotalTicks=Math.Max(ec.TotalTicks,ec.RemainingTicks);
            }
            if(charms.Has(CharmId.C28) && (result.Reaction==Reaction.Shatter || result.PreludeReactions.Contains(Reaction.Shatter))) target.FrostCharges=2;
            foreach(var secondary in result.Secondary)
            {
                if(secondary.Origin==DamageOrigin.Superconduct && result.SuperconductConsumed && charms.Has(CharmId.C21)) {secondary.Soul=true;charms.ScarUntil=now+10;}
                if(secondary.Origin==DamageOrigin.Overload)
                {
                    if(charms.Has(CharmId.C13)) secondary.HeavyRecoil=false;
                    if(charms.Has(CharmId.C14)) {secondary.Delay=.35;secondary.Sparks=true;}
                }
            }
            if(result.Empowered && result.Reaction!=Reaction.None && !charms.SpellCasts.ContainsKey(hit.CastId))
            {charms.SpellCasts[hit.CastId]=now;charms.SpellCharges=Math.Max(0,charms.SpellCharges-1);}
            if(charms.Has(CharmId.C31) && Kind(hit.Source)==1 && charms.SpellCharges==0 && !charms.SpellCasts.ContainsKey(hit.CastId))
            {
                if(now-target.SpellStarted>20) {target.SpellStarted=now;target.Spells=0;}
                var slot=RuleConfig.SlotOf(hit.Source);target.Spells|=slot==AttackSlot.Wave?1:slot==AttackSlot.Dive?2:4;
                if(target.Spells==7) {charms.SpellCharges=3;target.Spells=0;target.SpellStarted=-1000;}
            }
            if(charms.Has(CharmId.C06) && hit.Element!=Element.None)
            {
                if(charms.FourUntil>now) charms.Bonuses[0]++;
                else
                {
                    if(target.Epoch!=charms.Epoch || now-target.FourStarted>10) {target.Epoch=charms.Epoch;target.FourStarted=now;target.Elements=0;}
                    target.Elements|=1<<((int)hit.Element-1);
                    if(target.Elements==7 || target.Elements==11 || target.Elements==13 || target.Elements==14 || target.Elements==15) {charms.FourUntil=now+10;charms.Epoch++;Array.Clear(charms.Bonuses,0,charms.Bonuses.Length);}
                }
            }
            foreach(var reaction in result.PreludeReactions) charms.Reaction(reaction,now);
            charms.Reaction(result.Reaction,now);
        }
    }
    public sealed class ElementDeck
    {
        private readonly Random random;private readonly Element[] bag={Element.Pyro,Element.Hydro,Element.Electro,Element.Cryo};private int index=4;
        public ElementDeck(int seed) {random=new Random(seed);}
        public Element Peek()
        {
            if(index>=4) {for(int i=3;i>0;i--) {int j=random.Next(i+1);var t=bag[i];bag[i]=bag[j];bag[j]=t;}index=0;}
            return bag[index];
        }
        public Element Next() {var result=Peek();index++;return result;}
    }
}
