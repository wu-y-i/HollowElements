import json
import time
from runtime_acceptance import command,RESULTS,SESSION,LOG


def status(): return command("status")
def wait(predicate,timeout=35):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        s=status()
        if predicate(s): return s
        time.sleep(.15)
    raise TimeoutError("lifecycle condition")
def game_wait(seconds):
    now=status()["time"]; return wait(lambda s:s["time"]>=now+seconds)
def target(s,name): return next(e for e in s["enemies"] if e["name"]==name)
def aura(name,element): command("enemyAura",target=name,element=element)
def hit(name,element,**extra): return command("hit",target=name,element=element,source=extra.pop("source","WhiteWave"),damage=extra.pop("damage",10),**extra)
def capture(name,delay=.1):
    time.sleep(delay+.4)
    (RESULTS/(name+".png")).write_bytes((SESSION/"reaction.png").read_bytes())
    time.sleep(.5)


if __name__=="__main__":
    tests=[]
    try:
        command("goto",scene="GG_Atrium",gate="door_dreamEnter")
        wait(lambda s:s["scene"]=="GG_Atrium"); time.sleep(2)
        command("ready"); command("position",target="hero",x=111,y=14.4); command("face",right=True)
        command("dummy",offset=4,recoilable=False); command("dummy",offset=5,recoilable=True)
        fixed="反应木桩·固定"; movable="反应木桩·可击退"
        for name,base,element in [("vaporize","Pyro","Hydro"),("melt","Cryo","Pyro"),("freeze","Hydro","Cryo"),("superconduct","Cryo","Electro")]:
            command("clear"); aura(fixed,base); r=hit(fixed,element,capture=True); capture(name)
            tests.append(dict(test=name,result=r))
        command("clear"); aura(fixed,"Pyro"); before=status()
        r=hit(fixed,"Electro",capture=True); capture("overload"); after=status()
        assert target(before,fixed)["hp"]-target(after,fixed)["hp"]==31
        assert target(before,movable)["hp"]-target(after,movable)["hp"]==21
        assert target(before,movable)["position"]!=target(after,movable)["position"]
        tests.append(dict(test="范围超载：主目标10+21，邻近目标21，实际击退",before=before,after=after))
        command("clear"); aura(fixed,"Hydro"); hit(fixed,"Cryo")
        shatter=hit(fixed,"None",source="GreatSlash",capture=True); capture("shatter")
        tests.append(dict(test="碎冰特效",result=shatter))
        command("clear"); aura(fixed,"Hydro"); charged=hit(fixed,"Electro",capture=True,delay=1.1); capture("electro-tick",1.1)
        tests.append(dict(test="感电实际跳伤电流",result=charged))
        command("clear"); aura(movable,"Hydro"); frozen=hit(movable,"Cryo")
        assert frozen["state"]["HardFrozen"]
        a=target(status(),movable); time.sleep(.25); b=target(status(),movable)
        assert a["position"]==b["position"] and b["clock"]["AcceptedPhysics"]==a["clock"]["AcceptedPhysics"]
        command("pool",target=movable); pooled=target(status(),movable)
        assert pooled["state"]["Aura"]==0 and pooled["state"]["FrozenUntil"]==0 and pooled["clock"]["scale"]==1
        tests.append(dict(test="真实硬冻结不推进位置/物理，池复用清理",before=a,during=b,pooled=pooled))
        command("clear"); aura(fixed,"Hydro"); hit(fixed,"Electro")
        command("nativePause"); paused=wait(lambda s:s["timeScale"]==0)
        time.sleep(1.5); paused_after=status()
        assert paused["time"]==paused_after["time"]
        assert target(paused,fixed)["hp"]==target(paused_after,fixed)["hp"]
        assert target(paused,fixed)["state"]["ElectroCharged"]["RemainingTicks"]==target(paused_after,fixed)["state"]["ElectroCharged"]["RemainingTicks"]
        command("nativePause"); resumed=wait(lambda s:s["timeScale"]==1)
        tests.append(dict(test="原版暂停冻结反应计时和跳伤",paused=paused,after=paused_after,resumed=resumed))
        command("clear"); command("invincible",value=False); command("blueHealth",red=1,blue=8)
        command("playerAura",element="Hydro"); assert command("hurt",element="Electro",damage=1)
        blue_start=status(); assert blue_start["hero"]["health"]==1 and blue_start["hero"]["blue"]==7 and blue_start["hero"]["state"]["ElectroCharged"]
        blue_done=wait(lambda s:s["hero"]["state"]["ElectroCharged"] is None)
        assert blue_done["hero"]["health"]==1 and blue_done["hero"]["blue"]==4
        tests.append(dict(test="蓝血真实受伤及三次感电扣蓝血",before=blue_start,after=blue_done))
        command("blueHealth",red=9,blue=0); command("clear"); command("invincible",value=True)
        command("goto",scene="GG_Gruz_Mother",gate="door_dreamEnter")
        wait(lambda s:s["scene"]=="GG_Gruz_Mother" and s["enemies"]); time.sleep(3)
        command("ready"); command("invincible",value=False); command("playerAura",element="Hydro")
        assert command("hurt",element="Cryo",damage=1)
        command("invincible",value=True); command("enemyControl",target="Giant Fly",hard=True,seconds=10)
        time.sleep(.2); command("unloadAudit",reload=True); time.sleep(.5)
        audit=json.loads((SESSION/"unload-audit.json").read_text(encoding="utf-8-sig"))
        assert audit["passed"] and audit["spellLevel"]==audit["rawSpellLevel"]==2,audit
        (RESULTS/"unload-audit.json").write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding="utf-8")
        tests.append(dict(test="活体Boss冻结期间卸载恢复与重新启用",audit=audit))
        command("blueHealth",red=9,blue=0); command("invincible",value=False); command("bossLevel",value=2)
        command("playerAura",element="Pyro"); game_wait(1.6)
        assert command("hurt",element="Electro",damage=1)
        death=status(); assert death["hero"]["health"]==0
        recovered=wait(lambda s:s["scene"]!="GG_Gruz_Mother" and s["hero"] and s["hero"]["health"]>0,50)
        assert recovered["hero"]["state"]["Aura"]==0 and recovered["hero"]["state"]["OverloadUntil"]==0
        # Native dream returns wait for a direction/attack to stand up; send that existing event
        # before using any developer control helper so the real animation can finish normally.
        ready=wait(lambda s:s["hero"] and (s["hero"]["canCast"] or any(f["FsmName"]=="Dream Return" and f["ActiveStateName"]=="Ready" for f in s["hero"]["fsm"])),15)
        dream=next(f for f in ready["hero"]["fsm"] if f["FsmName"]=="Dream Return")
        if dream["ActiveStateName"]=="Ready": command("fsmEvent",fsm="Dream Return",event="GET UP")
        awake=wait(lambda s:s["hero"] and s["hero"]["canCast"],15)
        tests.append(dict(test="辐辉原版致死、死亡清理及原版起身后重战",death=death,recovered=recovered,awake=awake))
        report=dict(passed=True,tests=tests,commands=LOG)
        print("PASS",len(tests),"effect, AoE, pool, pause, blue-health and unload/death checks",flush=True)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        RESULTS.mkdir(parents=True,exist_ok=True)
        (RESULTS/"effects-lifecycle.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
