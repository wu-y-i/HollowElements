"""Package the reviewed charm fixes and optimized release textures."""
import json,shutil,zipfile
from package_element_guide import ROOT,sha,read,check_element_guide
from verify_release import verify

def main():
 stage=ROOT/'artifacts/mod/HollowElements';out=ROOT/'artifacts/acceptance/charms-update';texture=ROOT/'artifacts/acceptance/texture-optimization';digest=sha(stage/'HollowElements.dll')
 for key in ['ui','core','freeze','protection','bosses','audio','persistence','element_guide']:
  report=read(out/(key+'.json'));assert report['passed'] and report['dllSha256']==digest,key
 assert read(out/'element_guide.json')['unload']['passed']
 quality=read(texture/'quality.json');assert quality['passed'] and quality['resolution']==[2160,1440] and quality['dllSha256']==digest
 assert len(quality['comparisons'])==13 and '53 passed, 0 failed' in (ROOT/'artifacts/acceptance/charms-update-tests.log').read_text(encoding='utf-8-sig')
 assets=read(texture/'assets.json');assert assets['sourceArtworkUntouched'] and assets['frames']==525 and assets['fps']==60
 assert not (stage/'development-session.json').exists()
 book=check_element_guide(stage/'ElementGuide');assert book['entries']==51 and book['images']==35
 from optimize_runtime_textures import resized
 from PIL import Image
 for p in (stage/'Assets/Charms').glob('*.png'):
  assert Image.open(p).size==(256,256)
  assert Image.open(p).convert('RGBA').tobytes()==resized(Image.open(ROOT/'workspaces/art/source/charms'/p.name),(256,256)).tobytes()
 assert len(list((stage/'Assets/Charms').glob('*.png')))==20
 version='0.1.9.2';release=ROOT/('artifacts/releases/mod_v'+version);archive=release.parent/(release.name+'.zip');assert not release.exists() and not archive.exists()
 mod=release/'HollowElements';mod.mkdir(parents=True)
 for file in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json']:shutil.copy2(stage/file,mod/file)
 for folder in ['Assets','ElementGuide']:shutil.copytree(stage/folder,mod/folder)
 for source,target in [('v0.1.9.2护符与资源优化.md','本版说明.md'),('8 护符设计.md','护符设计.md'),('v0.1.9护符实现与验收.md','v0.1.9护符实现与验收.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:shutil.copy2(ROOT/'docs'/source,release/target)
 design=release/'护符设计.md';design.write_text(design.read_text(encoding='utf-8').replace('(v0.1.9.2护符与资源优化.md)','(本版说明.md)'),encoding='utf-8')
 metadata=dict(version=version,releaseName=release.name,game='1.5.78.11833',api=77,debugDefault=False,effects=13,frames=525,fps=60,audio=7,icons=5,charms=20,charmSlots=[1,1,2,1],flexibleAmplificationSlot=True,maximumTransformative=3,
  element_guide=book,finalDllSha256=digest,ruleChecks=53,sourceArtworkUntouched=True,optimizedTextures=True,textureBytesBefore=assets['beforeBytes'],textureBytesAfter=assets['afterBytes'],effectRgbaBytesBefore=assets['effectRgbaBytesBefore'],effectRgbaBytesAfter=assets['effectRgbaBytesAfter'],qualityResolution=[2160,1440],
  scope='Native permissions, flexible-slot persistence, reaction combinations, freeze-to-frost, masks across scenes, player debuffs, forced native Dung/White Defender underground stuns, Oro/Mato charge exit and False Knight/Failed Champion return trajectories; 51 element_guide articles and unload/reload; 13 texture A/B frames at 2160x1440. No complete P5 run or physical controller hardware test.')
 (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
 files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
 (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),ensure_ascii=False,indent=2),encoding='utf-8')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for p in release.rglob('*'):
   if p.is_file():z.write(p,p.relative_to(release).as_posix())
 result=verify(archive);result.update(bytes=archive.stat().st_size,previousBytes=(ROOT/'artifacts/releases/mod_v0.1.9.zip').stat().st_size,textureSaved=assets['beforeBytes']-assets['afterBytes'])
 (out/'package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
