"""Exercise the actual Modding API menu callbacks and combat paths in an isolated game session."""
import hashlib,json,time
from balance_acceptance import room,reset,aura,hit,loss,status,command,ROOT,SESSION,LOG

def main():
    assert json.loads((SESSION/'connected.json').read_text(encoding='utf-8-sig'))['version']=='0.1.5.1'
    b=room();rows=[]
    original=command('damageReductionMenu')
    assert original['Values']==['55%','50%','20%','0%'],original
    try:
        for index,multiplier,plain,forward,reverse in [(0,.45,14,57,36),(1,.5,16,64,40),(2,.8,25,102,64),(3,1,32,128,80)]:
            menu=command('damageReductionMenu',index=index)
            assert menu['index']==index and menu['multiplier']==multiplier and menu['saved']==multiplier,menu
            for base,incoming,expected in [('None','None',plain),('Pyro','Hydro',forward),('Hydro','Pyro',reverse)]:
                reset(b)
                if base!='None':aura(b,base)
                r=hit(b,incoming,32);assert loss(r)==expected,(index,base,incoming,r)
            reset(b);aura(b,'Pyro');r=hit(b,'Electro',32)
            assert loss(r)==plain+status()['hero']['nail'],r
            reset(b);aura(b,'Hydro');r=hit(b,'Electro',32)
            assert r['state']['ElectroCharged']['Damage']==int(status()['hero']['nail']*.45),r
            reset(b);r=command('unclassifiedHit',targetId=b['id'],player=False,damage=32)
            assert loss(r)==32,r
            rows.append(dict(menu=menu,neutral=plain,forward=forward,reverse=reverse,overloadUnscaled=True,electroUnscaled=True,worldDamage=32))
        # Reinitialization must load the saved 0% setting, not reset to 55%.
        command('pause',value=True)
        audit=SESSION/'unload-audit.json';before=audit.stat().st_mtime_ns if audit.exists() else 0
        command('unloadAudit',reload=True)
        for _ in range(100):
            if audit.exists() and audit.stat().st_mtime_ns!=before:break
            time.sleep(.1)
        audit_result=json.loads(audit.read_text(encoding='utf-8-sig'));assert audit_result['passed'],audit_result
        time.sleep(1)
        reloaded=command('damageReductionMenu');assert reloaded['index']==3 and reloaded['multiplier']==reloaded['saved']==1,reloaded
    finally:
        command('damageReductionMenu',index=original['index'])
        command('debugMode',value=False)
    result=dict(passed=True,version='0.1.5.1',dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(),cases=rows,reloaded=reloaded,audit=audit_result,restored=command('damageReductionMenu'),commands=LOG)
    (ROOT/'artifacts/acceptance/damage-menu.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    command('quit')
    print('PASS four menu presets, saved config, direct/amp damage, unscaled reactions/world damage, unload/reload persistence')

if __name__=='__main__':main()
