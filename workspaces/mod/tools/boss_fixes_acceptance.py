"""2026-10-24 native integration checks; development saves are suppressed."""
import argparse,hashlib,json,time,traceback
from pathlib import Path
from runtime_acceptance import command,ROOT,SESSION,LOG
from boss_26_42_acceptance import setup,hit,state,current,loss,objects,active,vulnerable
OUT=ROOT/'artifacts/acceptance/boss-fixes';OUT.mkdir(parents=True,exist_ok=True)

def status():return command('status')
def pause():command('pause',value=True)
def overload(b):
    vulnerable(b);command('enemyAura',targetId=b['id'],element='Pyro');return hit(b,'Electro',1)
def shot(prefix):return [p for p in command('projectiles') if p['active'] and p['name'].startswith(prefix)]
def save_capture(name):
    command('capture');time.sleep(.3);(OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())

def hornet():
    results=[]
    for scene,name in [('GG_Hornet_1','Hornet Boss 1'),('GG_Hornet_2','Hornet Boss 2')]:
        b=setup(scene,name);pause();command('enemyHp',targetId=b['id'],value=10000)
        values=[loss(hit(b,'Pyro',64)) for _ in range(7)]
        assert values==[64,32,16,8,4,2,1],values
        r=hit(b,'Hydro',32);assert loss(r)==64,r
        assert current(b)['mechanic']['stacks']==1
        r=hit(b,'Pyro',64);assert loss(r)==70,r
        for _ in range(12):
            command('enemyAura',targetId=b['id'],element='Hydro');hit(b,'Pyro',1)
        expected=1.5 if scene.endswith('_2') else 2
        assert abs(current(b)['mechanic']['tempo']-expected)<.001,current(b)
        results.append(dict(scene=scene,damage=values,mechanic=current(b)['mechanic']))
    return results

def shield():
    results=[]
    for scene,n in [('GG_Crystal_Guardian',1),('GG_Crystal_Guardian_2',2)]:
        b=setup(scene,'Zombie Beam Miner Rematch' if scene.endswith('_2') else 'Mega Zombie Beam Miner (1)');pause()
        for i in range(n):
            r=overload(b);assert loss(r)==0,r
            assert current(b)['mechanic']['shield']==(i<n-1),current(b)
        results.append(dict(scene=scene,mechanic=current(b)['mechanic']))
    return results

def shell():
    results=[]
    for scene in ['GG_False_Knight','GG_Failed_Champion']:
        b=setup(scene,'False Knight Dream' if scene=='GG_Failed_Champion' else 'False Knight New');pause()
        head=next(o for o in objects() if o['path'].split('/')[-1]=='Head' and 'HealthManager' in o['components']);active(head)
        h=next(e for e in status()['enemies'] if e['objectId']==head['id'])
        command('enemyAura',targetId=h['id'],element='Cryo');state(b,'FalseyControl','Stun Fail');vulnerable(b)
        command('enemyHp',targetId=b['id'],value=5000)
        immune=hit(b,'Cryo',40);assert loss(immune)==0,immune
        reacted=hit(b,'Pyro',20);assert loss(reacted)==40,reacted
        results.append(dict(scene=scene,immune=immune,reacted=reacted))
    return results

def oblob():
    setup('GG_Oblobbles','Mega Fat Bee');pause();results=[]
    for b in [e for e in status()['enemies'] if e['mechanic'] and e['mechanic']['kind']=='Oblobble']:
        command('clear');command('heal');command('ready');command('invincible',value=False)
        r=command('contactHit',targetId=b['id'],damage=1)
        assert r['player']['Aura']==b['mechanic']['aura'],r
        results.append(r);command('invincible',value=True);command('pause',value=False);time.sleep(1.7);pause()
    return results

def moss():
    b=setup('GG_Mega_Moss_Charger','Mega Moss Charger');pause();vulnerable(b)
    state(b,'Mossy Control','Hit Left');v=status()['enemies'];velocity=current(b)['velocity']
    assert abs(float(velocity.strip('()').split(',')[0]))<=5.1,velocity
    # Boss frost no longer increases its outgoing contact damage.
    command('enemyControl',targetId=b['id'],hard=False,seconds=10)
    command('clear');command('heal');command('invincible',value=False)
    r=command('contactHit',targetId=b['id'],damage=1)
    assert r['before']-r['after']==1,r
    command('pause',value=False);command('invincible',value=True);time.sleep(1.8);pause();command('ready');command('clear');command('heal')
    command('playerAura',element='Hydro');command('invincible',value=False)
    for _ in range(20):
        if command('hurt',element='Cryo',damage=1):break
        command('pause',value=False);time.sleep(.2);pause()
    command('pause',value=False);command('invincible',value=True);time.sleep(1.6);pause();command('ready');command('invincible',value=False)
    r=command('contactHit',targetId=b['id'],damage=1)
    assert r['before']-r['after']==2,r
    command('invincible',value=True);return dict(recoil=velocity,playerFrostContact=r)

def share():
    b=setup('GG_God_Tamer','Lobster');pause();command('enemyHp',target='Lobster',value=5000);command('enemyHp',target='Lancer',value=5000)
    other=next(e for e in status()['enemies'] if e['name']=='Lancer')
    command('position',targetId=other['id'],x=80,y=8)
    hit(b,'Pyro',1);start=current(other)['hp'];r=hit(b,'Hydro',20)
    assert loss(r)==40 and start-current(other)['hp']==40,(r,current(other))
    hit(b,'Pyro',1);start=current(other)['hp'];r=hit(b,'Electro',1)
    source_loss=loss(r)-1;mirrored=start-current(other)['hp']
    # The area blast may also legitimately hit the second actor; park it outside the radius.
    assert mirrored==source_loss,(source_loss,mirrored)
    return dict(amplified=40,secondary=source_loss,mirrored=mirrored)

def collector():
    b=setup('GG_Collector','Jar Collector');pause();command('enemyHp',targetId=b['id'],value=5)
    # Trigger native summon deaths, not an injected player hit.
    state(b,'Control','Spawn');command('pause',value=False);time.sleep(1);pause()
    summons=[e for e in status()['enemies'] if e['mechanic'] and e['mechanic']['kind']=='CollectorSummon']
    assert summons,'No native collector summons'
    for bug in summons:command('nativeExtra',targetId=bug['id'],damage=10000)
    assert current(b)['hp']==1 and not current(b)['isDead'],current(b)
    vulnerable(b);hit(b,'Pyro',20);assert current(b)['hp']<=0 or current(b)['isDead']
    return dict(summons=len(summons),nonlethal=True)

def grimm():
    b=setup('GG_Grimm_Nightmare','Nightmare Grimm Boss');pause();results=[]
    for aura,level,expected in [('None',0,2),('Pyro',0,3),('None',1,4),('Pyro',1,6)]:
        command('pause',value=False);command('invincible',value=True);time.sleep(1.7);command('ready');pause()
        command('clear');command('heal');command('bossLevel',value=level)
        if aura!='None':command('playerAura',element=aura)
        for _ in range(30):
            command('invincible',value=False);r=command('contactHit',targetId=b['id'],damage=1)
            if r['after']<r['before']:break
            command('invincible',value=True);command('pause',value=False);time.sleep(.25);command('position',target='hero',x=47,y=100);pause()
        assert r['before']-r['after']==expected,(aura,level,r)
        results.append(dict(aura=aura,level=level,loss=expected))
    command('bossLevel',value=0);command('invincible',value=True)
    return results

def bars():
    results=[]
    for scene,name in [('GG_Grey_Prince_Zote','Grey Prince'),('GG_Hollow_Knight','HK Prime'),('GG_Soul_Tyrant','Dream Mage Lord')]:
        b=setup(scene,name);pause();d=command('fixDiagnostics')['ui']
        bosses=[x for x in d['bars'] if x['visible'] and x['boss']]
        assert len(bosses)==1 and bosses[0]['name']==name,bosses
        if scene=='GG_Soul_Tyrant':
            p=next(e for e in status()['enemies'] if e['name']=='Dream Mage Lord Phase2')
            state(p,'Mage Lord 2','Wait');time.sleep(.2);d=command('fixDiagnostics')['ui']
            bosses=[x for x in d['bars'] if x['visible'] and x['boss']]
            assert len(bosses)==1 and bosses[0]['name'].endswith('Phase2'),bosses
        results.append(dict(scene=scene,bars=d))
    return results

def ui():
    b=setup('GG_Gruz_Mother','Giant Fly');command('clear');command('playerAura',element='Hydro');results=[]
    for w,h in [(1280,720),(1280,960),(1920,810),(1024,768)]:
        command('resolution',width=w,height=h);time.sleep(1)
        d=command('fixDiagnostics')['ui'];width=d['screen']['width'];height=d['screen']['height']
        assert all(0<=p['x']<=width and 0<=p['y']<=height for p in d['playerCorners']),d
        results.append(d);save_capture('ui-'+str(w)+'x'+str(h))
    command('resolution',width=1280,height=720);return results

def dung():
    results=[]
    for scene in ['GG_Dung_Defender','GG_White_Defender']:
        b=setup(scene,'Dung Defender' if scene=='GG_Dung_Defender' else 'White Defender');pause();command('enemyHp',targetId=b['id'],value=10000)
        for n in range(4):
            state(b,'Dung Defender','Idle');overload(b);time.sleep(.2)
            assert current(b)['mechanic']['stacks']==min(n+1,3),current(b)
        for p in shot('Dung Ball'):command('projectileDisable',objectId=p['id'])
        state(b,'Dung Defender','Throw 1');time.sleep(.2)
        parent=next((s for s in command('fixDiagnostics')['splits'] if not s['Small'] and s['Count']==4),None)
        if parent:
            command('objectState',objectId=parent['id'],fsm='Ball Control',state='Bounce');time.sleep(.3)
        children=[s for s in command('fixDiagnostics')['splits'] if s['Small']]
        assert len(children)==4,children
        for child in children:command('objectState',objectId=child['id'],fsm='Ball Control',state='Bounce')
        time.sleep(.2);assert len([s for s in command('fixDiagnostics')['splits'] if s['Small']])==4
        results.append(dict(scene=scene,children=children))
    return results

def uumuu():
    b=setup('GG_Uumuu','Mega Jellyfish GG');pause();command('soul',value=0)
    r=hit(b,'Pyro',20,'Nail');assert loss(r)==0 and status()['hero']['soul']>0,r
    hit(b,'Hydro',20);time.sleep(.2);assert current(b)['mechanic']['nodes']==1,current(b)
    command('enemyAura',targetId=b['id'],element='Pyro');hit(b,'Hydro',20);time.sleep(.2)
    assert not current(b)['mechanic']['shielded'],current(b)
    exposed=hit(b,'None',20);assert loss(exposed)==20,exposed
    return dict(nodes=True,exposed=exposed)

def gate():
    results=[]
    for scene,name,fsm,normal,forced in [('GG_Hollow_Knight','HK Prime','Control','Dash Antic','Tendril Antic'),('GG_Traitor_Lord','Mantis Traitor Lord','Mantis','Attack Antic','Slam Antic')]:
        b=setup(scene,name);pause();command('enemyHp',targetId=b['id'],value=10000)
        overload(b);state(b,fsm,normal);time.sleep(.1);first=current(b)['mechanic']['replacements'];assert first==1,current(b)
        overload(b);assert not current(b)['mechanic']['pending'],current(b)
        state(b,fsm,'Idle');overload(b);assert not current(b)['mechanic']['pending'],current(b)
        state(b,fsm,normal);overload(b);assert not current(b)['mechanic']['pending'],current(b)
        state(b,fsm,'Idle');overload(b);assert current(b)['mechanic']['pending'],current(b)
        results.append(current(b)['mechanic'])
    return results

def marmu():
    b=setup('GG_Ghost_Marmu','Ghost Warrior Marmu');pause();overload(b);time.sleep(.2)
    until=current(b)['mechanic']['restUntil'];overload(b);time.sleep(.2);assert current(b)['mechanic']['restUntil']==until
    command('pause',value=False);time.sleep(3.2);pause()
    overload(b);assert current(b)['mechanic']['restUntil']==until
    state(b,'Control','Chase');state(b,'Control','Hit Down');overload(b);time.sleep(.2)
    assert current(b)['mechanic']['restUntil']>until,current(b)
    return current(b)

def vessel():
    results=[]
    for scene,name,n in [('GG_Broken_Vessel','Infected Knight',1),('GG_Lost_Kin','Lost Kin',2)]:
        b=setup(scene,name);pause();command('enemyHp',targetId=b['id'],value=10000)
        for _ in range(n):overload(b);time.sleep(.2)
        assert current(b)['mechanic']['knockdowns']==1,current(b)
        state(b,'IK Control','Stun Recover');state(b,'IK Control','Idle')
        for _ in range(n):overload(b)
        time.sleep(.2);assert current(b)['mechanic']['knockdowns']==1,current(b)
        state(b,'IK Control','Dash Antic');state(b,'IK Control','Idle')
        for _ in range(n):overload(b);time.sleep(.2)
        assert current(b)['mechanic']['knockdowns']==2,current(b)
        assert not current(b)['mechanic']['empowered'],current(b)
        results.append(current(b)['mechanic'])
    return results

def markoth():
    b=setup('GG_Ghost_Markoth','Ghost Warrior Markoth');pause()
    shield=next(s for s in command('markothShields') if s['element']==1)
    command('hitActor',objectId=shield['id'],source='Nail',element='Hydro',damage=20)
    first=current(b)['mechanic'];assert abs(first['readyAt']-first['stoppedUntil']-3)<.01,first
    command('pause',value=False);time.sleep(3.2)
    assert current(b)['mechanic']['burst'],current(b)
    time.sleep(1);assert not current(b)['mechanic']['burst'],current(b)
    return dict(first=first,after=current(b)['mechanic'])

def perf():
    b=setup('GG_Ghost_Galien','Ghost Warrior Galien');vulnerable(b)
    command('position',target='hero',x=34,y=8);time.sleep(.5)
    before=next(p for p in shot('Galien') if p['name']=='Galien Hammer')
    command('profileStart');time.sleep(5);p=command('profileStop')
    after=next(p for p in shot('Galien') if p['name']=='Galien Hammer')
    assert p['count']>60 and p['p95Ms']<33.34,p
    assert abs(before['rotation']-after['rotation'])>1,(before,after)
    save_capture('performance-GG_Ghost_Galien')
    return [dict(scene='GG_Ghost_Galien',profile=p,before=before,after=after)]

def radiance_tinted():
    b=setup('GG_Radiance','Absolute Radiance');vulnerable(b)
    command('enemyHp',targetId=b['id'],value=10000)
    rounds=[]
    for first,follow in [('Pyro','Hydro'),('Cryo','Electro')]:
        if rounds:
            # Let the actual ten-second boss colour expire before selecting a new colour.
            time.sleep(10.5)
        hit(b,first,1);hit(b,follow,1)
        command('position',target='hero',x=60,y=20)
        for index in range(4):
            # Native initialization sets the twelve-sword count and enables the spawn point.
            # Sample while swords exist, before their normal flight recycles them.
            pause();state(b,'Attack Commands','Nail Fan')
            command('profileStart');state(b,'Attack Commands','CW Spawn');command('pause',value=False)
            d=command('tintDiagnostics')
            swords=[t for t in d['tints'] if t['active'] and t['name'].startswith('Radiant Nail')]
            assert swords,(first,index,d)
            expected=1 if first=='Pyro' else 4
            assert all(t['Element']==expected and t['ancestorTints']==1 and t['maxColourError']<.005 for t in swords),swords
            assert all(sprite['original']=='RGBA(1.000, 1.000, 1.000, 1.000)' for t in swords for sprite in t['sprites']),swords
            time.sleep(.5);p=command('profileStop')
            assert p['count']>30 and p['p95Ms']<33.34,p
            # A formation parent must never own an additional copy of the tint.
            assert all(t['sprites'] for t in swords),swords
            rounds.append(dict(colour=first,followup=follow,round=index,profile=p,diagnostics=d))
            if index==1:save_capture('tinted-radiance-'+first)
        command('pause',value=False)
    return rounds

def grimm_physics():
    from incoming_attack_acceptance import catch
    b=setup('GG_Grimm_Nightmare','Nightmare Grimm Boss');pause();results=[]
    for aura,expected in [('None',2),('Pyro',3)]:
        command('invincible',value=True);command('position',target='hero',x=47,y=100)
        command('pause',value=False);time.sleep(2);pause();command('ready')
        command('position',targetId=b['id'],x=34,y=18)
        state(b,'Control','Idle')
        def prepare_contact():
            command('enemyControl',targetId=b['id'],hard=True,seconds=15)
            command('position',targetId=b['id'],x=34,y=18)
            if aura=='Pyro':command('playerAura',element='Pyro')
        r=catch(b['objectId'],1,'梦魇格林真实本体碰撞 '+aura,'Nightmare Grimm Boss',settle=False,
            after_clear=prepare_contact)
        assert r['before']-r['after']==expected,r
        results.append(r)
    return results

CASES={name:globals()[name] for name in ['hornet','shield','shell','moss','oblob','share','collector','grimm','bars','ui','dung','uumuu','gate','marmu','vessel','markoth','perf','radiance_tinted','grimm_physics']}
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('cases',nargs='*');args=parser.parse_args()
    if not status()['hero']:
        command('load',slot=3)
        for _ in range(80):
            if status()['hero']:break
            time.sleep(.4)
        time.sleep(5);command('ready')
    for name in args.cases or CASES:
        try:result=dict(passed=True,evidence=CASES[name]());print('PASS',name,flush=True)
        except Exception as e:result=dict(passed=False,error=repr(e));print('FAIL',name,repr(e)[:400],flush=True);traceback.print_exc()
        result['dllSha256']=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest()
        result['commands']=LOG[:];LOG.clear();(OUT/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    reports={p.stem:json.loads(p.read_text(encoding='utf-8'))['passed'] for p in OUT.glob('*.json') if p.stem in CASES}
    (OUT/'summary.json').write_text(json.dumps(dict(passed=len(reports)==len(CASES) and all(reports.values()),cases=reports),indent=2),encoding='utf-8')
