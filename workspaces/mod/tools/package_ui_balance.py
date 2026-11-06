"""Export v0.1.8 with verified native fixes and the author's current compact illustrated book."""
import hashlib,json,shutil,zipfile
from pathlib import Path
from PIL import Image
from package_element_guide import check_element_guide
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'artifacts/acceptance/ui-balance'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    stage=ROOT/'artifacts/mod/HollowElements';digest=sha(stage/'HollowElements.dll');proof=read(OUT/'native-source-proof.json')
    # The final user steering only changes the new-install non-bench default.
    # Verify that all natively tested runtime sources remain byte-identical,
    # and record the original test build rather than relabelling its reports.
    for relative,expected in proof['files'].items():
        path=ROOT/relative
        if relative.endswith('/Settings/SettingsStore.cs'):
            assert path.read_text(encoding='utf-8')==proof['settingsStore'].replace('public bool AllowAwayFromBench;','public bool AllowAwayFromBench = true;')
        else:assert sha(path)==expected,relative
    for case in ['grimm','cyclone','healthbars','menu','element_guide']:
        report=read(OUT/(case+'.json'));assert report['passed'] and report['dllSha256']==proof['nativeDllSha256'],case
    assert read(OUT/'element_guide.json')['unload']['passed']
    install=read(OUT/'install.json');assert install['passed'] and install['dllSha256']==digest
    final_defaults=read(OUT/'defaults.json');assert final_defaults['passed'] and final_defaults['dllSha256']==digest
    assert '37 passed, 0 failed' in (ROOT/'artifacts/acceptance/ui-balance-build.log').read_text(encoding='utf-8-sig')
    images=read(OUT/'images.json');assert images['passed'] and len(images['imported'])==25 and images['allImagesBelow1MB']
    for item in images['imported']:
        path=stage/'ElementGuide'/item['target'];assert path.stat().st_size==item['bytes']<1_000_000
        with Image.open(path) as image:assert image.size==(1280,735)
    assert not (stage/'development-session.json').exists()
    element_guide=check_element_guide(stage/'ElementGuide');assert element_guide['entries']==51 and element_guide['images']==35
    files_on_disk={p.resolve() for p in (stage/'ElementGuide/images').rglob('*') if p.is_file()}
    assert len(files_on_disk)==35 and all(p.stat().st_size<1_000_000 for p in files_on_disk)
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.7.1.zip') as previous:
        for path in (stage/'Assets').rglob('*'):
            if path.is_file():assert path.read_bytes()==previous.read('HollowElements/'+path.relative_to(stage).as_posix())
    defaults=read(stage/'balance.defaults.json');assert defaults['DirectDamageMultiplier']==.8 and defaults['Attacks']['Cyclone']['AuraUnits']==.5
    version='0.1.8';release=ROOT/('artifacts/releases/mod_v'+version);archive=release.parent/(release.name+'.zip')
    assert not release.exists() and not archive.exists()
    mod=release/'HollowElements';mod.mkdir(parents=True)
    for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json']:shutil.copy2(stage/name,mod/name)
    for name in ['Assets','ElementGuide']:shutil.copytree(stage/name,mod/name)
    for source,target in [('v0.1.8修复与图鉴图片更新.md','本版说明.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:shutil.copy2(ROOT/'docs'/source,release/target)
    metadata=dict(version=version,releaseName=release.name,game='1.5.78.11833',api=77,debugDefault=False,effects=13,frames=525,fps=60,audio=7,icons=5,element_guide=element_guide,
        finalDllSha256=digest,nativeValidatedDllSha256=proof['nativeDllSha256'],nativeRuntimeSourceEqualityVerified=True,finalDefaultChange='AllowAwayFromBench=true',ruleChecks=37,
        importedScreenshots=25,allElementGuideImagesBelow1MB=True,enemyHPBarVersion='3.1.5',scope='Native special-death, cyclone, actual EnemyHPBar anchors, settings navigation/scroll and full editable-book lifecycle checks. Final non-bench default covered by rule checks. No full P5 completion or physical controller hardware test.')
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as output:
        for path in release.rglob('*'):
            if path.is_file():output.write(path,path.relative_to(release).as_posix())
    result=verify(archive);result['element_guide']=element_guide;result['allElementGuideImagesBelow1MB']=True
    (OUT/'package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
