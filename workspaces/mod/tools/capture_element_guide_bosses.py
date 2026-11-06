"""Capture native mechanism moments for all 42 element_guide Boss entries in one session.

Uses the save-suppressed development bridge and native FSM actions; no mock images.
Each result records scene, action and a simple world-image check to reject blank frames.
"""
import argparse
import json
import time
from pathlib import Path
from PIL import Image, ImageStat
from runtime_acceptance import command, ROOT, SESSION
OUT=ROOT/'artifacts/acceptance/boss-update/photos'
CONTENT=ROOT/'workspaces/mod/content/ElementGuide'

# Native actor, FSM and preferred states. Only states present in the current
# prefab are dispatched; arena variants can use a different subset.
PLANS={
 1:('Giant Buzzer','Big Buzzer',['Roar Left']),2:('Giant Fly','Big Fly Control',['Dive Start','Charge']),
 3:('False Knight New','FalseyControl',['Stun Fail','Idle']),4:('Mega Moss Charger','Mossy Control',['Charge','Run']),
 5:('Hornet Boss 1','Control',['GDash','Throw']),6:('Ghost Warrior Slug','Attacking',['Attack']),
 7:('Dung Defender','Dung Defender',['Throw 1']),8:('Mage Knight','Mage Knight',['Shoot']),
 9:('Mawlek Body','Mawlek Control',['L','Shoot']),10:('Mato','nailmaster',['Slash S1','Idle']),
 11:('Ghost Warrior Xero','Attacking',['Summon']),12:('Mega Zombie Beam Miner (1)','Beam Miner',['Laser','Idle']),
 13:('Mage Lord','Mage Lord',['Shot']),14:('Mega Fat Bee','Fatty Fly Attack',['Attack']),
 15:('Mantis Lord S1','Mantis Lord',['Throw','Dstab Antic','Dash Antic']),16:('Ghost Warrior Marmu','Control',['Chase']),
 17:('Fluke Mother','Fluke Mother',[]),18:('Infected Knight','IK Control',['Idle']),
 19:('Ghost Warrior Galien','Movement',['Idle','Chase']),20:('Paintmaster','nailmaster_sheo',['GSlash','GSlash Antic','Slash']),
 21:('Hive Knight','Control',['Glob Antic','Stomp Antic','Idle']),22:('Ghost Warrior Hu','Attacking',['M 1']),
 23:('Jar Collector','Control',['Summon?','Land']),24:('Lobster','Control',['Idle']),
 25:('Grimm Boss','Control',['Firebat 1']),26:('Black Knight 1','Black Knight',['Idle']),
 27:('Mega Jellyfish GG','Mega Jellyfish',['Idle']),28:('Mimic Spider','Mimic Spider',['Spit 1']),
 29:('Sly Boss','Control',['GSlash 1']),30:('Hornet Boss 2','Control',['Idle']),
 31:('Zombie Beam Miner Rematch','Beam Miner',['Laser','Idle']),32:('Lost Kin','IK Control',['Idle']),
 33:('Ghost Warrior No Eyes','Shot Spawn',['Spawn L']),34:('Mantis Traitor Lord','Mantis',['Slam Antic']),
 35:('White Defender','Dung Defender',['Throw 1']),36:('Hornet Nosk','Hornet Nosk',['Spit 1']),
 37:('Ghost Warrior Markoth','Attacking',['Attack','Idle']),38:('Grey Prince','Control',['Land Waves']),
 39:('False Knight Dream','FalseyControl',['Stun Fail','Idle']),40:('Nightmare Grimm Boss','Control',['Firebat 1']),
 41:('HK Prime','Control',['SmallShot Antic']),42:('Absolute Radiance','Attack Commands',['Nail Fan'])}


def position(value):return [float(x) for x in value.strip('()').split(',')[:2]]
def status():return command('status')
def objects():return command('sceneObjects')
def actor_state(actor,fsm,state):return command('objectState',objectId=actor['id'],fsm=fsm,state=state)
def set_active(actor):
    parts=actor['path'].split('/')
    for n in range(1,len(parts)+1):
        candidate=next((o for o in objects() if o['path']=='/'.join(parts[:n])),None)
        if candidate and not candidate['active']:command('objectActive',objectId=candidate['id'],value=True)
def react(b,base='Pyro',incoming='Electro'):
    if b['name']!='Mega Jellyfish GG':command('enemyVulnerable',targetId=b['id'])
    command('enemyAura',targetId=b['id'],element=base)
    return command('hit',targetId=b['id'],source='WhiteWave',element=incoming,damage=32,attackType='Spell')
def state(b,fsm,name):command('enemyState',targetId=b['id'],fsm=fsm,state=name)
def moment(index,b):
    if index in [5,30]:
        for _ in range(3):react(b,'Pyro','Hydro')
        if index==30:
            region=next(o for o in objects() if o['path'].endswith('Barb Region'));actor_state(region,'Spawn Barbs','Spawn 1')
    elif index in [7,35]:
        for _ in range(1 if index==7 else 2):react(b);time.sleep(.15)
        state(b,'Dung Defender','Throw 1')
    elif index in [18,32]:
        for _ in range(1 if index==18 else 2):react(b);time.sleep(.15)
        state(b,'IK Control','Stun Recover');state(b,'IK Control','Idle')
    elif index==26:react(b);react(b,'Cryo','Electro')
    elif index==27:react(b)  # One of two shield nodes removed.
    elif index==24:command('enemyAura',targetId=b['id'],element='Hydro')
    elif index==42:
        react(b,'Hydro','Pyro');state(b,'Attack Commands','Nail Fan');state(b,'Attack Commands','CW Spawn')
    elif index==9:
        for _ in range(4):
            command('enemyVulnerable',targetId=b['id']);command('hit',targetId=b['id'],source='WhiteWave',element='None',damage=1,attackType='Spell')
    elif index in [3,39]:
        head=next((e for e in status()['enemies'] if e['name']=='Head'),None)
        if head:command('enemyAura',targetId=head['id'],element='Cryo')
    elif index==1:
        state(b,'Big Buzzer','Roar Left');time.sleep(.15);state(b,'Big Buzzer','Roar Left')
    elif index==15:
        sub=next(o for o in objects() if o['path']=='Mantis Battle/Battle Sub');actor_state(sub,'Start','Start')
        command('pause',value=False);time.sleep(1.5);command('pause',value=True)
        for actor in status()['enemies']:
            if actor['mechanic'] and actor['mechanic'].get('kind')=='Mantis':state(actor,'Mantis Lord','Throw')
    elif index==16:
        react(b);command('pause',value=False);time.sleep(3.3);command('pause',value=True)
    elif index==17:
        fly=next((o for o in objects() if 'Fluke Fly' in o['path'] and 'HealthManager' in o['components']),None)
        if fly:set_active(fly);actor_state(fly,'Fluke Fly','Spawn')


def capture(index):
    command('capture');time.sleep(.25)
    image=Image.open(SESSION/'game.png').convert('RGB')
    crop=image.crop((image.width*.15,image.height*.22,image.width*.85,image.height*.78))
    deviation=sum(ImageStat.Stat(crop).stddev)/3
    return image,deviation


def run(index,entry):
    scene=entry['Scenes'][0]
    command('pause',value=False);command('invincible',value=True);command('absoluteInvincible',value=True)
    command('goto',scene=scene)
    for _ in range(100):
        s=status()
        if s['scene']==scene and s['hero']:break
        time.sleep(.1)
    time.sleep(3.3);command('ready');command('bossLevel',value=0)
    s=status();spawn=position(s['hero']['position']);name,fsm,choices=PLANS[index]
    all_objects=objects();actor=next((o for o in all_objects if o['path'].split('/')[-1]==name and 'HealthManager' in o['components']),None)
    if actor:set_active(actor)
    s=status();b=next((e for e in s['enemies'] if e['name']==name),None)
    if not b:b=next((e for e in s['enemies'] if e['boss']),None)
    if not b:raise AssertionError('No live Boss actor')
    # Keep the native camera in the arena. Acceptance tests park outside the
    # arena; photos intentionally place the knight back at the real entry gate.
    command('position',target='hero',x=spawn[0],y=spawn[1]);command('ready');time.sleep(.6)
    command('pause',value=True);command('enemyHp',targetId=b['id'],value=10000)
    if index in [4,7,15,18,23,24,28,32,33,42]:
        command('position',targetId=b['id'],x=spawn[0]+6,y=spawn[1]+(8 if index==42 else 1))
    dump=command('dumpAll');native=next((f for f in dump if f['obj']==b['name'] and f['FsmName']==fsm),None)
    available={s['Name'] for s in native['states']} if native else set()
    action=next((n for n in choices if n in available),None)
    if action:state(b,fsm,action)
    moment(index,b)
    command('pause',value=False);time.sleep(.28 if index!=41 else .8);command('pause',value=True)
    image,score=capture(index)
    if score<4:
        command('position',target='hero',x=spawn[0],y=spawn[1]);command('ready');command('pause',value=False);time.sleep(1);command('pause',value=True)
        image,score=capture(index)
    assert score>=4,('Blank world capture',score)
    folder=CONTENT/'images/bosses';folder.mkdir(exist_ok=True)
    image.save(folder/(f'{index:02}.png'))
    return dict(passed=True,index=index,scene=scene,boss=b['name'],nativeState=action,worldDeviation=score,
                projectiles=[p for p in command('projectiles') if p['active']],mechanic=next((e['mechanic'] for e in status()['enemies'] if e['id']==b['id']),None))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('indices',nargs='*',type=int);args=parser.parse_args()
    OUT.mkdir(exist_ok=True,parents=True)
    book=json.loads((CONTENT/'index.json').read_text(encoding='utf-8'))
    entries={int(e['Id'].split('-')[1]):e for c in book['Categories'] for e in c['Entries'] if e['Id'].startswith('boss-')}
    for index in args.indices or sorted(entries):
        try:report=run(index,entries[index])
        except Exception as error:report=dict(passed=False,index=index,error=repr(error))
        (OUT/f'{index:02}.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        print(('PASS' if report['passed'] else 'FAIL'),'photo',index,report.get('error',''),flush=True)


if __name__=='__main__':main()
