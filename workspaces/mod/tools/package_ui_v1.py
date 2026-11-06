"""Package the tested 1.0 build, with UI evidence tied to its DLL hash."""
import hashlib
import json
import shutil
import zipfile

from verify_release import ROOT, verify


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    stage = ROOT / 'artifacts/mod/HollowElements'
    evidence = ROOT / 'artifacts/acceptance/ui-v1'
    digest = sha(stage / 'HollowElements.dll')
    reports = []
    for name in ['runtime', 'pointer-final', 'lifecycle', 'install']:
        path = evidence / (name + '.json')
        result = json.loads(path.read_text(encoding='utf-8-sig'))
        assert result['passed'] and result['dllSha256'] == digest, name
        reports.append(dict(name=path.name, sha256=sha(path)))
    assert '64 passed, 0 failed' in (evidence / 'build-final.log').read_text(encoding='utf-8-sig')
    install = json.loads((evidence / 'install.json').read_text(encoding='utf-8-sig'))
    assert install['developmentMarkerRemoved'] and install['savesUnchanged']
    release = ROOT / 'artifacts/releases/mod_v1.0'
    archive = release.parent / (release.name + '.zip')
    assert not release.exists() and not archive.exists(), 'Release is immutable'
    mod = release / 'HollowElements'
    mod.mkdir(parents=True)
    for name in ['HollowElements.dll', 'HollowElements.pdb', 'balance.defaults.json', 'BossHints.json']:
        shutil.copy2(stage / name, mod / name)
    for name in ['Assets', 'ElementGuide']:
        shutil.copytree(stage / name, mod / name)
    shutil.copy2(ROOT / 'docs/v1.0 菜单与HUD修复.md', release / '本版说明.md')
    shutil.copy2(ROOT / 'docs/mod_v0.1使用说明.md', release / '安装与使用.md')
    metadata = dict(version='1.0.0', displayVersion='1.0', releaseName=release.name,
                    game='1.5.78.11833', api=77, runtime='net472', language='zh-CN',
                    effects=13, frames=525, audio=7, icons=5, debugDefault=False,
                    ruleChecks=64, finalDllSha256=digest, reports=reports,
                    guideImageMaxHeight=480,
                    scope='UI regression, 51 articles, configuration pages, carousel, hover/wheel/directional navigation, cursor, HUD layer and unload/reload. No full P5 or physical controller test.')
    (release / 'verification.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding='utf-8')
    files = [dict(path=p.relative_to(release).as_posix(), bytes=p.stat().st_size, sha256=sha(p))
             for p in sorted(release.rglob('*')) if p.is_file()]
    (release / 'manifest.json').write_text(json.dumps(dict(version='1.0.0', files=files), indent=2), encoding='utf-8')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as output:
        for path in release.rglob('*'):
            if path.is_file():
                output.write(path, path.relative_to(release).as_posix())
    result = verify(archive)
    (evidence / 'package.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
