"""Native regression: no ordinary Cryo residue, no Hydro-only freeze extension."""
import json,time,hashlib
from runtime_acceptance import command,ROOT,SESSION
from balance_acceptance import room
from boss_fixes_acceptance import current
import charms_acceptance as charms
OUT=ROOT/'artifacts/acceptance/no-ice';OUT.mkdir(parents=True,exist_ok=True)
def target():
 room();charms.equip();command('dummy',recoilable=True,offset=-3)
 return next(e for e in command('status')['enemies'] if e['name']=='反应木桩·可击退')
def freeze(b):
 command('clear');charms.aura(b,'Hydro');r=charms.hit(b,'Cryo');assert r['state']['HardFrozen'] and r['state']['Aura']==0,r;return r
def water():
 b=target();first=freeze(b);end=first['state']['FrozenUntil'];rows=[]
 for _ in range(20):
  r=charms.hit(b,'Hydro');s=r['state'];assert s['Aura']==2 and not (s['FrozenUntil']>end+.001),r;rows.append(r)
 assert current(b)['state']['FrozenUntil']==0
 fresh=charms.hit(b,'Cryo');assert fresh['state']['HardFrozen'] and fresh['state']['Aura']==0,fresh
 return dict(initial=first,hydroHits=rows,freshCryo=fresh)
def breaks():
 b=target();rows=[]
 for e in ['Pyro','Electro','None']:
  freeze(b);r=charms.hit(b,e);assert r['state']['Aura']==0 and r['state']['FrozenUntil']==0 and r['state']['HardFrozenUntil']==0,r
  time.sleep(1.1);after=current(b)['state'];assert after['FrozenUntil']==0 and not after['HardFrozen'],after
  rows.append(dict(element=e,hit=r,after=after))
 freeze(b);r=charms.hit(b,'Pyro','GreatSlash');assert r['state']['Aura']==1 and r['state']['FrozenUntil']==0 and r['before']-r['after']==39,r
 return dict(breaks=rows,sword=r)
def charms_case():
 b=target();charms.equip('C31')
 for source in ['WhiteWave','WhiteDiveShock','WhiteShriek']:charms.hit(b,'None',source)
 assert command('charms')['state']['SpellCharges']==3
 charms.aura(b,'Hydro');r=charms.hit(b,'Cryo');assert r['state']['Aura']!=4 and r['state']['FrozenUntil']>command('status')['time']
 before=command('charms')['state']['SpellCharges'];r=charms.hit(b,'Pyro');assert r['state']['Aura']!=4 and r['state']['FrozenUntil']==0 and command('charms')['state']['SpellCharges']==before-1,r
 water=charms.hit(b,'Hydro');assert water['state']['FrozenUntil']==0 and water['state']['Aura']==2,water
 charms.equip('C28');freeze(b);charms.hit(b,'None','GreatSlash');first=charms.hit(b,'Hydro');second=charms.hit(b,'Hydro');assert first['state']['FrozenUntil']>0 and second['state']['FrozenUntil']>first['state']['FrozenUntil'],(first,second)
 end=second['state']['FrozenUntil'];third=charms.hit(b,'Hydro');assert third['state']['FrozenUntil']==end and third['state']['Aura']==2,third
 return dict(empowered=r,hydro=water,iceShardFirst=first,iceShardSecond=second,chargesSpent=third)
def main():
 rows={key:fn() for key,fn in [('water',water),('breaks',breaks),('charms',charms_case)]}
 (OUT/'combat.json').write_text(json.dumps(dict(passed=True,result=rows,dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest()),ensure_ascii=False,indent=2),encoding='utf-8');print('PASS native no-ice combat',flush=True)
if __name__=='__main__':main()
