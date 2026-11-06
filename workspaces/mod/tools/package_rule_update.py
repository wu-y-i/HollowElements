"""Package v0.1.10.1; authoring XLSX never enters the runtime package."""
import hashlib,json,shutil,zipfile
from pathlib import Path
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 stage=ROOT/'artifacts/mod/HollowElements';out=ROOT/'artifacts/acceptance/rule-update';digest=sha(stage/'HollowElements.dll')
 for name in ['difficulty','freeze','electro_player','hints','guide']:
  report=json.loads((out/(name+'.json')).read_text(encoding='utf-8'));assert report['passed'] and report['dllSha256']==digest,name
 assert '61 passed, 0 failed' in (out/'tests.log').read_text(encoding='utf-8-sig')
 install=json.loads((out/'install.json').read_text(encoding='utf-8-sig'));assert install['passed'] and install['developmentMarkerRemoved'] and install['dllSha256']==digest
 hints=json.loads((stage/'BossHints.json').read_text(encoding='utf-8'));assert len(hints['Entries'])==42
 version='0.1.10.1';release=ROOT/('artifacts/releases/mod_v'+version);archive=release.parent/(release.name+'.zip')
 assert not release.exists() and not archive.exists(),'Release is immutable'
 mod=release/'HollowElements';mod.mkdir(parents=True)
 for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json','BossHints.json']:shutil.copy2(stage/name,mod/name)
 for name in ['Assets','ElementGuide']:shutil.copytree(stage/name,mod/name)
 shutil.copy2(ROOT/'docs/v0.1.10.1 文档规则同步.md',release/'本版说明.md')
 meta=json.loads((ROOT/'artifacts/releases/mod_v0.1.9.8/verification.json').read_text(encoding='utf-8-sig'))
 meta.update(version=version,releaseName=release.name,finalDllSha256=digest,ruleChecks=61,bossHintRows=42,bossHintsEnabled=sum(e['Enabled'] for e in hints['Entries']),bossHintHoldSeconds=1.5,bossHintTransitionSeconds=.75,defenderOverloadInterval=8,
  scope='61 pure rule checks; native all five difficulty presets and four reaction damage types, amplification, freeze refresh and refreeze, frost elemental reactions, sword-art shatter continuation and ICD, electro refresh, shared player superconduct duration, edited Boss hints, all 51 guide pages, scrolling, permissions, unload/reload. No full P5 or physical controller test.')
 (release/'verification.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
 assert not any(p.suffix.lower() in ['.xlsx','.xls'] for p in release.rglob('*'))
 files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
 (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),ensure_ascii=False,indent=2),encoding='utf-8')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for p in release.rglob('*'):
   if p.is_file():z.write(p,p.relative_to(release).as_posix())
 report=verify(archive);report['bytes']=archive.stat().st_size;(out/'package.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
