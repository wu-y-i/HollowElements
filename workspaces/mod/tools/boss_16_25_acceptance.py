"""Opt-in real-game acceptance. One mailbox client; cases can be rerun independently."""
import json
import sys
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_design_acceptance import enter, enemy, state, hit, status, wait, park
from incoming_attack_acceptance import catch, tests as contacts

OUT=ROOT/'artifacts/acceptance/boss-16-25'
tests=[]

def passed(text,**evidence):
    tests.append(dict(test=text,**evidence));print('PASS',text,flush=True)

def current(b): return next(e for e in status()['enemies'] if e['id']==b['id'])
def objects(name): return [o for o in command('sceneObjects') if name in o['path']]
def shots(): return [p for p in command('projectiles') if p['active']]
def setup(scene,name):
    command('pause',value=False)
    enter(scene);s=wait(lambda s:any(e['name']==name for e in s['enemies']),10)
    b=enemy(s,name);park();command('clear')
    command('enemyVulnerable',targetId=b['id']);return b
def overload(b):
    command('enemyVulnerable',targetId=b['id'])
    command('enemyAura',targetId=b['id'],element='Pyro')
    return hit(b,'Electro',1)
def audit():
    command('pause',value=True);command('unloadAudit',reload=True);time.sleep(.9)
    r=json.loads((SESSION/'unload-audit.json').read_text(encoding='utf-8-sig'))
    assert r['passed'],r
    passed('卸载清理与重载',audit=r);command('pause',value=False)

def marmu():
    for scene in ['GG_Ghost_Marmu','GG_Ghost_Marmu_V']:
        b=setup(scene,'Ghost Warrior Marmu')
        overload(b);s=current(b);until=s['mechanic']['restUntil']
        assert 2.2<until-status()['time']<=3,s
        positions=[]
        for _ in range(4):
            time.sleep(.3);positions.append(current(b)['position'])
        assert len(set(positions))==1,positions
        wait(lambda s:s['time']>until+.1,7)
        assert current(b)['clock']['scale']>0,current(b)
        command('enemyAura',targetId=b['id'],element='Hydro');hit(b,'Cryo',1)
        assert current(b)['mechanic']['restUntil']>until,current(b)
        passed(scene+' 超载停跳3秒后恢复；冻结再次停跳',positions=positions,boss=current(b))

def vessel():
    for scene,name,dream in [('GG_Broken_Vessel','Infected Knight',False),('GG_Lost_Kin','Lost Kin',True)]:
        b=setup(scene,name)
        command('enemyHp',targetId=b['id'],value=3000)
        for _ in range(15): hit(b,'None',1);time.sleep(.13)
        assert current(b)['mechanic']['knockdowns']==0,current(b)
        overload(b);time.sleep(.2)
        if dream:
            assert current(b)['mechanic']['knockdowns']==0,current(b)
            overload(b);time.sleep(.2)
        assert current(b)['mechanic']['knockdowns']==1,current(b)
        state(b,'IK Control','Stun Recover');time.sleep(.9)
        empowered=wait(lambda s:enemy(s,name)['mechanic']['empowered'],6)
        command('enemyVulnerable',targetId=b['id']);command('clear')
        r=hit(b,'None',20);assert r['before']-r['after']==(20 if dream else 30),r
        passed(scene+' 禁用计数倒地、超载阈值、起身增伤',boss=enemy(empowered,name),hit=r)
        # Supplementary deterministic check of native TakeDamage with actual boss source.
        park();command('heal');command('clear');command('invincible',value=False)
        r=command('contactHit',targetId=b['id'],damage=1)
        command('invincible',value=True)
        assert r['before']-r['after']==2,r
        state(b,'Spawn Balloon','Spawn');time.sleep(1.5)
        balloons=[e for e in status()['enemies'] if e['mechanic'] and e['mechanic']['kind']=='Balloon']
        assert balloons and all(e['mechanic']['element'] in (2,4) and e['mechanic']['outline'] for e in balloons),balloons
        command('pause',value=True)
        r=catch(balloons[0]['objectId'],balloons[0]['mechanic']['element'],scene+' 感染气球真实碰撞附着','Balloon',stage=True)
        passed(scene+' 起身对玩家翻倍、召唤气球独立水冰',contact=r,balloons=balloons)

def fluke():
    b=setup('GG_Flukemarm','Fluke Mother')
    # Object name contains Spawner, but its FSM and HP belong to the released Flukefey.
    f=next(o for o in objects('Fluke Fly') if 'HealthManager' in o['components'])
    command('objectActive',objectId=f['id'],value=True)
    command('objectState',objectId=f['id'],fsm='Fluke Fly',state='Spawn');time.sleep(.3)
    bugs=[e for e in status()['enemies'] if e['mechanic'] and e['mechanic']['kind']=='Flukefey']
    assert bugs and all(e['mechanic']['element']==1 and e['mechanic']['outline'] for e in bugs),bugs
    command('pause',value=True)
    r=catch(bugs[0]['objectId'],1,'吸虫幼雏原版碰撞造成火附着','Fluke',stage=True)
    passed('吸虫之母本体不变，原版幼雏出生回调标记火与轮廓',bugs=bugs,contact=r)

def grimm():
    for scene,name in [('GG_Grimm','Grimm Boss'),('GG_Grimm_Nightmare','Nightmare Grimm Boss')]:
        b=setup(scene,name);command('enemyHp',targetId=b['id'],value=3000)
        results=[]
        for element,damage in [('Pyro',10),('Hydro',30),('None',20)]:
            command('clear');r=hit(b,element,20);assert r['before']-r['after']==damage,r;results.append(r)
        command('enemyAura',targetId=b['id'],element='Hydro');r=hit(b,'Cryo',1)
        assert r['state']['FrozenUntil']==0 and not r['state']['HardFrozen'],r
        passed(scene+' 火伤50%、水伤150%、免疫冻结结霜',hits=results,freeze=r)
        # Native contact source exercises same-element bonus separately from player reactions.
        park();command('heal');command('clear');command('playerAura',element='Pyro');command('invincible',value=False)
        r=command('contactHit',targetId=b['id'],damage=1);command('invincible',value=True)
        assert r['before']-r['after']==2 and r['player']['Aura']==1,r
        passed(scene+' 火附着玩家再受火伤翻倍',contact=r)
        park();command('pause',value=True);state(b,'Control','Firebat 1')
        p=next(p for p in shots() if 'Firebat' in p['name'] and 'Pillar' not in p['name'])
        r=catch(p['id'],1,scene+' 原版火蝠真实碰撞附着','Firebat',stage=True,settle=False)
        passed(scene+' 实际火蝠伤害接入',projectile=p,contact=r)

def tamer():
    b=setup('GG_God_Tamer','Lobster')
    trainer=next(o for o in command('sceneObjects') if o['path'].split('/')[-1]=='Lancer')
    command('objectActive',objectId=trainer['id'],value=True);time.sleep(.2)
    a=enemy(status(),'Lancer');command('enemyVulnerable',targetId=a['id'])
    # Real accepted hit commits aura to both entities.
    command('clear');hit(b,'Hydro',1)
    s=status();assert enemy(s,'Lobster')['state']['Aura']==2 and enemy(s,'Lancer')['state']['Aura']==2,s
    r=hit(a,'Pyro',20);s=status()
    assert r['before']-r['after']==30,r
    assert enemy(s,'Lobster')['state']['AuraUnits']==.5 and enemy(s,'Lancer')['state']['AuraUnits']==.5,s
    assert enemy(s,'Lobster')['state']['Icd']!=enemy(s,'Lancer')['state']['Icd'],s
    passed('神之驯服者两本体共享附着与消耗，保留独立命中计数',result=r,status=s)
    command('clear');hit(b,'Cryo',1);s=status()
    assert all(enemy(s,n)['state']['Aura']==4 and enemy(s,n)['state']['FrozenUntil']==0 for n in ['Lobster','Lancer']),s
    passed('调试清空同时清除共享附着缓存',status=s)

def sheo():
    b=setup('GG_Painter','Sheo Boss')
    for attack,prefix,element in [('S 3','Paint Shot B',4),('RedShot L','Paint Shot R',1),('GSlash 2','Paint Shot P',3)]:
        command('pause',value=True);state(b,'nailmaster_sheo',attack)
        p=next(p for p in shots() if p['name'].startswith(prefix))
        assert p['element']==element and not p['tinted'] and not p['outlineVisible'],p
        r=catch(p['id'],element,'席奥 '+attack+' 真实弹幕附着',prefix,stage=True,settle=False)
        passed('席奥原色弹幕 '+attack,projectile=p,contact=r)
        command('pause',value=False);park()
        for p in shots():command('projectileDisable',objectId=p['id'])
    command('pause',value=True);state(b,'nailmaster_sheo','Stab2')
    stabs=[o for o in objects('Stab Effect/Hit') if o['active'] and 'DamageHero' in o['components']]
    assert stabs,objects('Stab Effect')
    r=catch(stabs[0]['id'],2,'席奥黄色刺击真实附着水','Stab Effect',settle=False)
    passed('席奥黄色原版刺击水元素',contact=r)

def hu():
    b=setup('GG_Ghost_Hu','Ghost Warrior Hu')
    command('pause',value=True)
    state(b,'Attacking','Place Rings');state(b,'Attacking','Ring Antic')
    # Selecting native ring activation directly preserves the current ordinary attack element.
    ring=next(o for o in objects('Ring Holder/') if o['path'].count('/')==1 and 'PlayMakerFSM' in o['components'])
    command('objectActive',objectId=ring['id'],value=True)
    command('objectState',objectId=ring['id'],fsm='Control',state='Antic')
    p=next(p for p in shots() if p['id']==ring['id']);assert p['element']==3,p
    command('objectState',objectId=ring['id'],fsm='Control',state='Down')
    r=catch(ring['id'],3,'胡长老普通锤子真实雷附着','Ring Holder',settle=False)
    passed('胡长老普通锤子雷元素与轮廓',projectile=p,contact=r)
    command('pause',value=False);park();command('pause',value=True)
    state(b,'Attacking','M 1')
    command('objectActive',objectId=ring['id'],value=True)
    command('objectState',objectId=ring['id'],fsm='Control',state='Antic')
    p=next(p for p in shots() if p['id']==ring['id']);assert p['element']==2,p
    command('objectState',objectId=ring['id'],fsm='Control',state='Down')
    r=catch(ring['id'],2,'胡长老连砸真实水附着','Ring Holder',settle=False)
    passed('胡长老连砸水元素与轮廓',projectile=p,contact=r)

def galien():
    # Scene entry may leave the native Warrior parent inactive until its cutscene starts.
    command('pause',value=False);command('goto',scene='GG_Ghost_Galien')
    wait(lambda s:s['scene']=='GG_Ghost_Galien');time.sleep(2)
    warrior=next(o for o in command('sceneObjects') if o['path']=='Warrior')
    command('objectActive',objectId=warrior['id'],value=True);time.sleep(.5)
    command('ready');s=wait(lambda s:any(e['name']=='Ghost Warrior Galien' for e in s['enemies']),10)
    b=enemy(s,'Ghost Warrior Galien');park();command('clear')
    state(b,'Summon Minis','Summon');state(b,'Summon Minis','Summon 2');time.sleep(.3)
    p=shots();main=next(p for p in p if p['name']=='Galien Hammer');minis=[a for a in p if a['name'].startswith('Galien Mini Hammer')]
    assert main['element']==3 and minis and all(p['element']==1 and p['tinted'] for p in minis),p
    command('objectState',objectId=main['id'],fsm='Control',state='Spinning')
    command('objectState',objectId=main['id'],fsm='Attack',state='Chase')
    command('enemyControl',targetId=b['id'],seconds=5,hard=True)
    time.sleep(.2);before=next(p for p in shots() if p['id']==main['id']);time.sleep(.7);after=next(p for p in shots() if p['id']==main['id'])
    assert 0<after['clockScale']<1 and after['clock']['SkippedFrames']>before['clock']['SkippedFrames'],(before,after)
    assert before['rotation']!=after['rotation'] and before['position']!=after['position'],(before,after)
    passed('加利安大雷轮小火轮、硬冻结也只减速；实际移动与自转仍推进',main=main,minis=minis,before=before,after=after)
    command('clear');command('pause',value=True)
    command('position',targetId=b['id'],x=70,y=60)
    r=catch(main['id'],3,'加利安大齿轮真实雷附着','Galien Hammer',stage=True,settle=False,
            after_clear=lambda:command('enemyControl',targetId=b['id'],seconds=4,hard=True))
    passed('加利安齿轮真实伤害',contact=r)
    audit()

def collector():
    for scene in ['GG_Collector','GG_Collector_V']:
        b=setup(scene,'Jar Collector');command('enemyHp',targetId=b['id'],value=3000)
        command('pause',value=True);state(b,'Control','Hop')
        command('enemyAura',targetId=b['id'],element='Hydro');r=hit(b,'Pyro',20)
        time.sleep(.2);s=current(b)
        assert s['mechanic']['summons']==1 and s['mechanic']['jumps']==0,s
        assert next(f['ActiveStateName'] for f in s['fsms'] if f['FsmName']=='Control')!='Jump Antic',s
        command('pause',value=False)
        spawned=wait(lambda s:any(e['mechanic'] and e['mechanic']['kind']=='CollectorSummon' and e['mechanic']['maximum']>0 for e in s['enemies']),8)
        summon=next(e for e in spawned['enemies'] if e['mechanic'] and e['mechanic']['kind']=='CollectorSummon' and e['mechanic']['maximum']>0)
        maximum=summon['mechanic']['maximum'];assert maximum==summon['hp'],summon
        command('pause',value=True);command('enemyVulnerable',targetId=b['id']);state(b,'Control','Sleep')
        before=current(b)['hp'];command('nativeExtra',targetId=summon['id'],damage=10000)
        time.sleep(.2);after=current(b)
        assert before-after['hp']==maximum and after['mechanic']['jumps']>=1,(maximum,before,after)
        passed(scene+' 反应原地落罐、原版召唤物死亡按最大HP反伤并触发上跳',reaction=r,summon=summon,boss=after)
        command('pause',value=False)

def hive():
    b=setup('GG_Hive_Knight','Hive Knight');command('enemyHp',targetId=b['id'],value=500)
    command('pause',value=True);state(b,'Control','Slash 1')
    # Forced FSM entry can leave a collider from the interrupted native attack active.
    for o in objects('Hive Knight/'):
        if o['active'] and 'DamageHero' in o['components'] and o['path'].split('/')[-1] in ['Stab Hit','Slash 2']:
            command('objectActive',objectId=o['id'],value=False)
    visuals=command('weaponVisuals');assert any(v['ResourceSet']=='HiveBlades' and v['element']==1 for v in visuals),visuals
    slash=next(o for o in objects('Hive Knight/') if o['active'] and 'DamageHero' in o['components'] and 'Slash' in o['path'])
    r=catch(slash['id'],1,'蜂巢骑士刀光真实火附着','Slash',settle=False)
    passed('蜂巢骑士技能火元素与刀光局部染色',visuals=visuals,contact=r)
    command('pause',value=False);park();command('pause',value=True)
    stinger=next(o for o in objects('Stinger') if 'HiveKnightStinger' in o['components'])
    # Native glob/stinger lifecycle: activating a stinger tests the persistent re-enable binding.
    parts=stinger['path'].split('/')
    for i in range(1,len(parts)+1):
        part=next(o for o in command('sceneObjects') if o['path']=='/'.join(parts[:i]))
        command('objectActive',objectId=part['id'],value=True)
    p=next(p for p in shots() if p['id']==stinger['id']);assert p['element']==2 and p['tinted'],p
    r=catch(stinger['id'],2,'蜂巢骑士钉刺真实水附着','Stinger',stage=True,settle=False)
    passed('蜂刺球分离的实际钉刺水伤与染色',projectile=p,contact=r)
    command('pause',value=False);park();command('pause',value=True)
    dropper=next(o for o in command('sceneObjects') if o['path'].split('/')[-1].startswith('Bee Dropper') and 'CircleCollider2D' in o['components'])
    command('objectActive',objectId=dropper['id'],value=True)
    command('objectState',objectId=dropper['id'],fsm='Control',state='Swarm Start')
    bee=next(x for x in command('bees') if x['id']==dropper['id']);assert bee['healthManagers']==0,bee
    before=current(b)['hp'];command('clear')
    command('hitActor',objectId=bee['id'],source='Nail',damage=20,cast=123123)
    command('hitActor',objectId=bee['id'],source='Nail',damage=20,cast=123123)
    after=current(b);assert after['hp']-before==10 and after['state']['Aura']==bee['element'],(before,after,bee)
    passed('小蜜蜂无HP、半伤回血、同次攻击去重与给Boss附着',bee=bee,boss=after)
    state(b,'Control','Idle');command('position',targetId=b['id'],x=58,y=32)
    command('position',objectId=bee['id'],x=70,y=40)
    r=catch(bee['id'],bee['element'],'小蜜蜂真实碰撞独立附着','Bee Dropper',settle=False)
    passed('小蜜蜂实际伤害元素',contact=r)
    command('pause',value=False);park();command('pause',value=True)
    for other in command('bees'):
        if other['id']!=bee['id']:command('objectActive',objectId=other['id'],value=False)
    command('enemyControl',targetId=b['id'],seconds=10,hard=True)
    state(b,'Control','Idle');command('setState',fsm='Roar Lock',state='Regain Control');command('ready')
    command('setState',fsm='Spell Control',state='Cancel All');command('setState',fsm='Nail Arts',state='Cancel All')
    command('objectState',objectId=bee['id'],fsm='Control',state='Swarm Start')
    command('position',objectId=bee['id'],x=62,y=40)
    command('position',target='hero',x=60,y=40);command('face',right=True)
    before=current(b)['mechanic']['beeHits']
    command('attack');command('pause',value=False);time.sleep(.3);command('pause',value=True)
    nail=current(b);assert nail['mechanic']['beeHits']==before+1,nail
    active=next(x for x in command('bees') if x['id']==bee['id'])
    assert next(f['ActiveStateName'] for f in active['fsms'] if f['FsmName']=='Control')=='Swarm',active
    passed('真实骨钉命中小蜜蜂：回血一次且仍存活',boss=nail,bee=active)
    # Do not rebirth the same bee inside the still-active previous nail slash.
    command('pause',value=False);park();command('pause',value=True)
    command('objectState',objectId=bee['id'],fsm='Control',state='Swarm Start')
    command('position',objectId=bee['id'],x=64,y=40)
    command('position',target='hero',x=60,y=40);command('face',right=True);command('heal')
    command('setState',fsm='Spell Control',state='Has Fireball?')
    command('pause',value=False);time.sleep(.6);command('pause',value=True)
    spell=current(b);assert spell['mechanic']['beeHits']==before+2,spell
    active=next(x for x in command('bees') if x['id']==bee['id'])
    assert next(f['ActiveStateName'] for f in active['fsms'] if f['FsmName']=='Control')=='Spell Death',active
    passed('真实法术命中小蜜蜂：回血一次且沿原版法术死亡',boss=spell,bee=active)

def water():
    command('pause',value=False);command('goto',scene='GG_Spa');wait(lambda s:s['scene']=='GG_Spa');time.sleep(2);command('ready')
    command('absoluteInvincible',value=True);command('clear')
    w=command('water');regions=[r for r in w['regions'] if r['active']]
    assert regions,w
    def xy(value):return [float(x) for x in value.strip('()').split(',')[:2]]
    region=next(r for r in regions if r['colliders'] and r['colliders'][0]['enabled'])
    centre=xy(region['colliders'][0]['centre'])
    command('position',target='hero',x=centre[0],y=centre[1]);time.sleep(.25)
    start=command('water');s=status();assert start['applications']>w['applications'] and s['hero']['state']['Aura']==2,(w,start,s)
    # Hold the fixture inside the real trigger: manual warp can bypass the pool's surface entry.
    until=status()['time']+2.2
    while status()['time']<until:command('position',target='hero',x=centre[0],y=centre[1])
    end=command('water');assert end['applications']>=start['applications']+2,(start,end)
    command('position',target='hero',x=centre[0]+30,y=centre[1]);time.sleep(.2)
    left=command('water');time.sleep(1.2);assert command('water')['applications']==left['applications']
    command('position',target='hero',x=centre[0],y=centre[1]);time.sleep(.2)
    reentry=command('water');assert reentry['applications']==left['applications']+1,(left,reentry)
    command('absoluteInvincible',value=False)
    passed('温泉真实触发器：进入立即1U、每秒刷新、离开停止、重入立即附着',start=start,end=end,reentry=reentry)

def acid():
    command('pause',value=False)
    for armour in [False,True]:
        command('acidArmour',value=armour);command('goto',scene='Waterways_13',gate='left1')
        wait(lambda s:s['scene']=='Waterways_13');time.sleep(4);command('ready');command('absoluteInvincible',value=True);command('clear')
        ids={o['id'] for o in command('sceneObjects')}
        w=command('water')
        # The upper pool overlaps a native scene exit; use the lowest interior basin.
        region=min((r for r in w['regions'] if r['id'] in ids and r['Acid'] and r['active'] and r['colliders'] and r['colliders'][0]['enabled']),
                   key=lambda r:float(r['colliders'][0]['centre'].strip('()').split(',')[1]))
        centre=[float(x) for x in region['colliders'][0]['centre'].strip('()').split(',')[:2]]
        end=status()['time']+1.4
        while status()['time']<end:command('position',target='hero',x=centre[0],y=centre[1])
        after=command('water');s=status()
        if armour:assert after['applications']==w['applications'] and s['hero']['state']['Aura']==0,(w,after,s)
        else:assert after['applications']>=w['applications']+2 and s['hero']['state']['Aura']==2,(w,after,s)
        passed('酸液'+('获取酸液排开后不附着' if armour else '获取酸液排开前1U并每秒刷新'),before=w,after=after)
    command('absoluteInvincible',value=False)

def lifecycle():
    b=setup('GG_Hive_Knight','Hive Knight');command('pause',value=True);state(b,'Control','Slash 1')
    dropper=next(o for o in command('sceneObjects') if o['path'].split('/')[-1].startswith('Bee Dropper') and 'CircleCollider2D' in o['components'])
    command('objectActive',objectId=dropper['id'],value=True)
    command('objectState',objectId=dropper['id'],fsm='Control',state='Swarm Start')
    assert command('bees'),status()
    audit()
    command('goto',scene='GG_Spa');wait(lambda s:s['scene']=='GG_Spa');time.sleep(3);command('ready')
    assert command('water')['regions']
    audit()

if __name__=='__main__':
    case=sys.argv[1];OUT.mkdir(parents=True,exist_ok=True)
    try:
        globals()[case]();report=dict(passed=True,tests=tests,physicalContacts=contacts,commands=LOG)
    except Exception as error:
        report=dict(passed=False,error=repr(error),tests=tests,physicalContacts=contacts,commands=LOG);raise
    finally:
        (OUT/(case+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
