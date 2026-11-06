using System;
using System.Linq;
using HollowElements.Rules;
using HollowElements.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class CharmTests
{
    private static RuleConfig Config() {var c=RuleConfig.Defaults();c.DirectDamageMultiplier=1;return c;}
    private static CharmState Equipment(params CharmId[] ids)
    {
        var c=new CharmState();foreach(var id in ids) {if(!c.Equipment.Toggle(id)) throw new Exception("Invalid fixture equipment");}return c;
    }
    private static HitResult Hit(TargetState state,CharmState charms,Element element,AttackSource source=AttackSource.WhiteWave,long cast=1,double time=1,bool aura=true)
        => new EnemyRules(Config()).Prepare(state,new AttackContext {Element=element,Source=source,CastId=cast,Damage=10,BaseNailDamage=20,CanApplyAura=aura},time,null,charms);
    private static TargetState Aura(Element element,double units=1) => new TargetState {Aura=element,AuraUnits=units,AuraUntil=100,AuraDuration=100};
    private static void Equal(double a,double b) {if(Math.Abs(a-b)>1e-6) throw new Exception(a+" != "+b);}
    private static void True(bool value) {if(!value) throw new Exception("Assertion failed");}
    internal static void EquipmentAndDeck()
    {
        True(CharmCatalog.All.Length==20);var e=new CharmEquipment();
        foreach(var group in CharmCatalog.All.GroupBy(c=>c.Category)) Equal(new[]{4,4,8,4}[group.Key],group.Count());
        e.Toggle(CharmId.C01);e.Toggle(CharmId.C06);True(e.General==CharmId.C06 && !e.Has(CharmId.C01));
        True(e.Toggle(CharmId.C13) && e.Toggle(CharmId.C14) && e.Toggle(CharmId.C17));True(e.Amplification==CharmId.C17 && !e.Toggle(CharmId.C05));e.Toggle(CharmId.C17);e.Toggle(CharmId.C05);True(!e.Toggle(CharmId.C17));e.Toggle(CharmId.C14);True(e.Toggle(CharmId.C17));
        e.TransformB=e.TransformA;e.General=CharmId.C40;e.Normalize();True(e.General==CharmId.None && e.TransformB==CharmId.None);
        var old=JsonConvert.DeserializeObject<Loadout>("{\"Nail\":1}");True(old.Charms!=null && old.Charms.Slots.All(i=>i==CharmId.None));
        var original=new Loadout {Charms=Equipment(CharmId.C31,CharmId.C12,CharmId.C17,CharmId.C19,CharmId.C40).Equipment};
        var loaded=JsonConvert.DeserializeObject<Loadout>(JsonConvert.SerializeObject(original));True(loaded.Charms.Slots.SequenceEqual(original.Charms.Slots));
        var deck=new ElementDeck(20);for(int n=0;n<30;n++) {var peek=deck.Peek();var bag=Enumerable.Range(0,4).Select(_=>deck.Next()).ToArray();True(bag[0]==peek && bag.Distinct().Count()==4);}
        var flexible=Equipment(CharmId.C13,CharmId.C14,CharmId.C17).Equipment;
        var round=JsonConvert.DeserializeObject<CharmEquipment>(JsonConvert.SerializeObject(flexible));round.Normalize();True(round.Slots.SequenceEqual(flexible.Slots) && round.Amplification==CharmId.C17);
        True(CharmCatalog.All.All(c=>!c.Description.Any(char.IsDigit)));
    }
    internal static void FourPhase()
    {
        var c=Equipment(CharmId.C06);var s=new TargetState();
        for(int n=1;n<=3;n++) {var r=Hit(s,c,(Element)n,cast:n,time:n,aura:false);s=r.NextState;c=r.NextCharms;}
        Equal(13,c.FourUntil);True(c.Bonuses.Sum()==0);
        var first=Hit(s,c,Element.Pyro,cast:5,time:5,aura:false);Equal(10,first.Damage);True(c.Bonuses.Sum()==0);c=first.NextCharms;
        var second=Hit(first.NextState,c,Element.Cryo,cast:6,time:6,aura:false);Equal(10.5,second.PreciseDamage);Equal(2,second.NextCharms.Bonuses[0]);
        var ended=Hit(second.NextState,second.NextCharms,Element.Pyro,cast:7,time:15,aura:false);Equal(10,ended.Damage);Equal(0,ended.NextCharms.Bonuses.Sum());
        var separate=Equipment(CharmId.C06);foreach(var el in new[]{Element.Pyro,Element.Hydro,Element.Cryo,Element.Electro}) separate=Hit(new TargetState(),separate,el,aura:false).NextCharms;
        Equal(0,separate.FourUntil);
    }
    internal static void SpellCircuit()
    {
        var c=Equipment(CharmId.C31);var s=new TargetState();long cast=0;
        foreach(var source in new[]{AttackSource.WhiteWave,AttackSource.BlackDiveShock,AttackSource.BlackShriek}) {var r=Hit(s,c,Element.Pyro,source,++cast,cast);s=r.NextState;c=r.NextCharms;}
        Equal(3,c.SpellCharges);s=Aura(Element.Pyro,100);
        for(int n=0;n<4;n++) {var r=Hit(s,c,Element.Hydro,AttackSource.BlackShriek,90,4+n*.1);Equal(40,r.Damage);True(r.Empowered);Equal(100,r.NextState.AuraUnits);s=r.NextState;c=r.NextCharms;}
        Equal(2,c.SpellCharges);
        var clear=Hit(Aura(Element.Pyro,100),Equipment(CharmId.C31,CharmId.C09),Element.Hydro);
        // A separate fixture with armed charges verifies clear-on-reaction wins.
        c=Equipment(CharmId.C31,CharmId.C09);c.SpellCharges=3;clear=Hit(Aura(Element.Pyro,100),c,Element.Hydro);
        Equal(45,clear.Damage);True(clear.NextState.Aura==Element.None);Equal(2,clear.NextCharms.SpellCharges);
    }
    internal static void Amplification()
    {
        var r=Hit(new TargetState(),Equipment(CharmId.C07),Element.Pyro);Equal(2,r.NextState.AuraUnits);Equal(12,r.NextState.AuraDuration);
        var s=Aura(Element.Pyro);s.Charms.LastKind=0;r=Hit(s,Equipment(CharmId.C05),Element.Hydro);Equal(.5,r.NextState.AuraUnits);True(r.NextState.Aura==Element.Hydro);
        s=Aura(Element.Pyro);s.Charms.LastKind=1;r=Hit(s,Equipment(CharmId.C05),Element.Hydro);True(r.NextState.Aura==Element.None);
        s=Aura(Element.Cryo);s.Charms.LastKind=0;r=Hit(s,Equipment(CharmId.C07,CharmId.C05),Element.Pyro);Equal(.5,r.NextState.AuraUnits);Equal(12,r.NextState.AuraDuration);
        r=Hit(Aura(Element.Hydro,2),Equipment(CharmId.C08),Element.Pyro);Equal(1.75,r.NextState.AuraUnits);Equal(25,r.Damage);
        r=Hit(Aura(Element.Hydro,20),Equipment(CharmId.C09),Element.Pyro);Equal(30,r.Damage);True(r.NextState.Aura==Element.None);
        s=new TargetState();s.SetIntrinsicAura(Element.Pyro);r=Hit(s,Equipment(CharmId.C09),Element.Hydro);Equal(45,r.Damage);True(r.NextState.Aura==Element.Pyro && r.NextState.IntrinsicAura==Element.Pyro);
    }
    internal static void SpecialFire()
    {
        var c=Equipment(CharmId.C12,CharmId.C07);var r=Hit(Aura(Element.Cryo,2),c,Element.Pyro,AttackSource.GreatSlash);Equal(1,r.NextState.Charms.FireUnits);Equal(8,r.NextState.Charms.FireDuration);
        var s=Aura(Element.Cryo,3);s.Charms.FireUnits=1;s.Charms.FireUntil=20;s.Charms.FireDuration=8;
        s.FrozenUntil=4;s.FrozenDuration=3;s.FrozenSpeed=.8;
        r=Hit(s,Equipment(CharmId.C12),Element.Hydro);True(r.UsedSpecialFire);Equal(4,r.NextState.FrozenUntil);Equal(40,r.Damage);Equal(.5,r.NextState.Charms.FireUnits);Equal(3,r.NextState.AuraUnits);True(r.NextState.Aura==Element.Cryo);
        s=new TargetState();s.Charms.FireUnits=1;s.Charms.FireUntil=20;
        r=Hit(s,Equipment(CharmId.C12),Element.Cryo,AttackSource.GreatSlash);Equal(40,r.Damage);Equal(0,r.NextState.Charms.FireUnits);True(r.UsedSpecialFire);
    }
    internal static void Overload()
    {
        var r=Hit(Aura(Element.Pyro),Equipment(CharmId.C13,CharmId.C14),Element.Electro);var blast=r.Secondary.Single();
        Equal(40,blast.Damage);Equal(6,blast.Radius);Equal(.35,blast.Delay);True(blast.Sparks && !blast.HeavyRecoil);
    }
    internal static void Electro()
    {
        var c=Equipment(CharmId.C17,CharmId.C19);var s=new TargetState {ElectroCharged=new PeriodicState {Damage=9,OriginalDamage=9,RemainingTicks=6,TotalTicks=6,Interval=1,NextTick=2}};
        var r=Hit(s,c,Element.Hydro,AttackSource.Cyclone,aura:false);Equal(54,r.Secondary.Single().Damage);Equal(7,r.NextState.ElectroCharged.Damage);Equal(6,r.NextState.ElectroCharged.RemainingTicks);
        for(int n=0;n<8;n++) {r=Hit(r.NextState,r.NextCharms,Element.None,AttackSource.Cyclone,2+n,1+n*.01,false);}
        Equal(3,r.NextState.ElectroCharged.Damage);Equal(18,r.Secondary.Single().Damage);
        s=r.NextState;s.Aura=Element.Hydro;s.AuraUnits=1;s.AuraUntil=100;r=Hit(s,c,Element.Electro,cast:30);Equal(9,r.NextState.ElectroCharged.Damage);
        s=Aura(Element.Hydro);s.ElectroCharged=new PeriodicState {Damage=4,RemainingTicks=2,TotalTicks=6,Interval=1,NextTick=3};
        r=Hit(s,c,Element.Hydro);Equal(3,r.NextState.ElectroCharged.RemainingTicks);Equal(4,r.NextState.ElectroCharged.Damage);
        c=Equipment(CharmId.C19,CharmId.C35);r=Hit(Aura(Element.Hydro),c,Element.Electro);Equal(1,r.NextState.ElectroCharged.RemainingTicks);
        for(int n=0;n<8;n++) {r=Hit(r.NextState,r.NextCharms,Element.Hydro,cast:100+n,time:2+n);}
        Equal(4,r.NextState.ElectroCharged.RemainingTicks);
    }
    internal static void Superconduct()
    {
        var c=Equipment(CharmId.C03,CharmId.C21);c.InsulatedUntil=10;
        var s=Aura(Element.Hydro);s.SuperconductStacks=3;s.SuperconductUntil=8;s.SuperconductDuration=3;s.SuperconductDamagePerLayer=10;s.SuperconductNeutralMultiplier=1.5;
        var r=Hit(s,c,Element.Pyro,AttackSource.GreatSlash);True(r.ResolvedElement==Element.None);Equal(30,r.Secondary.Single().Damage);True(r.Secondary.Single().Soul);True(r.Reaction==Reaction.None && r.NextState.Aura==Element.Hydro);
        var trigger=Hit(Aura(Element.Cryo),c,Element.Electro);Equal(13,trigger.NextCharms.InsulatedUntil);
    }
    internal static void FrostAndShatter()
    {
        var c=Equipment(CharmId.C27,CharmId.C28,CharmId.C40);
        var r=Hit(Aura(Element.Hydro),c,Element.Cryo);Equal(.7,r.NextState.FrozenSpeed);Equal(3,r.NextState.FrozenDuration);
        r=Hit(r.NextState,r.NextCharms,Element.None,AttackSource.GreatSlash,2,1.1);Equal(2,r.NextState.Charms.FrostCharges);Equal(1,r.NextCharms.Masks);
        r=Hit(r.NextState,r.NextCharms,Element.Hydro,AttackSource.WhiteWave,3,1.2);True(r.Reaction==Reaction.Freeze);Equal(1,r.NextState.Charms.FrostCharges);
        r=Hit(r.NextState,r.NextCharms,Element.Hydro,AttackSource.Cyclone,4,1.3);True(r.PreludeReactions.Contains(Reaction.Shatter));True(r.Reaction==Reaction.Freeze);True(r.NextState.FrozenUntil>1.3);True(r.NextState.Charms.FragileUntil>1.3);Equal(1,r.NextCharms.Masks);Equal(2,r.NextState.Charms.FrostCharges);
        r.NextCharms.Reaction(Reaction.Shatter,2);Equal(1,r.NextCharms.Masks);
        var hit=new AttackContext {Element=Element.Cryo,Source=AttackSource.WhiteWave,CastId=5,Damage=10,BaseNailDamage=20,TargetCanRecoil=true};
        r=new EnemyRules(Config()).Prepare(Aura(Element.Hydro),hit,1,null,c);Equal(3,r.NextState.FrozenDuration);Equal(2.5,r.NextState.HardFrozenUntil);
    }
    internal static void PlayerProtection()
    {
        var rules=new PlayerRules(Config());var c=Equipment(CharmId.C36);var s=new PlayerState {OverloadUntil=10};
        for(int n=0;n<4;n++) {var r=rules.Prepare(s,2,Element.None,1,n,c);Equal(n%2==0?4:2,r.Damage);s=r.NextState;c=r.NextCharms;}
        c.SkipOverload=true;s.Aura=Element.Pyro;s.AuraUnits=1;s.AuraUntil=20;Equal(3,rules.Prepare(s,2,Element.Hydro,1,5,c).Damage);
        c=Equipment(CharmId.C39);c.Reaction(Reaction.Freeze,0);c.Reaction(Reaction.Superconduct,5);Equal(13,c.VeilUntil);
        c.Reaction(Reaction.Overload,7);Equal(13,c.VeilUntil);Equal(-1000,c.LastReaction);
        s=new PlayerState {Aura=Element.Electro,AuraUntil=20,AuraUnits=1};var result=rules.Prepare(s,1,Element.Pyro,1,8,c);Equal(1,result.Damage);Equal(14,result.NextState.OverloadUntil);
        result=rules.Prepare(result.NextState,1,Element.None,1,13,result.NextCharms);Equal(2,result.Damage);
    }
    internal static void FrostMigration()
    {
        var document=JObject.Parse(SettingsStore.Serialize(Config()));document.Remove("CharmRevision");document["Reactions"]["FrostSpeed"]=.9;
        var settings=new SettingsStore();string message;True(settings.TryApply(document.ToString(),out message));Equal(.8,settings.Snapshot().Reactions.FrostSpeed);
        document["Reactions"]["FrostSpeed"]=.65;True(settings.TryApply(document.ToString(),out message));Equal(.65,settings.Snapshot().Reactions.FrostSpeed);
    }
    internal static void NewAttachmentPolicies()
    {
        var c=Equipment(CharmId.C01);var s=new TargetState();
        foreach(var step in new[]{(0d,true),(.2,false),(.4,true),(.6,false),(2.01,true)})
        {var r=Hit(s,c,Element.Hydro,AttackSource.Nail,(long)(step.Item1*100)+1,step.Item1);True(r.AppliedAura==step.Item2);s=r.NextState;}
        s=Aura(Element.Pyro,2);s.Charms.LastKind=0;var exact=Hit(s,Equipment(CharmId.C05),Element.Hydro);Equal(.5,exact.NextState.AuraUnits);True(exact.NextState.Aura==Element.Hydro);
        s=Aura(Element.Pyro,3);s.Charms.LastKind=0;var partial=Hit(s,Equipment(CharmId.C05),Element.Hydro);Equal(1,partial.NextState.AuraUnits);True(partial.NextState.Aura==Element.Pyro);
        s=Aura(Element.Cryo,10);c=Equipment(CharmId.C12,CharmId.C07);
        for(int n=0;n<4;n++) {var r=Hit(s,c,Element.Pyro,AttackSource.Cyclone,99,n*.1);True(r.AppliedAura==(n%2==0));if(n%2==0) True(Reactions.Amplifying(r.Reaction));Equal(1,r.NextState.Charms.FireUnits);Equal(8,r.NextState.Charms.FireDuration);s=r.NextState;c=r.NextCharms;}
    }
    internal static void FrostTailAndFragility()
    {
        var c=Equipment(CharmId.C27);var r=new EnemyRules(Config()).Prepare(Aura(Element.Hydro),new AttackContext {Source=AttackSource.WhiteWave,CastId=1,Element=Element.Cryo,Damage=10,BaseNailDamage=20,TargetCanRecoil=true},0,null,c);
        var s=r.NextState;True(s.HardFrozen);Equal(3,s.FrozenUntil);s.Expire(1.5);True(!s.HardFrozen);Equal(.7,s.FrozenSpeed);Equal(3,s.FrozenUntil);
        r=Hit(s,c,Element.Pyro,cast:2,time:2);Equal(7,r.NextState.Charms.FragileUntil);Equal(0,r.NextState.FrozenUntil);
        var next=Hit(r.NextState,r.NextCharms,Element.None,time:2.1);Equal(15,next.Damage);
        next=Hit(next.NextState,next.NextCharms,Element.None,time:7);Equal(10,next.Damage);
        r=Hit(Aura(Element.Hydro),c,Element.Cryo);r=Hit(r.NextState,r.NextCharms,Element.None,AttackSource.GreatSlash,2,1.1);True(r.Reaction==Reaction.Shatter);Equal(6.1,r.NextState.Charms.FragileUntil);
        s=new TargetState {FrozenUntil=3,HardFrozenUntil=1,HardFrozen=true,FrostAfterSpeed=.8,FrozenSpeed=0};s.Expire(3);True(!s.HardFrozen);Equal(0,s.FrozenUntil);Equal(1,s.FrozenSpeed);
    }
    internal static void SuperconductCombinations()
    {
        var c=Equipment(CharmId.C03,CharmId.C21);c.InsulatedUntil=20;
        var s=new TargetState {SuperconductStacks=4,SuperconductUntil=10,SuperconductDuration=6,SuperconductDamagePerLayer=10,SuperconductNeutralMultiplier=1.5};
        var a=Hit(s,c,Element.Pyro,cast:1,time:1,aura:false);Equal(3,a.Secondary.Single().Damage);Equal(4,a.NextState.SuperconductStacks);True(!a.Secondary.Single().Soul);
        var b=Hit(a.NextState,a.NextCharms,Element.Hydro,cast:2,time:1.3,aura:false);Equal(3,b.Secondary.Single().Damage);Equal(3,b.NextState.SuperconductStacks);True(b.Secondary.Single().Soul);Equal(11.3,b.NextCharms.ScarUntil);
        var doubled=Hit(b.NextState,b.NextCharms,Element.Electro,cast:3,time:1.6,aura:false);Equal(7,doubled.Secondary.Single().Damage);
        var neutral=Hit(doubled.NextState,doubled.NextCharms,Element.Cryo,AttackSource.GreatSlash,4,1.9,false);Equal(75,neutral.Secondary.Single().Damage);True(neutral.ResolvedElement==Element.None);
        var ctx=new AttackContext {Source=AttackSource.GreatSlash,CastId=22,Element=Element.Pyro,Damage=10,BaseNailDamage=20,AnySuperconductTarget=false};
        var clear=new EnemyRules(Config()).Prepare(new TargetState(),ctx,2,null,c);Equal(0,clear.NextCharms.InsulatedUntil);True(clear.ResolvedElement==Element.Pyro);
    }
    internal static void RevisedPlayerProtection()
    {
        var masks=Equipment(CharmId.C40);masks.Masks=2;Equal(1,masks.SacrificeMasks(2));Equal(0,masks.Masks);masks.Masks=2;Equal(0,masks.SacrificeMasks(1));Equal(0,masks.Masks);
        var rules=new PlayerRules(Config());var c=Equipment(CharmId.C35);
        var p=new PlayerState {Aura=Element.Electro,AuraUnits=1,AuraUntil=20};var r=rules.Prepare(p,2,Element.Pyro,1,0,c);Equal(3,r.Damage);Equal(6,r.NextState.OverloadUntil);
        r=rules.Prepare(r.NextState,2,Element.None,1,1,r.NextCharms);Equal(2,r.Damage);
        p=new PlayerState {Aura=Element.Hydro,AuraUnits=1,AuraUntil=20};r=rules.Prepare(p,1,Element.Electro,1,0,c);Equal(1,r.NextState.ElectroCharged.RemainingTicks);
        int pulses=0;rules.Tick(r.NextState,4,()=>{pulses++;return true;});rules.Tick(r.NextState,8,()=>{pulses++;return true;});Equal(1,pulses);
        p=new PlayerState {Aura=Element.Pyro,AuraUnits=1,AuraUntil=20};r=rules.Prepare(p,2,Element.Hydro,1,0,Equipment(CharmId.C36));Equal(3,r.Damage);True(r.Reaction==Reaction.VaporizeForward);
    }
    internal static void RevisedWindowsAndMigration()
    {
        var c=Equipment(CharmId.C06);var s=new TargetState();
        foreach(var t in new[]{(0d,Element.Pyro),(9d,Element.Hydro),(11d,Element.Cryo)}) {var r=Hit(s,c,t.Item2,cast:(long)t.Item1+1,time:t.Item1,aura:false);s=r.NextState;c=r.NextCharms;}Equal(0,c.FourUntil);
        c=Equipment(CharmId.C31);s=new TargetState();
        foreach(var t in new[]{(0d,AttackSource.WhiteWave),(9d,AttackSource.BlackDiveBody),(19d,AttackSource.BlackShriek)}) {var r=Hit(s,c,Element.Pyro,t.Item2,(long)t.Item1+1,t.Item1);s=r.NextState;c=r.NextCharms;}Equal(3,c.SpellCharges);
        var doc=JObject.Parse(SettingsStore.Serialize(Config()));doc["CharmRevision"]=1;doc["Reactions"]["SuperconductSeconds"]=3;doc["Reactions"]["SuperconductLayers"]=3;
        var settings=new SettingsStore();string message;True(settings.TryApply(doc.ToString(),out message));Equal(6,settings.Snapshot().Reactions.SuperconductSeconds);Equal(4,settings.Snapshot().Reactions.SuperconductLayers);
        doc["Reactions"]["SuperconductSeconds"]=9;doc["Reactions"]["SuperconductLayers"]=7;True(settings.TryApply(doc.ToString(),out message));Equal(9,settings.Snapshot().Reactions.SuperconductSeconds);Equal(7,settings.Snapshot().Reactions.SuperconductLayers);
    }

}

internal static class MaskBalanceTests
{
    internal static void Cooldown()
    {
        var c=new CharmState();c.Equipment.Toggle(CharmId.C40);
        c.Reaction(Reaction.Shatter,0);Check(c.Masks==1 && c.MaskReadyAt==5);
        c.Reaction(Reaction.Shatter,1);Check(c.Masks==1 && c.MaskReadyAt==5);
        c.Masks=0;c.Reaction(Reaction.Shatter,4.999);Check(c.Masks==0);
        c.Reaction(Reaction.Shatter,5);Check(c.Masks==1 && c.MaskReadyAt==10);
        c.Reaction(Reaction.Shatter,11);Check(c.Masks==1 && c.MaskReadyAt==10);
        var copy=c.Copy();copy.Masks=0;copy.Reaction(Reaction.Shatter,11);Check(copy.Masks==1 && copy.MaskReadyAt==16);
        copy.Masks=0;copy.Reaction(Reaction.Shatter,11);Check(copy.Masks==0);
        copy.Equipment.Toggle(CharmId.C40);copy.Reaction(Reaction.Shatter,20);Check(copy.Masks==0);
    }
    private static void Check(bool value) {if(!value)throw new Exception("碎晶护壳上限与生成间隔失败");}
}
