using System;
using System.Collections.Generic;
using System.IO;
using HollowElements.Rules;
using HollowElements.Settings;

internal static class Program
{
    // Existing numerical cases explicitly select the still-supported 55% reduction.
    private static RuleConfig CombatConfig() { var c=RuleConfig.Defaults();c.DirectDamageMultiplier=.45;return c; }
    private static int passed;
    private static int failed;
    private static void Main(string[] args)
    {
        Run("冻结不留冰，连续水不会续冻，新冰可以刷新",NoIceResidueTests.WaterCannotExtend);
        Run("虚拟冰不写回普通附着，蓄势与棱镜无残冰",NoIceResidueTests.VirtualIceIsNotOrdinaryAura);
        Run("反应碎冰清除全部控制，不接原结霜尾段",NoIceResidueTests.BreakHasNoFrostTail);
        Run("五档难度仅缩放四类附伤、玩家伤害不变", ContentRuleTests.Difficulty);
        Run("碎冰后元素续结算、保留底元素并遵守附着限制", ContentRuleTests.ShatterContinuation);
        Run("冻结视作冰反应与结霜刷新", ContentRuleTests.FrostElements);
        Run("重复感电重置伤害但保留下一跳", ContentRuleTests.ElectroRefresh);
        Run("玩家敌人超导使用同一时长", ContentRuleTests.UnifiedSuperconduct);
        Run("硬冻结衰减累积、慢恢复结霜与独立结霜时长", BossHintFreezeTests.FreezeDecay);
        Run("Boss 提示配置过滤与护符补充说明", BossHintFreezeTests.HintConfig);
        Run("碎晶护壳一层上限与五秒生成间隔", MaskBalanceTests.Cooldown);
        Run("新默认20%减伤和旋风旧参数迁移", NewDefaults);
        Run("反应矩阵：正逆与六组组合", ReactionMatrix);
        Run("普攻第1、4、7次；计数不重置计时", NailIcd);
        Run("不同目标独立；被拒命中不提交状态", Transactional);
        Run("同元素取最大并刷新；反应后不自动再附着", AuraRules);
        Run("正逆增幅及消耗量、整数取整", Amplification);
        Run("法术同次施放和砸地子段共用附着预算", CastAttachment);
        Run("旋风隔刀附着0.5U，每次施放从第一刀重新计数", CycloneAttachment);
        Run("超导来源系数、次数与伤害上限只影响额外伤害", SuperconductBudgets);
        Run("附着冷却不把元素伤害转为无元素", ElementDuringIcd);
        Run("碎冰先于元素剑技融化，保留独立持续状态", ShatterPriority);
        Run("冻结分类及不靠同元素续控制", Freeze);
        Run("超载二次伤害无递归", Secondary);
        Run("敌人感电六跳、刷新不推迟下一跳", EnemyElectro);
        Run("玩家正逆映射与旧超载取最大", PlayerDamage);
        Run("新超载不放大当前伤害；旧状态有效受伤后不清除", PlayerOverload);
        Run("玩家感电4、8、12共三跳", PlayerElectro);
        Run("玩家感电无敌延后且不集中补扣", DeferredElectro);
        Run("玩家外部受伤清旧感电、同次可生成新感电", ExternalHurt);
        Run("环境1U附着零伤害、不清旧感电，反应与刷新独立结算", EnvironmentalAttachment);
        Run("参数校验及坏JSON保留上次有效快照", SettingsValidation);
        Run("JSON保存重载与快照隔离", SettingsRoundTrip);
        Run("运行时修正不污染基础参数", RuntimeModifiers);
        Run("白黑法术及子段配置对象独立", IndependentProfiles);
        Run("玩家来源ICD可独立热配置，怪物有效命中不使用该ICD", IndependentAttachmentPolicies);
        Run("局部时钟90%推进、完全冻结及恢复", LocalClockSteps);
        Run("结霜修正和玩家冻结参数保留创建快照", ControlSnapshots);
        Run("固有无限附着持续反应、不被消耗或碎冰清除，换色不触发反应", IntrinsicAura);
        Run("禁止附着保留元素伤害且不占用ICD", BlockedAura);
        Run("普通自附着可衰减消耗反应，零伤害增幅不制造伤害或占ICD", SelfAttachment);
        Run("专属超导增伤可跳过通用物伤追加且不占通用预算", SuperconductOverride);
        Run("黑吼按每目标施法第1/3有效命中附着", ShriekApplications);
        Run("四层消费续时、补满不重置预算、到期清除", SuperconductLifecycle);
        Run("感电成长与困难档反应附伤削减", ReactionScaling);
        Run("旧参数迁移、备份与玩家参数保留", MigrateBalance);
        Run("守望者8秒迁移保留其他自定义参数", MigrateWatcher);
        Run("图鉴外部文件、排版解析与失败回退", ElementGuideTests.Documents);
        Run("护符分类、装备限额、旧存档与抽袋",CharmTests.EquipmentAndDeck);
        Run("四相逐命中加算、过期与目标隔离",CharmTests.FourPhase);
        Run("三式蓄势整次施法及清底优先",CharmTests.SpellCircuit);
        Run("余烬、棱镜、蒸馏与尽相倍率",CharmTests.Amplification);
        Run("特殊火优先消耗、不覆盖普通附着、不自补充",CharmTests.SpecialFire);
        Run("聚爆与延时引信组合",CharmTests.Overload);
        Run("引雷最低一伤、续流与绝缘薄膜上限",CharmTests.Electro);
        Run("绝缘剑技转换、超导附伤与回魂标记",CharmTests.Superconduct);
        Run("缓霜、冰棱续接与护壳上限",CharmTests.FrostAndShatter);
        Run("超载交替、帷幕持续状态新增与到期恢复",CharmTests.PlayerProtection);
        Run("基础结霜迁移保留自定义速度",CharmTests.FrostMigration);
        Run("万象2秒2hit、棱镜耗尽补0.5U、旋风炉心逐刀",CharmTests.NewAttachmentPolicies);
        Run("冻结接结霜总时长、反应破冰脆弱及到期",CharmTests.FrostTailAndFragility);
        Run("绝缘两次元素兑现、刻痕乘算、无目标清蓄势",CharmTests.SuperconductCombinations);
        Run("薄膜超载当次加伤及单跳、熄流固定逆向",CharmTests.RevisedPlayerProtection);
        Run("回路10秒与三式20秒、超导旧值迁移保留自定义",CharmTests.RevisedWindowsAndMigration);
        if (args.Length == 2 && args[0] == "--write-defaults") File.WriteAllText(args[1], SettingsStore.Serialize(RuleConfig.Defaults()));
        Console.WriteLine($"{passed} passed, {failed} failed");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    private static void NewDefaults()
    {
        var c=RuleConfig.Defaults();Near(.8,c.DirectDamageMultiplier);True(new GlobalSettings().ShowHealthNumbers);True(new GlobalSettings().AllowAwayFromBench);
        var engine=new EnemyRules(c);Eq(16,engine.Prepare(new TargetState(),Hit(Element.None),0).Damage);
        Eq(64,engine.Prepare(Aura(Element.Pyro),Hit(Element.Hydro),0).Damage);
        var doc=Newtonsoft.Json.Linq.JObject.Parse(SettingsStore.Serialize(c));doc.Remove("AttackRevision");
        doc["DirectDamageMultiplier"]=.5;doc["Attacks"]["Cyclone"]["AuraUnits"]=1;
        doc["Attacks"]["Cyclone"]["IcdSeconds"]=2.5;doc["Attacks"]["Cyclone"]["HitStride"]=3;
        doc["Attacks"]["Cyclone"]["MaxApplicationsPerCast"]=2;
        var store=new SettingsStore();string message;True(store.TryApply(doc.ToString(),out message));
        var next=store.Snapshot();Near(.5,next.DirectDamageMultiplier);var p=next.Attacks[AttackSource.Cyclone];
        Near(.5,p.AuraUnits);Near(0,p.IcdSeconds);Eq(1,p.HitStride);Eq(int.MaxValue,p.MaxApplicationsPerCast);
        Eq(2,p.SuperconductHitsPerCast);Near(.5,p.SuperconductCoefficient);
    }
    private static void LocalClockSteps()
    {
        var clock = new LocalStepBudget();
        int accepted = 0;
        for (int i=0;i<100;i++) if (clock.Advance(.9)) accepted++;
        if (accepted != 90) throw new Exception("90%速度应推进90/100步，实际："+accepted);
        for (int i=0;i<100;i++) if (clock.Advance(0)) throw new Exception("冻结期间不应推进。");
        for (int i=0;i<100;i++) if (!clock.Advance(1)) throw new Exception("恢复后应每步推进。");
    }
    private static void MigrateWatcher()
    {
        var document=Newtonsoft.Json.Linq.JObject.Parse(SettingsStore.Serialize(CombatConfig()));
        document.Remove("BossRevision");document["Reactions"]["WatcherSuperconductSeconds"]=4;
        document["Reactions"]["PlayerAttackSpeedScale"]=.6;document["DirectDamageMultiplier"]=.8;
        var store=new SettingsStore();string message;True(store.TryApply(document.ToString(),out message));
        var config=store.Snapshot();Eq(8,config.Reactions.WatcherSuperconductSeconds);Eq(.6,config.Reactions.PlayerAttackSpeedScale);Eq(.8,config.DirectDamageMultiplier);
    }
    private static void SuperconductOverride()
    {
        var current=ScState();var attack=Hit(Element.None,31);attack.SkipSuperconduct=true;
        var skipped=Engine().Prepare(current,attack,0);Eq(9,skipped.Damage);Eq(0,skipped.Secondary.Count);Eq(3,skipped.NextState.SuperconductStacks);
        attack.SkipSuperconduct=false;var ordinary=Engine().Prepare(skipped.NextState,attack,.1);
        Eq(9,ordinary.Damage);Eq(15,ordinary.Secondary[0].Damage);Eq(2,ordinary.NextState.SuperconductStacks);
        Eq(0,Engine().Prepare(ordinary.NextState,attack,.4).Secondary.Count);
    }
    private static void IntrinsicAura()
    {
        var state=new TargetState(); state.SetIntrinsicAura(Element.Pyro);
        var engine=Engine();
        for(int i=0;i<4;i++)
        {
            var result=engine.Prepare(state,Hit(Element.Hydro,i+1,AttackSource.WhiteWave),i*3);
            Eq(Reaction.VaporizeForward,result.Reaction); Eq(36,result.Damage);
            Eq(Element.Pyro,result.NextState.Aura); state=result.NextState;
        }
        state.SetIntrinsicAura(Element.Hydro);
        Eq(Element.Hydro,state.Aura); True(state.ElectroCharged==null);
        var frozen=engine.Prepare(state,Hit(Element.Cryo,10,AttackSource.WhiteWave),20).NextState;
        True(frozen.FrozenUntil>20); Eq(Element.Hydro,frozen.Aura);
        var shatter=engine.Prepare(frozen,Hit(Element.None,11),20.1);
        Eq(Reaction.Shatter,shatter.Reaction); Eq(Element.Hydro,shatter.NextState.Aura); Eq(0d,shatter.NextState.FrozenUntil);
        state.Expire(1e9); Eq(Element.Hydro,state.Aura);
    }
    private static void BlockedAura()
    {
        var context=Hit(Element.Pyro); context.CanApplyAura=false;
        var result=Engine().Prepare(new TargetState(),context,0);
        Eq(9,result.Damage); Eq(Element.None,result.NextState.Aura); Eq(0,result.NextState.Icd.Count);
    }
    private static void SelfAttachment()
    {
        var engine=Engine();
        var attached=engine.Attach(new TargetState(),Element.Cryo,1,20,false,10);
        Eq(0,attached.Damage); Eq(Element.Cryo,attached.NextState.Aura); Eq(0,attached.NextState.Icd.Count); Eq(0,attached.NextState.Budgets.Count);
        True(attached.NextState.AuraUntil<100); attached.NextState.Expire(100); Eq(Element.None,attached.NextState.Aura);
        var pyro=engine.Attach(new TargetState(),Element.Pyro,1,20,false,0).NextState;
        var melt=engine.Attach(pyro,Element.Cryo,1,20,false,1);
        Eq(Reaction.MeltReverse,melt.Reaction); Eq(0,melt.Damage); Eq(.5,melt.NextState.AuraUnits); Eq(0,melt.Secondary.Count);
        var electro=engine.Attach(new TargetState(),Element.Electro,1,20,false,0).NextState;
        var sc=engine.Attach(electro,Element.Cryo,1,20,false,1);
        Eq(Reaction.Superconduct,sc.Reaction); True(sc.NextState.SuperconductUntil>1); Eq(Element.None,sc.NextState.Aura);
        var hydro=engine.Attach(new TargetState(),Element.Hydro,1,20,false,0).NextState;
        var frost=engine.Attach(hydro,Element.Cryo,1,20,false,1);
        Eq(Reaction.Freeze,frost.Reaction); True(!frost.NextState.HardFrozen); True(frost.NextState.FrozenUntil>1);
        var overload=engine.Attach(pyro,Element.Electro,1,20,false,1);
        Eq(Reaction.Overload,overload.Reaction); Eq(1,overload.Secondary.Count); True(overload.Secondary[0].Damage>0);
        Eq(Element.Pyro,pyro.Aura); Eq(1d,pyro.AuraUnits);
    }
    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
    }
    private static void Eq<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
    private static void Near(double expected, double actual) { if (Math.Abs(expected - actual) > 1e-7) throw new Exception($"expected {expected}, got {actual}"); }
    private static void True(bool value) { if (!value) throw new Exception("assertion failed"); }
    private static AttackContext Hit(Element element, long cast = 1, AttackSource source = AttackSource.Nail, int damage = 20)
    { return new AttackContext { Element = element, CastId = cast, Source = source, Damage = damage, BaseNailDamage = 20 }; }
    private static TargetState Aura(Element element, double units = 1) { return new TargetState { Aura = element, AuraUnits = units, AuraUntil = 100 }; }
    private static EnemyRules Engine() { return new EnemyRules(CombatConfig()); }
    private static void ReactionMatrix()
    {
        Eq(Reaction.VaporizeForward, Reactions.Identify(Element.Pyro, Element.Hydro));
        Eq(Reaction.VaporizeReverse, Reactions.Identify(Element.Hydro, Element.Pyro));
        Eq(Reaction.MeltForward, Reactions.Identify(Element.Cryo, Element.Pyro));
        Eq(Reaction.MeltReverse, Reactions.Identify(Element.Pyro, Element.Cryo));
        foreach (Element a in Enum.GetValues(typeof(Element))) foreach (Element b in Enum.GetValues(typeof(Element))) {
            var reaction = Reactions.Identify(a,b);
            if (a == b || a == Element.None || b == Element.None) Eq(Reaction.None,reaction); else True(reaction != Reaction.None);
        }
        Eq(Reaction.Freeze, Reactions.Identify(Element.Cryo,Element.Hydro));
        Eq(Reaction.Overload, Reactions.Identify(Element.Electro,Element.Pyro));
        Eq(Reaction.ElectroCharged, Reactions.Identify(Element.Electro,Element.Hydro));
        Eq(Reaction.Superconduct, Reactions.Identify(Element.Electro,Element.Cryo));
    }
    private static void NailIcd()
    {
        var state = new TargetState(); var rules = Engine();
        for (int i=0;i<7;i++) { var r=rules.Prepare(state,Hit(Element.Pyro,i+1),i*0.3); Eq(i%3==0,r.AppliedAura); state=r.NextState; }
        Near(0,state.Icd["Nail"].WindowStart);
        var timer = rules.Prepare(state,Hit(Element.Pyro,8),2.5); True(timer.AppliedAura); Eq(1,timer.NextState.Icd["Nail"].Hits);
        Near(2.5,timer.NextState.Icd["Nail"].WindowStart);
    }
    private static void Transactional()
    {
        var state=new TargetState(); var rules=Engine();
        var discarded=rules.Prepare(state,Hit(Element.Pyro),0); Eq(Element.None,state.Aura); Eq(0,state.Icd.Count);
        True(rules.Prepare(state,Hit(Element.Hydro,2),0.1).AppliedAura);
        var other=new TargetState(); True(rules.Prepare(other,Hit(Element.Pyro,2),0.1).AppliedAura);
        Eq(Element.Pyro,discarded.NextState.Aura);
    }
    private static void AuraRules()
    {
        var rules=Engine(); var state=Aura(Element.Hydro,2);
        var same=rules.Prepare(state,Hit(Element.Hydro),1); Near(2,same.NextState.AuraUnits); Near(9,same.NextState.AuraUntil);
        var depleted=rules.Prepare(Aura(Element.Pyro),Hit(Element.Hydro),1); Eq(Element.None,depleted.NextState.Aura);
        var expired=Aura(Element.Cryo); expired.Expire(100); Eq(Element.None,expired.Aura);
    }
    private static void Amplification()
    {
        var rules=Engine();var forward=rules.Prepare(Aura(Element.Pyro,3),Hit(Element.Hydro),0);Eq(36,forward.Damage);Near(1,forward.NextState.AuraUnits);
        var reverse=rules.Prepare(Aura(Element.Hydro),Hit(Element.Pyro,1,AttackSource.Nail,21),0);Eq(23,reverse.Damage);Near(.5,reverse.NextState.AuraUnits);
        var precise=rules.Prepare(Aura(Element.Cryo),Hit(Element.Pyro,2,AttackSource.WhiteWave,32),0);Near(57.6,precise.PreciseDamage);Eq(57,precise.Damage);
        Eq(14,rules.Prepare(new TargetState(),Hit(Element.None,3,AttackSource.WhiteWave,32),0).Damage);
        Eq(36,rules.Prepare(Aura(Element.Hydro),Hit(Element.Pyro,4,AttackSource.WhiteWave,32),0).Damage);
        Eq(1,rules.Prepare(new TargetState(),Hit(Element.None,5,AttackSource.Nail,1),0).Damage);
        precise.PreciseDamage*=.75;Eq(43,precise.Damage); // Boss adjustment must also precede rounding.
    }
    private static void CastAttachment()
    {
        var rules=Engine();var first=rules.Prepare(new TargetState(),Hit(Element.Cryo,7,AttackSource.WhiteDiveBody),0);
        True(first.AppliedAura);True(!rules.Prepare(first.NextState,Hit(Element.Cryo,7,AttackSource.WhiteDiveShock),.1).AppliedAura);
        first=rules.Prepare(new TargetState(),Hit(Element.Cryo,8,AttackSource.BlackDiveBody),0);
        var second=rules.Prepare(first.NextState,Hit(Element.Cryo,8,AttackSource.BlackDiveShock),.1);True(second.AppliedAura);
        True(!rules.Prepare(second.NextState,Hit(Element.Cryo,8,AttackSource.BlackDiveShock),.2).AppliedAura);
        var shockOnly=rules.Prepare(new TargetState(),Hit(Element.Cryo,9,AttackSource.BlackDiveShock),0);
        True(!rules.Prepare(shockOnly.NextState,Hit(Element.Cryo,9,AttackSource.BlackDiveShock),.1).AppliedAura);
    }
    private static void CycloneAttachment()
    {
        var rules=Engine(); var state=new TargetState(); int count=0;
        for(int i=0;i<10;i++) { var r=rules.Prepare(state,Hit(Element.Electro,3,AttackSource.Cyclone),i*0.1); if(r.AppliedAura)count++; state=r.NextState; }
        Eq(5,count);Near(.5,state.AuraUnits);
        for(int cast=10;cast<13;cast++) for(int i=0;i<5;i++) {
            var r=rules.Prepare(state,Hit(Element.Electro,cast,AttackSource.Cyclone),2+cast+i*.01);
            True(r.AppliedAura==(i%2==0));state=r.NextState;
        }
    }
    private static void SuperconductBudgets()
    {
        var config=CombatConfig();config.Attacks[AttackSource.BlackShriek].SuperconductDamageCap=2;
        var rules=new EnemyRules(config);var state=ScState();
        var first=rules.Prepare(state,Hit(Element.None,4,AttackSource.BlackShriek),0);Eq(9,first.Damage);Eq(2,first.Secondary[0].Damage);
        Eq(0,rules.Prepare(first.NextState,Hit(Element.None,4,AttackSource.BlackShriek),.3).Secondary.Count);
        var cyclone=rules.Prepare(state,Hit(Element.None,6,AttackSource.Cyclone),0);Eq(7,cyclone.Secondary[0].Damage);
        var duplicate=rules.Prepare(cyclone.NextState,Hit(Element.None,6,AttackSource.Cyclone),.01);Eq(0,duplicate.Secondary.Count);Eq(2,duplicate.NextState.SuperconductStacks);
        cyclone=rules.Prepare(duplicate.NextState,Hit(Element.None,6,AttackSource.Cyclone),.3);Eq(7,cyclone.Secondary[0].Damage);
        Eq(0,rules.Prepare(cyclone.NextState,Hit(Element.None,6,AttackSource.Cyclone),.6).Secondary.Count);
    }
    private static void ElementDuringIcd()
    {
        var rules=Engine();var a=rules.Prepare(ScState(),Hit(Element.Pyro),0);
        Eq(10,a.Secondary[0].Damage);var b=rules.Prepare(a.NextState,Hit(Element.Pyro,2),.1);
        True(!b.AppliedAura);Eq(9,b.Damage);Eq(0,b.Secondary.Count);
    }
    private static void ShatterPriority()
    {
        var state=ScState();state.Aura=Element.Cryo;state.AuraUnits=1;state.AuraUntil=10;state.FrozenUntil=10;
        state.ElectroCharged=new PeriodicState {NextTick=1,Interval=1,RemainingTicks=6,Damage=9};
        var result=Engine().Prepare(state,Hit(Element.Pyro,3,AttackSource.GreatSlash),0);
        Eq(Reaction.MeltForward,result.Reaction);True(result.PreludeReactions.Contains(Reaction.Shatter));Eq(36,result.Damage);Eq(25,result.Secondary[0].Damage);Eq(DamageOrigin.Superconduct,result.Secondary[1].Origin);
        Eq(Element.None,result.NextState.Aura);Near(0,result.NextState.FrozenUntil);True(result.AppliedAura);
        True(result.NextState.ElectroCharged!=null);Eq(2,result.NextState.SuperconductStacks);
    }
    private static void Freeze()
    {
        var hit=Hit(Element.Cryo); hit.TargetCanRecoil=true; var frozen=Engine().Prepare(Aura(Element.Hydro),hit,0);
        True(frozen.NextState.HardFrozen); Near(3,frozen.NextState.FrozenUntil);Near(1,frozen.NextState.HardFrozenUntil); Eq(Element.None,frozen.NextState.Aura);
        var refresh=Engine().Prepare(frozen.NextState,Hit(Element.Cryo,2,AttackSource.WhiteWave),0.5); Near(3,refresh.NextState.FrozenUntil);
        var frost=Engine().Prepare(Aura(Element.Hydro),Hit(Element.Cryo),0); True(!frost.NextState.HardFrozen); Near(3,frost.NextState.FrozenUntil);
    }
    private static void Secondary()
    {
        var rules=Engine(); var overload=rules.Prepare(Aura(Element.Pyro),Hit(Element.Electro),0);
        Eq(17,overload.Secondary[0].Damage); True(overload.Secondary[0].HeavyRecoil); Near(2,overload.Secondary[0].Radius);
        var hit=Hit(Element.Pyro); hit.Origin=DamageOrigin.Overload;
        var secondary=rules.Prepare(Aura(Element.Electro),hit,0); Eq(Reaction.None,secondary.Reaction); Eq(0,secondary.Secondary.Count); Eq(0,secondary.NextState.Icd.Count);
    }
    private static void EnemyElectro()
    {
        var rules=Engine(); var state=rules.Prepare(Aura(Element.Hydro),Hit(Element.Electro),0).NextState;
        int damage=0; for(int i=1;i<=6;i++) foreach(var pulse in rules.Tick(state,i)) damage+=pulse.Damage;
        Eq(42,damage); True(state.ElectroCharged==null);
        state=rules.Prepare(Aura(Element.Hydro),Hit(Element.Electro),0).NextState;
        state.Aura=Element.Hydro; state.AuraUnits=1; state.AuraUntil=10;
        state=rules.Prepare(state,Hit(Element.Electro,2,AttackSource.WhiteWave),0.8).NextState;
        Near(1,state.ElectroCharged.NextTick); Eq(1,rules.Tick(state,1).Count);
    }
    private static PlayerRules Players() { return new PlayerRules(CombatConfig()); }
    private static PlayerState PlayerAura(Element element) { return new PlayerState {Aura=element,AuraUnits=1,AuraUntil=10}; }
    private static void PlayerDamage()
    {
        var rules=Players(); Eq(2,rules.Prepare(PlayerAura(Element.Pyro),1,Element.Cryo,1,0).Damage);
        Eq(3,rules.Prepare(PlayerAura(Element.Pyro),2,Element.Cryo,1,0).Damage);
        Eq(4,rules.Prepare(PlayerAura(Element.Cryo),2,Element.Pyro,1,0).Damage);
        var state=PlayerAura(Element.Pyro); state.OverloadUntil=6; Eq(4,rules.Prepare(state,2,Element.Cryo,1,0).Damage);
    }
    private static void PlayerOverload()
    {
        var rules=Players(); var first=rules.Prepare(PlayerAura(Element.Pyro),1,Element.Electro,1,0); Eq(1,first.Damage); Near(6,first.NextState.OverloadUntil);
        var next=rules.Prepare(first.NextState,1,Element.None,1,1); Eq(2,next.Damage); Near(6,next.NextState.OverloadUntil);
        Eq(1,rules.Prepare(first.NextState,1,Element.None,1,6).Damage);
    }
    private static PlayerState Charged() { return Players().Prepare(PlayerAura(Element.Hydro),1,Element.Electro,1,0).NextState; }
    private static void PlayerElectro()
    {
        var rules=Players(); var state=Charged(); int count=0;
        for(int second=0;second<=12;second++) rules.Tick(state,second,()=>{ count++; return true; });
        Eq(3,count); True(state.ElectroCharged==null);
    }
    private static void DeferredElectro()
    {
        var rules=Players(); var state=Charged(); int count=0;
        True(!rules.Tick(state,4,()=>false)); True(state.ElectroCharged!=null);
        rules.Tick(state,13,()=>{count++;return true;}); Eq(1,count); Near(17,state.ElectroCharged.NextTick);
        rules.Tick(state,13,()=>{count++;return true;}); Eq(1,count);
        rules.Tick(state,17,()=>{count++;return true;}); rules.Tick(state,21,()=>{count++;return true;}); Eq(3,count);
    }
    private static void ExternalHurt()
    {
        var rules=Players(); var state=Charged(); True(rules.Prepare(state,1,Element.None,1,1).NextState.ElectroCharged==null);
        state.Aura=Element.Hydro; state.AuraUnits=1; state.AuraUntil=10;
        var next=rules.Prepare(state,1,Element.Electro,1,1).NextState; True(next.ElectroCharged!=null); Near(5,next.ElectroCharged.NextTick);
    }
    private static void SettingsValidation()
    {
        var store=new SettingsStore(); string message;
        True(!store.TryApply("{bad",out message)); Near(4,store.Snapshot().Reactions.ForwardMultiplier);
        var config=CombatConfig(); config.Reactions.PlayerElectroInterval=double.NaN;
        True(!store.TryApply(SettingsStore.Serialize(config),out message));
        config=CombatConfig(); config.Attacks.Remove(AttackSource.BlackShriek); True(!store.TryApply(SettingsStore.Serialize(config),out message));
        True(!store.TryApply("{\"surprise\":1}",out message));
        True(!store.TryApply("{\"SchemaVersion\":1}",out message));
    }
    private static void SettingsRoundTrip()
    {
        var store=new SettingsStore(); var config=store.Snapshot(); config.Reactions.ForwardMultiplier=3; string message;
        True(store.TryApply(SettingsStore.Serialize(config),out message)); config.Reactions.ForwardMultiplier=9; Near(3,store.Snapshot().Reactions.ForwardMultiplier);
        string path=Path.Combine(AppContext.BaseDirectory,"HollowElements-test-"+Guid.NewGuid()+".json");
        try {store.Save(path); store.Save(path); var loaded=new SettingsStore(); True(loaded.TryReload(path,out message)); Near(3,loaded.Snapshot().Reactions.ForwardMultiplier);}
        finally {if(File.Exists(path))File.Delete(path);}
    }
    private sealed class Charm : IRuleModifier {public void Modify(AttackContext hit,AttackProfile attack,ReactionProfile reaction){reaction.ForwardMultiplier=3;}}
    private sealed class FrostCharm : IRuleModifier { public void Modify(AttackContext hit,AttackProfile attack,ReactionProfile reaction) { reaction.FrostSpeed=.7; } }
    private static void ControlSnapshots()
    {
        var config=CombatConfig(); var engine=new EnemyRules(config);
        var frost=engine.Prepare(Aura(Element.Hydro),Hit(Element.Cryo),0,new FrostCharm()).NextState;
        Near(.7,frost.FrozenSpeed); Near(3,frost.FrozenDuration); Near(.7,frost.Copy().FrozenSpeed);
        var later=engine.Prepare(frost,Hit(Element.Cryo,2,AttackSource.WhiteWave),.1).NextState;
        Near(.7,later.FrozenSpeed); Near(3,later.FrozenUntil);
        var frozen=new PlayerRules(config).Prepare(PlayerAura(Element.Hydro),1,Element.Cryo,1,0).NextState;
        config.Reactions.PlayerMoveScale=.5;
        Near(.85,frozen.MoveScale); Near(.75,frozen.AttackSpeedScale); Near(3,frozen.FrozenDuration);
    }
    private static void RuntimeModifiers()
    {
        var config=CombatConfig();var rules=new EnemyRules(config);
        Eq(27,rules.Prepare(Aura(Element.Pyro),Hit(Element.Hydro),0,new Charm()).Damage);
        Eq(36,rules.Prepare(Aura(Element.Pyro),Hit(Element.Hydro),0).Damage);Near(4,config.Reactions.ForwardMultiplier);
    }
    private static void IndependentProfiles()
    {
        var config=CombatConfig(); config.Attacks[AttackSource.WhiteShriek].AuraUnits=2;
        Near(1,config.Attacks[AttackSource.BlackShriek].AuraUnits);
        config.Attacks[AttackSource.BlackDiveBody].SuperconductCoefficient=0.1;
        Near(0.75,config.Attacks[AttackSource.BlackDiveShock].SuperconductCoefficient);
    }
    private static void EnvironmentalAttachment()
    {
        var rules=new PlayerRules(CombatConfig());
        var first=rules.Attach(new PlayerState(),Element.Hydro,1,0);
        Eq(0,first.Damage); Eq(Element.Hydro,first.NextState.Aura); Near(1,first.NextState.AuraUnits);
        var refreshed=rules.Attach(first.NextState,Element.Hydro,1,1);
        Near(9,refreshed.NextState.AuraUntil); Near(1,refreshed.NextState.AuraUnits);
        var ec=rules.Attach(PlayerAura(Element.Electro),Element.Hydro,1,0);
        Eq(0,ec.Damage); Eq(Reaction.ElectroCharged,ec.Reaction); Eq(3,ec.NextState.ElectroCharged.RemainingTicks);
        var wet=rules.Attach(ec.NextState,Element.Hydro,1,1);
        Near(4,wet.NextState.ElectroCharged.NextTick); Eq(3,wet.NextState.ElectroCharged.RemainingTicks);
        var amp=rules.Attach(PlayerAura(Element.Pyro),Element.Hydro,1,0); Eq(0,amp.Damage); Eq(Reaction.VaporizeForward,amp.Reaction);
        var reverse=rules.Attach(PlayerAura(Element.Hydro),Element.Pyro,1,0); Eq(0,reverse.Damage); Eq(Reaction.VaporizeReverse,reverse.Reaction);
    }
    private static void IndependentAttachmentPolicies()
    {
        var config=CombatConfig();
        var nail=config.Attacks[AttackSource.Nail]; nail.IcdSeconds=20; nail.HitStride=3;
        var wave=config.Attacks[AttackSource.WhiteWave]; wave.IcdSeconds=10; wave.HitStride=2;
        var store=new SettingsStore(); string message;
        True(store.TryApply(SettingsStore.Serialize(config),out message));
        var snapshot=store.Snapshot(); var rules=new EnemyRules(snapshot);
        var first=rules.Prepare(new TargetState(),Hit(Element.Pyro,1),0); True(first.AppliedAura);
        var second=rules.Prepare(first.NextState,Hit(Element.Pyro,2),1); True(!second.AppliedAura);
        var independent=rules.Prepare(second.NextState,Hit(Element.Pyro,3,AttackSource.WhiteWave),1.1); True(independent.AppliedAura);
        True(!rules.Prepare(independent.NextState,Hit(Element.Pyro,4,AttackSource.WhiteWave),1.2).AppliedAura);
        True(rules.Prepare(independent.NextState,Hit(Element.Pyro,5,AttackSource.BlackWave),1.2).AppliedAura);
        // Accepted incoming hits are intentionally closer than all outgoing attack timers.
        var incomingRules=new PlayerRules(snapshot); var player=new PlayerState();
        for(int i=0;i<4;i++)
        {
            var accepted=incomingRules.Prepare(player,1,Element.Pyro,1,i*.01);
            Eq(Element.Pyro,accepted.NextState.Aura); Near(i*.01+config.Reactions.AuraSeconds,accepted.NextState.AuraUntil);
            player=accepted.NextState;
        }
        Eq(Reaction.VaporizeForward,incomingRules.Prepare(player,1,Element.Hydro,1,.04).Reaction);
        nail.IcdSeconds=0; nail.HitStride=1;
        True(store.TryApply(SettingsStore.Serialize(config),out message));
        True(new EnemyRules(store.Snapshot()).Prepare(second.NextState,Hit(Element.Pyro,6),1.3).AppliedAura);
        Near(20,snapshot.Attacks[AttackSource.Nail].IcdSeconds);
        var legacy=SettingsStore.Serialize(config).Replace("\"IcdGroup\": \"Nail\",", "\"IcdGroup\": null,");
        True(store.TryApply(legacy,out message)); Eq("Nail",store.Snapshot().Attacks[AttackSource.Nail].IcdGroup);
    }
    private static TargetState ScState() => new TargetState {SuperconductUntil=3,SuperconductDuration=3,SuperconductStacks=3,SuperconductDamagePerLayer=10,SuperconductNeutralMultiplier=1.5};
    private static void ShriekApplications()
    {
        foreach(var source in new[]{AttackSource.WhiteShriek,AttackSource.BlackShriek})
        {
            var state=new TargetState();var rules=Engine();
            for(int i=0;i<4;i++) {var r=rules.Prepare(state,Hit(Element.Pyro,11,source),i*.1);Eq(i==0 || source==AttackSource.BlackShriek && i==2,r.AppliedAura);state=r.NextState;}
            True(rules.Prepare(state,Hit(Element.Pyro,12,source),1).AppliedAura);
        }
    }
    private static void SuperconductLifecycle()
    {
        var rules=Engine();var created=rules.Prepare(Aura(Element.Cryo),Hit(Element.Electro),0);Eq(4,created.NextState.SuperconductStacks);Eq(0,created.Secondary.Count);
        var consumed=rules.Prepare(created.NextState,Hit(Element.Pyro,2,AttackSource.WhiteWave),5.5);Eq(6,consumed.Secondary[0].Damage);Near(11.5,consumed.NextState.SuperconductUntil);Eq(3,consumed.NextState.SuperconductStacks);
        Eq(4,created.NextState.SuperconductStacks);
        var current=consumed.NextState;current.Aura=Element.Cryo;current.AuraUnits=1;current.AuraUntil=20;
        var refresh=rules.Prepare(current,Hit(Element.Electro,3,AttackSource.WhiteWave),6);Eq(1,refresh.Secondary.Count);Eq(4,refresh.NextState.SuperconductStacks);Near(12,refresh.NextState.SuperconductUntil);
        var sameCast=rules.Prepare(refresh.NextState,Hit(Element.None,3,AttackSource.WhiteWave),6.3);Eq(0,sameCast.Secondary.Count);Eq(4,sameCast.NextState.SuperconductStacks);
        current=refresh.NextState;current.Expire(12);Eq(0,current.SuperconductStacks);Eq(0,rules.Prepare(current,Hit(Element.None),12).Secondary.Count);
    }
    private static void ReactionScaling()
    {
        var rules=Engine();var hit=Hit(Element.Electro);hit.BaseNailDamage=21;
        var ec=rules.Prepare(Aura(Element.Hydro),hit,0);Eq(8,ec.NextState.ElectroCharged.Damage);Eq(6,ec.NextState.ElectroCharged.RemainingTicks);
        Eq(17,rules.Prepare(Aura(Element.Pyro),hit,0).Secondary[0].Damage);
        var state=ScState();var r=rules.Prepare(state,Hit(Element.None,1,AttackSource.Nail,100),0);Eq(45,r.Damage);Eq(15,r.Secondary[0].Damage);
        Eq(0,rules.Prepare(r.NextState,Hit(Element.None,1,AttackSource.Nail,100),.3).Secondary.Count);
        var rejected=ScState();True(!ReferenceEquals(rejected,r.NextState));Eq(3,rejected.SuperconductStacks);
    }
    private static void MigrateBalance()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"migration-"+Guid.NewGuid()+".json");
        try
        {
            var legacy=Newtonsoft.Json.Linq.JObject.Parse(SettingsStore.Serialize(CombatConfig()));legacy["SchemaVersion"]=1;
            legacy["Reactions"]["SuperconductBonus"]=.25;legacy["Reactions"]["ForwardMultiplier"]=2;legacy["Reactions"]["PlayerFreezeSeconds"]=5;
            File.WriteAllText(path,legacy.ToString());string original=File.ReadAllText(path);var store=new SettingsStore();string message;
            True(store.TryReload(path,out message));Eq(original,File.ReadAllText(path+".v1.bak"));Eq(2,store.Snapshot().SchemaVersion);Near(4,store.Snapshot().Reactions.ForwardMultiplier);Near(5,store.Snapshot().Reactions.PlayerFreezeSeconds);
            True(store.TryReload(path,out message));Eq(original,File.ReadAllText(path+".v1.bak"));
        }
        finally {if(File.Exists(path))File.Delete(path);if(File.Exists(path+".v1.bak"))File.Delete(path+".v1.bak");}
    }

}
