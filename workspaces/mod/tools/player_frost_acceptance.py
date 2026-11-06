import json,time
from runtime_acceptance import command,RESULTS,SESSION,LOG

def wait_scene(name=None):
    deadline=time.monotonic()+50
    while time.monotonic()<deadline:
        s=command('status')
        if s['hero'] and (s['scene']==name if name else s['scene']!='Menu_Title'):return
        time.sleep(.4)
    raise TimeoutError(name)

tests=[]
try:
    command('load',slot=3);wait_scene();time.sleep(5)
    command('ready');command('goto',scene='GG_Atrium',gate='door_dreamEnter');wait_scene('GG_Atrium');time.sleep(3)
    command('ready');command('position',target='hero',x=111,y=14.4)
    command('clear');command('heal');command('invincible',value=False);command('absoluteInvincible',value=False)
    command('playerAura',element='Hydro')
    for _ in range(30):
        if command('hurt',element='Cryo',damage=1):break
        time.sleep(.2)
    else:raise AssertionError('Native hurt not accepted')
    onset=command('presentationStatus')['effects']
    assert any(e['frame'].startswith('frost_') for e in onset),onset
    assert not any(e['frame'].startswith('freeze_') for e in onset),onset
    tests.append(dict(onset=onset))
    time.sleep(.65)
    loop=command('presentationStatus')['effects']
    assert any(e['frame'].startswith('soft-frost-state_') for e in loop),loop
    assert not any(e['frame'].startswith('frost-state_') for e in loop),loop
    hero=command('status')['hero'];assert 0<hero['state']['MoveScale']<1,hero
    tests.append(dict(loop=loop,moveScale=hero['state']['MoveScale'],spellLevels=hero['spellLevels']))
    command('capture');time.sleep(.3);(RESULTS/'fx-v4-player-frost.png').write_bytes((SESSION/'game.png').read_bytes())
    time.sleep(3)
    expired=command('presentationStatus')['effects']
    assert not any(e['frame'].startswith(('frost_','soft-frost-state_','freeze_','frost-state_')) for e in expired),expired
    report=dict(passed=True,tests=tests,expired=expired,commands=LOG)
    print('PASS player slowing uses frost onset and loop, no ice block, cleanup on expiry')
except Exception as error:
    report=dict(passed=False,error=repr(error),tests=tests,commands=LOG);raise
finally:
    (RESULTS/'fx-v4-runtime.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
