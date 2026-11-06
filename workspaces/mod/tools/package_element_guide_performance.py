"""Package the native-tested element_guide caching patch without changing media assets."""
import json
import shutil
import zipfile
from package_element_guide import ROOT, check_element_guide, read, sha
from verify_release import verify


def main():
    out=ROOT/'artifacts/acceptance/element_guide-performance'
    stage=ROOT/'artifacts/mod/HollowElements'
    digest=sha(stage/'HollowElements.dll')
    for name in ['optimized','element_guide']:
        report=read(out/(name+'.json'))
        assert report['passed'] and report['dllSha256']==digest, name
    assert read(out/'edits.json')['passed']
    assert read(out/'element_guide.json')['unload']['passed']
    assert read(out/'install.json')['passed']
    assert '37 passed, 0 failed' in (ROOT/'artifacts/acceptance/element_guide-performance-tests.log').read_text(encoding='utf-8-sig')
    assert not (stage/'development-session.json').exists()
    element_guide=check_element_guide(stage/'ElementGuide')
    assert element_guide['entries']==51 and element_guide['images']==35
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.8.zip') as previous:
        for path in (stage/'Assets').rglob('*'):
            if path.is_file(): assert path.read_bytes()==previous.read('HollowElements/'+path.relative_to(stage).as_posix()),path
    version='0.1.8.1'
    release=ROOT/('artifacts/releases/mod_v'+version)
    archive=release.parent/(release.name+'.zip')
    assert not release.exists() and not archive.exists()
    mod=release/'HollowElements';mod.mkdir(parents=True)
    for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json']: shutil.copy2(stage/name,mod/name)
    for name in ['Assets','ElementGuide']: shutil.copytree(stage/name,mod/name)
    for source,target in [('v0.1.8.1图鉴加载优化.md','本版说明.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:
        shutil.copy2(ROOT/'docs'/source,release/target)
    metadata=dict(version=version,releaseName=release.name,game='1.5.78.11833',api=77,debugDefault=False,
                  effects=13,frames=525,fps=60,audio=7,icons=5,element_guide=element_guide,finalDllSha256=digest,
                  ruleChecks=37,pictureCacheBudgetMiB=32,battleAssetsUnchanged=True,
                  scope='Native first/repeated opens, external document/index/image edits and fallback, 51 articles, scroll/resizing, config permissions and unload/reload. No full Boss combat rerun or physical controller hardware test. Additional forced Boss-transition fixture did not yield a stable automatic open test; it is not counted as passed.')
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as output:
        for path in release.rglob('*'):
            if path.is_file():output.write(path,path.relative_to(release).as_posix())
    result=verify(archive);result.update(element_guide=element_guide,battleAssetsUnchanged=True)
    (out/'package.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
