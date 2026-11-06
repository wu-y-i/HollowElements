import json
import re
import time
from runtime_acceptance import command, RESULTS, LOG


def position(value):
    return [float(n) for n in re.findall(r"-?\d+(?:\.\d+)?",value)]


def enemy(status,name):
    return next(e for e in status["enemies"] if name in e["name"])


def test(scene,match):
    command("invincible",value=True)
    command("goto",scene=scene,gate="door_dreamEnter")
    deadline=time.monotonic()+25
    while time.monotonic()<deadline:
        time.sleep(.4)
        status=command("status")
        if status["scene"]==scene and status["enemies"]:
            break
    else:
        raise TimeoutError(scene)
    print(scene,[(e["name"],e["hp"],e["position"],e["boss"]) for e in status["enemies"]],flush=True)
    command("ready")
    time.sleep(4)
    status=command("status")
    target=next(e for e in status["enemies"] if match.lower() in e["name"].lower())
    name=target["name"]
    assert target["boss"], ("boss not identified",target)
    # Native vulnerable hit confirms freeze/frost selection against the real component setup.
    command("enemyAura",target=name,element="Hydro")
    classified=command("hit",target=name,source="WhiteWave",element="Cryo",damage=1)
    if classified["before"]==classified["after"]:
        raise AssertionError(("boss is still invulnerable",classified,status))
    assert classified["state"]["FrozenUntil"]>0
    command("enemyControl",target=name,seconds=.001,hard=False)
    time.sleep(.8)
    command("enemyControl",target=name,seconds=3,hard=False)
    before=enemy(command("status"),name)
    started=time.monotonic()
    while True:
        time.sleep(.2)
        after=enemy(command("status"),name)
        if after["state"]["FrozenUntil"]==0 or time.monotonic()-started>1.9:
            break
    a=before["clock"]; b=after["clock"]
    physics=(b["AcceptedPhysics"]-a["AcceptedPhysics"])
    skipped=(b["SkippedPhysics"]-a["SkippedPhysics"])
    frames=b["AcceptedFrames"]-a["AcceptedFrames"]
    frame_skips=b["SkippedFrames"]-a["SkippedFrames"]
    anim_in=b["AnimationInput"]-a["AnimationInput"]
    anim_out=b["AnimationOutput"]-a["AnimationOutput"]
    assert physics+skipped>50 and frames+frame_skips>30
    assert abs(physics/(physics+skipped)-.9)<.03
    assert abs(frames/(frames+frame_skips)-.9)<.03
    assert anim_in>.5 and abs(anim_out/anim_in-.9)<.02
    time.sleep(1.5)
    recovered=enemy(command("status"),name)
    assert recovered["clock"]["scale"]==1
    command("capture")
    time.sleep(.2)
    screenshot=RESULTS/(scene+".png")
    screenshot.write_bytes((RESULTS.parents[1]/"local/game-session/game.png").read_bytes())
    result=dict(scene=scene,target=name,classified=classified,physicsRate=physics/(physics+skipped),fsmRate=frames/(frames+frame_skips),animationRate=anim_out/anim_in,before=before,after=after,recovered=recovered)
    print("PASS",scene,result["physicsRate"],result["fsmRate"],result["animationRate"],flush=True)
    return result


if __name__=="__main__":
    RESULTS.mkdir(parents=True,exist_ok=True)
    tests=[]
    try:
        tests.append(test("GG_False_Knight","False Knight"))
        tests.append(test("GG_Gruz_Mother","Giant Fly"))
        report=dict(passed=True,tests=tests,commands=LOG)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        (RESULTS/"boss-control.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
