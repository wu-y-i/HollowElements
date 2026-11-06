"""Validate and export v0.1.7, including the author's current editable element_guide."""
import hashlib
import json
import re
import shutil
import zipfile
from pathlib import Path
from package_element_guide import check_element_guide
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'artifacts/acceptance/boss-update'
CASES=['permissions','hornet','crystal','marmu','vessels','hu','tamer','watcher','uumuu','dung','pure','watcher_bars','popup_sizes']


def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    stage=ROOT/'artifacts/mod/HollowElements';digest=sha(stage/'HollowElements.dll')
    for name in CASES:
        case=read(OUT/(name+'.json'));assert case['passed'] and case['dllSha256']==digest,name
    for name in ['element_guide','input','install']:
        case=read(OUT/(name+'.json'));assert case['passed'] and case['dllSha256']==digest,name
    assert read(OUT/'element_guide.json')['unload']['passed']
    for index in range(1,43):assert read(OUT/'photos'/f'{index:02}.json')['passed']
    assert '36 passed, 0 failed' in (ROOT/'artifacts/acceptance/boss-update-build.log').read_text(encoding='utf-8-sig')
    compile_log=(ROOT/'artifacts/acceptance/boss-update-compile.log').read_text(encoding='utf-8-sig')
    assert '0 个警告' in compile_log and '0 个错误' in compile_log
    assert not (stage/'development-session.json').exists()
    element_guide=check_element_guide(stage/'ElementGuide');assert element_guide['entries']==51 and element_guide['images']==52
    for path in (stage/'ElementGuide/bosses').glob('*.md'):
        text=path.read_text(encoding='utf-8');assert not re.match(r'^#\s*c',text)
        assert '../images/bosses/'+path.stem+'.png' in text
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.6.2.zip') as previous:
        for path in (stage/'Assets').rglob('*'):
            if path.is_file():assert path.read_bytes()==previous.read('HollowElements/'+path.relative_to(stage).as_posix()),path
    version='0.1.7';release=ROOT/('artifacts/releases/mod_v'+version);archive=release.parent/(release.name+'.zip')
    assert not release.exists() and not archive.exists()
    mod=release/'HollowElements';mod.mkdir(parents=True)
    for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json']:shutil.copy2(stage/name,mod/name)
    shutil.copytree(stage/'Assets',mod/'Assets');shutil.copytree(stage/'ElementGuide',mod/'ElementGuide')
    for source,target in [('v0.1.7机制与图鉴更新.md','本版说明.md'),('v0.1.6图鉴说明.md','图鉴说明.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:
        shutil.copy2(ROOT/'docs'/source,release/target)
    metadata=dict(version=version,releaseName=release.name,game='1.5.78.11833',api=77,debugDefault=False,
                  effects=13,frames=525,fps=60,audio=7,icons=5,element_guide=element_guide,ruleChecks=36,nativeCases=CASES,
                  nativeValidatedBuildSha256=digest,
                  finalUiBuildSha256=digest,scope='Focused native Boss mechanics, 42 staged native screenshots, final-build document/UI/teardown and keyboard checks. No full P5 completion or physical controller test.')
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as output:
        for path in release.rglob('*'):
            if path.is_file():output.write(path,path.relative_to(release).as_posix())
    result=verify(archive);result['element_guide']=element_guide;result['battleAssetsUnchanged']=True
    (OUT/'package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))


if __name__=='__main__':main()
