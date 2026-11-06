"""Focused v0.1.7 checks against native actors; development save suppression required."""
import argparse
import hashlib
import json
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_26_42_acceptance import setup, state, current, hit, loss, objects, active, vulnerable
from boss_design_acceptance import park
from incoming_attack_acceptance import catch, tests as contacts
OUT = ROOT / 'artifacts/acceptance/boss-update'


def pause(): command('pause', value=True)
def react(b, base='Pyro', incoming='Electro'):
    if b['name']!='Mega Jellyfish GG':vulnerable(b)
    command('enemyAura',targetId=b['id'],element=base)
    return hit(b,incoming,32)
def statuses(b):
    return next(x['statuses'] for x in command('fixDiagnostics')['ui']['bars'] if x['id']==b['id'])


def permissions():
    from balance_acceptance import room
    room();command('bench',value=False);command('debugMode',value=True)
    command('menuSetting',name='允许非长椅配置元素',index=0)
    assert not command('loadoutShortcut')['handled']
    original=command('menuStatus')['loadout']
    command('element_guide',action='open');time.sleep(.7)
    r=command('element_guide');ids=[e['Id'] for c in json.loads((ROOT/'workspaces/mod/content/ElementGuide/index.json').read_text(encoding='utf-8'))['Categories'] for e in c['Entries']]
    command('element_guide',action='navigate',direction=-1-ids.index(r['selected']))
    assert command('element_guide')['selected']=='loadout'
    command('element_guide',action='navigate',submit=True)
    for step in [-1,1,1]: command('element_guide',action='configure',step=step)
    assert command('menuStatus')['loadout']==original
    command('capture');time.sleep(.3);(OUT/'config-locked.png').write_bytes((SESSION/'game.png').read_bytes())
    command('menuSetting',name='允许非长椅配置元素',index=1)
    command('element_guide',action='configure',step=1)
    assert command('menuStatus')['loadout']['Nail']!=original['Nail']
    command('element_guide',action='configure',step=-1)
    assert command('menuStatus')['loadout']==original
    command('capture');time.sleep(.3);(OUT/'config-enabled.png').write_bytes((SESSION/'game.png').read_bytes())
    command('element_guide',action='close');time.sleep(.7)
    assert command('loadoutShortcut')['handled'];command('closeMenu')
    command('menuSetting',name='允许非长椅配置元素',index=0)
    command('bench',value=True);assert command('loadoutShortcut')['handled'];command('closeMenu');command('bench',value=False)
    return dict(lockedDespiteDebug=True,enabledWorks=True,benchWorks=True)


def hornet():
    results=[]
    for scene,name,step in [('GG_Hornet_1','Hornet Boss 1',.1),('GG_Hornet_2','Hornet Boss 2',.05)]:
        b=setup(scene,name);pause();command('enemyHp',targetId=b['id'],value=100000)
        for n in range(12):
            react(b,'Pyro','Hydro');m=current(b)['mechanic']
            assert abs(m['tempo']-(1+min(n+1,10)*step))<.001,m
            assert abs(m['vulnerability']-(1+(n+1)*.1))<.001,m
        time.sleep(.2);buffs=statuses(b)
        assert {x['label']:x['stacks'] for x in buffs if x['label'] in ['易伤','加速']}=={'易伤':'12','加速':'10'},buffs
        if scene.endswith('_2'):
            region=next(o for o in objects() if o['path'].endswith('Barb Region'))
            command('objectState',objectId=region['id'],fsm='Spawn Barbs',state='Spawn 1');time.sleep(.2)
            barbs=[p for p in command('projectiles') if p['active'] and p['name'].startswith('Hornet Barb')]
            assert barbs and all(p['element'] and not p['outlineVisible'] for p in barbs),barbs
        results.append(dict(scene=scene,mechanic=m,buffs=buffs))
    return results


def crystal():
    results=[]
    for scene,name in [('GG_Crystal_Guardian','Mega Zombie Beam Miner (1)'),('GG_Crystal_Guardian_2','Zombie Beam Miner Rematch')]:
        b=setup(scene,name);pause();counts=[]
        for n in range(8):
            command('hit',targetId=b['id'],source='Cyclone',element='None',damage=20,direction=270,cast=70001)
            counts.append(current(b)['mechanic']['headHits'])
        assert counts==list(range(1,9)),counts
        assert not current(b)['mechanic']['shield'],current(b)
        results.append(dict(scene=scene,headHits=counts))
    return results


def marmu():
    b=setup('GG_Ghost_Marmu','Ghost Warrior Marmu');pause();react(b);time.sleep(.2)
    until=current(b)['mechanic']['restUntil'];command('pause',value=False)
    for _ in range(40):
        if command('status')['time']>until+.1:break
        time.sleep(.1)
    pause();state(b,'Control','Chase');dump=command('dumpAll')
    chase=[a['fields'] for f in dump if f['obj']=='Ghost Warrior Marmu' for s in f['states'] for a in s['actions'] if a['type'].endswith('ChaseObjectV2')]
    assert any(float(c['speedMax'])==60 and float(c['accelerationForce'])==127.5 for c in chase),chase
    return chase


def vessels():
    rows=[]
    for scene,name,n in [('GG_Broken_Vessel','Infected Knight',1),('GG_Lost_Kin','Lost Kin',2)]:
        b=setup(scene,name);pause();command('enemyHp',targetId=b['id'],value=10000)
        for _ in range(n):react(b);time.sleep(.15)
        assert current(b)['mechanic']['knockdowns']==1,current(b)
        state(b,'IK Control','Stun Recover');state(b,'IK Control','Idle')
        assert current(b)['mechanic']['empowered']
        command('clear');vulnerable(b);r=hit(b,'None',20);assert loss(r)==13,r
        command('pause',value=False);park();command('clear');command('heal');command('invincible',value=False)
        contact=command('contactHit',targetId=b['id'],damage=1);assert contact['before']-contact['after']==2,contact
        command('invincible',value=True);pause();state(b,'IK Control','Stun Start');assert not current(b)['mechanic']['empowered']
        rows.append(dict(scene=scene,damage=r,contact=contact))
    return rows


def hu():
    b=setup('GG_Ghost_Hu','Ghost Warrior Hu');pause()
    state(b,'Attacking','M 1');assert current(b)['mechanic']['element']==2
    shots=[p for p in command('projectiles') if p['active'] and p['owner']==b['id']]
    assert shots,command('status')
    event=catch(shots[0]['id'],shots[0]['element'],'Hu hammer native hit swaps elements',path=shots[0]['name'],stage=True,settle=False)
    assert current(b)['mechanic']['swapped']
    dash=command('dashTest');assert not dash['shadow'] and dash['ability'],dash
    state(b,'Attacking','M 1');assert current(b)['mechanic']['element']==3
    command('pause',value=False);park();time.sleep(1.5);pause()
    restored=command('dashTest');assert restored['shadow'] and restored['ability'],restored
    return dict(hit=event,disabled=dash,restored=restored)


def tamer():
    b=setup('GG_God_Tamer','Lobster');pause()
    trainer=next(o for o in objects() if o['path'].split('/')[-1]=='Lancer');active(trainer)
    other=next(e for e in command('status')['enemies'] if e['name']=='Lancer')
    for actor in [b,other]:command('enemyHp',targetId=actor['id'],value=10000);vulnerable(actor)
    command('clear');hit(b,'Pyro',32)
    before=current(other)['hp'];r=hit(b,'Hydro',32);mirrored=before-current(other)['hp']
    assert mirrored==int(loss(r)*1.5),(r,mirrored)
    return dict(primary=loss(r),mirrored=mirrored)


def watcher():
    b=setup('GG_Watcher_Knights','Black Knight 1');pause();state(b,'Black Knight','Idle');command('enemyHp',targetId=b['id'],value=10000)
    react(b);assert not current(b)['mechanic']['armoured']
    r=react(b,'Cryo','Electro');assert r['state']['SuperconductStacks']==3,r
    due=current(b)['mechanic']['vulnerableUntil'];assert abs(due-command('status')['time']-8)<.05
    used=[]
    for n in range(3):
        command('pause',value=False);time.sleep(.24);pause();vulnerable(b);r=hit(b,'None',32,'Nail');used.append(r)
        assert r['state']['SuperconductStacks']==2-n,r
    assert used[-1]['state']['SuperconductUntil']==0 and current(b)['mechanic']['vulnerableUntil']==due
    return dict(due=due,hits=used)


def uumuu():
    rows=[]
    for scene in ['GG_Uumuu','GG_Uumuu_V']:
        b=setup(scene,'Mega Jellyfish GG');pause()
        react(b,'Pyro','Hydro');assert current(b)['mechanic']['nodes']==2
        react(b,'Pyro','Electro');time.sleep(.2);assert current(b)['mechanic']['nodes']==1 and current(b)['mechanic']['shielded']
        react(b,'Cryo','Electro');time.sleep(.2);assert not current(b)['mechanic']['shielded'] and current(b)['mechanic']['exposure']==1.5,current(b)
        command('clear');r=hit(b,'None',20);assert loss(r)==13,r
        state(b,'Mega Jellyfish','Recover');command('pause',value=False);time.sleep(.7);pause();assert current(b)['mechanic']['shielded']
        spawner=next(o for o in objects() if o['path']=='Jellyfish Spawner')
        command('objectState',objectId=spawner['id'],fsm='Spawn',state='Spawn')
        command('pause',value=False);time.sleep(.4);pause()
        corpse=next(o for o in objects('Corpse Jellyfish') if 'PlayMakerFSM' in o['components'])
        pos=[float(x) for x in current(b)['position'].strip('()').split(',')[:2]]
        command('position',objectId=corpse['id'],x=pos[0],y=pos[1]);active(corpse)
        command('pause',value=False);time.sleep(.5);pause()
        assert not current(b)['mechanic']['shielded'] and current(b)['mechanic']['exposure']==.5,current(b)
        # Finish the corpse's real multi-frame blast and native evasion window.
        command('pause',value=False);time.sleep(.35);pause()
        command('clear');vulnerable(b);r2=hit(b,'None',20);assert loss(r2)==4,r2
        rows.append(dict(scene=scene,reactionExposure=r,oomaExposure=r2))
    return rows


def dung():
    rows=[]
    for scene,name,threshold in [('GG_Dung_Defender','Dung Defender',1),('GG_White_Defender','White Defender',2)]:
        b=setup(scene,name);pause();command('enemyHp',targetId=b['id'],value=10000)
        for knock in range(4):
            state(b,'Dung Defender','Idle')
            for event in range(threshold):
                react(b);time.sleep(.15)
                if event<threshold-1:assert current(b)['mechanic']['knockdowns']==knock,current(b)
            m=current(b)['mechanic'];assert m['knockdowns']==knock+1 and m['stacks']==min(3,knock+1),m
        buffs=statuses(b);assert any(x['label']=='分裂' and x['stacks']=='3' for x in buffs),buffs
        rows.append(dict(scene=scene,mechanic=m,buffs=buffs))
    return rows


def pure():
    b=setup('GG_Hollow_Knight','HK Prime');pause()
    state(b,'Control','SmallShot Antic');command('pause',value=False);time.sleep(1);pause()
    shots=[p for p in command('projectiles') if p['active'] and p['element']==2]
    assert shots,command('tintDiagnostics')
    state(b,'Control','Plume Up');command('pause',value=False);time.sleep(.5);pause()
    blast=next(o for o in objects() if o['path'].split('/')[-1]=='HK Prime Blast')
    active(blast);command('animateOffscreen',objectId=blast['id']);command('objectState',objectId=blast['id'],fsm='Control',state='Blast')
    command('pause',value=False);time.sleep(.2);pause()
    tints=command('tintDiagnostics')['tints'];visible=[t for t in tints if t['active'] and t['sprites']]
    assert visible and all(t['maxColourError']<.001 and t['BindCount']==1 and t['ancestorTints']==1 for t in visible),visible
    return dict(shots=shots,tints=tints)


def watcher_bars():
    b=setup('GG_Watcher_Knights','Black Knight 1');pause()
    knights=[e for e in command('status')['enemies'] if e['name'].startswith('Black Knight ')]
    assert len(knights)==6,knights
    def shown():
        time.sleep(.12)
        return [x['name'] for x in command('fixDiagnostics')['ui']['bars'] if x['visible'] and x['name'].startswith('Black Knight ')]
    for actor in knights:state(actor,'Black Knight','Rest')
    assert shown()==[],shown()
    first=next(e for e in knights if e['name']=='Black Knight 1');second=next(e for e in knights if e['name']=='Black Knight 2')
    state(first,'Black Knight','Idle');assert shown()==['Black Knight 1'],shown()
    state(second,'Black Knight','Bugs In');assert set(shown())=={'Black Knight 1','Black Knight 2'},shown()
    state(second,'Black Knight','Idle');react(first);react(first,'Cryo','Electro')
    command('enemyHp',targetId=first['id'],value=0);assert shown()==['Black Knight 2'],shown()
    state(second,'Black Knight','Rest');assert shown()==[],shown()
    return dict(sixTracked=True,visibleCounts=[0,1,2,1,0],dormantDoNotOccupySlots=True)


def popup_sizes():
    from balance_acceptance import room
    b=room();original=command('globalSettings');menus=command('modMenu')
    assert sum(x['Name']=='跳字大小' for x in menus)==1
    assert not any(x['Name'] in ['伤害跳字大小','反应跳字大小'] for x in menus)
    legacy=dict(original,PopupScaleMigrated=False,DamagePopupScale=1.25,ReactionPopupScale=2.5)
    migrated=command('globalSettings',settings=legacy)
    assert migrated['PopupScale']==2.5 and 'DamagePopupScale' not in migrated and 'ReactionPopupScale' not in migrated,migrated
    command('globalSettings',settings=original)
    rows=[]
    for index,damage,reaction in [(0,48,60),(2,72,90),(5,120,150)]:
        command('menuSetting',name='跳字大小',index=index);command('clear');command('enemyHp',targetId=b['id'],value=100000)
        command('pause',value=False);react(b);pause()
        popups=command('fixDiagnostics')['ui']['popups'];assert any(p['Reaction'] for p in popups),popups
        for popup in popups:assert popup['size']==popup['outlineSize']==(reaction if popup['Reaction'] else damage),popup
        rows.append(popups)
    command('menuSetting',name='跳字大小',index=2);command('pause',value=False);command('clear');command('heal');command('invincible',value=False)
    command('playerAura',element='Pyro');assert command('hurt',element='Electro',damage=1);pause()
    player=command('fixDiagnostics')['ui']['popups'];assert any(p['Reaction'] and p['size']==90 and p['style']=='Italic' for p in player),player
    command('invincible',value=True);command('pause',value=False);command('clear');return dict(sizes=rows,legacy=migrated,player=player)


CASES={name:globals()[name] for name in ['permissions','hornet','crystal','marmu','vessels','hu','tamer','watcher','uumuu','dung','pure','watcher_bars','popup_sizes']}
def main():
    parser=argparse.ArgumentParser();parser.add_argument('cases',nargs='*',choices=list(CASES));args=parser.parse_args()
    OUT.mkdir(exist_ok=True)
    digest=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest()
    for name in args.cases or CASES:
        start=len(LOG)
        try:result=CASES[name]();report=dict(passed=True,result=result)
        except Exception as error:report=dict(passed=False,error=repr(error))
        report.update(case=name,version='0.1.7',dllSha256=digest,commands=LOG[start:])
        (OUT/(name+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        print(('PASS' if report['passed'] else 'FAIL'),name,report.get('error',''),flush=True)


if __name__=='__main__':main()
