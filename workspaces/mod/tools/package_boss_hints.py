"""Package v0.1.10.0; authoring XLSX never enters the runtime package."""
import hashlib,json,shutil,zipfile
from pathlib import Path
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 stage=ROOT/'artifacts/mod/HollowElements';out=ROOT/'artifacts/acceptance/boss-hints';digest=sha(stage/'HollowElements.dll')
 for name in ['banner','ui','defenders','freeze','guide']:
  report=json.loads((out/(name+'.json')).read_text(encoding='utf-8'));assert report['passed'] and report['dllSha256']==digest,name
 assert '56 passed, 0 failed' in (out/'tests.log').read_text(encoding='utf-8-sig')
 install=json.loads((out/'install.json').read_text(encoding='utf-8-sig'));assert install['passed'] and install['developmentMarkerRemoved'] and install['dllSha256']==digest
 hints=json.loads((stage/'BossHints.json').read_text(encoding='utf-8'));assert len(hints['Entries'])==42
 version='0.1.10.0';release=ROOT/('artifacts/releases/mod_v'+version);archive=release.parent/(release.name+'.zip')
 assert not release.exists() and not archive.exists(),'Release is immutable'
 mod=release/'HollowElements';mod.mkdir(parents=True)
 for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json','BossHints.json']:shutil.copy2(stage/name,mod/name)
 for name in ['Assets','ElementGuide']:shutil.copytree(stage/name,mod/name)
 shutil.copy2(ROOT/'docs/v0.1.10 Boss提示与冻结衰减.md',release/'本版说明.md')
 meta=json.loads((ROOT/'artifacts/releases/mod_v0.1.9.8/verification.json').read_text(encoding='utf-8-sig'))
 meta.update(version=version,releaseName=release.name,finalDllSha256=digest,ruleChecks=56,bossHintRows=42,bossHintExamples=sum(e['Enabled'] for e in hints['Entries']),bossHintHoldSeconds=1.5,bossHintTransitionSeconds=.75,defenderOverloadInterval=8,
  scope='56 pure rule checks; native banner center/move/dock, pause/toggle and disabled rooms; all 20 charm explanations at 2160x1440 and 1024x768; Dung/White Defender cooldown with damage retained; hard-freeze decay and unchanged frost tail; 51 guide pages, scrolling, permissions and unload/reload. No complete P5 run or physical controller test.')
 (release/'verification.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
 assert not any(p.suffix.lower() in ['.xlsx','.xls'] for p in release.rglob('*'))
 files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
 (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),ensure_ascii=False,indent=2),encoding='utf-8')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for p in release.rglob('*'):
   if p.is_file():z.write(p,p.relative_to(release).as_posix())
 report=verify(archive);report['bytes']=archive.stat().st_size;(out/'package.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
