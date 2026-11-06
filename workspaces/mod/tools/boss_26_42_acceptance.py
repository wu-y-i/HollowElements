"""Concentrated native acceptance, run only after all Boss 26–42 code is implemented."""
import json
import sys
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_design_acceptance import status, wait, park, enemy, state
from incoming_attack_acceptance import catch, tests as contacts

OUT=ROOT/'artifacts/acceptance/boss-26-42'
tests=[]

def passed(label,**evidence):
    tests.append(dict(test=label,**evidence));print('PASS',label,flush=True)
def current(b):return next(e for e in status()['enemies'] if e['id']==b['id'])
def objects(text=''):return [o for o in command('sceneObjects') if text in o['path']]
def projectiles(prefix=''):return [p for p in command('projectiles') if p['active'] and p['name'].startswith(prefix)]
def active(obj):
    parts=obj['path'].split('/')
    for i in range(1,len(parts)+1):
        found=next((o for o in objects() if o['path']=='/'.join(parts[:i])),None)
        if found and not found['active']:command('objectActive',objectId=found['id'],value=True)
def setup(scene,name):
    command('pause',value=False);command('absoluteInvincible',value=False);command('invincible',value=True)
    command('goto',scene=scene);wait(lambda s:s['scene']==scene and s['hero']);time.sleep(3)
    obj=next(o for o in objects() if o['path'].split('/')[-1]==name and 'HealthManager' in o['components'])
    active(obj);command('ready');command('bossLevel',value=0)
    s=wait(lambda s:any(e['name']==name for e in s['enemies']),10);b=enemy(s,name)
    park();command('clear');return b
def hit(b,element,damage=20,source='WhiteWave',cast=None):
    v=dict(targetId=b['id'],element=element,damage=damage,source=source,attackType='Nail' if source in ['Nail','GreatSlash','DashSlash','Cyclone'] else 'Spell')
    if cast is not None:v['cast']=cast
    return command('hit',**v)
def loss(r):return r['before']-r['after']
def vulnerable(b):command('enemyVulnerable',targetId=b['id'])
def audit(label):
    command('pause',value=True);command('unloadAudit',reload=True);time.sleep(1)
    r=json.loads((SESSION/'unload-audit.json').read_text(encoding='utf-8-sig'));assert r['passed'],r
    passed(label,audit=r);command('pause',value=False)
def damage_object(root,path):return next(o for o in command('attackDetails',objectId=root) if path in o['path'] and o['damage'])

def watcher():
    b=setup('GG_Watcher_Knights','Black Knight 1');state(b,'Black Knight','Idle');vulnerable(b)
    command('soul',value=0);r=hit(b,'Pyro',20,'Nail');s=status()
    assert loss(r)==0 and r['state']['Aura']==1 and s['hero']['soul']>0,(r,s)
    passed('守望者新增骨钉护甲：零伤、附着和正常回魂',hit=r,soul=s['hero']['soul'])
    time.sleep(.3);r=hit(b,'Electro',20);assert not current(b)['mechanic']['armoured'] and loss(r)>20,(r,current(b))
    time.sleep(.3);r=hit(b,'None',20,'Nail');assert loss(r)==20,r
    passed('超载实际爆炸永久破甲；之后骨钉可扣血',hit=r,boss=current(b))
    command('clear');command('enemyAura',targetId=b['id'],element='Cryo');hit(b,'Electro',1)
    r=hit(b,'None',20);assert loss(r)==30,r
    r2=hit(b,'Hydro',20);assert loss(r2)==30,r2
    others=[e for e in status()['enemies'] if e['mechanic'] and e['mechanic']['kind']=='Watcher' and e['id']!=b['id']]
    assert len(others)==5 and all(e['mechanic']['armoured'] for e in others),others
    passed('超导全伤150%且不叠通用物伤；六名骑士独立护甲',neutral=r,elemental=r2,others=others)

def uumuu():
    for scene in ['GG_Uumuu','GG_Uumuu_V']:
        b=setup(scene,'Mega Jellyfish GG');assert current(b)['mechanic']['shielded'],current(b)
        command('soul',value=0);r=hit(b,'Hydro',20,'Nail');assert loss(r)==0 and r['state']['Aura']==2,r
        assert status()['hero']['soul']==0
        time.sleep(.3);r=hit(b,'Cryo',20);assert loss(r)==0 and r['state']['FrozenUntil']>0,r
        time.sleep(.3);r=hit(b,'None',20,'Nail');s=current(b)
        assert loss(r)==0 and not s['mechanic']['shielded'] and s['mechanic']['shatterBreaks']==1,(r,s)
        time.sleep(.3);command('soul',value=0);r=hit(b,'None',20,'Nail')
        assert loss(r)==10 and status()['hero']['soul']>0,(r,status())
        passed(scene+' 护盾零伤附着不回魂、冻结碎冰破盾、破盾半伤与回魂',hit=r,boss=s)
        # Re-form the native shield, then detonate a real native Ooma corpse at the boss.
        command('clear');state(b,'Mega Jellyfish','Recover');time.sleep(.6)
        sp=next(o for o in objects() if o['path']=='Jellyfish Spawner')
        command('objectState',objectId=sp['id'],fsm='Spawn',state='Spawn');time.sleep(.4)
        jelly=next(e for e in status()['enemies'] if e['name'].startswith('Jellyfish GG'))
        corpse=next(o for o in objects('Corpse Jellyfish') if 'PlayMakerFSM' in o['components'])
        p=[float(x) for x in current(b)['position'].strip('()').split(',')[:2]]
        command('position',objectId=corpse['id'],x=p[0],y=p[1])
        active(corpse);time.sleep(.4)
        assert not current(b)['mechanic']['shielded'],current(b)
        passed(scene+' 原版欧玛真实爆炸仍破盾',boss=current(b))

def nosk():
    for scene,name,fsm in [('GG_Nosk_Hornet','Hornet Nosk','Hornet Nosk'),('GG_Nosk','Mimic Spider','Mimic Spider'),('GG_Nosk_V','Mimic Spider','Mimic Spider')]:
        b=setup(scene,name);command('pause',value=True);state(b,fsm,'Spit 1')
        p=projectiles('Vomit Glob Nosk');assert p and all(a['element'] in [1,2,3,4] for a in p),p
        r=catch(p[0]['id'],p[0]['element'],scene+' 原版喷射弹真实碰撞附着','Vomit Glob',stage=True,settle=False)
        passed(scene+' 弹幕独立随机元素及轮廓',projectiles=p,contact=r)
        command('pause',value=False);park();state(b,fsm,'Spit 3');time.sleep(.2)
        p=projectiles('Vomit Glob Nosk');assert p and all(a['element'] for a in p),p
        passed(scene+' 延迟连射生成路径同样附着',projectiles=p)

def hornet():
    b=setup('GG_Hornet_2','Hornet Boss 2');vulnerable(b)
    command('enemyAura',targetId=b['id'],element='Hydro');hit(b,'Pyro',1)
    assert current(b)['mechanic']['stacks']==1,current(b)
    region=next(o for o in objects() if o['path'].split('/')[-1]=='Barb Region')
    command('pause',value=True);command('objectState',objectId=region['id'],fsm='Spawn Barbs',state='Spawn 1')
    p=projectiles('Hornet Barb');assert p and all(a['element'] for a in p),p
    r=catch(p[0]['id'],p[0]['element'],'岗哨陷阱真实碰撞附着','Hornet Barb',stage=True,settle=False)
    passed('岗哨继承反应叠层并给每个陷阱独立元素',boss=current(b),traps=p,contact=r)

def noeyes():
    for scene in ['GG_Ghost_No_Eyes','GG_Ghost_No_Eyes_V']:
        b=setup(scene,'Ghost Warrior No Eyes');command('pause',value=True);state(b,'Shot Spawn','Spawn L')
        p=projectiles('No Eyes Head');assert p and all(a['element'] and a['tinted'] for a in p),p
        r=catch(p[0]['id'],p[0]['element'],scene+' 幽灵真实碰撞附着','No Eyes Head',stage=True,settle=False)
        passed(scene+' 幽灵独立元素与染色',projectiles=p,contact=r)

def nextattack():command('pause',value=False);park();command('pause',value=True)

def sly():
    b=setup('GG_Sly','Sly Boss');command('loadout',Nail='Pyro',GreatSlash='Hydro',DashSlash='Electro',Cyclone='Cryo')
    command('pause',value=True)
    for attack,child,element in [('Slash S1','/S1',1),('GSlash 1','/S1',2),('D Slash S1','/DS1',3),('Cyclone Start','Cyclone Tink',4),('Rage Slash','Spin Tink',4)]:
        state(b,'Control',attack)
        candidates=[o for o in objects() if o['path'].endswith(child) and 'DamageHero' in o['components']]
        obj=candidates[0];active(obj)
        for o in objects():
            if o['id']!=obj['id'] and 'DamageHero' in o['components'] and o['path'].split('/')[-1] in ['S1','S2','S3','S4','GS1','GS2','DS1','Spin Tink','Cyclone Tink','Shockwave']:
                command('objectActive',objectId=o['id'],value=False)
        r=catch(obj['id'],element,'斯莱 '+attack+' 对应玩家元素',child,settle=False,
                after_clear=lambda:command('enemyControl',targetId=b['id'],seconds=4,hard=True))
        passed('斯莱招式映射 '+attack,contact=r,visuals=command('weaponVisuals'));nextattack()
    command('loadout',Cyclone='Electro');state(b,'Control','Rage Slash')
    assert current(b)['mechanic']['element']==3,current(b)
    passed('斯莱动态读取修改后的玩家配装',boss=current(b))
    audit('斯莱刀光与脱离攻击物卸载还原')

def replacements():
    for scene,name,fsm,entry,expected in [('GG_Traitor_Lord','Mantis Traitor Lord','Mantis','Attack Antic','Slam Antic'),('GG_Hollow_Knight','HK Prime','Control','Slash1 Antic','Tendril Antic')]:
        b=setup(scene,name);vulnerable(b);command('pause',value=True)
        command('enemyAura',targetId=b['id'],element='Hydro');hit(b,'Pyro',1)
        command('enemyAura',targetId=b['id'],element='Cryo');hit(b,'Pyro',1)
        s=current(b);assert s['mechanic']['pending'] and s['mechanic']['replacements']==0,s
        r=state(b,fsm,entry);assert r['state']==expected,r
        s=current(b);assert not s['mechanic']['pending'] and s['mechanic']['replacements']==1,s
        r=state(b,fsm,entry);assert r['state']==entry,r
        passed(scene+' 多次反应只替换一次下一招、不打断当前招',boss=s,next=r)

def pure():
    b=setup('GG_Hollow_Knight','HK Prime');command('pause',value=True)
    state(b,'Control','SmallShot LowHigh');command('pause',value=False);time.sleep(.15);command('pause',value=True)
    p=projectiles('Shot HK');assert p and all(a['element']==2 for a in p),p
    r=catch(p[0]['id'],2,'纯粹容器扇形剑阵真实水附着','Shot HK',stage=True,settle=False);passed('扇形水剑',projectiles=p,contact=r)
    nextattack();state(b,'Control','Plume Gen');p=projectiles('HK Plume Prime');assert p and all(a['element']==1 for a in p),p
    r=catch(p[0]['id'],1,'纯粹容器竖直剑阵真实火附着','HK Plume',stage=True,settle=False);passed('竖向火剑',projectiles=p,contact=r)
    nextattack();blast=next(o for o in objects() if o['path'].split('/')[-1]=='HK Prime Blast')
    active(blast);command('animateOffscreen',objectId=blast['id']);command('objectState',objectId=blast['id'],fsm='Control',state='Blast')
    r=catch(blast['id'],3,'纯粹容器聚焦爆炸真实雷附着','HK Prime Blast',stage=True,settle=False);passed('聚焦雷爆炸',contact=r)

def markoth():
    for scene in ['GG_Ghost_Markoth','GG_Ghost_Markoth_V']:
        b=setup(scene,'Ghost Warrior Markoth');command('pause',value=True)
        shields=command('markothShields');assert {s['element'] for s in shields}=={1,2},shields
        first=next(s for s in shields if s['element']==1);second=next(s for s in shields if s['element']==2)
        root=next(o for o in objects() if o['path']==first['path'].rsplit('/',1)[0])
        command('objectState',objectId=root['id'],fsm='Summon Shield',state='Summon');state(b,'Attacking','Nail')
        p=projectiles('Shot Markoth Nail');assert p and all(a['element'] in [3,4] for a in p),p
        command('hitActor',objectId=first['id'],source='WhiteWave',element='Hydro',damage=20,spell=True)
        assert current(b)['mechanic']['clears']==0
        command('hitActor',objectId=first['id'],source='Nail',element='Hydro',damage=20)
        s=current(b);assert s['mechanic']['clears']==1 and not projectiles('Shot Markoth Nail'),s
        before=command('markothShields');command('pause',value=False);time.sleep(.8);command('pause',value=True);after=command('markothShields')
        assert before[0]['rotation']==after[0]['rotation'],(before,after)
        command('hitActor',objectId=second['id'],source='GreatSlash',element='Pyro',damage=20)
        assert current(b)['mechanic']['clears']==1,current(b)
        passed(scene+' 双盾固定附着、非咒术增幅清剑阵、停转、共享冷却',shields=shields,before=before,after=after,boss=s)
        nextattack();command('pause',value=False);wait(lambda value:value['time']>s['mechanic']['stoppedUntil']+.1,6)
        s1=command('markothShields');time.sleep(.4);s2=command('markothShields');assert s1[0]['rotation']!=s2[0]['rotation'],(s1,s2)
        passed(scene+' 3秒停转结束后恢复',before=s1,after=s2)
    audit('马科斯无血量盾牌、轮廓和局部时钟卸载清理')

def zote():
    b=setup('GG_Grey_Prince_Zote','Grey Prince');command('pause',value=True)
    for attack,want in [('Land Waves',{3}),('Ft Waves',{2}),('Slash Waves L',{2,3})]:
        state(b,'Control',attack);p=projectiles('Shockwave Wave Z')
        assert want.issubset({a['element'] for a in p}),(attack,p)
        command('pause',value=False);time.sleep(.1);command('pause',value=True)
        spurts=projectiles('Shockwave Spurt');assert spurts and want.issubset({a['element'] for a in spurts}),(attack,spurts)
        shot=next(a for a in spurts if a['element'] in want)
        r=catch(shot['id'],shot['element'],'佐特 '+attack+' 实际喷涌附着','Shockwave Spurt',stage=True,settle=False)
        passed('佐特按可见高度区分水雷 '+attack,waves=p,spurts=spurts,contact=r);nextattack()
        for p in projectiles():command('projectileDisable',objectId=p['id'])
    balloon=next(o for o in objects() if o['path'].split('/')[-1]=='Zote Balloon' and 'HealthManager' in o['components'])
    command('objectState',objectId=balloon['id'],fsm='Control',state='Explode');time.sleep(.1)
    p=projectiles('Gas Explosion M');assert p and all(a['element']==1 for a in p),p
    r=catch(p[0]['id'],1,'不稳定迷你佐特爆炸真实火附着','Gas Explosion',stage=True,settle=False)
    passed('迷你佐特爆炸为火，其余召唤类型保持原版',projectiles=p,contact=r)

def white():
    b=setup('GG_White_Defender','White Defender');vulnerable(b);command('pause',value=True)
    state(b,'Dung Defender','Throw 1');p=projectiles('Dung Ball Large W');assert p and p[0]['element'],p
    r=catch(p[0]['id'],p[0]['element'],'白色防御者粪球真实元素附着','Dung Ball Large W',stage=True,settle=False)
    passed('白色防御者元素粪球及轮廓',projectile=p[0],contact=r);nextattack()
    state(b,'Dung Defender','RJ Launch');s=current(b);assert s['mechanic']['ball'] and s['mechanic']['tempo']==1.3 and s['state']['Aura']==3,s
    vulnerable(b);command('enemyAura',targetId=b['id'],element='Pyro');hit(b,'Electro',1)
    command('pause',value=False);time.sleep(.2);s=current(b);assert s['mechanic']['knockdowns']>=1,s
    passed('白色防御者普通自附雷、球速130%、超载倒地',boss=s)

def champion():
    b=setup('GG_Failed_Champion','False Knight Dream');vulnerable(b)
    r=hit(b,'Pyro',20);assert loss(r)==20 and r['state']['Aura']==0,r
    r=hit(b,'None',20);assert loss(r)==15,r
    hit(b,'None',1000)
    s=wait(lambda s:any(e['name']=='Head' and not e['IsInvincible'] for e in s['enemies']),15);head=enemy(s,'Head')
    hit(head,'Cryo',10);state(b,'FalseyControl','Stun Fail');s=current(b)
    assert s['state']['IntrinsicAura']==4,s
    vulnerable(b);r=hit(b,'Pyro',20);assert loss(r)==40,r
    passed('失败冠军复用75%无元素外壳、露头附着与无限固有反应',boss=s,hit=r)

def radiance():
    b=setup('GG_Radiance','Absolute Radiance');vulnerable(b);command('enemyHp',targetId=b['id'],value=10000)
    command('pause',value=True);hit(b,'Pyro',1);first=current(b)
    assert first['mechanic']['colour']==1 and first['state']['IntrinsicAura']==1,first
    hit(b,'Hydro',20);s=current(b);assert s['mechanic']['followup']==2 and s['mechanic']['until']==first['mechanic']['until'],s
    hit(b,'Cryo',1);assert current(b)['mechanic']['followup']==2
    beams=[o for o in objects('Eye Beam Glow/Burst 1') if 'DamageHero' in o['components']]
    beam=beams[0];active(beam);command('objectState',objectId=beam['id'],fsm='Control',state='Fire')
    # Test one known source after a different pre-existing aura; native health acceptance is required.
    command('clear');command('heal');command('ready');command('playerAura',element='Cryo');command('invincible',value=False)
    r=command('contactHit',objectId=beam['id'],damage=1);command('invincible',value=True)
    assert r['before']-r['after']==2 and r['player']['Aura']==2,r
    passed('无上辐光固定10秒染色与首次后手锁定；真实伤害入口清旧附着后逆向蒸发',boss=s,contact=r)
    command('pause',value=False);park();command('pause',value=True)
    command('enemyControl',targetId=b['id'],seconds=4,hard=True);active(beam);command('ready')
    command('objectState',objectId=beam['id'],fsm='Control',state='Fire')
    # catch clears the player only; boss colour/record is deliberately preserved.
    command('clearPlayerHits');command('heal');command('interceptAttack',objectId=beam['id'],seconds=2);command('invincible',value=False);command('pause',value=False)
    wait(lambda s:bool(command('playerHits')),5);command('invincible',value=True);command('pause',value=True)
    received=command('playerHits')[0];assert received['element']==1 and received['before']-received['after']==3,received
    passed('无上辐光中心光线真实物理碰撞触发记录反应',hit=received)
    command('pause',value=False)
    wait(lambda s:s['time']>first['mechanic']['until']+.2,15);s=current(b)
    assert s['mechanic']['colour']==0 and s['mechanic']['followup']==0 and s['state']['IntrinsicAura']==0,s
    vulnerable(b);hit(b,'Electro',1);assert current(b)['mechanic']['colour']==3
    passed('染色到期清除记录并允许重新染色',boss=s,new=current(b))
    audit('无上辐光身体染色、光线和附着记录卸载清理')

def markoth_contacts():
    b=setup('GG_Ghost_Markoth_V','Ghost Warrior Markoth');command('pause',value=True)
    shields=command('markothShields');first=next(s for s in shields if s['element']==1)
    root=next(o for o in objects() if o['path']==first['path'].rsplit('/',1)[0]);command('objectState',objectId=root['id'],fsm='Summon Shield',state='Summon')
    for element in [1,2]:
        shield=next(s for s in command('markothShields') if s['element']==element)
        r=catch(shield['id'],element,'马科斯'+('火' if element==1 else '水')+'盾真实碰撞附着','Shield',stage=True,settle=False)
        passed('马科斯盾实际伤害',contact=r);nextattack()
    state(b,'Attacking','Nail');p=projectiles('Shot Markoth Nail')[0]
    r=catch(p['id'],p['element'],'马科斯剑阵真实碰撞附着','Shot Markoth Nail',stage=True,settle=False)
    passed('马科斯雷冰剑实际伤害',projectile=p,contact=r)

def radiance_attacks():
    b=setup('GG_Radiance','Absolute Radiance');vulnerable(b);command('enemyHp',targetId=b['id'],value=10000);command('ready');command('pause',value=True)
    state(b,'Attack Commands','Spawn Fireball');p=projectiles('Radiant Orb')[0]
    command('objectState',objectId=p['id'],fsm='Orb Control',state='Init')
    command('objectState',objectId=p['id'],fsm='Orb Control',state='Chase Hero')
    command('position',objectId=p['id'],x=60,y=25)
    r=catch(p['id'],p['element'],'辐光光球独立随机元素真实碰撞','Radiant Orb',settle=False,
            after_clear=lambda:command('enemyControl',targetId=b['id'],seconds=4,hard=True))
    passed('辐光光球随机元素与轮廓',projectile=p,contact=r);nextattack()
    sweeper=next(o for o in objects() if o['path'].split('/')[-1]=='Beam Sweeper');active(sweeper)
    command('objectState',objectId=sweeper['id'],fsm='Control',state='Beam Sweep L');command('pause',value=False);time.sleep(.1);command('pause',value=True)
    p=projectiles('Radiant Beam R')[0];assert not p['tinted'] and not p['outlineVisible'],p
    r=catch(p['id'],1,'辐光竖直移动光柱真实火附着','Radiant Beam R',stage=True,settle=False)
    passed('辐光移动光柱固定火且无额外染色轮廓',projectile=p,contact=r);nextattack()
    vulnerable(b);hit(b,'Pyro',1);hit(b,'Hydro',1);state(b,'Attack Commands','CW Spawn');p=projectiles('Radiant Nail')[0]
    command('position',objectId=p['id'],x=60,y=25)
    command('ready');r=catch(p['id'],1,'辐光光剑动态染色元素真实碰撞','Radiant Nail',settle=False,aura_expected=2,
            after_clear=lambda:command('enemyControl',targetId=b['id'],seconds=4,hard=True))
    assert r['before']-r['after']==3,r
    passed('辐光光剑也触发记录后的逆向增幅',projectile=p,contact=r)

if __name__=='__main__':
    case=sys.argv[1];OUT.mkdir(parents=True,exist_ok=True)
    report=dict(passed=False)
    try:
        globals()[case]();report['passed']=True
    except Exception as error:report['error']=repr(error);raise
    finally:
        report.update(tests=tests,physicalContacts=contacts,commands=LOG)
        (OUT/(case+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        command('pause',value=False)
