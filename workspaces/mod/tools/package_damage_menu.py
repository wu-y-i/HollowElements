"""Package the focused v0.1.5.1 menu update without replacing historical releases."""
import hashlib,json,shutil,zipfile
from pathlib import Path
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    stage=ROOT/'artifacts/mod/HollowElements';evidence=ROOT/'artifacts/acceptance'
    report=json.loads((evidence/'damage-menu.json').read_text(encoding='utf-8'))
    assert report['passed'] and report['dllSha256']==sha(stage/'HollowElements.dll')
    assert '34 passed, 0 failed' in (evidence/'damage-menu-build.log').read_text(encoding='utf-8-sig')
    assert not (stage/'development-session.json').exists()
    name='mod_v0.1.5.1';release=ROOT/'artifacts/releases'/name;archive=release.parent/(name+'.zip')
    assert not release.exists() and not archive.exists()
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.5.zip') as previous:
        for path in (stage/'Assets').rglob('*'):
            if path.is_file() and 'Font' not in path.parts:
                assert path.read_bytes()==previous.read('HollowElements/'+path.relative_to(stage).as_posix()),path
    release.mkdir();mod=release/'HollowElements';mod.mkdir()
    for name in ['HollowElements.dll','HollowElements.pdb']:shutil.copy2(stage/name,mod/name)
    shutil.copytree(stage/'Assets',mod/'Assets')
    shutil.copy2(ROOT/'workspaces/mod/balance.defaults.json',mod/'balance.defaults.json')
    for source,target in [('v0.1.5.1减伤菜单说明.md','本版说明.md'),('v0.1.5战斗平衡说明.md','战斗平衡说明.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:shutil.copy2(ROOT/'docs'/source,release/target)
    metadata=dict(version='0.1.5.1',releaseName='mod_v0.1.5.1',game='1.5.78.11833',api=77,debugDefault=False,effects=13,frames=525,fps=60,audio=7,icons=5,
        scope='Four damage-reduction menu presets, persistence and focused native combat checks; 34 existing rule checks. No full Boss rerun.',reportSha256=sha(evidence/'damage-menu.json'))
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version='0.1.5.1',files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in release.rglob('*'):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    result=verify(archive);(evidence/'damage-menu-package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
