using System;
using System.Linq;
using HollowElements.Rules;
internal static class NoIceResidueTests
{
    private static void Check(bool value) {if(!value)throw new Exception("冻结无残冰规则失败");}
    private static AttackContext Hit(Element e,long cast,AttackSource source=AttackSource.WhiteWave) => new AttackContext {Element=e,CastId=cast,Source=source,Damage=10,BaseNailDamage=20,TargetCanRecoil=true};
    private static TargetState Ice(EnemyRules rules) => rules.Prepare(new TargetState {Aura=Element.Hydro,AuraUnits=1,AuraUntil=100},Hit(Element.Cryo,1),0).NextState;
    internal static void WaterCannotExtend()
    {
        var rules=new EnemyRules(RuleConfig.Defaults());var s=Ice(rules);Check(s.Aura==Element.None && s.AuraUnits==0);double hard=s.HardFrozenUntil,end=s.FrozenUntil;
        for(int i=0;i<20;i++) {var r=rules.Prepare(s,Hit(Element.Hydro,2+i),i*.2+.1);Check(r.Reaction!=Reaction.Freeze);s=r.NextState;if(i*.2+.1<end)Check(s.FrozenUntil==end && s.HardFrozenUntil==hard);}
        Check(s.FrozenUntil==0 && s.Aura==Element.Hydro);
        var iceBottom=new TargetState {Aura=Element.Cryo,AuraUnits=4,AuraUntil=100};var r2=rules.Prepare(iceBottom,Hit(Element.Hydro,100),0);Check(r2.Reaction==Reaction.Freeze && r2.NextState.Aura==Element.None);
        var second=rules.Prepare(Ice(rules),Hit(Element.Hydro,101),.2);var fresh=rules.Prepare(second.NextState,Hit(Element.Cryo,102),.4);Check(fresh.Reaction==Reaction.Freeze && fresh.NextState.HardFrozenUntil>1 && fresh.NextState.Aura==Element.None);
    }
    internal static void VirtualIceIsNotOrdinaryAura()
    {
        var rules=new EnemyRules(RuleConfig.Defaults());
        foreach(var element in new[]{Element.Pyro,Element.Electro})
        {
            var s=Ice(rules);var c=new CharmState {SpellCharges=3};c.Equipment.General=CharmId.C31;
            var r=rules.Prepare(s,Hit(element,2),.2,null,c);Check(r.UsedFrostAura && r.Empowered && r.NextState.Aura==Element.None && r.NextState.FrozenUntil==0);
            var water=rules.Prepare(r.NextState,Hit(Element.Hydro,3),.3);Check(water.Reaction!=Reaction.Freeze && water.NextState.Aura==Element.Hydro);
        }
        var state=Ice(rules);state.Charms.LastKind=0;var prism=new CharmState();prism.Equipment.Amplification=CharmId.C05;
        var p=rules.Prepare(state,Hit(Element.Electro,4),.2,null,prism);Check(p.NextState.Aura==Element.None);
    }
    internal static void BreakHasNoFrostTail()
    {
        var rules=new EnemyRules(RuleConfig.Defaults());
        foreach(var e in new[]{Element.Pyro,Element.Electro,Element.None})
        {
            var r=rules.Prepare(Ice(rules),Hit(e,2),.2);Check(r.ConsumedFrost && r.NextState.FrozenUntil==0 && r.NextState.HardFrozenUntil==0);
            r.NextState.Expire(1.1);Check(!r.NextState.HardFrozen && r.NextState.FrozenUntil==0);
        }
        var sword=rules.Prepare(Ice(rules),Hit(Element.Pyro,5,AttackSource.GreatSlash),.2);Check(sword.PreludeReactions.Contains(Reaction.Shatter) && sword.Reaction==Reaction.None && sword.NextState.Aura==Element.Pyro && sword.NextState.FrozenUntil==0);
    }
}
