import json,time
from runtime_acceptance import command,RESULTS,SESSION,LOG

def wait_scene(name):
    for _ in range(120):
        s=command('status')
        if s['scene']==name and s['hero']: return
        time.sleep(.2)
    raise TimeoutError(name)

tests=[]
try:
    # Reuse the already loaded, save-suppressed native session. Loading a save
    # and immediately warping can race the game's asynchronous transition.
    status=command('status')
    assert status['hero'], 'Load the development save before running FX checks'
    if status['scene']!='GG_Atrium':
        command('ready');command('goto',scene='GG_Atrium',gate='door_dreamEnter')
    wait_scene('GG_Atrium');time.sleep(3)
    command('ready');command('position',target='hero',x=111,y=14.4);time.sleep(2)
    for base,element,name in [('Pyro','Hydro','vaporize'),('Cryo','Pyro','melt'),('Pyro','Electro','overload'),('Hydro','Cryo','freeze'),('Cryo','Electro','superconduct'),('Hydro','Electro','electro-charged')]:
        command('clear');command('heal');command('invincible',value=False);command('absoluteInvincible',value=False);command('playerAura',element=base)
        for attempt in range(30):
            if command('hurt',element=element,damage=1): break
            time.sleep(.2)
        else: raise AssertionError('hurt rejected '+name)
        expected='frost' if name=='freeze' else name
        p=command('presentationStatus');assert any(e['frame'].startswith(expected+'_') for e in p['effects']),(name,p['effects'])
        command('capture');time.sleep(.3);(RESULTS/('fx-v2-player-'+name+'.png')).write_bytes((SESSION/'game.png').read_bytes())
        tests.append(dict(playerReaction=name,effects=p['effects']))
        if name in ('freeze','superconduct'):
            time.sleep(1.3);loop=command('presentationStatus')['effects']
            prefix='soft-frost-state_' if name=='freeze' else 'superconduct-state_'
            assert any(e['frame'].startswith(prefix) for e in loop),loop
            tests.append(dict(playerState=prefix,effects=loop))
        if name=='electro-charged':
            deadline=time.monotonic()+10
            while time.monotonic()<deadline:
                p=command('presentationStatus')
                if any(e['frame'].startswith('electro-state_') for e in p['effects']): break
                time.sleep(.15)
            else: raise AssertionError('player electro tick missing')
            tests.append(dict(playerReaction='electro-tick',effects=p['effects']))
        time.sleep(1.6)
    command('clear');command('dummy',recoilable=False,offset=4);command('enemyControl',hard=True,seconds=10);time.sleep(.2)
    small=command('presentationStatus')['effects'];assert small
    command('clear');command('invincible',value=True);command('goto',scene='GG_Gruz_Mother',gate='door_dreamEnter');wait_scene('GG_Gruz_Mother');time.sleep(4);command('ready')
    command('enemyControl',target='Giant Fly',hard=True,seconds=10);time.sleep(.2)
    large=command('presentationStatus')['effects'];assert large and max(e['scale'] for e in large)>max(e['scale'] for e in small),(small,large)
    command('capture');time.sleep(.3);(RESULTS/'fx-v2-large-boss.png').write_bytes((SESSION/'game.png').read_bytes())
    report=dict(passed=True,tests=tests,small=small,large=large,commands=LOG)
    print('PASS player reaction onsets, state loops, actual electro pulse and body scaling.')
except Exception as error:
    report=dict(passed=False,error=repr(error),tests=tests,commands=LOG)
    raise
finally:
    (RESULTS/'fx-v2-runtime.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
