"""Incoming aura checks through real Unity attack colliders, not contactHit/hurt injection.

Native FSM entries select an attack; the test player intercepts its enabled collider.
The independently recorded accepted-hit source must match that real collider's path.
"""
import json
import sys
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_design_acceptance import enter, enemy, state, status, park

OUT = ROOT / "artifacts/acceptance/incoming-attacks"
tests = []


def shots():
    return [p for p in command("projectiles") if p["active"] and p["element"]]


def setup(scene, name):
    s=enter(scene)
    b=enemy(s,name)
    command("invincible",value=True)
    park()
    command("pause",value=True)
    command("clear");command("heal");command("blueHealth",red=9,blue=0)
    if "Xero" not in scene:
        for p in shots():command("projectileDisable",objectId=p["id"])
    return b


def next_attack():
    command("pause",value=False);command("invincible",value=True);park()
    command("pause",value=True)
    for p in shots():command("projectileDisable",objectId=p["id"])


def catch(obj, expected, label, path=None, blue=False, stage=False, settle=True, y_offset=0, manual_point=None, after_clear=None, aura_expected=None):
    # Let native launch/throw frames separate the projectile from the owner's melee collider.
    if stage:command("position",objectId=obj,x=45,y=17)
    elif settle:command("pause",value=False);time.sleep(.3);command("pause",value=True)
    command("clear")
    if after_clear:after_clear()
    command("blueHealth",red=9,blue=8 if blue else 0)
    command("clearPlayerHits")
    command("invincible",value=False)
    if manual_point:command("position",target="hero",x=manual_point[0],y=manual_point[1])
    else:command("interceptAttack",objectId=obj,seconds=2,yOffset=y_offset)
    command("pause",value=False)
    deadline=time.monotonic()+3
    received=[]
    while time.monotonic()<deadline:
        received=command("playerHits")
        if received:break
        time.sleep(.08)
    command("invincible",value=True)
    command("pause",value=True)
    assert received, (label,"no native HP hit",status())
    first=received[0]
    assert first["element"]==expected,(label,"element lost",first)
    assert first["state"]["Aura"]==(expected if aura_expected is None else aura_expected),(label,"aura not committed",first)
    if path:assert path in first["source"],(label,"wrong attack intercepted",first)
    tests.append(dict(test=label,hit=first,blue=blue));print("PASS",label,first["source"],flush=True)
    return first


def run(case):
    if case=="gorb":
        b=setup("GG_Ghost_Gorb","Ghost Warrior Slug")
        state(b,"Attacking","Attack")
        p=shots()[0];catch(p["id"],p["element"],"戈布剑阵真实碰撞附着",p["name"])
    elif case=="warrior":
        b=setup("GG_Mage_Knight_V","Mage Knight")
        state(b,"Mage Knight","Shoot")
        p=shots()[0];catch(p["id"],p["element"],"灵魂战士光球真实碰撞附着",p["name"])
    elif case=="dung":
        b=setup("GG_Dung_Defender","Dung Defender")
        # Forced scene entry can interrupt an opening roar; finish its native unlock first.
        command("setState",fsm="Roar Lock",state="Regain Control");command("ready")
        state(b,"Dung Defender","Throw 1")
        p=shots()[0];catch(p["id"],p["element"],"粪球真实碰撞附着",p["name"],stage=True)
    elif case=="mawlek":
        b=setup("GG_Brooding_Mawlek","Mawlek Body")
        state(b,"Mawlek Control","L")
        p=shots()[0];catch(p["id"],p["element"],"毛里克大喷射真实碰撞附着",p["name"])
    elif case=="xero":
        b=setup("GG_Ghost_Xero","Ghost Warrior Xero")
        p=next(p for p in shots() if p["name"]=="Sword 1")
        first=catch(p["id"],4,"泽若冰剑真实碰撞附着",p["name"],blue=True)
        command("pause",value=False);command("position",target="hero",x=47,y=100)
        while status()["time"]<first["time"]+1.4:time.sleep(.03)
        command("pause",value=True)
        second=catch(p["id"],4,"怪物两次有效伤害在2.5秒内各自附着并刷新",p["name"],blue=True,settle=False)
        assert 0<second["time"]-first["time"]<2.5
        assert second["state"]["AuraUntil"]>first["state"]["AuraUntil"]
    elif case=="oblobble":
        b=setup("GG_Oblobbles","Mega Fat Bee")
        state(b,"Fatty Fly Attack","Attack")
        p=shots()[0];catch(p["id"],p["element"],"奥波路波弹幕真实碰撞附着",p["name"],stage=True)
    elif case in ("master","tyrant"):
        scene="GG_Soul_Master" if case=="master" else "GG_Soul_Tyrant"
        b=setup(scene,"Mage Lord" if case=="master" else "Dream Mage Lord")
        for attack,prefix in [("Shot","Mage Orb"),("Quake Waves","Shockwave Spurt"),("HS Orb","Mage Orb Over")]:
            if tests:next_attack()
            if attack=='HS Orb':
                # Recycling the previous fixture's orbs can cancel their tween completion,
                # leaving this native FSM in Spawn, where SPINNER SUMMON is ignored.
                for spinner in command('sceneObjects'):
                    if spinner['path']=='Orb Spinner':command('objectState',objectId=spinner['id'],fsm='Summon Orbs',state='Idle')
            state(b,"Mage Lord",attack)
            if attack in ("HS Orb","Quake Waves"):
                command("pause",value=False)
                deadline=time.monotonic()+3
                while time.monotonic()<deadline and not any(p['name'].startswith(prefix) for p in shots()):time.sleep(.05)
                command("pause",value=True)
            p=next(p for p in shots() if p["name"].startswith(prefix))
            catch(p["id"],p["element"],scene+" "+attack+" 真实碰撞附着",prefix,stage=True)
        next_attack()
        phase=next(e for e in status()["enemies"] if "Phase2" in e["name"])
        state(phase,"Mage Lord 2","Spawn Fireball")
        p=shots()[0];catch(p["id"],p["element"],scene+" 二阶段光球真实碰撞附着",p["name"],stage=True)
        if case=="tyrant":
            next_attack();state(phase,"Mage Lord 2","Quake Waves")
            command("pause",value=False);time.sleep(.08);command("pause",value=True)
            p=next(p for p in shots() if p["name"].startswith("Shockwave Spurt"))
            catch(p["id"],2,"灵魂暴君二阶段波真实碰撞附着",p["name"],stage=True)
    elif case=="brothers":
        b=setup("GG_Nailmasters","Oro")
        m=enemy(status(),"Mato");state(m,"nailmaster","Activate")
        command("setState",fsm="Roar Lock",state="Regain Control");command("ready")
        details=command("attackDetails",objectId=b["objectId"])
        child=next(d for d in details if d["path"].endswith("Oro/S1"))
        state(b,"nailmaster","Combo Slash1")
        catch(child["id"],2,"奥罗挥砍真实碰撞附着","Oro/S1",settle=False)
    elif case in ("mantis","sisters"):
        scene="GG_Mantis_Lords" if case=="mantis" else "GG_Mantis_Lords_V"
        b=setup(scene,"Mantis Lord")
        state(b,"Mantis Lord","Arrive");state(b,"Mantis Lord","Throw")
        p=shots()[0];catch(p["id"],4,scene+" 飞刃真实碰撞附着",p["name"],stage=True)
        next_attack();command("position",targetId=b["id"],x=30,y=15);state(b,"Mantis Lord","Dash Attack")
        details=command("attackDetails",objectId=b["objectId"])
        child=next(d for d in details if d["path"].endswith("/Dash Hit"))
        catch(child["id"],4,scene+" 冲刺刀光真实碰撞附着","Dash Hit",settle=False)
    else:raise ValueError(case)


if __name__=="__main__":
    case=sys.argv[1]
    OUT.mkdir(parents=True,exist_ok=True)
    result=dict(passed=False)
    try:
        run(case);result["passed"]=True
    except Exception as error:
        result["error"]=repr(error);raise
    finally:
        result.update(tests=tests,commands=LOG)
        (OUT/(case+".json")).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
        command("pause",value=False)
