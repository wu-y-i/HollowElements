import json
import time
from runtime_acceptance import command, RESULTS, LOG


def status(): return command("status")


def wait_until(predicate,timeout=30):
    deadline=time.monotonic()+timeout
    while time.monotonic()<deadline:
        r=status()
        if predicate(r): return r
        time.sleep(.2)
    raise TimeoutError("player acceptance condition")


def wait_game(seconds):
    now=status()["time"]
    return wait_until(lambda r:r["time"]>=now+seconds)


def reset():
    command("clear"); command("heal"); command("invincible",value=False)


def apply(aura,element,damage=1):
    command("playerAura",element=aura)
    assert command("hurt",element=element,damage=damage)
    return status()


if __name__=="__main__":
    tests=[]
    try:
        command("invincible",value=True)
        command("goto",scene="GG_Atrium",gate="door_dreamEnter")
        wait_until(lambda r:r["scene"]=="GG_Atrium")
        time.sleep(2)
        command("ready"); command("position",target="hero",x=111,y=14.4)
        for aura,element,damage,expected in [("Pyro","Cryo",1,2),("Pyro","Cryo",2,3),("Cryo","Pyro",2,4)]:
            reset(); before=status()["hero"]["health"]
            after=apply(aura,element,damage)
            assert before-after["hero"]["health"]==expected
            tests.append(dict(test="玩家增幅映射",base=damage,expected=expected,status=after))
            wait_game(1.5)
        reset(); overloaded=apply("Pyro","Electro")
        expiry=overloaded["hero"]["state"]["OverloadUntil"]
        wait_game(1.5); command("heal")
        result=apply("Pyro","Cryo",2)
        assert result["hero"]["health"]==5 and result["hero"]["state"]["OverloadUntil"]==expiry
        tests.append(dict(test="旧超载与逆向增幅取大，受伤不清除",status=result))
        wait_game(1.5); reset(); command("spellLevel",wave=2,dive=2,shriek=2)
        frozen=apply("Hydro","Cryo")
        expiry=frozen["hero"]["state"]["FrozenUntil"]
        assert set(frozen["hero"]["spellLevels"].values())=={1}
        assert set(frozen["hero"]["rawSpellLevels"].values())=={2}
        wait_game(1.4)
        measure=command("measurePlayer")
        assert abs(measure["moveRatio"]-.85)<.005,measure
        assert abs(measure["cooldown"]/(measure["baseCooldown"]/.75)-1)<.005,measure
        restored=wait_until(lambda r:r["time"]>expiry+.1)
        assert set(restored["hero"]["spellLevels"].values())=={2}
        tests.append(dict(test="冻结85%移速、75%攻速、只读法术降级和恢复",applied=frozen,measure=measure,restored=restored))
        reset(); sc=apply("Cryo","Electro")
        expiry=sc["hero"]["state"]["SuperconductUntil"]
        blocked=wait_game(1.5)
        assert not blocked["hero"]["canCast"] and not blocked["hero"]["canFocus"]
        assert blocked["hero"]["soul"]==99
        recovered=wait_until(lambda r:r["time"]>expiry+.1)
        assert recovered["hero"]["canCast"] and recovered["hero"]["canFocus"]
        tests.append(dict(test="超导封灵、保留灵魂与到期恢复",blocked=blocked,recovered=recovered))
        reset(); charged=apply("Hydro","Electro")
        assert charged["hero"]["state"]["ElectroCharged"]
        wait_game(1.5); assert command("hurt",element="None",damage=1)
        canceled=status(); assert canceled["hero"]["state"]["ElectroCharged"] is None
        later=wait_game(4.2); assert later["hero"]["health"]==canceled["hero"]["health"]
        tests.append(dict(test="外部真实伤害解除旧感电",canceled=canceled,later=later))
        report=dict(passed=True,tests=tests,commands=LOG)
        print("PASS",len(tests),"player runtime checks",flush=True)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        RESULTS.mkdir(exist_ok=True,parents=True)
        (RESULTS/"player-states.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
