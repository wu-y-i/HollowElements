using System;
using HollowElements.Rules;
using HollowElements.Settings;
internal static class BossHintFreezeTests
{
    private static void Check(bool yes) {if(!yes)throw new Exception("冻结衰减或提示配置检查失败");}
    private static void Near(double x,double y) {Check(Math.Abs(x-y)<1e-7);}
    internal static void FreezeDecay()
    {
        var config=RuleConfig.Defaults();var engine=new EnemyRules(config);
        var state=new TargetState {Aura=Element.Hydro,AuraUnits=1,AuraUntil=100};
        var hit=new AttackContext {Damage=10,BaseNailDamage=20,CastId=1,Source=AttackSource.WhiteWave,Element=Element.Cryo,TargetCanRecoil=true};
        state=engine.Prepare(state,hit,0).NextState;Near(state.HardFrozenUntil,1);Near(state.FrozenUntil,3);
        state.Expire(.5);Near(state.FreezeDecayRate,1.125);
        state.Aura=Element.Hydro;state.AuraUnits=1;state.AuraUntil=100;hit.Element=Element.Cryo;hit.CastId=2;state=engine.Prepare(state,hit,.5).NextState;
        Near(state.FreezeDecayRate,1.125);Check(state.HardFrozenUntil>1 && state.HardFrozenUntil<1.5);Near(state.FrozenUntil-state.HardFrozenUntil,2);
        double hardEnd=state.HardFrozenUntil;state.Expire(hardEnd);Check(!state.HardFrozen);double rate=state.FreezeDecayRate;
        var normal=state.Copy();normal.EndFrost();state.Expire(hardEnd+1);normal.Expire(hardEnd+1);Near(state.FreezeDecayRate,rate-.05);Check(normal.FreezeDecayRate<state.FreezeDecayRate);
        state.Aura=Element.Hydro;state.AuraUnits=1;state.AuraUntil=100;hit.CastId=3;state=engine.Prepare(state,hit,hardEnd+1).NextState;
        Check(state.HardFrozen && state.HardFrozenUntil-(hardEnd+1)<1);Near(state.FrozenUntil-state.HardFrozenUntil,2);
        var copy=state.Copy();Near(copy.FreezeDecayRate,state.FreezeDecayRate);copy.Expire(30);Near(copy.FreezeDecayRate,1);
        hit.CastId=4;hit.TargetCanRecoil=false;hit.Element=Element.Cryo;state=new TargetState {Aura=Element.Hydro,AuraUnits=1,AuraUntil=100};
        state=engine.Prepare(state,hit,0).NextState;Check(!state.HardFrozen);Near(state.FrozenUntil,3);state.Expire(1);Near(state.FreezeDecayRate,1);
    }
    internal static void HintConfig()
    {
        var c=new BossHintSettings();c.Entries.Add(new BossHintEntry {Id="boss-03",Name="假骑士",Text="切换元素",Enabled=true,Scenes=new[]{"GG_False_Knight"}});c.Validate();Check(c.ForScene("GG_False_Knight")!=null && c.ForScene("GG_Hornet_1")==null);
        c.Entries[0].Enabled=false;Check(c.ForScene("GG_False_Knight")==null);c.Entries[0].Enabled=true;c.Entries[0].Text="";bool failed=false;try{c.Validate();}catch(ArgumentException){failed=true;}Check(failed);
        foreach(var charm in CharmCatalog.All) Check(!string.IsNullOrWhiteSpace(CharmExplanation.For(charm.Id)));
    }
}
