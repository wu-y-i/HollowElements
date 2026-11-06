"""Native stagger and original Uumuu shield-break regressions for v0.1.4."""
import hashlib,json,time,traceback
from boss_fixes_acceptance import *

def native_stagger():
    rows=[]
    for scene,name in [('GG_Broken_Vessel','Infected Knight'),('GG_Lost_Kin','Lost Kin')]:
        b=setup(scene,name);pause();vulnerable(b);command('enemyHp',targetId=b['id'],value=10000)
        events=[]
        for index in range(30):
            vulnerable(b);r=hit(b,'None',1,'Nail');s=current(b)
            events.append(dict(index=index+1,loss=loss(r),fsms=s['fsms'],mechanic=s['mechanic']))
            if any(f['FsmName']=='IK Control' and f['ActiveStateName'].startswith('Stun') for f in s['fsms']):break
        assert index<29,(scene,events)
        assert s['mechanic']['knockdowns']==0,(scene,s)
        rows.append(dict(scene=scene,hits=events))
    return rows

def uumuu_original():
    rows=[]
    for scene in ['GG_Uumuu','GG_Uumuu_V']:
        b=setup(scene,'Mega Jellyfish GG')
        hit(b,'Hydro',20);time.sleep(.35);hit(b,'Cryo',20);time.sleep(.35);r=hit(b,'None',20,'Nail');time.sleep(.2)
        assert loss(r)==0 and not current(b)['mechanic']['shielded'],(r,current(b))
        assert current(b)['mechanic']['shatterBreaks']==1,current(b)
        command('clear');state(b,'Mega Jellyfish','Recover');command('pause',value=False);time.sleep(.6)
        assert current(b)['mechanic']['nodes']==2 and current(b)['mechanic']['shielded'],current(b)
        sp=next(o for o in objects() if o['path']=='Jellyfish Spawner')
        command('objectState',objectId=sp['id'],fsm='Spawn',state='Spawn');time.sleep(.4)
        corpse=next(o for o in objects('Corpse Jellyfish') if 'PlayMakerFSM' in o['components'])
        point=[float(x) for x in current(b)['position'].strip('()').split(',')[:2]]
        command('position',objectId=corpse['id'],x=point[0],y=point[1]);active(corpse);time.sleep(.5)
        assert not current(b)['mechanic']['shielded'],current(b)
        rows.append(dict(scene=scene,shatter=r,afterOoma=current(b)))
    return rows

if __name__=='__main__':
    report=dict(passed=True,cases={},dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest())
    for name in ['native_stagger','uumuu_original']:
        try:
            report['cases'][name]=dict(passed=True,evidence=globals()[name]());print('PASS',name,flush=True)
        except Exception as error:
            report['passed']=False;report['cases'][name]=dict(passed=False,error=repr(error));traceback.print_exc()
    report['commands']=LOG
    (OUT/'regressions.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
