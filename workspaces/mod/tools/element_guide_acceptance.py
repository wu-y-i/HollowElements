"""Focused native inventory/document checks; save-suppressed development bridge required."""
import hashlib,json,time
from pathlib import Path
from balance_acceptance import room
from balance_acceptance import reset,aura,hit
from runtime_acceptance import command,ROOT,SESSION,LOG
GAME=Path(r'E:/SteamLibrary/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Mods/HollowElements')
OUT=ROOT/'artifacts/acceptance/element_guide';OUT.mkdir(parents=True,exist_ok=True)
def snapshot(name):
 command('capture');time.sleep(.25);(OUT/(name+'.png')).write_bytes((SESSION/'game.png').read_bytes())
def element_guide(action=None,**kwargs):return command('element_guide',**(dict(action=action) if action else {}),**kwargs)
def settle():time.sleep(.65);return element_guide()
def native():return command('inventoryProbe')[0]
def pane():return int(next(v['value'] for v in native()['variables'] if v['Name']=='Current Pane Num'))
def select(index):
 catalog=json.loads((GAME/'ElementGuide/index.json').read_text(encoding='utf-8'));entries=[e for c in catalog['Categories'] for e in c['Entries']]
 current=next(i for i,e in enumerate(entries) if e['Id']==element_guide()['selected'])
 return element_guide('navigate',direction=index-current)
def extra_checks():
 results=[]
 # Auto-selection is triggered only on entry, so manual reading remains possible.
 for scene,expected in [('GG_Hornet_1','boss-05'),('GG_Hornet_2','boss-30'),('GG_God_Tamer','boss-24')]:
  command('goto',scene=scene);time.sleep(3);command('ready');command('invincible',value=True)
  for _ in range(50):
   r=element_guide()
   if r['canOpen']:break
   time.sleep(.2)
  element_guide('open');r=settle();assert r['visible'] and r['selected']==expected,(scene,r)
  element_guide('navigate',direction=1);assert element_guide()['selected']!=expected
  element_guide('close');settle();element_guide('open');r=settle();assert r['selected']==expected,r
  results.append(dict(scene=scene,selected=r['selected']));snapshot(expected);element_guide('close');settle()
 b=room();element_guide('open');r=settle();assert r['selected']=='boss-24',r
 element_guide('close');settle()
 sizes=[]
 for damage,reaction,expected_damage,expected_reaction in [(0,5,24,75),(5,0,60,30),(2,2,36,45)]:
  command('menuSetting',name='伤害跳字大小',index=damage);command('menuSetting',name='反应跳字大小',index=reaction)
  reset(b);command('pause',value=False);aura(b,'Pyro');hit(b,'Electro',32);command('pause',value=True)
  p=command('fixDiagnostics')['ui']['popups'];assert p and any(x['Reaction'] for x in p),p
  for popup in p:assert popup['size']==popup['outlineSize']==(expected_reaction if popup['Reaction'] else expected_damage),popup
  sizes.append(p)
 command('pause',value=False);command('clear');command('heal');command('invincible',value=False);command('playerAura',element='Pyro');assert command('hurt',element='Electro',damage=1)
 p=command('fixDiagnostics')['ui']['popups'];assert any(x['Reaction'] and x['style']=='Italic' and x['size']==45 for x in p),p
 command('clear');command('invincible',value=True);return dict(autoSelection=results,popupSizes=sizes,playerReaction=p)
def main():
 room();rows=[]
 element_guide('open');r=settle();assert r['visible'] and r['entries']==51 and r['timeScale']==0 and r['error']=='' and r['renderer']=='TextMeshProUGUI' and r['font']=='HollowElements.ElementGuideGenshin',r
 select(0)
 snapshot('guide')
 # The original FSM, original page labels and native tween transitions all execute.
 for expected in [0,1,2,3,4]:
  element_guide('move',direction=1);r=settle();assert pane()==expected,(expected,native())
  assert r['timeScale']==(0 if expected==4 else 1),r
  rows.append(dict(pane=expected,state=r['nativeState'],timeScale=r['timeScale']))
 for expected in [3,2,1,0,4]:
  element_guide('move',direction=-1);r=settle();assert pane()==expected,(expected,native())
 article=[]
 for index in range(51):
  r=select(index);assert r['visible'] and r['text'] and r['error']=='',r
  assert not r['missingCharacters'] and not any('[图片未加载]' in t or '[未知参数' in t for t in r['text']),r
  article.append(dict(id=r['selected'],textBlocks=len(r['text']),images=r['images']))
  if index==2:snapshot('vaporize')
  if index==8:snapshot('superconduct')
  if index==13:snapshot('hornet')
 select(8);r=element_guide('navigate',submit=True);assert r['focusedBody']
 r=element_guide('scroll',pixels=99999);assert r['scroll']>0 and r['scroll']<=r['bodyHeight']-r['viewportHeight']+1,r
 assert not element_guide('back')['focusedBody'] and element_guide()['visible']
 # Re-read changed external files, dynamic CJK, image failures and transaction rollback.
 index_path=GAME/'ElementGuide/index.json';page=GAME/'ElementGuide/basics/start.md';index_bytes=index_path.read_bytes();page_bytes=page.read_bytes()
 try:
  page.write_text('# 外部编辑验证\n\n新增文字：龘、魑魅魍魉。\n\n当前减伤 {{DirectDamageReduction}}\n\n![缺图占位](../images/does-not-exist.png)\n',encoding='utf-8')
  select(0);element_guide('reload');r=element_guide();assert any('龘' in t for t in r['text']) and any('[图片未加载]' in t for t in r['text']),r
  assert not r['missingCharacters'],r
  snapshot('edited-document')
  command('damageReductionMenu',index=2);element_guide('reload');r=element_guide();assert any('20%' in t for t in r['text']),r
  command('damageReductionMenu',index=0)
  index_path.write_text('{invalid json',encoding='utf-8');element_guide('reload');r=element_guide();assert '读取失败' in r['error'] and r['entries']==51,r
  index_path.write_bytes(index_bytes);index_data=json.loads(index_bytes);index_data['Categories'][0]['Entries'][0]['Layout']=dict(FontSize=30,ImageMaxHeight=100)
  index_path.write_text(json.dumps(index_data,ensure_ascii=False),encoding='utf-8');element_guide('reload');assert element_guide()['error']==''
 finally:
  index_path.write_bytes(index_bytes);page.write_bytes(page_bytes);command('damageReductionMenu',index=0);element_guide('reload')
 resolutions=[]
 for width,height in [(1600,1200),(2560,1080),(1280,720)]:
  command('resolution',width=width,height=height);time.sleep(.7);select(2);r=element_guide();assert r['visible'] and r['panelWidth']>600 and r['viewportHeight']>250,r
  snapshot(f'{width}x{height}');resolutions.append(dict(width=width,height=height,ui=r))
 element_guide('close');r=settle();assert not r['visible'] and r['timeScale']==1 and r['nativeState']=='Closed',r
 extras=extra_checks()
 command('ready');command('heal')
 for _ in range(40):
  if element_guide()['canOpen']:break
  time.sleep(.2)
 element_guide('open');r=settle();assert r['visible'] and r['error']=='',r
 # Unload while reading must restore the native carousel and prior time scale.
 audit=SESSION/'unload-audit.json';before=audit.stat().st_mtime_ns if audit.exists() else 0
 command('unloadAudit',reload=True)
 for _ in range(100):
  if audit.exists() and audit.stat().st_mtime_ns!=before:break
  time.sleep(.1)
 report=json.loads(audit.read_text(encoding='utf-8-sig'));assert report['passed'],report
 for _ in range(30):
  r=element_guide()
  if r['attached']:break
  time.sleep(.2)
 assert not r['visible'] and r['timeScale']==1,r
 element_guide('open');r=settle();assert r['visible'] and r['entries']==51,r
 element_guide('close');settle();command('debugMode',value=False)
 result=dict(passed=True,version='0.1.6',dllSha256=hashlib.sha256((GAME/'HollowElements.dll').read_bytes()).hexdigest(),carousel=rows,articles=article,resolutions=resolutions,extras=extras,unload=report,commands=LOG)
 (OUT/'acceptance.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS native carousel, 51 pages, scrolling, external editing/reload, invalid-file fallback, live values, 3 resolutions and unload/reload')
 command('quit')
if __name__=='__main__':
 try:main()
 except Exception as error:
  (OUT/'last-failure.json').write_text(json.dumps(dict(passed=False,error=repr(error),commands=LOG),ensure_ascii=False,indent=2),encoding='utf-8');raise
