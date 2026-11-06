"""Package the final native-tested independent charm release."""
import json
import shutil
import zipfile
from package_element_guide import ROOT,check_element_guide,read,sha
from verify_release import verify


def main():
    out=ROOT/'artifacts/acceptance/charms';stage=ROOT/'artifacts/mod/HollowElements';digest=sha(stage/'HollowElements.dll')
    for name in ['ui','core','protection','boss','persistence','element_guide','optimized']:
        report=read(out/(name+'.json'));assert report['passed'] and report['dllSha256']==digest,name
    assert read(out/'persistence.json')['result']['unload']['remainingCharmAssets']==0
    assert read(out/'install.json')['passed'] and read(out/'install.json')['dllSha256']==digest
    assert '48 passed, 0 failed' in (ROOT/'artifacts/acceptance/charms-tests.log').read_text(encoding='utf-8-sig')
    icons=read(out/'icons.json');assert icons['passed'] and len(icons['icons'])==20
    for entry in icons['icons']:
        source=ROOT/'workspaces/art/source/charms'/entry['file'];target=stage/'Assets/Charms'/entry['file']
        assert sha(source)==sha(target)==entry['sha256']
    assert not (stage/'development-session.json').exists()
    book=check_element_guide(stage/'ElementGuide');assert book['entries']==51 and book['images']==35
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.8.1.zip') as old:
        for p in (stage/'Assets').rglob('*'):
            if p.is_file() and p.parent.name!='Charms':assert p.read_bytes()==old.read('HollowElements/'+p.relative_to(stage).as_posix()),p
    defaults=read(stage/'balance.defaults.json');assert defaults['Reactions']['FrostSpeed']==.8 and defaults['DirectDamageMultiplier']==.8
    version='0.1.9';release=ROOT/('artifacts/releases/mod_v'+version);archive=release.parent/(release.name+'.zip')
    assert not release.exists() and not archive.exists()
    mod=release/'HollowElements';mod.mkdir(parents=True)
    for name in ['HollowElements.dll','HollowElements.pdb','balance.defaults.json']:shutil.copy2(stage/name,mod/name)
    for name in ['Assets','ElementGuide']:shutil.copytree(stage/name,mod/name)
    for source,target in [('v0.1.9护符实现与验收.md','本版说明.md'),('8 护符设计.md','护符设计.md'),('mod_v0.1使用说明.md','通用安装与使用.md')]:shutil.copy2(ROOT/'docs'/source,release/target)
    metadata=dict(version=version,releaseName=release.name,game='1.5.78.11833',api=77,debugDefault=False,
                  effects=13,frames=525,fps=60,audio=7,icons=5,charms=20,charmSlots=[1,1,2,1],charmEquipAtBenchOnly=True,
                  element_guide=book,finalDllSha256=digest,ruleChecks=48,existingBattleAssetsUnchanged=True,
                  scope='Native configuration permissions/limits, twenty icons and descriptions, reaction combinations, mask damage absorption, incoming debuffs during veil, isolated save/load, 51 articles and complete unload/reload. Targeted Enraged Guardian, White Defender and Hornet blast counters with actors held in blast footprint. No full P5 playthrough or physical controller hardware test.')
    (release/'verification.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version=version,files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as output:
        for p in release.rglob('*'):
            if p.is_file():output.write(p,p.relative_to(release).as_posix())
    result=verify(archive);result.update(charms=20,element_guide=book,existingBattleAssetsUnchanged=True)
    (out/'package.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))


if __name__=='__main__':main()
