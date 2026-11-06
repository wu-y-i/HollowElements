"""Export the focused v0.1.7.1 UI patch without relabelling prior Boss tests."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path
from package_element_guide import check_element_guide
from verify_release import verify

ROOT=Path(__file__).resolve().parents[3]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))

def main():
    stage=ROOT/'artifacts/mod/HollowElements';out=ROOT/'artifacts/acceptance/element_guide-shortcut';digest=sha(stage/'HollowElements.dll')
    for name in ['native','install']:
        report=read(out/(name+'.json'));assert report['passed'] and report['dllSha256']==digest,name
    assert report['developmentMarkerRemoved']
    log=(ROOT/'artifacts/acceptance/element_guide-shortcut-build.log').read_text(encoding='utf-8-sig')
    assert '0 个警告' in log and '0 个错误' in log
    assert not (stage/'development-session.json').exists()
    source=ROOT/'workspaces/mod/content/ElementGuide'
    for path in source.rglob('*'):
        if path.is_file():assert path.read_bytes()==(stage/'ElementGuide'/path.relative_to(source)).read_bytes()
    element_guide=check_element_guide(stage/'ElementGuide')
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.7.zip') as previous:
        for path in (stage/'Assets').rglob('*'):
            if path.is_file():assert path.read_bytes()==previous.read('HollowElements/'+path.relative_to(stage).as_posix())
    version='0.1.7.1';release=ROOT/('artifacts/releases/mod_v'+version)
    archive=release.parent/(release.name+'.zip')
    assert not release.exists() and not archive.exists()
    mod=release/'HollowElements';mod.mkdir(parents=True)
    for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json']:shutil.copy2(stage/name,mod/name)
    for name in ['Assets','ElementGuide']:shutil.copytree(stage/name,mod/name)
    for source_name,target in [('v0.1.7.1图鉴快捷入口.md','本版说明.md'),('v0.1.7机制与图鉴更新.md','v0.1.7机制与图鉴更新.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:
        shutil.copy2(ROOT/'docs'/source_name,release/target)
    metadata=dict(version=version,releaseName=release.name,game='1.5.78.11833',api=77,debugDefault=False,effects=13,frames=525,fps=60,audio=7,icons=5,element_guide=element_guide,
                  dllSha256=digest,previousBattleRegressionVersion='0.1.7',unityAssetBundleBuildSucceeded=False,
                  scope='Focused keyboard shortcut, read-only/edit permission and source/package checks. Physical controller not connected. No new Boss regression run; Boss logic unchanged.')
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as result:
        for path in release.rglob('*'):
            if path.is_file():result.write(path,path.relative_to(release).as_posix())
    result=verify(archive);result['element_guide']=element_guide
    (out/'package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))

if __name__=='__main__':main()
