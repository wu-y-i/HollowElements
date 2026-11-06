"""Hot-apply independent player ICD through the real F8 editor, then restore original settings."""
import copy
import json
from runtime_acceptance import command, ROOT, LOG


def apply(config):
    config=copy.deepcopy(config)
    slots=["Nail","Wave","Dive","Shriek","GreatSlash","DashSlash","Cyclone"]
    for p in config["Attacks"].values():
        if isinstance(p["Slot"],int):p["Slot"]=slots[p["Slot"]]
    command("menu",debug=True)
    command("menuJson",text=json.dumps(config))
    menu=command("menuStatus")
    command("menuClick",index=menu["buttons"].index("应用"))
    result=command("menuStatus")
    assert "无效" not in result["status"],result["status"]
    command("closeMenu")
    return result


if __name__=="__main__":
    original=None
    result=dict(passed=False)
    try:
        command("invincible",value=True)
        command("goto",scene="GG_Atrium",gate="door_dreamEnter")
        import time
        for _ in range(100):
            if command("status")["scene"]=="GG_Atrium":break
            time.sleep(.1)
        time.sleep(2);command("ready");command("position",target="hero",x=111,y=14.4)
        command("menu",debug=True);original=command("menuStatus")["config"];command("closeMenu")
        draft=copy.deepcopy(original)
        for source in ("Nail","WhiteWave","BlackWave"):
            draft["Attacks"][source]["IcdSeconds"]=60
            draft["Attacks"][source]["HitStride"]={"Nail":3,"WhiteWave":2,"BlackWave":1}[source]
        applied=apply(draft)
        assert applied["config"]["Attacks"]["WhiteWave"]["IcdGroup"]=="WhiteWave"
        command("clear");dummy=command("dummy",offset=5)
        target=next(e for e in command("status")["enemies"] if "反应木桩" in e["name"])["id"]
        command("pause",value=True)
        values={}
        for source,pattern in [("Nail",[1,0,0,1]),("WhiteWave",[1,0,1]),("BlackWave",[1,1,1])]:
            a=[]
            for _ in pattern:
                command("enemyAura",targetId=target,element="None",units=0)
                h=command("hit",targetId=target,source=source,element="Pyro",damage=1)
                a.append(h["state"]["Aura"])
            assert a==pattern,(source,a)
            values[source]=a
        result.update(passed=True,patterns=values,config=applied["config"])
        print("PASS F8独立热配置：普攻1/4、白波1/3、黑波每次，分组互不干扰",flush=True)
    except Exception as error:
        result["error"]=repr(error);raise
    finally:
        command("pause",value=False)
        if original is not None:apply(original)
        command("clear")
        result["commands"]=LOG
        (ROOT/"artifacts/acceptance/incoming-attacks/config.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
