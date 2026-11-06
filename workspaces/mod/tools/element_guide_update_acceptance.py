"""All current documents, embedded loadout permissions and element_guide teardown."""
import hashlib
import json
import re
import time
from runtime_acceptance import command, ROOT, SESSION
from balance_acceptance import room
OUT=ROOT/'artifacts/acceptance/boss-update'


def open_page():
    if command('element_guide')['visible']:return
    command('element_guide',action='open');time.sleep(.8)
    if not command('element_guide')['visible']:
        # Forced Boss-photo transitions can interrupt a native roar/HUD OUT.
        # Run the native close cleanup, including its HUD IN broadcast.
        inventory=command('inventoryProbe')[0]
        command('objectState',objectId=inventory['id'],fsm='Inventory Control',state='Close')
        command('objectState',objectId=inventory['id'],fsm='Inventory Control',state='Regain Control')
        time.sleep(.6);command('ready');command('element_guide',action='open');time.sleep(.8)
    assert command('element_guide')['visible']


def main():
    if not command('status')['hero']:
        command('load',slot=3)
        for _ in range(100):
            if command('element_guide')['canOpen']:break
            time.sleep(.2)
        command('ready')
    room();time.sleep(.3);open_page();command('element_guide',action='reload')
    book=json.loads((ROOT/'workspaces/mod/content/ElementGuide/index.json').read_text(encoding='utf-8-sig'))
    ids=[e['Id'] for c in book['Categories'] for e in c['Entries']];rows=[]
    navigation=['loadout','charms']+ids
    for n,name in enumerate(ids):
        current=command('element_guide')['selected'];old=navigation.index(current)
        command('element_guide',action='navigate',direction=navigation.index(name)-old);r=command('element_guide')
        assert r['selected']==name and not r['error'] and not r['missingCharacters'],r
        assert not any('[图片未加载]' in t or '[未知参数' in t for t in r['text']),r
        entry=next(e for c in book['Categories'] for e in c['Entries'] if e['Id']==name)
        document=(ROOT/'workspaces/mod/content/ElementGuide'/entry['File']).read_text(encoding='utf-8-sig')
        expected=len(re.findall(r'^!\[[^\]]*\]\([^\n]+\)$',document,re.M))
        assert r['images']==expected,(name,expected,r)
        performance=r.get('performance')
        if performance: assert performance['cachedImageBytes']<=32*1024*1024,performance
        rows.append(dict(id=name,images=r['images'],bodyHeight=r['bodyHeight'],performance=performance))
        if name=='boss-30':
            command('capture');time.sleep(.25);(OUT/'element_guide-page.png').write_bytes((SESSION/'game.png').read_bytes())
    command('element_guide',action='scroll',pixels=99999);r=command('element_guide')
    assert abs(r['scroll']-max(0,r['bodyHeight']-r['viewportHeight']))<1,r
    current=command('element_guide')['selected'];command('element_guide',action='navigate',direction=-navigation.index(current))
    command('element_guide',action='navigate',submit=True)
    command('menuSetting',name='允许非长椅配置元素',index=0);command('bench',value=False)
    before=command('menuStatus')['loadout'];command('element_guide',action='configure',step=1)
    assert command('menuStatus')['loadout']==before
    command('menuSetting',name='允许非长椅配置元素',index=1)
    command('element_guide',action='configure',step=1);assert command('menuStatus')['loadout']['Nail']!=before['Nail']
    command('element_guide',action='configure',step=-1);assert command('menuStatus')['loadout']==before
    command('menuSetting',name='允许非长椅配置元素',index=0)
    command('unloadAudit',reload=True);time.sleep(1.5)
    audit=json.loads((SESSION/'unload-audit.json').read_text(encoding='utf-8-sig'));assert audit['passed'],audit
    for _ in range(40):
        if command('element_guide')['attached']:break
        time.sleep(.1)
    open_page();r=command('element_guide');assert not r['error'],r
    command('element_guide',action='close');time.sleep(.7);assert command('element_guide')['timeScale']==1
    result=dict(passed=True,dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(),articles=rows,unload=audit,permissions=True,final=r)
    (OUT/'element_guide.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PASS current 51 documents and referenced images, scroll, permissions, complete cleanup and reopen',flush=True)


if __name__=='__main__':main()
