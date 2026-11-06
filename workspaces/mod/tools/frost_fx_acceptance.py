import json,time
from runtime_acceptance import command,RESULTS,SESSION,LOG

def wait_scene(name=None):
    deadline=time.monotonic()+50
    while time.monotonic()<deadline:
        s=command('status')
        if s['hero'] and (s['scene']==name if name else s['scene']!='Menu_Title'):return
        time.sleep(.4)
    raise TimeoutError(name)

def expect(prefix):
    effects=command('presentationStatus')['effects']
    assert any(e['frame'].startswith(prefix+'_') for e in effects),(prefix,effects)
    return effects

tests=[]
try:
    if command('status')['scene']!='GG_Atrium':
        command('load',slot=3);wait_scene();time.sleep(5)
        command('ready');command('goto',scene='GG_Atrium',gate='door_dreamEnter');wait_scene('GG_Atrium');time.sleep(3)
    command('ready');command('position',target='hero',x=111,y=14.4)
    for recoilable,onset,loop,shatter in [(False,'frost','soft-frost-state','frost-shatter'),(True,'freeze','frost-state','shatter')]:
        command('clear');target=command('dummy',recoilable=recoilable,offset=4 if not recoilable else 7)
        command('enemyAura',target=target,element='Hydro')
        result=command('hit',target=target,source='WhiteWave',element='Cryo',damage=10)
        assert result['state']['HardFrozen']==recoilable
        tests.append(dict(onset=onset,effects=expect(onset)))
        # The default hard freeze lasts only one second. Extend the fixture in
        # memory so mailbox/capture latency cannot expire it before shattering.
        command('enemyControl',target=target,hard=recoilable,seconds=4)
        time.sleep(.8);tests.append(dict(loop=loop,effects=expect(loop)))
        command('capture');time.sleep(.25);(RESULTS/('fx-v3-'+onset+'.png')).write_bytes((SESSION/'game.png').read_bytes())
        command('hit',target=target,source='Nail',element='None',damage=10)
        tests.append(dict(shatter=shatter,effects=expect(shatter)))
        time.sleep(.8)
        assert not any(e['frame'].startswith((loop+'_',shatter+'_')) for e in command('presentationStatus')['effects'])
    report=dict(passed=True,tests=tests,commands=LOG)
    print('PASS soft frost and hard freeze: independent onset, loop, shatter and cleanup')
except Exception as error:
    report=dict(passed=False,error=repr(error),tests=tests,commands=LOG);raise
finally:
    (RESULTS/'fx-v3-runtime.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
