"""Targeted real-game checks: native armour, pool spawns, phases and soul gain."""
import json
import os
import sys
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_design_acceptance import enter, enemy, state, hit, status, wait, park

OUT=ROOT/"artifacts/acceptance/boss-11-15"
tests=[]


def passed(text,**evidence):
    tests.append(dict(test=text,**evidence));print("PASS",text,flush=True)


def shots(e=None):
    values=command("projectiles")
    owners={e["id"]} if e else set()
    # Some native Godhome prefabs contain two HealthManagers on one actor.
    # The controller uses GetComponent's canonical instance, whose ID need not be first in diagnostics.
    if e and "objectId" in e:owners|={h["id"] for h in status()["enemies"] if h["objectId"]==e["objectId"]}
    return [p for p in values if p["active"] and (e is None or p["owner"] in owners)]


def capture(name):
    if os.environ.get('HOLLOW_ELEMENTS_SKIP_CAPTURE'):return
    command("capture");time.sleep(.3);(OUT/(name+".png")).write_bytes((SESSION/"game.png").read_bytes())


def audit(label):
    command("pause",value=True);command("unloadAudit",reload=True);time.sleep(.8)
    result=json.loads((SESSION/"unload-audit.json").read_text(encoding="utf-8-sig"));assert result["passed"],result
    command("pause",value=False);passed(label,audit=result)


def run_core():
    s=enter("GG_Ghost_Xero");x=enemy(s,"Ghost Warrior Xero");p=shots(x)
    assert {a["name"]:a["element"] for a in p}=={"Sword 1":4,"Sword 2":3} and all(a["tinted"] for a in p)
    state(x,"Attacking","Summon");time.sleep(.2);p=shots(x)
    assert {a["name"]:a["element"] for a in p}=={"Sword 1":4,"Sword 2":3,"Sword 3":2,"Sword 4":1} and all(a["tinted"] for a in p)
    capture("xero-four-elements");passed("泽若初始冰雷，第二阶段新增水火，四剑染色",projectiles=p)
    audit("泽若活体剑染色卸载恢复")

    s=enter("GG_Crystal_Guardian");c=s["enemies"][0];park()
    first=hit(c,"Pyro",20);assert first["before"]==first["after"] and first["state"]["Aura"]==1
    command("nativeExtra",targetId=c["id"],damage=30);assert enemy(status(),c["name"])["hp"]==first["after"]
    capture("crystal-eight-shard-shield")
    second=hit(c,"Electro",20);current=enemy(status(),c["name"])
    assert second["before"]==second["after"] and not current["mechanic"]["shield"]
    due=current["mechanic"]["returnsAt"];assert 14.5<due-status()["time"]<=15
    exposed=hit(c,"None",20);assert exposed["before"]-exposed["after"]==20
    passed("护盾零伤仍附着，原版追加伤害也拦截，超载破盾后可正常受伤",first=first,overload=second,exposed=exposed)
    wait(lambda s:s["time"]>due+.1,20);current=enemy(status(),c["name"])
    assert current["mechanic"]["shield"] and current["mechanic"]["headHits"]==0
    blocked=hit(c,"None",20);assert blocked["before"]==blocked["after"]
    passed("破盾15秒后重新生成且清空头部计数",boss=current)
    audit("护盾晶片与材质卸载清理")

    s=enter("GG_Crystal_Guardian_2");c=s["enemies"][0];park();counted=[]
    # Armour keeps its original head immunity and native evasion interval.
    # Multiple accepted hits from one cast (e.g. cyclone) all count.
    for i in range(8):
        time.sleep(.24)
        r=command("hit",targetId=c["id"],source="Cyclone",element="None",damage=20,direction=270,cast=50000)
        assert r["before"]==r["after"]
        count=enemy(status(),c["name"])["mechanic"]["headHits"];assert count==i+1,(i,count)
        counted.append(r)
    assert not enemy(status(),c["name"])["mechanic"]["shield"]
    time.sleep(.24);head=command("hit",targetId=c["id"],source="Nail",element="None",damage=20,direction=270)
    assert head["before"]==head["after"]
    time.sleep(.24)
    body=hit(c,"None",20);assert body["before"]-body["after"]==20
    passed("暴怒守卫8次原版头部弹刀破盾，同次旋风斩多段可累计，破盾后头部仍不扣血",headHits=counted,head=head,body=body)

    park();command("clear");command("heal");command("loadout",Nail="Hydro",Wave="Hydro");command("invincible",value=False)
    hurt=command("contactHit",targetId=c["id"],damage=1);s=status();assert hurt["after"]<hurt["before"] and s["forcedNail"] in (1,3)
    until=s["forcedNailUntil"];assert 5.5<until-s["time"]<=6
    command("invincible",value=True);command("dummy",offset=8);dummy=next(e for e in status()["enemies"] if "反应木桩" in e["name"])
    forced=command("hit",targetId=dummy["id"],source="Nail",element="Hydro",damage=10);assert forced["state"]["Aura"]==s["forcedNail"]
    command("enemyAura",targetId=dummy["id"],element="None",units=0)
    wave=command("hit",targetId=dummy["id"],source="WhiteWave",element="Hydro",damage=10);assert wave["state"]["Aura"]==2
    old=status()["forcedNailUntil"];command("contactHit",targetId=c["id"],damage=1);assert status()["forcedNailUntil"]==old
    park();command("heal");command("invincible",value=False);command("contactHit",targetId=c["id"],damage=1);command("invincible",value=True)
    refreshed=status()["forcedNailUntil"];assert refreshed>until+1
    wait(lambda s:s["time"]>refreshed+.1,10);command("enemyAura",targetId=dummy["id"],element="None",units=0)
    restored=command("hit",targetId=dummy["id"],source="Nail",element="Hydro",damage=10);assert restored["state"]["Aura"]==2
    passed("强制普攻火雷持续6秒，有效受伤刷新，无敌不刷新，法术不受影响，到期恢复原配装",forced=forced,wave=wave,restored=restored)



def run_remaining():
    for scene,bossName in [("GG_Soul_Master","Mage Lord"),("GG_Soul_Tyrant","Dream Mage Lord")]:
        s=enter(scene);b=enemy(s,bossName);command("pause",value=True)
        state(b,"Mage Lord","Shot");state(b,"Mage Lord","Quake Waves");command("pause",value=False);time.sleep(.2)
        first=shots(b);orbs=[p for p in first if p["name"].startswith("Mage Orb")];waves=[p for p in first if p["name"].startswith("Shockwave")]
        assert orbs and all(p["element"] in (2,4) and p["outlineVisible"] for p in orbs)
        emitters=[p for p in waves if p['name'].startswith('Shockwave Wave')]
        assert len(emitters)==2 and all(p["element"]==2 and p["tinted"] for p in waves)
        for spinner in command('sceneObjects'):
            if spinner['path']=='Orb Spinner':command('objectState',objectId=spinner['id'],fsm='Summon Orbs',state='Idle')
        state(b,"Mage Lord","HS Orb");time.sleep(.35);rings=[p for p in shots(b) if p["name"].startswith("Mage Orb Over")]
        assert rings and all(p["element"] in (2,4) and p["outlineVisible"] for p in rings)
        phase=next(e for e in status()["enemies"] if "Phase2" in e["name"])
        state(phase,"Mage Lord 2","Spawn Fireball");time.sleep(.15);phaseOrbs=shots(phase)
        assert phaseOrbs and all(p["element"] in (2,4) for p in phaseOrbs)
        if scene=="GG_Soul_Tyrant":
            command("pause",value=True);state(phase,"Mage Lord 2","Quake Waves");command("pause",value=False);time.sleep(.15)
            assert any(p["name"].startswith("Shockwave") and p["element"]==2 and p["tinted"] for p in shots(phase))
        passed(scene+"：普通／环绕／二阶段光球水冰轮廓，砸地波水染色",projectiles=first,spinner=rings,phase2=phaseOrbs)

    s=enter("GG_Oblobbles");a=enemy(s,"Mega Fat Bee");b=enemy(s,"Mega Fat Bee (1)")
    assert a["state"]["IntrinsicAura"]==1 and b["state"]["IntrinsicAura"]==3
    command("pause",value=True)
    for e,element in [(a,3),(b,1)]:
        state(e,"Fatty Fly Attack","Attack");p=shots(e);assert p and all(x["element"]==element and x["tinted"] for x in p)
    command("pause",value=False);dead=hit(a,"None",9999)
    wait(lambda s:enemy(s,b["name"])["mechanic"]["enraged"],8)
    b=enemy(status(),b["name"]);assert b["state"]["IntrinsicAura"]==3
    command("soul",value=0);immune=command("hit",targetId=b["id"],source="Nail",element="Electro",damage=20)
    assert immune["before"]==immune["after"] and status()["hero"]["soul"]>0
    time.sleep(.2);normal=hit(b,"None",20);assert normal["before"]-normal["after"]==20
    command("pause",value=True);colours=set()
    for _ in range(6):
        state(b,"Fatty Fly Attack","Attack");colours|={p["element"] for p in shots(b)}
        if colours=={1,3}:break
    command("pause",value=False);assert colours=={1,3}
    passed("奥波路波反向固有附着及弹幕，真实击杀同伴后狂暴保持附着、免疫但回魂、弹幕火雷随机",dead=dead,immune=immune,neutral=normal,colours=list(colours))

    for scene in ["GG_Mantis_Lords","GG_Mantis_Lords_V"]:
        s=enter(scene);objects=command("sceneObjects");sub=next(o for o in objects if o["path"]=="Mantis Battle/Battle Sub")
        command("objectState",objectId=sub["id"],fsm="Start",state="Start");time.sleep(1.5)
        actors={}
        for e in status()["enemies"]:
            if e["mechanic"] and e["mechanic"]["kind"]=="Mantis" and e["name"] not in actors:actors[e["name"]]=e
        assert actors["Mantis Lord"]["mechanic"]["element"]==4 and actors["Mantis Lord S1"]["mechanic"]["element"]==3 and actors["Mantis Lord S2"]["mechanic"]["element"]==2
        if scene.endswith("_V"):assert actors["Mantis Lord S3"]["mechanic"]["element"]==4
        command("pause",value=True)
        for name,e in actors.items():
            state(e,"Mantis Lord","Throw");p=shots(e);assert p and all(x["element"]==e["mechanic"]["element"] and x["tinted"] for x in p)
        visuals=command("weaponVisuals");assert all(v["ResourceSet"]=="MantisBlades" and v["overlayVisible"] for v in visuals)
        command("pause",value=False)
        passed(scene+"：固定冰雷水、三位原版出场与投掷、武器及刀光局部染色",actors=list(actors.values()),visuals=visuals)
    audit("螳螂局部遮罩及活动投掷物染色卸载恢复")

    for scene,bossName,fsm,action,prefix in [("GG_Ghost_Gorb","Ghost Warrior Slug","Attacking","Attack","Shot Slug Spear"),("GG_Mage_Knight","Mage Knight","Mage Knight","Shoot","Mage Orb"),("GG_Dung_Defender","Dung Defender","Dung Defender","Throw 1","Dung Ball Large")]:
        s=enter(scene);e=enemy(s,bossName);state(e,fsm,action);time.sleep(.2);p=[p for p in shots(e) if p["name"].startswith(prefix)];assert p
        if "Gorb" in scene:assert all(x["tinted"] for x in p)
        else:assert all(x["outlineVisible"] for x in p)
        capture("previous-"+scene);passed(scene+"：旧弹幕元素显示复查",projectiles=p)


if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    try:
        if "--resume-soul" in sys.argv:
            tests.extend(json.loads((OUT/"results.json").read_text(encoding="utf8"))["tests"][:7])
        else:run_core()
        run_remaining();report=dict(passed=True,tests=tests)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests);raise
    finally:
        (OUT/"results.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf8")
        (OUT/"commands.json").write_text(json.dumps(LOG,ensure_ascii=False,indent=2),encoding="utf8")
