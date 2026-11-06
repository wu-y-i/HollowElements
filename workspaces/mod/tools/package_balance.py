"""Export the tested v0.1.5 balance build; preserve previous releases."""
import hashlib,json,shutil,zipfile
from pathlib import Path
from balance_acceptance import CASES
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    stage=ROOT/'artifacts/mod/HollowElements';evidence=ROOT/'artifacts/acceptance';digest=sha(stage/'HollowElements.dll')
    assert not (stage/'development-session.json').exists()
    reports=[]
    paths=[*('balance-v015/'+name+'.json' for name in CASES),*('balance-v015/regression-'+name+'.json' for name in ['incoming','original','radiance','galien']),
        'balance-v015-install.json','balance-v015-unload.json','balance-v015-migration.json','balance-v015-rules.json']
    for name in paths:
        p=evidence/name;r=read(p);assert r['passed'] and r['dllSha256']==digest,(name,'failed or stale build')
        reports.append(dict(name=name,sha256=sha(p)))
    assert all(p.stat().st_mtime<=(stage/'HollowElements.dll').stat().st_mtime for p in (ROOT/'workspaces/mod/src').rglob('*.cs')),'Uncompiled source'
    config=read(ROOT/'workspaces/mod/balance.defaults.json');assert config['SchemaVersion']==2 and config['DirectDamageMultiplier']==.45
    assert config['Reactions']['ForwardMultiplier']==4 and config['Reactions']['ReverseMultiplier']==2.5
    # Full-frame media and all approved audio remain byte-for-byte identical to v0.1.4.
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.4.zip') as previous:
        for p in (stage/'Assets').rglob('*'):
            if p.is_file() and any(part in ('Effects','Audio','Icons') for part in p.parts):assert p.read_bytes()==previous.read('HollowElements/'+p.relative_to(stage).as_posix()),p
    name='mod_v0.1.5';release=ROOT/'artifacts/releases'/name;archive=release.parent/(name+'.zip')
    assert not release.exists() and not archive.exists(),'Release already exists'
    release.mkdir();mod=release/'HollowElements';mod.mkdir()
    for file in ['HollowElements.dll','HollowElements.pdb']:shutil.copy2(stage/file,mod/file)
    shutil.copytree(stage/'Assets',mod/'Assets');shutil.copy2(ROOT/'workspaces/mod/balance.defaults.json',mod/'balance.defaults.json')
    shutil.copy2(ROOT/'docs/v0.1.5战斗平衡说明.md',release/'本版说明.md')
    shutil.copy2(ROOT/'docs/mod_v0.1使用说明.md',release/'通用安装与使用.md')
    metadata=dict(version='0.1.5',releaseName=name,game='1.5.78.11833',api=77,runtime='net472',language='zh-CN',effects=13,frames=525,fps=60,audio=7,icons=5,debugDefault=False,reports=reports,
        scope='Combat balance, native spell stages and targeted Boss/performance regressions. Charms deferred; not a complete 42-fight balance study.')
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version='0.1.5',files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in release.rglob('*'):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    result=verify(archive);result['mediaUnchanged']=True
    (evidence/'balance-v015-package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
