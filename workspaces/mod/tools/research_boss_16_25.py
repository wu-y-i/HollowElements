"""Read native encounter FSMs without writing saves. Requires an explicit development session."""
import json
import time
import sys
from runtime_acceptance import command, ROOT

OUT=ROOT/"local/research/boss-16-25"
SCENES=["GG_Ghost_Marmu","GG_Ghost_Marmu_V","GG_Flukemarm","GG_Broken_Vessel","GG_Lost_Kin",
        "GG_Ghost_Galien","GG_Painter","GG_Hive_Knight","GG_Ghost_Hu","GG_Collector","GG_Collector_V",
        "GG_God_Tamer","GG_Grimm","GG_Grimm_Nightmare","GG_Spa"]


def save(name,value):
    (OUT/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding="utf-8")


if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    valid={s.rsplit("/",1)[-1].removesuffix(".unity") for s in command("sceneNames")}
    for scene in sys.argv[1:] or SCENES:
        assert scene in valid,scene
        command("invincible",value=True);command("goto",scene=scene)
        deadline=time.monotonic()+25
        while time.monotonic()<deadline:
            s=command("status")
            if s["scene"]==scene and s["hero"]:break
            time.sleep(.15)
        else:raise TimeoutError(scene)
        time.sleep(1.8);command("ready")
        command("pause",value=True)
        save(scene+"-status.json",command("status"))
        save(scene+"-all.json",command("dumpAll"))
        objs=command("sceneObjects");save(scene+"-objects.json",objs)
        for e in command("status")["enemies"]:
            save(scene+"-"+str(e["objectId"])+"-detail.json",command("attackDetails",objectId=e["objectId"]))
            if scene=="GG_Hive_Knight" and e["name"]=="Hive Knight":command("exportEnemySprites",targetId=e["id"])
        command("pause",value=False)
        print("READ",scene,[(e["name"],e["hp"]) for e in s["enemies"]],flush=True)
