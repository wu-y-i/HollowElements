"""Real game integration checks through the opt-in development bridge.

Run in a stable Godhome room, with no live boss and no charm blocking damage.
The game has already backed up saves and suppresses save writes in this session.
"""
import json
import os
from pathlib import Path
import time
import uuid

ROOT = Path(__file__).resolve().parents[3]
SESSION = ROOT / "local/game-session"
RESULTS = ROOT / "artifacts/acceptance"
LOG = []


def command(command_name, **values):
    request = dict(command=command_name, id=uuid.uuid4().hex, **values)
    tmp = SESSION / "command.tmp"
    tmp.write_text(json.dumps(request, ensure_ascii=False), encoding="utf-8")
    os.replace(tmp, SESSION / "command.json")
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        time.sleep(.05)
        try:
            response = json.loads((SESSION / "response.json").read_text(encoding="utf-8-sig"))
        except (FileNotFoundError, json.JSONDecodeError):
            continue
        if response.get("id") != request["id"]:
            continue
        LOG.append(dict(request=request, response=response))
        assert response["ok"], response
        return response["result"]
    raise TimeoutError(command_name)


def aura(element, units=1):
    return command("enemyAura", element=element, units=units)


def hit(element, source="WhiteWave", cast=None, damage=10):
    values = dict(element=element, source=source, damage=damage)
    if cast is not None:
        values["cast"] = cast
    return command("hit", **values)


def loss(result):
    return result["before"] - result["after"]


def reset():
    command("clear")


def run():
    results = []
    status = command("status")
    assert status["scene"].startswith("GG_")
    if not any("反应木桩" in e["name"] for e in status["enemies"]):
        command("dummy", recoilable=True, offset=4)
    reset(); r=command("hit",source="Nail",element="Hydro",damage=10,multiplier=0)
    assert loss(r)==0 and r["state"]["Aura"]==0 and not r["state"]["Icd"]
    results.append("PASS 被归零的伤害不改写伤害、不附着、不推进计数")
    reset(); aura("Pyro"); r=hit("Hydro"); assert loss(r)==20 and r["state"]["Aura"]==0
    results.append("PASS 真实有效命中：正向蒸发10→20")
    reset(); aura("Hydro",2); r=hit("Pyro"); assert loss(r)==15 and r["state"]["AuraUnits"]==1.5
    results.append("PASS 逆向蒸发10→15，附着消耗0.5U")
    reset(); r=hit("Hydro",cast=100); r=hit("Pyro",cast=100)
    assert loss(r)==10 and r["state"]["Aura"]==2
    results.append("PASS 同次法术施放：第二段不重复附着/增幅")
    reset(); r=hit("Hydro","Nail",cast=201); start=r["state"]["AuraUntil"]
    hit("Hydro","Nail",cast=202); r=hit("Hydro","Nail",cast=203)
    assert r["state"]["AuraUntil"]==start
    r=hit("Hydro","Nail",cast=204); assert r["state"]["AuraUntil"]>start
    results.append("PASS 普攻前三次计数，第4次重新附着")
    reset(); aura("Cryo"); hit("Electro"); r=hit("None","BlackShriek",cast=301,damage=20)
    assert loss(r)==22
    r=hit("None","BlackShriek",cast=301,damage=20); assert loss(r)==20
    results.append("PASS 超导对吼独立系数与单次施放预算")
    reset(); aura("Hydro"); r=hit("Cryo"); assert r["state"]["HardFrozen"]
    r=hit("Pyro","GreatSlash"); assert loss(r)==41 and r["state"]["Aura"]==0 and r["state"]["FrozenUntil"]==0
    results.append("PASS 元素剑技优先碎冰：10+31，清除附着和控制")
    reset(); aura("Hydro"); r=hit("Electro"); initial=r["after"]; assert r["state"]["ElectroCharged"]["RemainingTicks"]==6
    time.sleep(6.5)
    enemy=next(e for e in command("status")["enemies"] if "反应木桩" in e["name"])
    assert enemy["hp"]==initial-30 and enemy["state"]["ElectroCharged"] is None
    results.append("PASS 敌人感电六跳，每跳5伤，不漏末跳")
    command("heal"); command("playerAura",element="Hydro"); assert command("hurt",element="Electro",damage=1)
    initial=command("status")["hero"]["health"]
    # Native hit-stop slows scaled game time. Wall-clock sleep is not a valid 12-second assertion.
    deadline=time.monotonic()+30
    while True:
        time.sleep(.35)
        player=command("status")["hero"]
        if player["state"]["ElectroCharged"] is None or time.monotonic()>deadline:
            break
    assert player["health"]==initial-3 and player["state"]["ElectroCharged"] is None
    results.append("PASS 玩家感电4/8/12秒三跳")
    for result in results:
        print(result, flush=True)
    return results


if __name__=="__main__":
    RESULTS.mkdir(parents=True,exist_ok=True)
    try:
        tests=run()
        report=dict(passed=True,tests=tests,commands=LOG)
    except Exception as error:
        report=dict(passed=False,error=repr(error),commands=LOG)
        raise
    finally:
        (RESULTS/"runtime-reactions.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
