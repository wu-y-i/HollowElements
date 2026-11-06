"""Four real Godhome encounters; uses native FSM states, physics, spawners and damage hooks."""
import json
import os
import time
from pathlib import Path
from runtime_acceptance import command, ROOT, SESSION, LOG

OUT = ROOT / "artifacts/acceptance/boss-design"
tests = []


def status(): return command("status")


def wait(predicate, seconds=25):
    deadline = time.monotonic()+seconds
    while time.monotonic()<deadline:
        s=status()
        if predicate(s): return s
        time.sleep(.15)
    raise TimeoutError("boss design condition")


def enter(scene):
    command("invincible",value=True)
    command("goto",scene=scene)
    wait(lambda s:s["scene"]==scene and s["enemies"])
    time.sleep(2)
    command("ready")
    command("bossLevel",value=0)
    wait(lambda s:s["hero"] and s["hero"]["canCast"],15)
    return status()


def enemy(s, name): return next(e for e in s["enemies"] if e["name"]==name)
def state(e, fsm, value): return command("enemyState",targetId=e["id"],fsm=fsm,state=value)
def hit(e, element, damage=20): return command("hit",targetId=e["id"],element=element,source="WhiteWave",damage=damage)


def capture(name):
    if os.environ.get('HOLLOW_ELEMENTS_SKIP_CAPTURE'):return
    command("capture"); time.sleep(.3)
    (OUT/(name+".png")).write_bytes((SESSION/"game.png").read_bytes())


def park():
    # invinciTest prevents HP loss but deliberately preserves native recoil/invulnerability.
    # Move away from physical contacts and wait for that ordinary recovery window to end.
    command("position",target="hero",x=47,y=100)
    now=status()["time"]
    # A native recoil coroutine can overwrite a one-off teleport on its next frame.
    # Keep the fixture away from body contacts until that real recovery has ended.
    while status()["time"]<now+1.8:
        command("position",target="hero",x=47,y=100)
        time.sleep(.1)


def kings(s): return [e for e in s["enemies"] if e["mechanic"] and e["mechanic"]["kind"]=="VengeflyKing"]


def run():
    s=enter("GG_Vengefly_V"); ks=kings(s)
    assert len(ks)==2 and all(k["mechanic"]["roars"]==0 and k["state"]["Aura"]==0 for k in ks)
    for k in ks: state(k,"Big Buzzer","Roar Left")
    time.sleep(1.8); first=status()
    assert len({k["mechanic"]["element"] for k in kings(first)})==2
    first_minions=[e for e in first["enemies"] if e["mechanic"] and e["mechanic"]["kind"]=="Vengefly"]
    assert first_minions and all(e["mechanic"]["element"]==0 for e in first_minions)
    tests.append(dict(test="双王初始无附着、开场吼叫不换色、首次召唤无元素",initial=s,after=first))
    for k in kings(first): state(k,"Big Buzzer","Roar Left")
    time.sleep(1.8); second=status()
    assert len({k["mechanic"]["element"] for k in kings(second)})==2
    new=[e for e in second["enemies"] if e["id"] not in {x["id"] for x in first["enemies"]} and e["mechanic"] and e["mechanic"]["kind"]=="Vengefly"]
    old_elements={k["mechanic"]["element"] for k in kings(first)}
    assert new and all(e["mechanic"]["element"] in old_elements and e["mechanic"]["outline"] for e in new)
    by_owner={k["id"]:k["mechanic"]["element"] for k in kings(first)}
    assert all(e["mechanic"]["element"]==by_owner[e["mechanic"]["summonerId"]] for e in new)
    capture("vengefly-outline")
    tests.append(dict(test="原版预生成召唤回调传递吼叫前元素；真实动画轮廓显示",before=first,after=second))
    minion=new[0]
    park()
    command("clear"); command("heal"); command("invincible",value=False)
    contact=command("contactHit",targetId=minion["id"],damage=1)
    assert contact["before"]-contact["after"]==1 and contact["player"]["Aura"]==minion["mechanic"]["element"]
    command("invincible",value=True)
    tests.append(dict(test="小怪真实接触伤害给玩家附着继承元素",contact=contact))
    for _ in range(5):
        for k in kings(status()): state(k,"Big Buzzer","Roar Left")
        s=status(); assert len({k["mechanic"]["element"] for k in kings(s)})==2
    k=kings(status())[0]; element=k["mechanic"]["element"]
    command("enemyVulnerable",targetId=k["id"])
    trigger={1:"Hydro",2:"Pyro",3:"Pyro",4:"Pyro"}[element]
    r=hit(k,trigger); assert r["state"]["Aura"]==element and r["state"]["IntrinsicAura"]==element
    tests.append(dict(test="多次换色始终双王互斥、反应后固有附着保留",hit=r))
    s=enter("GG_Vengefly"); assert len(kings(s))==1
    tests.append(dict(test="单王场景独立适配",state=s))

    s=enter("GG_Gruz_Mother"); g=enemy(s,"Giant Fly")
    state(g,"Big Fly Control","Charge")
    physical=wait(lambda s:enemy(s,"Giant Fly")["mechanic"]["collisions"]>0,12)
    park(); command("clear"); command("heal"); command("invincible",value=False)
    contact=command("contactHit",targetId=g["id"],damage=1)
    assert contact["before"]-contact["after"]==1
    current=enemy(status(),"Giant Fly"); assert current["mechanic"]["element"] in (1,2,3,4)
    assert current["state"]["IntrinsicAura"]==0
    assert contact["player"]["Aura"]==current["mechanic"]["element"]
    tests.append(dict(test="格鲁兹物理墙地碰撞换色及真实玩家碰撞伤害",physics=physical,contact=contact,state=current))

    s=enter("GG_False_Knight"); shell=enemy(s,"False Knight New")
    command("enemyVulnerable",targetId=shell["id"])
    elemental=hit(shell,"Pyro"); assert elemental["before"]-elemental["after"]==20 and elemental["state"]["Aura"]==0
    neutral=hit(shell,"None"); assert neutral["before"]-neutral["after"]==15
    tests.append(dict(test="假骑士外壳不附着、元素伤害正常、无元素75%",elemental=elemental,neutral=neutral))
    hit(shell,"None",1000)
    exposed=wait(lambda s:any(e["name"]=="Head" and not e["IsInvincible"] for e in s["enemies"]),12)
    head=enemy(exposed,"Head"); head_hit=hit(head,"Cryo",10)
    assert head_hit["state"]["Aura"]==4
    state(shell,"FalseyControl","Stun Fail")
    infused=status(); infused_shell=enemy(infused,"False Knight New")
    assert infused_shell["state"]["IntrinsicAura"]==4
    command("enemyVulnerable",targetId=shell["id"])
    reaction=hit(shell,"Pyro")
    assert reaction["state"]["IntrinsicAura"]==4 and reaction["state"]["Aura"]==4
    assert reaction["before"]-reaction["after"]==40
    tests.append(dict(test="原版露头命中附着、回壳转永久附着并可反应",head=head_hit,returned=infused,reacted=reaction))

    s=enter("GG_Mega_Moss_Charger"); moss=enemy(s,"Mega Moss Charger")
    park()
    command("clear"); command("enemyVulnerable",targetId=moss["id"])
    baseline=state(moss,"Mossy Control","Hit Right"); assert abs(baseline["x"]-10)<.01
    command("enemyAura",targetId=moss["id"],element="Hydro")
    frozen=hit(moss,"Cryo",10); assert frozen["state"]["FrozenUntil"]>0 and not frozen["state"]["HardFrozen"]
    recoil=state(moss,"Mossy Control","Hit Right"); assert abs(recoil["x"]-5)<.01
    state(moss,"Mossy Control","Charge")
    native_hit=hit(moss,"Cryo",10)
    assert abs(abs(native_hit["velocityX"])-5)<.01,native_hit
    command("heal"); command("invincible",value=False)
    contact=command("contactHit",targetId=moss["id"],damage=1)
    assert contact["before"]-contact["after"]==2,contact
    assert contact["player"]["Aura"] in (2,4)
    capture("moss-frost")
    tests.append(dict(test="苔藓固定结霜、原版击退10变5、对玩家1变2",baseline=baseline,recoil=recoil,frozen=frozen,contact=contact))
    time.sleep(3.3)
    normal=state(moss,"Mossy Control","Hit Right"); assert abs(normal["x"]-10)<.01
    tests.append(dict(test="结霜结束后恢复原版击退",recoil=normal))


if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    try:
        run(); report=dict(passed=True,tests=tests,commands=LOG)
        print("PASS",len(tests),"native boss design checks",flush=True)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        (OUT/"four-bosses.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
