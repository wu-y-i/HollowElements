"""Build an immutable patch release from the preceding verified distribution."""
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
    stage = ROOT / 'artifacts/mod/HollowElements'
    evidence = ROOT / 'artifacts/acceptance/xero/acceptance.json'
    report = json.loads(evidence.read_text(encoding='utf-8'))
    assert report['passed'] and report['dllSha256'] == sha(stage / 'HollowElements.dll')
    assert len(report['scenes']) == 2 and len(report['contacts']) == 8
    install = json.loads((evidence.parent / 'install.json').read_text(encoding='utf-8'))
    assert install['passed'] and install['dllSha256'] == report['dllSha256']
    build = (evidence.parent / 'build.log').read_text(encoding='utf-8-sig')
    assert '0 个警告' in build and '0 个错误' in build
    assert not (stage / 'development-session.json').exists()
    version = '0.1.6.2'
    release = ROOT / ('artifacts/releases/mod_v' + version)
    archive = release.parent / (release.name + '.zip')
    assert not release.exists() and not archive.exists()
    with zipfile.ZipFile(ROOT / 'artifacts/releases/mod_v0.1.6.1.zip') as previous:
        # The workspace contains the author's ongoing ElementGuide edits. Keep those
        # drafts untouched and retain the previous release's documents in this
        # narrowly scoped binary patch; installation also preserves live files.
        for folder in ['Assets']:
            for path in (stage / folder).rglob('*'):
                if path.is_file():
                    assert path.read_bytes() == previous.read('HollowElements/' + path.relative_to(stage).as_posix()), path
        previous.extractall(release)
    for name in ['HollowElements.dll', 'HollowElements.pdb']:
        shutil.copy2(stage / name, release / 'HollowElements' / name)
    shutil.copy2(ROOT / 'docs/v0.1.6.2泽若染色修复.md', release / '泽若染色修复.md')
    metadata_path = release / 'verification.json'
    metadata = json.loads(metadata_path.read_text(encoding='utf-8'))
    metadata.update(version=version, releaseName=release.name,
                    scope='Xero phase-two tint activation only: both GG scenes, native half-HP transition, actual sprite colours and eight native collision auras. No full Pantheon rerun.',
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
    (evidence.parent / 'package.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
