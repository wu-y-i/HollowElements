import json
import time
from runtime_acceptance import command,RESULTS,SESSION,LOG

if __name__=="__main__":
    try:
        command("invincible",value=True)
        command("goto",scene="GG_Oblobbles",gate="door_dreamEnter")
        deadline=time.monotonic()+25
        while time.monotonic()<deadline:
            time.sleep(.4)
            s=command("status")
            if s["scene"]=="GG_Oblobbles" and len([e for e in s["enemies"] if e["boss"]])>=2: break
        else: raise TimeoutError("two native bosses")
        command("ready"); time.sleep(3)
        s=command("status"); bosses=[e for e in s["enemies"] if e["boss"]]
        assert len(bosses)>=2
        command("capture"); time.sleep(.2)
        (RESULTS/"multiple-boss-bars.png").write_bytes((SESSION/"game.png").read_bytes())
        report=dict(passed=True,bosses=bosses,status=s,commands=LOG)
        print("PASS",[(e["name"],e["hp"]) for e in bosses],flush=True)
    except Exception as error:
        report=dict(passed=False,error=repr(error),commands=LOG)
        raise
    finally:
        (RESULTS/"multiple-boss-bars.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
