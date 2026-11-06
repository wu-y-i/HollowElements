using System;
using System.Linq;
using HollowElements.Rules;
using HollowElements.Settings;
internal static class ContentRuleTests
{
    private static void Check(bool value) {if(!value)throw new Exception("文档新规则检查失败");}
    private static void Near(double a,double b) {Check(Math.Abs(a-b)<1e-7);}
    private static TargetState Aura(Element e) => new TargetState {Aura=e,AuraUnits=1,AuraUntil=100,AuraDuration=100};
    private static AttackContext Hit(Element e,AttackSource source=AttackSource.WhiteWave,long cast=1) => new AttackContext {Element=e,Source=source,CastId=cast,Damage=100,BaseNailDamage=100,TargetCanRecoil=true};
    internal static void Difficulty()
    {
        foreach(double direct in new[]{.35,.45,.5,.8,1})
        {
            var c=RuleConfig.Defaults();c.DirectDamageMultiplier=direct;var rules=new EnemyRules(c);double scale=direct==.35?.65:direct==.45?.85:1;
            var overload=rules.Prepare(Aura(Element.Pyro),Hit(Element.Electro),0);Check(overload.Secondary[0].Damage==Reactions.Damage(100*scale));Near(overload.PreciseDamage,100*direct);
            var ec=rules.Prepare(Aura(Element.Hydro),Hit(Element.Electro),0);Check(ec.NextState.ElectroCharged.Damage==Reactions.Damage(45*scale));
            var sc=rules.Prepare(Aura(Element.Cryo),Hit(Element.Electro),0);var paid=rules.Prepare(sc.NextState,Hit(Element.None,AttackSource.Nail,2),.3);Check(paid.Secondary[0].Damage==Reactions.Damage(75*scale));
            var ice=Aura(Element.Cryo);ice.FrozenUntil=3;var shatter=rules.Prepare(ice,Hit(Element.None,AttackSource.Nail),0);Check(shatter.Secondary[0].Damage==Reactions.Damage(150*scale));
            var amp=rules.Prepare(Aura(Element.Cryo),Hit(Element.Pyro),0);Near(amp.PreciseDamage,400*direct);
            var player=new PlayerRules(c).Prepare(new PlayerState {Aura=Element.Pyro,AuraUnits=1,AuraUntil=10},2,Element.Hydro,1,0);Check(player.Damage==4);
            var serialized=SettingsStore.Serialize(c);Check(!serialized.Contains("ReactionDamageMultiplier"));
        }
    }
    internal static void ShatterContinuation()
    {
        var rules=new EnemyRules(RuleConfig.Defaults());var ice=Aura(Element.Cryo);ice.FrozenUntil=10;
        var pyro=Hit(Element.Pyro,AttackSource.GreatSlash);var melt=rules.Prepare(ice,pyro,0);
        Check(melt.PreludeReactions.SequenceEqual(new[]{Reaction.Shatter}) && melt.Reaction==Reaction.MeltForward && melt.ConsumedFrost);Near(melt.PreciseDamage,320);Check(melt.Secondary.Count==1 && melt.NextState.Aura==Element.None);
        var blocked=ice.Copy();foreach(var p in melt.NextState.Budgets)blocked.Budgets.Add(p.Key,p.Value.Copy());
        var second=rules.Prepare(blocked,pyro,.1);Check(second.Reaction==Reaction.Shatter && !second.AppliedAura && second.NextState.Aura==Element.Cryo);Near(second.PreciseDamage,80);
        var neutral=rules.Prepare(ice,Hit(Element.None,AttackSource.Nail),0);Check(neutral.NextState.Aura==Element.Cryo && neutral.NextState.FrozenUntil==0);
        var hydro=rules.Prepare(ice,Hit(Element.Hydro,AttackSource.GreatSlash),0,null,new CharmState());Check(hydro.Reaction==Reaction.Freeze && hydro.NextState.HardFrozen);
    }
    internal static void FrostElements()
    {
        var rules=new EnemyRules(RuleConfig.Defaults());
        foreach(var incoming in new[]{Element.Pyro,Element.Electro})
        {
            var ice=Aura(Element.Hydro);ice.FrozenUntil=10;ice.HardFrozen=true;ice.HardFrozenUntil=1;
            var result=rules.Prepare(ice,Hit(incoming),0);Check(result.Reaction==(incoming==Element.Pyro?Reaction.MeltForward:Reaction.Superconduct) && result.NextState.FrozenUntil==0);
            var hit=Hit(incoming);hit.CanApplyAura=false;result=rules.Prepare(ice,hit,0);Check(result.Reaction==Reaction.None && result.NextState.FrozenUntil==10);
        }
        var fixedTarget=Hit(Element.Cryo);fixedTarget.TargetCanRecoil=false;var first=rules.Prepare(Aura(Element.Hydro),fixedTarget,0);first.NextState.Aura=Element.Hydro;first.NextState.AuraUnits=1;first.NextState.AuraUntil=100;var again=Hit(Element.Cryo,cast:2);again.TargetCanRecoil=false;var refreshed=rules.Prepare(first.NextState,again,1);Near(refreshed.NextState.FrozenUntil,4);Check(!refreshed.NextState.HardFrozen);
    }
    internal static void ElectroRefresh()
    {
        var rules=new EnemyRules(RuleConfig.Defaults());var target=Aura(Element.Hydro);target.ElectroCharged=new PeriodicState {Damage=1,OriginalDamage=9,Cashouts=5,NextTick=1.2,TotalTicks=6,RemainingTicks=2,Interval=1};
        var r=rules.Prepare(target,Hit(Element.Electro),1);var ec=r.NextState.ElectroCharged;Check(ec.Damage==45 && ec.OriginalDamage==45 && ec.Cashouts==0 && ec.RemainingTicks==6);Near(ec.NextTick,1.2);
        var tick=rules.Tick(r.NextState,1.2);Check(tick.Count==1 && tick[0].Damage==45 && r.NextState.SuperconductStacks==0);
    }
    internal static void UnifiedSuperconduct()
    {
        var c=RuleConfig.Defaults();c.Reactions.SuperconductSeconds=8;c.Reactions.PlayerSuperconductSeconds=4;
        var player=new PlayerRules(c).Prepare(new PlayerState {Aura=Element.Cryo,AuraUnits=1,AuraUntil=20},1,Element.Electro,1,2);Near(player.NextState.SuperconductUntil,10);Near(c.Reactions.PlayerSuperconductSeconds,8);
        var store=new SettingsStore();string message;Check(store.TryApply(SettingsStore.Serialize(c),out message));Near(store.Snapshot().Reactions.PlayerSuperconductSeconds,8);
    }
}
