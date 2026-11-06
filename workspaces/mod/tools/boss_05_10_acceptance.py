"""Native FSM / damage / projectile integration checks for Bosses 05–10.

Uses the opt-in development bridge with real save writes suppressed. Run only
after start-acceptance-room.py. Outputs evidence separately from 01–04 checks.
"""
import json
import os
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_design_acceptance import enter, enemy, state, hit, status, wait, park

OUT = ROOT / "artifacts/acceptance/boss-05-10"
tests = []


def passed(text, **evidence):
    tests.append(dict(test=text, **evidence))
    print("PASS", text, flush=True)


def projectiles(e):
    return [p for p in command("projectiles") if p["owner"] == e["id"] and p["active"]]


def clear_shots(e):
    for p in projectiles(e):
        command("projectileDisable", objectId=p["id"])


def capture(name):
    if os.environ.get('HOLLOW_ELEMENTS_SKIP_CAPTURE'):return
    command("capture"); time.sleep(.3)
    (OUT / (name + ".png")).write_bytes((SESSION / "game.png").read_bytes())


def run():
    s=enter("GG_Hornet_1"); h=enemy(s,"Hornet Boss 1")
    command("enemyVulnerable",targetId=h["id"])
    for i in range(12):
        command("enemyAura",targetId=h["id"],element="Hydro")
        r=hit(h,"Pyro",1); assert r["after"]<r["before"]
        time.sleep(.15)
    h=enemy(status(),h["name"])
    assert h["mechanic"]["stacks"]==12 and h["mechanic"]["vulnerability"]==2.2 and h["mechanic"]["tempo"]==2
    damage=hit(h,"None",10); assert damage["before"]-damage["after"]==22
    dash=state(h,"Control","G Dash"); assert abs(dash["x"])==50
    passed("大黄蜂12层易伤2.2、速度封顶2.0，原版冲刺速度50",boss=h,damage=damage,dash=dash)

    s=enter("GG_Hornet_2"); h=enemy(s,"Hornet Boss 2")
    assert h["mechanic"]["kind"]=="Hornet" and h["mechanic"]["stacks"]==0
    passed("大黄蜂岗哨按第30项继承机制，独立从零叠层",boss=h)

    s=enter("GG_Ghost_Gorb"); g=enemy(s,"Ghost Warrior Slug")
    volleys=[]
    for hp,expected in [(650,1),(400,2),(200,4)]:
        command("pause",value=True)
        clear_shots(g); command("enemyHp",targetId=g["id"],value=hp)
        state(g,"Attacking","Attack")
        if expected>1:
            command("pause",value=False)
            wait(lambda s:enemy(s,g["name"])["mechanic"]["ring"]==expected,5)
            command("pause",value=True)
        current=enemy(status(),g["name"]); shots=projectiles(g)
        assert current["mechanic"]["ring"]==expected, current
        for ring in range(1,expected+1):
            group=[p for p in shots if p["ring"]==ring]
            assert len(group)==8 and len({p["element"] for p in group})==1 and group[0]["element"] in (1,2,3,4), group
        volleys.append(dict(hp=hp,boss=current,shots=shots))
    command("pause",value=False)
    passed("戈布三个阶段为1/2/4圈，每圈八剑同元素",volleys=volleys)
    command("enemyVulnerable",targetId=g["id"]); command("enemyAura",targetId=g["id"],element="Hydro"); hit(g,"Pyro",1)
    suppressed=enemy(status(),g["name"])["mechanic"]["suppressedUntil"]
    assert 5.5<suppressed-status()["time"]<=6
    command("pause",value=True); clear_shots(g); state(g,"Attacking","Attack"); command("pause",value=False)
    wait(lambda s:enemy(s,g["name"])["mechanic"]["ring"]==2,5)
    assert enemy(status(),g["name"])["mechanic"]["ring"]==2
    wait(lambda s:s["time"]>suppressed+.1,10)
    command("pause",value=True); clear_shots(g); state(g,"Attacking","Attack"); command("pause",value=False)
    wait(lambda s:enemy(s,g["name"])["mechanic"]["ring"]==4,5)
    assert enemy(status(),g["name"])["mechanic"]["ring"]==4
    passed("戈布反应后6秒减为两圈，到期恢复四圈",until=suppressed,state=status())

    s=enter("GG_Dung_Defender"); d=enemy(s,"Dung Defender")
    state(d,"Dung Defender","Throw 1"); time.sleep(.2); balls=projectiles(d)
    assert balls and all(p["element"] in (1,2,3,4) for p in balls)
    command("clear"); launch=state(d,"Dung Defender","RJ Launch"); time.sleep(.15)
    current=enemy(status(),d["name"])
    assert current["mechanic"]["ball"] and current["mechanic"]["tempo"]==1.3
    assert current["state"]["Aura"]==3 and current["state"]["IntrinsicAura"]==0
    assert 0<current["state"]["AuraUntil"]-status()["time"]<=8 and abs(launch["y"]-45.5)<.01
    until=current["state"]["AuraUntil"]
    state(d,"Dung Defender","RJ Bounce"); time.sleep(.15)
    assert enemy(status(),d["name"])["state"]["AuraUntil"]==until
    state(d,"Dung Defender","RJ Unroll"); time.sleep(.1)
    current=enemy(status(),d["name"])
    assert not current["mechanic"]["ball"] and current["mechanic"]["tempo"]==1 and current["state"]["Aura"]==3
    passed("粪球逐球元素；弹球1U有限雷不重复刷新，速度1.3，退出后保留附着",balls=balls,launch=launch,state=current)
    command("enemyVulnerable",targetId=d["id"]); command("enemyAura",targetId=d["id"],element="Pyro"); hit(d,"Electro",1); time.sleep(.2)
    stunned=enemy(status(),d["name"])
    assert stunned["mechanic"]["knockdowns"]==1 and next(f["ActiveStateName"] for f in stunned["fsms"] if f["FsmName"]=="Dung Defender").startswith("Stun")
    passed("超载触发原版倒地状态",state=stunned)

    s=enter("GG_Mage_Knight_V"); w=enemy(s,"Mage Knight")
    orbs=[]
    for _ in range(3):
        state(w,"Mage Knight","Shoot"); time.sleep(.2)
        current=projectiles(w); assert current and all(p["scale"]=="(1.3, 1.3, 1.3)" and p["element"] in (3,4) for p in current)
        orbs.extend(current); clear_shots(w)
    recycled=[p for p in command("projectiles") if p["id"] in {a["id"] for a in orbs}]
    assert recycled and all(not p["active"] and p["element"]==0 and p["scale"]=="(1.0, 1.0, 1.0)" for p in recycled)
    passed("强化灵魂战士光球保持1.3，不累计放大，回收后清元素及恢复尺寸",orbs=orbs,recycled=recycled)
    assert all(e["mechanic"] is None for e in status()["enemies"] if e["name"].startswith("Folly"))

    s=enter("GG_Brooding_Mawlek"); m=enemy(s,"Mawlek Body")
    command("pause",value=True); clear_shots(m)
    # L chooses the native aim interval then synchronously transitions into Shoot.
    # Explicitly entering Shoot again would fabricate a second volley.
    state(m,"Mawlek Control","L")
    shots=projectiles(m); assert len(shots)==25 and {p["element"] for p in shots}=={1,3}
    command("pause",value=False)
    passed("毛里克25发大喷射逐弹火雷随机",shots=shots)
    command("enemyVulnerable",targetId=m["id"])
    for _ in range(30):
        command("enemyAura",targetId=m["id"],element="Pyro"); hit(m,"None",1); time.sleep(.12)
    current=enemy(status(),m["name"])
    assert current["mechanic"]["hits"]==30 and 0<current["mechanic"]["cryo"]<30 and current["state"]["IntrinsicAura"]==0
    passed("毛里克有效直接受击抽取有限自附冰，可与原火附着反应且不递归",boss=current)

    s=enter("GG_Nailmasters"); o=enemy(s,"Oro"); m=enemy(s,"Mato")
    assert not o["mechanic"]["dual"] and not m["mechanic"]["dual"]
    state(m,"nailmaster","Activate"); time.sleep(.1); s=status(); o=enemy(s,"Oro"); m=enemy(s,"Mato")
    assert o["mechanic"]["element"]==2 and m["mechanic"]["element"]==1
    park(); command("clear"); command("heal"); command("invincible",value=False)
    body=command("contactHit",targetId=o["id"],damage=1); assert body["after"]<body["before"] and body["player"]["Aura"]==0
    park(); command("clear"); command("heal")
    blade=command("contactHit",targetId=o["id"],child="S1",damage=1); assert blade["after"]<blade["before"] and blade["player"]["Aura"]==2
    command("invincible",value=True)
    passed("骨钉兄弟单人无元素、双人固定奥罗水马托火、身体接触无元素而挥砍附着",body=body,blade=blade,bosses=[o,m])
    command("position",target="hero",x=45,y=7)
    command("position",targetId=o["id"],x=44,y=6.4); command("position",targetId=m["id"],x=55,y=6.4)
    state(o,"nailmaster","Combo Slash1"); state(m,"nailmaster","Combo Slash3"); command("pause",value=True); capture("nailmasters-water-fire"); command("pause",value=False)

    # At pause native actors cannot mutate transform/colour while unload restoration is audited.
    command("pause",value=True); command("unloadAudit",reload=True); time.sleep(.8)
    audit=json.loads((SESSION/"unload-audit.json").read_text(encoding="utf-8-sig")); assert audit["passed"],audit
    command("pause",value=False)
    passed("卸载恢复颜色、重力、尺寸，清除弹幕标记、逐帧遮罩、控制器及Hook",audit=audit)


if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    try:
        run()
        (OUT/"results.json").write_text(json.dumps(dict(passed=True,tests=tests),ensure_ascii=False,indent=2),encoding="utf-8")
    finally:
        (OUT/"commands.json").write_text(json.dumps(LOG,ensure_ascii=False,indent=2),encoding="utf-8")
