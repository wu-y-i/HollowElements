"""Native checks for the user-edited guide mechanics and difficulty presets."""
import json,time,math,hashlib
from runtime_acceptance import command,ROOT,SESSION,LOG
from balance_acceptance import room
from boss_fixes_acceptance import current,setup
import charms_acceptance as charms
OUT=ROOT/'artifacts/acceptance/rule-update';OUT.mkdir(parents=True,exist_ok=True)
def fresh(b):command('clear');command('enemyHp',targetId=b['id'],value=100000)
def loss(r):return r['before']-r['after']
def capture(name):
 before=(SESSION/'game.png').stat().st_mtime_ns if (SESSION/'game.png').exists() else 0
 command('capture');time.sleep(.25);assert (SESSION/'game.png').stat().st_mtime_ns>before
 (OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())
def difficulty():
 b=room();charms.equip();nail=command('status')['hero']['nail'];rows=[]
 for i,direct,scale in [(0,.35,.65),(1,.45,.85),(2,.5,1),(3,.8,1),(4,1,1)]:
  command('damageReductionMenu',index=i);fresh(b);charms.aura(b,'Pyro');over=charms.hit(b,'Electro');assert loss(over)==math.floor(10*direct+1e-8)+math.floor(nail*scale+1e-8),(i,over)
  fresh(b);charms.aura(b,'Hydro');ec=charms.hit(b,'Electro');assert ec['state']['ElectroCharged']['Damage']==math.floor(nail*.45*scale+1e-8),(i,ec)
  fresh(b);charms.aura(b,'Cryo');charms.hit(b,'Electro');paid=charms.hit(b,'None','Nail');assert loss(paid)==math.floor(10*direct+1e-8)+math.floor(nail*.5*1.5*scale+1e-8),(i,paid)
  fresh(b);charms.aura(b,'Hydro');charms.hit(b,'Cryo');shatter=charms.hit(b,'None','Nail');assert loss(shatter)==math.floor(10*direct+1e-8)+math.floor(nail*1.5*scale+1e-8),(i,shatter)
  fresh(b);charms.aura(b,'Cryo');amp=charms.hit(b,'Pyro');assert loss(amp)==math.floor(10*direct*4+1e-8),(i,amp)
  rows.append(dict(index=i,direct=direct,reaction=scale,overload=over,electro=ec,superconduct=paid,shatter=shatter,amplify=amp))
 command('damageReductionMenu',index=3);return rows
def freeze():
 b=room();charms.equip();command('dummy',recoilable=True,offset=-3);b=next(e for e in command('status')['enemies'] if e['name']=='反应木桩·可击退');fresh(b)
 charms.aura(b,'Hydro');first=charms.hit(b,'Cryo')['state'];assert first['HardFrozen']
 refreshed=charms.hit(b,'Hydro')['state'];assert refreshed['HardFrozenUntil']>first['HardFrozenUntil'] and refreshed['FreezeDecayRate']>first['FreezeDecayRate'],(first,refreshed)
 assert refreshed['HardFrozenUntil']-refreshed['FreezeDecayAt']<1 and abs(refreshed['FrozenUntil']-refreshed['HardFrozenUntil']-2)<.001,refreshed
 while command('status')['time']<refreshed['HardFrozenUntil']+.1:time.sleep(.05)
 soft=current(b)['state'];assert not soft['HardFrozen'] and soft['FrozenUntil']>command('status')['time'],soft
 again=charms.hit(b,'Hydro')['state'];assert again['HardFrozen'] and abs(again['FrozenUntil']-again['HardFrozenUntil']-2)<.001,again
 fresh(b);charms.aura(b,'Hydro');charms.hit(b,'Cryo');melt=charms.hit(b,'Pyro','GreatSlash',cast=9001);nail=command('status')['hero']['nail'];assert loss(melt)==32+math.floor(nail*1.5),melt
 assert melt['state']['Aura']==0 and melt['state']['FrozenUntil']==0,melt
 # A repeated application from the same sword art must still shatter, but cannot attach/melt again.
 charms.aura(b,'Cryo');command('enemyControl',targetId=b['id'],hard=False,seconds=3);blocked=charms.hit(b,'Pyro','GreatSlash',cast=9001);assert loss(blocked)==8+math.floor(nail*1.5) and blocked['state']['Aura']==4,blocked
 checks=[]
 for element in ['Pyro','Electro']:
  fresh(b);charms.aura(b,'Hydro');command('enemyControl',targetId=b['id'],hard=False,seconds=3);r=charms.hit(b,element);assert r['state']['FrozenUntil']==0,r
  if element=='Pyro':assert loss(r)==32,r
  else:assert r['state']['SuperconductStacks']==4 and r['state']['ElectroCharged'] is None,r
  checks.append(r)
 return dict(first=first,refresh=refreshed,soft=soft,refreeze=again,shatterMelt=melt,blockedSword=blocked,frostElement=checks)
def electro_player():
 b=room();charms.equip('C17');command('pause',value=True);charms.aura(b,'Hydro');created=charms.hit(b,'Electro');ec=created['state']['ElectroCharged']
 reduced=charms.hit(b,'None','Cyclone')['state']['ElectroCharged'];assert reduced['Damage']<ec['Damage']
 charms.aura(b,'Hydro');refreshed=charms.hit(b,'Electro')['state']['ElectroCharged'];assert refreshed['Damage']==ec['Damage'] and refreshed['Cashouts']==0 and refreshed['NextTick']==ec['NextTick'],(ec,refreshed)
 command('pause',value=False);charms.equip();command('heal');command('absoluteInvincible',value=False);command('invincible',value=False);command('playerAura',element='Cryo');assert command('hurt',element='Electro',damage=1)
 hero=command('status')['hero'];assert hero['state']['SuperconductDuration']==6 and hero['health']==8,hero
 command('invincible',value=True);return dict(created=ec,reduced=reduced,refreshed=refreshed,player=hero)
def hints():
 config=json.loads((ROOT/'workspaces/mod/content/BossHints.json').read_text(encoding='utf-8'));assert len(config['Entries'])==42 and sum(e['Enabled'] for e in config['Entries'])==31
 entry=next(e for e in config['Entries'] if e['Enabled'] and 'GG_Dung_Defender' in e['Scenes'])
 b=setup('GG_Dung_Defender','Dung Defender');command('menuSetting',name='Boss 机制提示',index=1);p=[float(x) for x in current(b)['position'].strip('()').split(',')];command('position',target='hero',x=p[0],y=p[1]);time.sleep(1.2);r=command('bossHint');assert r['text']==entry['Text'] and r['shown'],r;capture('edited-boss-hint');return r
if __name__=='__main__':
 import sys
 key=sys.argv[1];result=globals()[key]();report=dict(passed=True,result=result,dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest())
 (OUT/(key+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS',key)
