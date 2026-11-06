"""Native checks for the three v0.1.1 presentation changes; save writes stay isolated."""
import json
import math
import time
from runtime_acceptance import command, RESULTS, SESSION, LOG


def wait(predicate, timeout=25):
    deadline=time.monotonic()+timeout
    while time.monotonic()<deadline:
        state=command('status')
        if predicate(state): return state
        time.sleep(.15)
    raise TimeoutError('presentation acceptance state')


def presentation():
    time.sleep(.12)
    return command('presentationStatus')


def capture(name):
    command('capture'); time.sleep(.5)
    (RESULTS/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())


def reaction_capture(name, since):
    path=SESSION/'reaction.png'
    deadline=time.monotonic()+3
    while not path.exists() or path.stat().st_mtime<since:
        if time.monotonic()>deadline: raise TimeoutError('reaction screenshot')
        time.sleep(.05)
    (RESULTS/(name+'.png')).write_bytes(path.read_bytes())


if __name__=='__main__':
    tests=[]
    try:
        command('load',slot=3)
        wait(lambda s:s['hero'] and s['hero']['atBench'] and s['scene']!='Menu_Title')
        time.sleep(1)
        command('spellLevel',wave=2,dive=2,shriek=2); command('artUnlocks',great=True,dash=True,cyclone=True)
        command('menu',debug=False)
        black=command('menuStatus')
        assert black['slots']==['Nail','Wave','Dive','Shriek','GreatSlash','DashSlash','Cyclone']
        assert [len(group) for group in black['icons']]==[1,2,2,2,1,1,1]
        assert all('_level2' not in group[0] and '_level2' in group[1] for group in black['icons'][1:4])
        assert [group[0] for group in black['icons'][4:]]==['charge_slash_effect0002','dash_slash_effect0002','cyclone_slash_effect0004']
        assert all(black['buttons'][i].startswith('\u2039') for i in range(7))
        focus=command('menuNavigate',vertical=4)
        assert focus['selected']==4 and '\u5f3a\u529b\u65a9' in focus['status']
        command('menuNavigate',vertical=-4)
        capture('loadout-native-icons')
        command('menuNavigate',cancel=True)
        command('spellLevel',wave=1,dive=1,shriek=1)
        command('menu',debug=False); white=command('menuStatus')
        assert all(len(group)==1 and group[0] and '_level2' not in group[0] for group in white['icons'][1:4])
        assert white['icons'][4:]==black['icons'][4:]
        command('menuNavigate',cancel=True); command('spellLevel',wave=2,dive=2,shriek=2)
        tests.append(dict(test='Native icons, white/black side by side, distinctive sword-art frames and focus label',black=black['icons'],white=white['icons']))

        command('spellLevel',wave=0,dive=1,shriek=0); command('artUnlocks',great=False,dash=True,cyclone=False)
        command('menu',debug=False); partial=command('menuStatus')
        assert partial['slots']==['Nail','Dive','DashSlash'] and len(partial['buttons'])==3
        changed=command('menuNavigate',vertical=1,horizontal=1)
        assert changed['selected']==1 and '\u7838\u5730' in changed['status']
        capture('loadout-partial-unlocks'); command('menuNavigate',cancel=True); applied=command('menuStatus')
        assert applied['loadout']['Dive']==(partial['loadout']['Dive']+1)%5
        assert all(applied['loadout'][slot]==partial['loadout'][slot] for slot in partial['loadout'] if slot!='Dive')
        command('spellLevel',wave=0,dive=0,shriek=0); command('artUnlocks',great=False,dash=False,cyclone=False)
        command('menu',debug=False); starter=command('menuStatus')
        assert starter['slots']==['Nail'] and len(starter['buttons'])==1
        command('menuNavigate',vertical=1,horizontal=1)
        assert command('menuStatus')['selected']==0
        command('menuNavigate',cancel=True)
        command('spellLevel',wave=2,dive=2,shriek=2); command('artUnlocks',great=True,dash=True,cyclone=True)
        tests.append(dict(test='Locked rows hidden, sparse controller mapping edits only the intended slot, nail-only loadout',partial=partial['slots'],applied=applied['loadout'],starter=starter['slots']))

        initial=presentation()['settings']
        assert initial['ReactionFxScale']==1.8 and initial['ShowHealthNumbers'] is False
        changed=command('menuSetting',name='\u53cd\u5e94\u7279\u6548\u5927\u5c0f',index=3)
        assert math.isclose(changed['settings']['ReactionFxScale'],2.2,abs_tol=.001)
        command('menuSetting',name='\u53cd\u5e94\u7279\u6548\u5927\u5c0f',index=2)
        command('goto',scene='GG_Atrium',gate='door_dreamEnter')
        wait(lambda s:s['scene']=='GG_Atrium'); time.sleep(2)
        command('ready'); command('position',target='hero',x=111,y=14.4)
        command('dummy',recoilable=False,offset=4)
        hidden=presentation(); assert hidden['numbers'] and not any(n['visible'] for n in hidden['numbers'])
        command('menuSetting',name='\u8840\u6761\u663e\u793a\u751f\u547d\u6570\u503c',index=1)
        command('hit',element='None',source='Nail',damage=10)
        shown=presentation()
        assert any(n['visible'] and n['text']=='999990 / 1000000' and n['height']==26 for n in shown['numbers'])
        capture('health-numbers-mob')
        tests.append(dict(test='Health numbers off/on and live HP on a real HealthManager hit',hidden=hidden['numbers'],shown=shown['numbers']))

        command('clear'); command('enemyAura',element='Pyro')
        stamp=time.time(); command('hit',element='Hydro',source='WhiteWave',damage=10,capture=True,delay=.1)
        steam=presentation()
        assert any(math.isclose(e['scale'],2.7,abs_tol=.001) for e in steam['effects']),steam
        reaction_capture('vaporize-large',stamp)
        command('clear'); command('enemyAura',element='Pyro')
        stamp=time.time(); command('hit',element='Electro',source='WhiteWave',damage=10,capture=True,delay=.1)
        explosion=presentation()
        assert any(math.isclose(e['scale'],1.8,abs_tol=.001) for e in explosion['effects']),explosion
        reaction_capture('overload-large',stamp)
        command('clear'); command('invincible',value=False); command('heal')
        command('playerAura',element='Pyro'); command('hurt',element='Hydro',damage=1)
        player=presentation()
        assert any(math.isclose(e['scale'],1.755,abs_tol=.001) for e in player['effects']),player
        tests.append(dict(test='Larger reaction effects and steam; independent player scale preserved',steam=steam['effects'],overload=explosion['effects'],player=player['effects']))

        command('clear'); command('invincible',value=True)
        command('goto',scene='GG_Gruz_Mother',gate='door_dreamEnter')
        wait(lambda s:s['scene']=='GG_Gruz_Mother' and s['enemies']); time.sleep(3)
        command('ready'); command('enemyControl',target='Giant Fly',hard=True,seconds=10)
        boss=command('status'); target=next(e for e in boss['enemies'] if e['name']=='Giant Fly')
        bars=presentation()
        assert any(n['visible'] and n['text']==f"{target['hp']} / {target['hp']}" and n['width']==680 for n in bars['numbers'])
        capture('health-numbers-boss')
        command('menuSetting',name='\u8840\u6761\u663e\u793a\u751f\u547d\u6570\u503c',index=0)
        off=presentation(); assert not any(n['visible'] for n in off['numbers']) and all(n['height']==9 for n in off['numbers'])
        tests.append(dict(test='Boss centered current/max HP and return to compact bars when disabled',shown=bars['numbers'],off=off['numbers']))
        report=dict(passed=True,version='0.1.1',tests=tests,commands=LOG)
        print('PASS',len(tests),'native presentation checks',flush=True)
    except Exception as error:
        report=dict(passed=False,version='0.1.1',error=repr(error),tests=tests,commands=LOG)
        raise
    finally:
        RESULTS.mkdir(parents=True,exist_ok=True)
        (RESULTS/'presentation-update.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
