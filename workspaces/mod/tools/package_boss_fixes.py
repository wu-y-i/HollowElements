"""Package v0.1.4 only when current-build native evidence and normal install pass."""
import hashlib,json,shutil,zipfile
from pathlib import Path
from verify_release import verify
from boss_fixes_acceptance import CASES
ROOT=Path(__file__).resolve().parents[3]
NAME='mod_v0.1.4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))

def main():
    stage=ROOT/'artifacts/mod/HollowElements';evidence=ROOT/'artifacts/acceptance';digest=sha(stage/'HollowElements.dll')
    assert not (stage/'development-session.json').exists()
    reports=[]
    for name in [*('boss-fixes/'+case+'.json' for case in CASES),'boss-fixes/regressions.json','boss-fixes-install.json','boss-fixes-unload.json','boss-fixes-rules.json','boss-fixes-save-sync.json']:
        p=evidence/name;r=read(p)
        assert r['passed'] and r['dllSha256']==digest,(name,'failed or stale build')
        reports.append(dict(name=name,sha256=sha(p)))
    for name in ['boss-fixes-build.log','boss-fixes-rules.log']:
        reports.append(dict(name=name,sha256=sha(evidence/name)))
    assert all(p.stat().st_mtime<= (stage/'HollowElements.dll').stat().st_mtime for p in (ROOT/'workspaces/mod/src').rglob('*.cs')),'Uncompiled C# changes'
    # Hold global balance, approved sound and all effect pixels/playback parameters fixed.
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.3-fx5.zip') as previous:
        assert previous.read('HollowElements/balance.defaults.json')==(ROOT/'workspaces/mod/balance.defaults.json').read_bytes()
        for p in (stage/'Assets').rglob('*'):
            if p.is_file() and ('Effects' in p.parts or 'Audio' in p.parts or 'Icons' in p.parts):
                assert p.read_bytes()==previous.read('HollowElements/'+p.relative_to(stage).as_posix()),p
    release=ROOT/'artifacts/releases'/NAME;archive=release.parent/(NAME+'.zip')
    assert not release.exists() and not archive.exists(),'Published releases are immutable'
    release.mkdir();mod=release/'HollowElements';mod.mkdir()
    for name in ['HollowElements.dll','HollowElements.pdb']:shutil.copy2(stage/name,mod/name)
    shutil.copytree(stage/'Assets',mod/'Assets')
    shutil.copy2(ROOT/'workspaces/mod/balance.defaults.json',mod/'balance.defaults.json')
    shutil.copy2(ROOT/'docs/v0.1.4修复与Boss调整.md',release/'本版说明.md')
    shutil.copy2(ROOT/'docs/五门Boss改动填写表.md',release/'五门Boss改动填写表.md')
    shutil.copy2(ROOT/'docs/mod_v0.1使用说明.md',release/'通用安装与使用.md')
    metadata=dict(version='0.1.4',releaseName=NAME,game='1.5.78.11833',api=77,runtime='net472',language='zh-CN',effects=13,frames=525,fps=60,audio=7,icons=5,debugDefault=False,reports=reports,
        scope='Current-build Boss fixes, native mechanism cases and regressions; not a complete 42-fight playthrough. Global balance and charms deferred.',globalBalanceUnchanged=True)
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version='0.1.4',files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in release.rglob('*'):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    result=verify(archive);result.update(globalBalanceUnchanged=True,assetsMatchFx5=True)
    (evidence/'boss-fixes-package.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))
if __name__=='__main__':main()
