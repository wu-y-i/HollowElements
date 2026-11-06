import json
import time
from runtime_acceptance import command,RESULTS,ROOT,SESSION,LOG


def ui(): return command("menuStatus")
def click(label): return command("menuClick",index=ui()["buttons"].index(label))


if __name__=="__main__":
    tests=[]
    try:
        command("load",slot=3)
        deadline=time.monotonic()+25
        while time.monotonic()<deadline:
            time.sleep(.4)
            s=command("status")
            if s["hero"] and s["hero"]["atBench"] and s["scene"]!="Menu_Title": break
        else: raise TimeoutError("native bench load")
        time.sleep(2)
        assert command("loadoutShortcut")["handled"]
        initial=ui(); assert initial["open"] and initial["captures"] and initial["pausedBenches"]>0
        assert initial['title']=='\u5143\u7d20\u914d\u7f6e' and len(initial['buttons'])==len(initial['slots'])
        command("menuNavigate",horizontal=1)
        draft=ui(); assert draft["buttons"][0]!=initial["buttons"][0]
        command("capture"); time.sleep(.5); RESULTS.mkdir(parents=True,exist_ok=True)
        (RESULTS/"element-config-autosave.png").write_bytes((SESSION/"game.png").read_bytes())
        persisted=command("closeReloadConfig",slot=3,via="cancel")
        assert persisted["saved"] and persisted["loaded"] and persisted["matched"]
        (RESULTS/'loadout-persistence.json').write_text(json.dumps(dict(ok=True,result=persisted),indent=2),encoding='utf-8')
        changed=ui(); assert changed["loadout"]["Nail"]==(initial["loadout"]["Nail"]+1)%5
        closed=ui()
        assert not closed["open"] and not closed["captures"] and closed["pausedBenches"]==0
        assert command("status")["timeScale"]==1
        tests.append(dict(test="长椅、手柄共享导航、保存读取及关闭输入占用",initial=initial,changed=changed,persistence=persisted,closed=closed))

        command('goto',scene='GG_Atrium',gate='door_dreamEnter')
        for _ in range(100):
            s=command('status')
            if s['scene']=='GG_Atrium': break
            time.sleep(.2)
        time.sleep(2); command('ready'); time.sleep(1)
        command('debugMode',value=False)
        denied=command('loadoutShortcut'); assert not denied['handled'] and not denied['menu']['open']
        command('debugMode',value=True)
        allowed=command('loadoutShortcut'); assert allowed['handled'] and allowed['menu']['open']
        time.sleep(.3); assert ui()['open'] and ui()['pausedBenches']==0
        command('menuNavigate',horizontal=1)
        shortcut_save=command('closeReloadConfig',slot=3,via='shortcut')
        assert shortcut_save['saved'] and shortcut_save['loaded'] and shortcut_save['matched']
        assert not ui()['open'] and not ui()['captures']
        assert command('loadoutShortcut')['handled']
        command('menuNavigate',vertical=1,horizontal=1)
        pause_save=command('closeReloadConfig',slot=3,via='pause')
        assert pause_save['saved'] and pause_save['loaded'] and pause_save['matched']
        assert not ui()['open'] and command('status')['timeScale']==1
        tests.append(dict(test='Debug E shortcut outside bench; normal mode blocked; E and native pause autosave/reload',denied=denied,allowed=allowed,shortcut=shortcut_save,pause=pause_save))
        command("menu",debug=True)
        default=json.loads((ROOT/"workspaces/mod/balance.defaults.json").read_text(encoding="utf-8-sig"))
        default["Reactions"]["ForwardMultiplier"]=2.2
        command("menuJson",text=json.dumps(default))
        click("应用"); applied=ui(); assert applied["config"]["Reactions"]["ForwardMultiplier"]==2.2
        command("menuJson",text="{broken")
        click("应用"); rejected=ui(); assert rejected["config"]["Reactions"]["ForwardMultiplier"]==2.2 and "无效" in rejected["status"]
        click("重置"); click("应用"); click("保存")
        assert ui()["config"]["Reactions"]["ForwardMultiplier"]==2
        default["Reactions"]["ForwardMultiplier"]=3
        command("menuJson",text=json.dumps(default)); click("应用")
        click("重载"); reloaded=ui(); assert reloaded["config"]["Reactions"]["ForwardMultiplier"]==2
        command("capture"); time.sleep(.15)
        (RESULTS/"debug-workshop.png").write_bytes((SESSION/"game.png").read_bytes())
        command("menuNavigate",cancel=True)
        tests.append(dict(test="真实面板参数应用、坏JSON保留快照、保存、重置和重载",applied=applied,rejected=rejected,reloaded=reloaded))
        report=dict(passed=True,version='0.1.3',tests=tests,commands=LOG)
        print("PASS bench/menu persistence, navigation, input capture and JSON controls",flush=True)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        RESULTS.mkdir(parents=True,exist_ok=True)
        (RESULTS/"ui-workshop.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
