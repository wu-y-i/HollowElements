"""Validate and package the editable element_guide with the tested runtime."""
import hashlib,json,re,shutil,zipfile
from pathlib import Path
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def check_element_guide(folder):
 documents=None
 if (folder/'content.zip').exists():
  with zipfile.ZipFile(folder/'content.zip') as z:documents=json.loads(z.read('documents.json'))
 def text(relative):return documents[relative] if documents is not None else (folder/relative).read_text(encoding='utf-8')
 index=json.loads(text('index.json').lstrip('\ufeff'));assert index['Version']==1
 entries=[e for c in index['Categories'] for e in c['Entries']];assert 0<len(entries)<=500 and len({e['Id'] for e in entries})==len(entries)
 images=set();source=ROOT/'workspaces/mod/content/ElementGuide'
 for entry in entries:
  page=(folder/entry['File']).resolve();assert page.is_relative_to(folder.resolve());body=text(entry['File']).replace('\r\n','\n');assert body.strip()
  for image in re.findall(r'^!\[.*?\]\((.*?)\)$',body,re.M):
   path=(page.parent/image).resolve();assert path.is_relative_to(folder.resolve()) and path.is_file();images.add(path)
 if (folder/'asset-optimization.json').exists():
  from verify_guide_media import verify_media
  verify_media(lambda name:(folder/name).read_bytes(),[p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()])
 else:
  for path in source.rglob('*'):
   if path.is_file():
    relative=path.relative_to(source).as_posix()
    actual=text(relative).encode('utf-8') if documents is not None and relative in documents else (folder/relative).read_bytes()
    assert path.read_bytes()==actual,path
 scenes=[s for e in entries for s in e.get('Scenes',[])];assert len(scenes)==len(set(scenes))
 font=read(folder/'Font/font.json');glyphs=[g['id'] for p in font['pages'] for g in p['glyphs']];assert len(glyphs)==33552 and len(glyphs)==len(set(glyphs))
 for page in font['pages']:
  from PIL import Image
  with Image.open(folder/'Font'/page['file']) as atlas:
   assert atlas.width in (2048,4096) and atlas.height==atlas.width and atlas.convert('RGBA').getchannel('A').getbbox()
 return dict(entries=len(entries),images=len(images),sceneAliases=len(scenes),font='Genshin TMP SDF',fontGlyphs=len(glyphs),fontPages=len(font['pages']),files=sum(p.is_file() for p in folder.rglob('*')))
def main():
 stage=ROOT/'artifacts/mod/HollowElements';evidence=ROOT/'artifacts/acceptance';digest=sha(stage/'HollowElements.dll')
 assert not (stage/'development-session.json').exists()
 report=read(evidence/'element_guide/acceptance.json');assert report['passed'] and report['dllSha256']==digest
 inputs=read(evidence/'element_guide/input.json');assert inputs['passed'] and inputs['dllSha256']==digest
 assert '35 passed, 0 failed' in (evidence/'element_guide-build.log').read_text(encoding='utf-8-sig')
 content=check_element_guide(stage/'ElementGuide')
 with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.5.1.zip') as previous:
  for path in (stage/'Assets').rglob('*'):
   if path.is_file():assert path.read_bytes()==previous.read('HollowElements/'+path.relative_to(stage).as_posix()),path
 name='mod_v0.1.6';release=ROOT/'artifacts/releases'/name;archive=release.parent/(name+'.zip');assert not release.exists() and not archive.exists()
 release.mkdir();mod=release/'HollowElements';mod.mkdir()
 for name in ['HollowElements.dll','HollowElements.pdb']:shutil.copy2(stage/name,mod/name)
 shutil.copytree(stage/'Assets',mod/'Assets');shutil.copytree(stage/'ElementGuide',mod/'ElementGuide')
 shutil.copy2(ROOT/'workspaces/mod/balance.defaults.json',mod/'balance.defaults.json')
 for source,target in [('v0.1.6图鉴说明.md','本版说明.md'),('v0.1.5战斗平衡说明.md','战斗平衡说明.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:shutil.copy2(ROOT/'docs'/source,release/target)
 metadata=dict(version='0.1.6',releaseName='mod_v0.1.6',game='1.5.78.11833',api=77,debugDefault=False,effects=13,frames=525,fps=60,audio=7,icons=5,element_guide=content,
  scope='Native inventory element_guide, external documents and focused lifecycle/UI checks; existing 35 rules. No full Boss rerun.',acceptanceSha256=sha(evidence/'element_guide/acceptance.json'))
 (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
 files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
 (release/'manifest.json').write_text(json.dumps(dict(version='0.1.6',files=files),indent=2),encoding='utf-8')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for path in release.rglob('*'):
   if path.is_file():z.write(path,path.relative_to(release).as_posix())
 result=verify(archive);result['element_guide']=content;result['battleAssetsUnchanged']=True
 (evidence/'element_guide/package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
