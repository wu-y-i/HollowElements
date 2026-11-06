"""v0.1.9.6 native difficulty and mask cooldown regression."""
import json,time,hashlib
from runtime_acceptance import command,ROOT
from balance_acceptance import room
import charms_acceptance as charms

def main():
 for _ in range(80):
  if command('element_guide')['attached'] and command('status')['hero']:break
  time.sleep(.2)
 b=room();charms.equip('C40');initial=command('damageReductionMenu');assert initial['index']==3,initial
 rows=[]
 for n,m in enumerate([.35,.45,.5,.8,1]):
  r=command('damageReductionMenu',index=n)
  assert r['Values']==['极限','困难','平衡','普通','简单'] and r['index']==n and abs(r['multiplier']-m)<1e-6 and abs(r['saved']-m)<1e-6,r
  hit=charms.hit(b,'None',damage=100);assert hit['before']-hit['after']==round(100*m),hit
  rows.append(r)
 command('damageReductionMenu',index=3)
 def shatter():
  command('enemyAura',targetId=b['id'],element='Hydro');charms.hit(b,'Cryo');charms.hit(b,'None',source='GreatSlash')
  return command('charms')['state']
 first=shatter();assert first['Masks']==1,first
 assert shatter()['Masks']==1
 command('heal');command('invincible',value=False);command('absoluteInvincible',value=False);command('hurt',element='None',damage=1)
 used=command('charms')['state'];assert used['Masks']==0,used
 early=shatter();assert early['Masks']==0 and early['MaskReadyAt']==first['MaskReadyAt'],early
 remaining=first['MaskReadyAt']-command('status')['time']
 if remaining>0:time.sleep(remaining+.1)
 later=shatter();assert later['Masks']==1,later
 command('invincible',value=True);command('goto',scene='GG_Workshop');time.sleep(5);command('ready');across=command('charms')['state'];assert across['Masks']==1 and across['MaskReadyAt']==later['MaskReadyAt'],across
 room()
 from element_guide_update_acceptance import open_page
 open_page();guide=command('element_guide');assert guide['visible'] and guide['entries']==51 and not guide['error'],guide
 command('element_guide',action='close');time.sleep(.7)
 report=dict(passed=True,dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(),difficulty=rows,defaultIndex=initial['index'],masks=dict(first=first,used=used,early=early,later=later,scene=across),looseGuide=True)
 (ROOT/'artifacts/acceptance/mask-difficulty/runtime.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS five named difficulty values, actual damage, persisted settings, mask cap/cooldown and scene persistence; loose guide');command('quit')
if __name__=='__main__':main()
