"""Package the focused element_guide reading-colour patch without replacing older releases."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path
from verify_release import verify

ROOT = Path(__file__).resolve().parents[3]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = '0.1.6.1'
    stage = ROOT / 'artifacts/mod/HollowElements'
    evidence = ROOT / 'artifacts/acceptance/element_guide-reading.json'
    report = json.loads(evidence.read_text(encoding='utf-8'))
    assert report['passed'] and report['dllSha256'] == sha(stage / 'HollowElements.dll')
    assert not (stage / 'development-session.json').exists()
    build = (ROOT / 'artifacts/acceptance/element_guide-reading-build.log').read_text(encoding='utf-8-sig')
    assert '0 个警告' in build and '0 个错误' in build
    old = ROOT / 'artifacts/releases/mod_v0.1.6.zip'
    release = ROOT / ('artifacts/releases/mod_v' + version)
    archive = release.parent / (release.name + '.zip')
    assert not release.exists() and not archive.exists()
    with zipfile.ZipFile(old) as previous:
        # Neither combat media nor user-editable documents changed in this patch.
        for folder in ['Assets', 'ElementGuide']:
            for path in (stage / folder).rglob('*'):
                if path.is_file():
                    assert path.read_bytes() == previous.read('HollowElements/' + path.relative_to(stage).as_posix()), path
        previous.extractall(release)
    for name in ['HollowElements.dll', 'HollowElements.pdb']:
        shutil.copy2(stage / name, release / 'HollowElements' / name)
    shutil.copy2(ROOT / 'docs/v0.1.6.1图鉴阅读修订.md', release / '阅读配色修订.md')
    metadata_path = release / 'verification.json'
    metadata = json.loads(metadata_path.read_text(encoding='utf-8'))
    metadata.update(version=version, releaseName=release.name,
                    scope='ElementGuide reading text colour only; build and focused long-document scroll check. Native input/layout already covered by v0.1.6.',
                    acceptanceSha256=sha(evidence))
    metadata_path.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding='utf-8')
    files = [dict(path=p.relative_to(release).as_posix(), bytes=p.stat().st_size, sha256=sha(p))
             for p in sorted(release.rglob('*')) if p.is_file() and p != release / 'manifest.json']
    (release / 'manifest.json').write_text(json.dumps(dict(version=version, files=files), indent=2), encoding='utf-8')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as output:
        for path in release.rglob('*'):
            if path.is_file():
                output.write(path, path.relative_to(release).as_posix())
    result = verify(archive)
    (ROOT / 'artifacts/acceptance/element_guide-reading-package.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
