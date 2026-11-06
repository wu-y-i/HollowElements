"""Native v0.1.9.1 checks, run only in the backed-up save-suppressed session."""
import json,time,hashlib,argparse,traceback
from runtime_acceptance import command,ROOT,SESSION,LOG
import charms_acceptance as old
from balance_acceptance import room
from boss_fixes_acceptance import setup,current,state as fsm,vulnerable
OUT=ROOT/'artifacts/acceptance/charms-update';OUT.mkdir(exist_ok=True,parents=True);old.OUT=OUT;old.capture=lambda name:capture(name)

def capture(name):
 # Batch player has no output framebuffer; never copy an older screenshot as new evidence.
 if not __import__('os').environ.get('HOLLOW_ELEMENTS_FOREGROUND_CAPTURE'):return
 before=(SESSION/'game.png').stat().st_mtime_ns if (SESSION/'game.png').exists() else 0
 command('capture');time.sleep(.3);assert (SESSION/'game.png').stat().st_mtime_ns>before
 (OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())
def eq():return old.state()['state']
def hit(b,e,source='WhiteWave',cast=None):return old.hit(b,e,source,cast)
def core():
 b=room();command('damageReductionMenu',index=3);old.equip('C06')
 for n,e in enumerate(['Pyro','Hydro','Electro']):hit(b,e,cast=100+n)
 assert eq()['FourUntil']>command('status')['time'];hit(b,'Pyro',cast=110);hit(b,'Pyro',cast=111)
 old.aura(b,'Cryo');r=hit(b,'Cryo',cast=112);assert r['before']-r['after']==11,r
 old.equip('C07','C12');old.aura(b,'Cryo',10)
 for _ in range(4):
  r=hit(b,'Pyro','Cyclone',cast=200);assert r['state']['Charms']['FireUnits']==1 and r['state']['Charms']['FireDuration']==8,r
 old.equip('C05');hit(b,'None','Nail');old.aura(b,'Pyro',2);r=hit(b,'Hydro');assert r['state']['Aura']==2 and r['state']['AuraUnits']==.5,r
 old.equip('C17','C19');old.aura(b,'Hydro');r=hit(b,'Electro');base=r['state']['ElectroCharged']['OriginalDamage'];command('pause',value=True)
 for n in range(5):
  r=hit(b,'None','Cyclone');assert r['state']['ElectroCharged']['Damage']==max(1,int(base*(1-min(.6,(n+1)*.2))+1e-8)),r
 command('pause',value=False)
 old.equip('C03','C21');old.aura(b,'Cryo');r=hit(b,'Electro');assert r['state']['SuperconductStacks']==4
 assert 11<eq()['InsulatedUntil']-command('status')['time']<=12
 time.sleep(.21);r=hit(b,'Pyro');assert r['state']['SuperconductStacks']==4
 time.sleep(.21);r=hit(b,'Hydro');assert r['state']['SuperconductStacks']==3 and eq()['ScarUntil']>command('status')['time']
 capture('combat-sdf');fonts=command('fontDiagnostics');assert all('ElementGuide' in (f['font'] or '') for f in fonts),fonts
 assert all(m['texture']==m['atlas'] for f in fonts for m in f['submeshes']),fonts
 assert all('TexCoord1' in (f.get('channels') or '') for f in fonts),fonts
 return dict(specialFireCyclone=True,threeElements=True,paletteSharedBonus=True,exactExhaustion=True,electroDecay=True,superconductFourLayers=True,fontDiagnostics=fonts)
def freeze():
 b=room();old.equip('C27');command('dummy',recoilable=True,offset=-3)
 target=next(e for e in command('status')['enemies'] if e['name'].startswith('反应木桩') and '可冻结' in e['name']) if False else command('status')['enemies'][-1]
 candidates=[e for e in command('status')['enemies'] if e['name'].startswith('反应木桩')]
 # Native training dummy's recoil capability is visible in its component state after an actual reaction.
 rows=[]
 for target in candidates:
  old.aura(target,'Hydro');r=hit(target,'Cryo');rows.append((target,r))
 target,r=next((b,r) for b,r in rows if r['state']['HardFrozen'])
 end=r['state']['FrozenUntil'];assert abs(r['state']['FrozenDuration']-3)<1e-6
 time.sleep(1.6);s=next(e for e in command('status')['enemies'] if e['id']==target['id'])['state'];assert not s['HardFrozen'] and s['FrozenUntil']==end and abs(s['FrozenSpeed']-.7)<1e-6,s
 r=hit(target,'Pyro');assert r['state']['FrozenUntil']==0 and r['state']['Charms']['FragileUntil']>command('status')['time'],r
 r=hit(target,'None');assert r['before']-r['after']==15,r
 return dict(freezeThenFrost=True,totalSeconds=3,fragility=True)
def protection():
 b=room();old.equip('C40');command('invincible',value=False);command('absoluteInvincible',value=False);command('blueHealth',red=9,blue=0)
 for _ in range(2):old.aura(b,'Hydro');hit(b,'Cryo');hit(b,'None','GreatSlash')
 assert eq()['Masks']==2
 command('goto',scene='GG_Workshop');time.sleep(5);command('ready');assert eq()['Masks']==2
 room();assert eq()['Masks']==2
 command('blueHealth',red=9,blue=0);command('invincible',value=False);command('absoluteInvincible',value=False);command('playerAura',element='Hydro')
 assert command('hurt',element='Cryo',damage=1);hero=command('status')['hero'];assert hero['health']==8 and eq()['Masks']==0,hero
 old.equip('C35');command('heal');time.sleep(1.8);command('playerAura',element='Electro');command('hurt',element='Pyro',damage=1);assert command('status')['hero']['health']==7
 time.sleep(1.8);command('hurt',element='None',damage=1);assert command('status')['hero']['health']==6
 old.equip('C35');command('heal');time.sleep(1.8);command('playerAura',element='Hydro');command('hurt',element='Electro',damage=1)
 assert command('status')['hero']['state']['ElectroCharged']['RemainingTicks']==1
 time.sleep(4.4);hero=command('status')['hero'];assert hero['health']==7 and hero['state']['ElectroCharged'] is None,hero
 command('invincible',value=True)
 return dict(sceneMasksRetained=True,freezePostDamagePenalty=True,overloadInitialPlusOne=True,overloadDebuffIgnored=True,oneElectroPulse=True)
def ui():
 room();r=old.ui();old.page();command('bench',value=True)
 for id in old.equipped():command('charms',action='toggle',charm=id)
 for id in ['C13','C14','C17']:command('charms',action='toggle',charm=id)
 assert set(old.equipped())=={'C13','C14','C17'},old.equipped()
 command('charms',action='toggle',charm='C05');assert set(old.equipped())=={'C13','C14','C17'}
 command('charms',action='toggle',charm='C17');command('charms',action='toggle',charm='C05');assert set(old.equipped())=={'C13','C14','C05'}
 command('charms',action='toggle',charm='C17');assert set(old.equipped())=={'C13','C14','C05'}
 capture('shared-slot');old.close();return dict(**r,flexibleSlot=True)
def bosses():
 rows=[]
 for scene,name in [('GG_Dung_Defender','Dung Defender'),('GG_White_Defender','White Defender')]:
  b=setup(scene,name);command('absoluteInvincible',value=True);command('pause',value=True);command('clear');vulnerable(b);command('enemyHp',targetId=b['id'],value=10000)
  fsm(b,'Dung Defender','Idle');floor=current(b)['mechanic']['surface'];command('position',targetId=b['id'],x=65,y=-2.5)
  for _ in range(2 if 'White' in scene else 1):old.aura(b,'Pyro');hit(b,'Electro')
  command('pause',value=False);time.sleep(.6);r=current(b);assert float(r['position'].strip('()').split(',')[1])>0,r
  assert r['mechanic']['knockdowns']>=1 and abs(r['mechanic']['surface']-floor)<1e-6,r
  rows.append(dict(scene=scene,after=r))
 # Both brothers must leave charge under a continuous soft-frost state.
 b=setup('GG_Nailmasters','Oro');command('absoluteInvincible',value=True)
 for name in ['Oro','Mato']:
  all=command('status')['enemies'];target=next(e for e in all if e['name']==name)
  if name=='Mato':fsm(target,'nailmaster','Activate')
  command('enemyControl',targetId=target['id'],hard=False,seconds=12)
  fsm(target,'nailmaster','NA Charge');time.sleep(2.2);r=current(target)
  assert not any(f['FsmName']=='nailmaster' and f['ActiveStateName'] in ['NA Charge','NA Charged'] for f in r['fsms']),r
  rows.append(dict(scene='GG_Nailmasters',after=r))
 # Compare native return-to-center trajectories with and without persistent frost.
 for scene,name in [('GG_False_Knight','False Knight New'),('GG_Failed_Champion','False Knight Dream')]:
  b=setup(scene,name);command('absoluteInvincible',value=True);traces=[];start=[float(v) for v in current(b)['position'].strip('()').split(',')[:2]]
  for frost in [False,True]:
   command('pause',value=True);command('clear');command('position',targetId=b['id'],x=start[0]-5,y=start[1])
   if frost:command('enemyControl',targetId=b['id'],hard=False,seconds=15)
   fsm(b,'FalseyControl','Rage Jump Antic');command('pause',value=False)
   samples=[];until=time.monotonic()+8
   while time.monotonic()<until:
    r=current(b);native=next(f['ActiveStateName'] for f in r['fsms'] if f['FsmName']=='FalseyControl');samples.append(dict(position=r['position'],state=native,velocity=r['velocity']))
    if native in ['Rage','Rage Check','Rage Slam','Rage Land']:break
    time.sleep(.06)
   assert samples[-1]['state'] in ['Rage','Rage Check','Rage Slam','Rage Land'],samples
   traces.append(samples)
  xs=[float(t[-1]['position'].strip('()').split(',')[0]) for t in traces]
  assert abs(xs[0]-xs[1])<2,(scene,xs,traces)
  rows.append(dict(scene=scene,baseline=traces[0],frost=traces[1],landingX=xs))
 room();return rows
CASES=dict(ui=ui,core=core,freeze=freeze,protection=protection,bosses=bosses)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('cases',nargs='*');args=p.parse_args()
 for key in args.cases or CASES:
  try:
   result=CASES[key]();report=dict(passed=True,result=result)
  except Exception:
   report=dict(passed=False,error=traceback.format_exc());print(report['error'])
  report['dllSha256']=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest()
  (OUT/(key+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');(OUT/(key+'-commands.json')).write_text(json.dumps(LOG,ensure_ascii=False,indent=2),encoding='utf-8')
  print(key,report['passed'],flush=True)
  if not report['passed']:raise SystemExit(1)
