"""Native banner/UI/defender checks in the backed-up save-suppressed session."""
import json,time,hashlib
from runtime_acceptance import command,ROOT,SESSION,LOG
from balance_acceptance import room
from boss_fixes_acceptance import setup,current,state,vulnerable
import charms_acceptance as charms
OUT=ROOT/'artifacts/acceptance/boss-hints';OUT.mkdir(parents=True,exist_ok=True)
def capture(name):
 before=(SESSION/'game.png').stat().st_mtime_ns if (SESSION/'game.png').exists() else 0
 command('capture');time.sleep(.25);assert (SESSION/'game.png').stat().st_mtime_ns>before
 (OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())
def banner():
 command('resolution',width=2160,height=1440);command('pause',value=False);command('invincible',value=True)
 command('menuSetting',name='Boss 机制提示',index=1);command('goto',scene='GG_Dung_Defender')
 samples=[];deadline=time.monotonic()+20;stages=set();readied=False
 while time.monotonic()<deadline:
  if not readied and command('status')['scene']=='GG_Dung_Defender':command('ready');readied=True
  r=command('bossHint');samples.append(r)
  if r['visible']:
   if .2<r['age']<1.1 and 'center' not in stages:capture('banner-center');stages.add('center')
   if 1.6<r['age']<2.1 and 'moving' not in stages:capture('banner-moving');stages.add('moving')
   if r['age']>2.4:capture('banner-top');stages.add('top');break
  time.sleep(.04)
 assert 'center' in stages and 'top' in stages,(stages,samples)
 command('nativePause');time.sleep(.6);a=command('bossHint');time.sleep(.3);b=command('bossHint');assert not b['visible'] and abs(b['age']-a['age'])<.02,(a,b)
 command('nativePause');time.sleep(.6);command('menuSetting',name='Boss 机制提示',index=0);assert not command('bossHint')['visible']
 command('menuSetting',name='Boss 机制提示',index=1);assert command('bossHint')['visible'];time.sleep(.4);assert command('bossHint')['age']>2.4
 room();assert not command('bossHint')['shown'];setup('GG_Gruz_Mother','Giant Fly');assert not command('bossHint')['shown']
 return dict(samples=samples,pause=True,toggle=True,disabledBoss=True,stages=list(stages))
def ui():
 room();charms.page();command('bench',value=False);rows=[]
 for id in charms.IDS[1:]:
  r=command('charms',charm=id);assert r['details'] and r['description'];g=command('element_guide');assert not g['missingCharacters'] and not g['error'],g
  rows.append(dict(id=id,details=r['details']))
 command('charms',charm='C03');capture('charms-2160x1440')
 command('resolution',width=1024,height=768);time.sleep(.7);command('charms',charm='C40');capture('charms-1024x768')
 command('resolution',width=2160,height=1440);time.sleep(.7);charms.close()
 return dict(descriptions=rows,benchRestricted=not command('charms')['canEquip'])
def defenders():
 rows=[]
 for scene,name,threshold in [('GG_Dung_Defender','Dung Defender',1),('GG_White_Defender','White Defender',2)]:
  b=setup(scene,name);command('absoluteInvincible',value=True);command('enemyHp',targetId=b['id'],value=100000);command('clear');state(b,'Dung Defender','Idle')
  def overload():
   vulnerable(b);charms.aura(b,'Pyro');return charms.hit(b,'Electro',damage=1)
  for i in range(threshold):overload()
  time.sleep(.25);first=current(b);assert first['mechanic']['knockdowns']==1,first
  cooldown=first['mechanic']['readyAt'];assert cooldown>command('status')['time']+7
  for _ in range(4):assert (lambda h:h['before']>h['after'])(overload())
  held=current(b);assert held['mechanic']['knockdowns']==1 and held['mechanic']['overloads']==0,held
  while command('status')['time']<cooldown+.05:time.sleep(.25)
  for _ in range(threshold):overload()
  time.sleep(.25);after=current(b);assert after['mechanic']['knockdowns']==2,after
  rows.append(dict(scene=scene,first=first,duringCooldown=held,after=after))
 return rows
def freeze():
 b=room();charms.equip();command('dummy',recoilable=True,offset=-3)
 b=next(e for e in command('status')['enemies'] if e['name']=='反应木桩·可击退');command('enemyHp',targetId=b['id'],value=100000)
 def frozen():charms.aura(b,'Hydro');return charms.hit(b,'Cryo')['state']
 first=frozen();assert first['HardFrozen'];assert abs((first['FrozenUntil']-first['HardFrozenUntil'])-2)<.001,first
 while command('status')['time']<first['FrozenUntil']:time.sleep(.02)
 # Residual Cryo is still present. One real Hydro hit avoids spending the short
 # normal recovery window on an unnecessary debug aura replacement command.
 second=charms.hit(b,'Hydro')['state'];assert second['HardFrozen'];assert second['FreezeDecayRate']>1;assert abs((second['FrozenUntil']-second['HardFrozenUntil'])-2)<.001,second
 assert second['HardFrozenUntil']-second['FreezeDecayAt']<.99,second
 time.sleep(1.2);soft=current(b)['state'];assert not soft['HardFrozen'] and soft['FrozenUntil']>command('status')['time'],soft
 return dict(first=first,second=second,soft=soft)
if __name__=='__main__':
 import sys
 key=sys.argv[1];result=globals()[key]();report=dict(passed=True,result=result,dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest())
 (OUT/(key+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS',key)
