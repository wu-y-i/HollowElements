"""Native charm acceptance; opt-in bridge and a fresh full save backup required."""
import argparse
import hashlib
import json
import time
from runtime_acceptance import command,ROOT,SESSION,LOG
from balance_acceptance import room
from element_guide_update_acceptance import open_page

OUT=ROOT/'artifacts/acceptance/charms'
IDS=['None','C01','C06','C07','C31','C05','C08','C09','C12','C13','C14','C17','C19','C21','C03','C27','C28','C35','C36','C39','C40']


def state():return command('charms')
def close():
    if command('element_guide')['visible']:
        command('element_guide',action='close');time.sleep(.65)


def page():
    open_page();command('charms',action='show')


def equipped():return [IDS[i] for i in state()['equipped']['Slots'] if i]


def equip(*ids):
    page();command('bench',value=True)
    for old in equipped():command('charms',action='toggle',charm=old)
    for id in ids:command('charms',action='toggle',charm=id)
    assert set(equipped())==set(ids),(equipped(),ids)
    command('bench',value=False);close();command('clear')


def hit(b,element,source='WhiteWave',cast=None,damage=10):
    v=dict(targetId=b['id'],element=element,source=source,damage=damage)
    if cast is not None:v['cast']=cast
    return command('hit',**v)


def aura(b,element,units=1):command('enemyAura',targetId=b['id'],element=element,units=units)
def dummy():return next(e for e in command('status')['enemies'] if e['name'].startswith('反应木桩'))
def capture(name):
    command('capture');time.sleep(.25);(OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())


def ui():
    page();r=state();assert r['buttons']==20 and r['icons']==20,r
    command('menuSetting',name='允许非长椅配置元素',index=1);command('bench',value=False)
    before=state()['equipped'];r=command('charms',action='toggle',charm='C01');assert r['equipped']==before and not r['canEquip']
    command('bench',value=True)
    for id in equipped():command('charms',action='toggle',charm=id)
    assert '空位' not in command('element_guide')['text']
    capture('page-empty-patterns')
    for id in ['C01','C05','C13','C14','C40']:command('charms',action='toggle',charm=id)
    assert set(equipped())=={'C01','C05','C13','C14','C40'}
    before=state()['equipped'];r=command('charms',action='toggle',charm='C17');assert r['equipped']==before
    r=command('charms',action='toggle',charm='C06');assert 'C01' not in equipped() and 'C06' in equipped()
    descriptions=[]
    for id in IDS[1:]:
        r=command('charms',charm=id);assert r['description'] and not any(c.isdigit() for c in r['description'])
        c=command('element_guide');assert not c['missingCharacters'] and not c['error'],c
        descriptions.append(dict(id=id,text=r['description']))
    command('charms',charm='C01')
    for expected in ['C05','C13','C35','C01']:
        assert command('charms',action='navigate',vertical=1)['selected']==expected
    capture('page-equipped-1280')
    command('resolution',width=1024,height=768);time.sleep(.8);capture('page-equipped-1024')
    command('resolution',width=1280,height=720);time.sleep(.8)
    command('bench',value=False);close()
    return dict(descriptions=descriptions,limits=True,awayFromBenchBlocked=True,keyboardControllerNavigation=True)


def core():
    rows=[];b=dummy()
    equip('C06')
    for n,element in enumerate(['Pyro','Hydro','Electro','Cryo']):hit(b,element,cast=100+n)
    assert state()['state']['FourUntil']>command('status')['time']
    hit(b,'Pyro',cast=110);r=hit(b,'Pyro',cast=111);assert r['before']-r['after']==11,r
    rows.append('four phase direct hits and growth')
    equip('C31')
    for source in ['WhiteWave','BlackDiveShock','BlackShriek']:hit(b,'Pyro',source)
    assert state()['state']['SpellCharges']==3
    aura(b,'Pyro',100)
    for _ in range(4):
        r=hit(b,'Hydro','BlackShriek',cast=200);assert r['before']-r['after']==40 and r['state']['AuraUnits']==100,r
    assert state()['state']['SpellCharges']==2
    rows.append('three-spell charge, four independent shriek hits, one charge per cast')
    equip('C07','C12');aura(b,'Cryo')
    r=hit(b,'Pyro','GreatSlash');assert r['state']['Charms']['FireUnits']==2,r
    r=hit(b,'Hydro','GreatSlash');assert r['state']['Charms']['FireUnits']==1,r
    r=hit(b,'Hydro','GreatSlash');assert r['state']['Charms']['FireUnits']==0,r
    rows.append('special fire consumes independently and cannot refill itself')
    equip('C07','C05');hit(b,'None','Nail');aura(b,'Cryo');r=hit(b,'Pyro');assert r['state']['Aura']==1 and r['state']['AuraUnits']==1.5 and r['state']['AuraDuration']==12,r
    rows.append('interwoven remainder includes ember attachment strength and duration')
    equip('C03','C21');aura(b,'Cryo');hit(b,'Electro');command('soul',value=0)
    r=hit(b,'Pyro','GreatSlash');nail=command('status')['hero']['nail'];expected=10+int(nail*.5*1.5*2+1e-8)
    assert r['before']-r['after']==expected and command('status')['hero']['soul']>0,r
    rows.append('insulated neutral sword plus native soul gain')
    equip('C17','C19');aura(b,'Hydro');hit(b,'Electro');command('pause',value=True)
    r=hit(b,'None','Cyclone');assert r['state']['ElectroCharged']['Damage']==max(1,int(nail*.45)//2),r
    for _ in range(6):r=hit(b,'None','Cyclone')
    assert r['state']['ElectroCharged']['Damage']==1,r
    aura(b,'Hydro');r=hit(b,'Hydro');assert r['state']['ElectroCharged']['Damage']==1,r
    command('pause',value=False);rows.append('cyclone repeated cash-out minimum one and extension without restoring damage')
    equip('C13','C14');aura(b,'Pyro');r=hit(b,'Electro');initial=r['after'];assert state()['pendingExplosions']>0
    time.sleep(.6);after=dummy()['hp'];assert initial-after>=nail*2,(initial,after,nail)
    time.sleep(1.2);assert state()['pendingExplosions']==0
    capture('overload-after');rows.append('delayed amplified blast and three expiring sparks')
    return dict(checks=rows)


def protection():
    b=dummy();equip('C27','C28','C40');aura(b,'Hydro');r=hit(b,'Cryo');assert abs(r['state']['FrozenSpeed']-.7)<1e-7,r
    hit(b,'None','GreatSlash');assert state()['state']['Masks']==1
    time.sleep(.2);capture('temporary-mask')
    command('blueHealth',red=9,blue=0);command('invincible',value=False);command('absoluteInvincible',value=False)
    before=command('status')['hero']['health'];r=command('hurt',element='None',damage=4)
    assert r and command('status')['hero']['health']==before and state()['state']['Masks']==0,(r,state())
    time.sleep(1.8);command('hurt',element='None',damage=1);assert command('status')['hero']['health']<before
    mask_native=state()['maskSprites']
    equip('C35');command('heal');time.sleep(1.8)
    command('playerAura',element='Hydro');assert command('hurt',element='Electro',damage=1)
    before=command('status')['hero']['health'];time.sleep(4.5);r=command('status')['hero']
    assert r['health']==before and r['state']['ElectroCharged']['RemainingTicks']==2,r
    equip('C39');command('heal');time.sleep(1.8)
    aura(b,'Hydro');hit(b,'Electro');aura(b,'Pyro');hit(b,'Hydro')
    veil=state()['state']['VeilUntil'];assert veil>command('status')['time']
    time.sleep(1)
    command('playerAura',element='Cryo');assert command('hurt',element='Electro',damage=1);time.sleep(1.4)
    hero=command('status')['hero'];assert hero['state']['SuperconductUntil']>veil and hero['canCast'],hero
    while command('status')['time']<veil+.1:time.sleep(.1)
    hero=command('status')['hero'];assert hero['state']['SuperconductUntil']>command('status')['time'] and not hero['canCast'],hero
    return dict(maskAbsorbsWholeHit=True,ordinaryDamageAfterMask=True,nativeMaskSprites=mask_native,electroImmunity=True,veilStoresNewDebuffThenRestores=True)


def persistence():
    equip('C01','C09','C13','C14','C40');before=state()['equipped']
    r=command('saveRoundTrip',slot=3);assert r['saved'] and r['loaded'] and r['matched'],r
    assert state()['equipped']==before
    page();command('charms',charm='C40');capture('page-final')
    command('unloadAudit',reload=True);time.sleep(2)
    audit=json.loads((SESSION/'unload-audit.json').read_text(encoding='utf-8-sig'));assert audit['passed'] and audit['remainingCharmAssets']==0,audit
    for _ in range(60):
        if command('element_guide')['attached']:break
        time.sleep(.1)
    command('ready');page();assert state()['icons']==20
    close()
    return dict(saveRoundTrip=r,unload=audit,reopened=True)


def boss():
    from boss_26_42_acceptance import setup,current,state as fsm,vulnerable
    equip('C13','C14');rows=[]
    for scene,name in [('GG_Crystal_Guardian_2','Zombie Beam Miner Rematch'),('GG_White_Defender','White Defender'),('GG_Hornet_1','Hornet Boss 1')]:
        b=setup(scene,name);command('absoluteInvincible',value=True);command('pause',value=True)
        command('enemyHp',targetId=b['id'],value=10000);vulnerable(b)
        if scene=='GG_White_Defender':fsm(b,'Dung Defender','Idle')
        position=[float(x) for x in current(b)['position'].strip('()').split(',')[:2]]
        aura(b,'Pyro');r=hit(b,'Electro');marked=current(b)['mechanic']
        if scene=='GG_White_Defender':assert marked['knockdowns']==0 and marked['overloads']==0,marked
        if scene=='GG_Crystal_Guardian_2':assert marked['shield'],marked
        command('pause',value=False);until=time.monotonic()+2.1
        while time.monotonic()<until:
            # Hold this test actor in the blast footprint so movement cannot turn
            # a counter assertion into a test of incidental spark accuracy.
            command('position',targetId=b['id'],x=position[0],y=position[1]);time.sleep(.08)
        final=current(b)['mechanic']
        if scene=='GG_White_Defender':assert final['knockdowns']>=1 and final['stacks']>=1,final
        if scene=='GG_Crystal_Guardian_2':assert not final['shield'],final
        if scene=='GG_Hornet_1':assert marked['stacks']==1 and final['stacks']==1,(marked,final)
        rows.append(dict(scene=scene,marked=marked,afterBlasts=final))
    room()
    return dict(actorsHeldInBlastFootprint=True,cases=rows)


CASES=dict(ui=ui,core=core,protection=protection,boss=boss,persistence=persistence)
def main():
    p=argparse.ArgumentParser();p.add_argument('cases',nargs='*');args=p.parse_args();OUT.mkdir(parents=True,exist_ok=True)
    if not command('status')['hero']:
        command('load',slot=3)
        for _ in range(120):
            if command('element_guide')['canOpen']:break
            time.sleep(.2)
    if not command('element_guide')['visible']:room()
    command('damageReductionMenu',index=3)
    digest=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest()
    for name in args.cases or CASES:
        start=len(LOG)
        try:
            result=CASES[name]();report=dict(passed=True,dllSha256=digest,result=result)
            print('PASS',name,flush=True)
        except Exception as error:
            report=dict(passed=False,dllSha256=digest,error=repr(error));raise
        finally:
            report['commands']=LOG[start:];(OUT/(name+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')


if __name__=='__main__':main()
