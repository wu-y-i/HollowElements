"""Focused v0.1.8 checks in a save-suppressed native game with EnemyHPBar installed."""
import argparse,hashlib,json,time
from runtime_acceptance import ROOT,SESSION,command,LOG
from boss_26_42_acceptance import setup,state,hit
from balance_acceptance import room
OUT=ROOT/'artifacts/acceptance/ui-balance'

def grimm():
    results=[]
    for electro in [True,False]:
        b=setup('GG_Grimm_Nightmare','Nightmare Grimm Boss');command('pause',value=True)
        state(b,'Control','Idle');command('enemyVulnerable',targetId=b['id'])
        old=command('deathCalls').get(str(b['id']),0)
        if electro:
            command('enemyHp',targetId=b['id'],value=200);command('enemyAura',targetId=b['id'],element='Hydro')
            hit(b,'Electro',1);command('enemyHp',targetId=b['id'],value=1)
        else:
            command('enemyHp',targetId=b['id'],value=1);hit(b,'None',100)
        command('pause',value=False);rows=[]
        for _ in range(7):
            time.sleep(.7);s=command('status')
            rows.append(dict(calls=command('deathCalls').get(str(b['id']),0)-old,
                actors=[e for e in s['enemies'] if e['id']==b['id']],
                control=[f['ActiveStateName'] for f in command('dumpAll') if f['obj']==b['name'] and f['FsmName']=='Control']))
        assert rows[-1]['calls']==1,rows
        assert all(e['state']['ElectroCharged'] is None for e in rows[-1]['actors']),rows[-1]
        assert 'Death Start' not in rows[-1]['control'],rows
        results.append(dict(electro=electro,rows=rows))
    return results

def cyclone():
    b=room();command('clear');command('damageReductionMenu',index=2);command('enemyHp',targetId=b['id'],value=10000)
    command('enemyAura',targetId=b['id'],element='Hydro',units=100)
    hits=[]
    for _ in range(12):
        r=hit(b,'Pyro',20,'Cyclone',cast=870001);hits.append(r)
        assert r['before']-r['after']==40,r
    assert abs(hits[-1]['state']['AuraUnits']-97)<1e-7,hits[-1]
    return dict(twelveHitsInOneCast=hits,auraConsumedPerHit=.25,attachmentUnits=.5)

def healthbars():
    b=room();command('clear');command('enemyHp',targetId=b['id'],value=1000)
    command('externalBar',targetId=b['id'],kind='enemy',attach=True)
    hit(b,'None',20);command('enemyAura',targetId=b['id'],element='Cryo')
    command('menuSetting',name='显示本 Mod 血条',index=0);time.sleep(.3)
    def check(actor,kind):
        source=command('externalBar',targetId=actor['id'],kind=kind)
        ui=command('fixDiagnostics')['ui'];bar=next(v for v in ui['bars'] if v['id']==actor['id'])
        assert source['alpha']>0 and bar['visible'] and not bar['barVisible'] and bar['external'],(source,bar)
        assert abs(bar['x']-(source['x']-ui['screen']['width']/2)/source['scale'])<1,(source,bar)
        assert abs(bar['y']-(source['y']-ui['screen']['height']/2)/source['scale'])<1,(source,bar)
        assert bar['statuses'],bar
        return dict(source=source,bar=bar)
    enemy=check(b,'enemy');command('capture');time.sleep(.3);(OUT/'external-enemy.png').write_bytes((SESSION/'game.png').read_bytes())
    b=setup('GG_Hornet_1','Hornet Boss 1');command('enemyVulnerable',targetId=b['id']);hit(b,'None',32)
    command('enemyAura',targetId=b['id'],element='Hydro');time.sleep(.3);boss=check(b,'boss')
    command('menuSetting',name='显示本 Mod 血条',index=1);time.sleep(.2)
    bar=next(v for v in command('fixDiagnostics')['ui']['bars'] if v['id']==b['id'])
    assert bar['barVisible'] and not bar['external'] and bar['statuses'],bar
    return dict(enemy=enemy,boss=boss,restore=bar)

def menu():
    room();command('ready');command('nativePause');time.sleep(.7)
    r=command('settingsMenu',action='open');time.sleep(.7);r=command('settingsMenu',action='select',index=0)
    names=[o['name'] for o in r['options']]
    for i,o in enumerate(r['options']):
        assert o['up']==('BackButton' if i==0 else names[i-1]),o
        assert o['down']==('BackButton' if i==len(names)-1 else names[i+1]),o
        for text in o['texts']:assert text['fontSize']==(22 if text['name']=='Description' else 34),text
    visited=[r['selected']]
    for expected in names[1:]+['BackButton',names[0]]:
        r=command('settingsMenu',action='move',direction=1);assert r['selected']==expected,(expected,r['selected'])
        visited.append(r['selected'])
    command('settingsMenu',action='select',index=len(names)-1)
    time.sleep(.3);last=command('settingsMenu')
    assert all(s['hidden']<=0 or s['y']>=s['hidden']*.9 for s in last['scrolls']),last['scrolls']
    command('capture');time.sleep(.3);(OUT/'settings-last.png').write_bytes((SESSION/'game.png').read_bytes())
    return dict(options=r['options'],visited=visited,wrap=True,scrolls=last['scrolls'])

CASES=dict(grimm=grimm,cyclone=cyclone,healthbars=healthbars,menu=menu)
def main():
    p=argparse.ArgumentParser();p.add_argument('cases',nargs='*',choices=list(CASES));args=p.parse_args();OUT.mkdir(exist_ok=True)
    digest=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest()
    for name in args.cases or CASES:
        start=len(LOG)
        try:r=dict(passed=True,result=CASES[name]())
        except Exception as e:r=dict(passed=False,error=repr(e))
        r.update(dllSha256=digest,commands=LOG[start:]);(OUT/(name+'.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
        print('PASS' if r['passed'] else 'FAIL',name,r.get('error',''),flush=True)
if __name__=='__main__':main()
