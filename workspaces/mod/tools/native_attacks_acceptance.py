import json
import time
from runtime_acceptance import command, RESULTS, LOG


def prepare(x=113,y=14.4,hero_y=14.4):
    command("setState",fsm="Nail Arts",state="Cancel All")
    command("setState",fsm="Spell Control",state="Cancel All")
    command("ready")
    command("clear")
    command("position",target="hero",x=111,y=hero_y)
    command("position",target="反应木桩",x=x,y=y)
    command("face",right=True)
    command("heal")


def sample(source,fsm=None,state=None,x=113,y=14.4,hero_y=14.4,wait=.9):
    prepare(x,y,hero_y)
    if fsm:
        if state=="Cyclone Start":
            # Native setup disables HeroAnimationController before the sword-art animation.
            command("setState",fsm=fsm,state="Take Control")
        command("setState",fsm=fsm,state=state)
    else:
        command("attack")
    visuals=command("attackVisuals")
    candidates=[v for v in visuals if v["source"]==source or "Dive" in source and "Dive" in v["source"]]
    assert any(v["Coloured"] and v["SpriteCount"]>0 for v in candidates),(source,visuals)
    time.sleep(wait)
    result=command("status")
    hits=[line for line in result["trace"] if " -> " in line]
    print(source,hits,flush=True)
    assert any(" "+source+" #" in line for line in hits), (source,hits)
    return dict(source=source,hits=hits,status=result,visuals=visuals)


if __name__=="__main__":
    tests=[]
    try:
        status=command("status")
        if not any("反应木桩" in e["name"] for e in status["enemies"]):
            command("dummy",offset=2,recoilable=True)
        command("ready")
        command("loadout",Nail="Hydro",Wave="Pyro",Dive="Cryo",Shriek="Electro",GreatSlash="Pyro",DashSlash="Hydro",Cyclone="Electro")
        tests.append(sample("Nail"))
        tests.append(sample("GreatSlash","Nail Arts","G Slash"))
        tests.append(sample("DashSlash","Nail Arts","Dash Slash"))
        tests.append(sample("Cyclone","Nail Arts","Cyclone Start",x=111.7,wait=1.6))
        command("spellLevel",wave=1,dive=1,shriek=1)
        tests.append(sample("WhiteWave","Spell Control","Has Fireball?",x=115,wait=1))
        tests.append(sample("WhiteShriek","Spell Control","Has Scream?",x=111,y=17,wait=1))
        tests.append(sample("WhiteDiveBody","Spell Control","Has Quake?",x=111,y=14.4,hero_y=19,wait=1.5))
        command("spellLevel",wave=2,dive=2,shriek=2)
        tests.append(sample("BlackWave","Spell Control","Has Fireball?",x=115,wait=1))
        tests.append(sample("BlackShriek","Spell Control","Has Scream?",x=111,y=17,wait=1))
        tests.append(sample("BlackDiveBody","Spell Control","Has Quake?",x=111,y=14.4,hero_y=19,wait=1.5))
        report=dict(passed=True,tests=tests,commands=LOG)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        RESULTS.mkdir(exist_ok=True,parents=True)
        (RESULTS/"native-attacks.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
