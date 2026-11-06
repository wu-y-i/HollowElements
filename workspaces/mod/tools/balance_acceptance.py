"""v0.1.5 native checks; requires the opt-in save-suppressed bridge."""
import argparse,hashlib,json,math,time,traceback
from pathlib import Path
from runtime_acceptance import command,ROOT,SESSION,LOG
from boss_fixes_acceptance import setup,hit,current,loss,vulnerable,state,objects,status
OUT=ROOT/'artifacts/acceptance/balance-v015';OUT.mkdir(exist_ok=True,parents=True)

def room():
    command('pause',value=False);command('invincible',value=True)
    if status()['scene']!='GG_Atrium':command('goto',scene='GG_Atrium');time.sleep(5)
    for _ in range(100):
        s=status()
        if s['scene']=='GG_Atrium' and s['hero']:break
        time.sleep(.2)
    time.sleep(.5);command('ready');command('position',target='hero',x=111,y=14.4)
    if not any(e['name'].startswith('反应木桩') for e in status()['enemies']):command('dummy',offset=2,recoilable=False)
    return next(e for e in status()['enemies'] if e['name'].startswith('反应木桩'))
def reset(b):command('clear');command('enemyHp',targetId=b['id'],value=1000000)
def aura(b,element,units=1):return command('enemyAura',targetId=b['id'],element=element,units=units)
def sc(b):
    reset(b);aura(b,'Cryo');r=hit(b,'Electro',32);assert r['state']['SuperconductStacks']==3,r
    return r
def capture(name):
    command('capture');time.sleep(.3);(OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())

def damage():
    b=room();results=[]
    for base,incoming,expected in [('None','None',14),('Pyro','Hydro',57),('Hydro','Pyro',36),('Cryo','Pyro',57),('Pyro','Cryo',36)]:
        reset(b)
        if base!='None':aura(b,base)
        r=hit(b,incoming,32);assert loss(r)==expected,(base,incoming,r)
        results.append(dict(base=base,incoming=incoming,result=r))
    reset(b);r=command('unclassifiedHit',targetId=b['id'],player=True,damage=32);assert loss(r)==14,r
    reset(b);r=command('unclassifiedHit',targetId=b['id'],player=False,damage=32);assert loss(r)==32,r
    reset(b);before=current(b)['hp'];command('nativeExtra',targetId=b['id'],damage=32,player=True);assert before-current(b)['hp']==14
    reset(b);before=current(b)['hp'];command('nativeExtra',targetId=b['id'],damage=32);assert before-current(b)['hp']==32
    reset(b);aura(b,'Pyro');r=hit(b,'Electro',32);nail=status()['hero']['nail'];assert loss(r)==14+nail,r
    reset(b);aura(b,'Hydro');r=hit(b,'Electro',32);assert r['state']['ElectroCharged']['Damage']==math.floor(nail*.45),r
    return dict(cases=results,nail=nail,auxiliary=14,nonPlayer=32,passive=14,overload=nail,electro=math.floor(nail*.45))

def stacks():
    b=room();created=sc(b);nail=status()['hero']['nail'];bonus=math.floor(nail*.5*1.5);rows=[]
    command('soul',value=0)
    for index in range(3):
        time.sleep(.23);r=hit(b,'None',32,'Nail');assert loss(r)==14+bonus,r
        assert r['state']['SuperconductStacks']==2-index,r
        rows.append(r)
    assert rows[-1]['state']['SuperconductUntil']==0
    sc(b);before=current(b)['state'];r=command('hit',targetId=b['id'],source='Nail',element='None',damage=32,multiplier=0)
    assert loss(r)==0 and r['state']['SuperconductStacks']==3,r
    # Pause does not advance the three-second combat clock.
    command('pause',value=True);t=status()['time'];time.sleep(1);assert abs(status()['time']-t)<.001
    command('pause',value=False);time.sleep(3.2);assert current(b)['state']['SuperconductStacks']==0
    return dict(created=created,consumed=rows,neutralBonus=bonus)

def limits():
    b=room();rows=[]
    for source in ['WhiteShriek','BlackShriek','WhiteWave','BlackWave']:
        reset(b);counts=[]
        for i in range(4):
            r=hit(b,'Pyro',32,source,cast=8000);budget=next(v for k,v in r['state']['Budgets'].items() if k.startswith('A/'))
            counts.append(budget['Attachments'])
        assert counts==([1,1,2,2] if source=='BlackShriek' else [1,1,1,1]),(source,counts)
        rows.append(dict(source=source,counts=counts))
    reset(b)
    a=hit(b,'Cryo',32,'BlackDiveBody',cast=8001);z=hit(b,'Cryo',32,'BlackDiveShock',cast=8001);repeat=hit(b,'Cryo',32,'BlackDiveShock',cast=8001)
    assert sum(v['Attachments'] for k,v in repeat['state']['Budgets'].items() if k.startswith('A/'))==2,repeat
    reset(b);r=hit(b,'Cryo',32,'BlackDiveShock',cast=8002);r=hit(b,'Cryo',32,'BlackDiveShock',cast=8002)
    assert sum(v['Attachments'] for k,v in r['state']['Budgets'].items() if k.startswith('A/'))==1,r
    return dict(shriek=rows,blackDive=repeat,shockOnly=r)

def hud():
    b=room();sc(b);snapshots=[]
    for count in [3,2,1]:
        command('pause',value=True);d=command('fixDiagnostics')['ui'];bar=next(x for x in d['bars'] if x['id']==b['id'])
        assert str(count) in bar['icons'],(count,bar)
        capture('stacks-'+str(count));snapshots.append(bar)
        command('pause',value=False);time.sleep(.23);hit(b,'None',32,'Nail')
    command('pause',value=True);assert '超导' not in next(x for x in command('fixDiagnostics')['ui']['bars'] if x['id']==b['id'])['icons']
    command('pause',value=False);return snapshots

def boss_boundaries():
    b=setup('GG_Watcher_Knights','Black Knight 1');command('pause',value=True)
    aura(b,'Cryo');hit(b,'Electro',1);r=hit(b,'None',32)
    assert loss(r)==21 and r['state']['SuperconductStacks']==0,r
    assert abs(r['state']['SuperconductDuration']-4)<.001,r
    b=setup('GG_Failed_Champion','False Knight Dream');command('pause',value=True)
    from boss_26_42_acceptance import active
    head=next(o for o in objects() if o['path'].endswith('/Head') and 'HealthManager' in o['components']);active(head)
    h=next(e for e in status()['enemies'] if e['objectId']==head['id']);aura(h,'Cryo');state(b,'FalseyControl','Stun Fail');vulnerable(b)
    hit(b,'Electro',1);r=hit(b,'Cryo',32);assert loss(r)==0 and r['state']['SuperconductStacks']==3,r
    return dict(watcherExclusive=True,immuneDoesNotConsume=r)

def share():
    b=setup('GG_God_Tamer','Lobster');command('pause',value=True)
    other=next(e for e in status()['enemies'] if e['name']=='Lancer');command('position',targetId=other['id'],x=80,y=8)
    command('enemyHp',targetId=b['id'],value=5000);command('enemyHp',targetId=other['id'],value=5000)
    hit(b,'Pyro',1);before=current(other)['hp'];r=hit(b,'Hydro',32);assert loss(r)==57 and before-current(other)['hp']==57,r
    hit(b,'Pyro',1);before=current(other)['hp'];r=hit(b,'Electro',1);nail=status()['hero']['nail'];assert before-current(other)['hp']==nail,(r,current(other))
    return dict(amplified=57,mirroredOverload=nail)

def collector():
    b=setup('GG_Collector','Jar Collector');command('pause',value=True);command('enemyHp',targetId=b['id'],value=5000)
    command('position',targetId=b['id'],x=34,y=14);state(b,'Control','Spawn');command('pause',value=False)
    for _ in range(30):
        bugs=[e for e in status()['enemies'] if e['mechanic'] and e['mechanic']['kind']=='CollectorSummon']
        if bugs:break
        time.sleep(.15)
    command('pause',value=True);assert bugs
    before=current(b)['hp'];maximum=bugs[0]['hp'];command('nativeExtra',targetId=bugs[0]['id'],damage=10000)
    assert before-current(b)['hp']==maximum,(before,current(b),maximum)
    initial=current(b)['mechanic']['summons'];aura(b,'Cryo');vulnerable(b);hit(b,'Electro',1)
    time.sleep(.25);vulnerable(b);r=hit(b,'None',32,'Nail');time.sleep(.2)
    assert current(b)['mechanic']['summons']==initial,current(b)
    return dict(reflection=maximum,superconductDoesNotCountAsReaction=r)

def native_spells():
    from native_attacks_acceptance import sample
    b=room();command('loadout',Nail='Hydro',Wave='Pyro',Dive='Cryo',Shriek='Electro',GreatSlash='Pyro',DashSlash='Hydro',Cyclone='Electro')
    rows=[]
    for level in [1,2]:
        command('spellLevel',wave=level,dive=level,shriek=level)
        prefix='White' if level==1 else 'Black'
        rows.append(sample(prefix+'Shriek','Spell Control','Has Scream?',x=111,y=17,wait=1))
        data=next(e for e in rows[-1]['status']['enemies'] if e['id']==b['id'])['state']['Budgets']
        applications=sum(v['Attachments'] for k,v in data.items() if k.startswith('A/'))
        assert applications==(1 if level==1 else 2),(prefix,data)
        rows.append(sample(prefix+'DiveBody','Spell Control','Has Quake?',x=111,y=14.4,hero_y=19,wait=1.5))
        data=next(e for e in rows[-1]['status']['enemies'] if e['id']==b['id'])['state']['Budgets']
        applications=sum(v['Attachments'] for k,v in data.items() if k.startswith('A/'))
        assert applications==(1 if level==1 else 2),(prefix,data)
    return rows

CASES={name:globals()[name] for name in ['damage','stacks','limits','hud','boss_boundaries','share','collector','native_spells']}
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('cases',nargs='*');args=parser.parse_args()
    if not status()['hero']:
        command('load',slot=3)
        for _ in range(100):
            if status()['hero']:break
            time.sleep(.3)
        time.sleep(4);command('ready')
    for name in args.cases or CASES:
        try:r=dict(passed=True,evidence=CASES[name]());print('PASS',name,flush=True)
        except Exception as error:r=dict(passed=False,error=repr(error));traceback.print_exc()
        r.update(dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(),commands=LOG[:]);LOG.clear()
        (OUT/(name+'.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
