"""Package frost visuals and the approved Sonilo overload audio."""
import argparse,hashlib,json,shutil,zipfile
from pathlib import Path
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--prefix',default='fx-v3',choices=['fx-v3','fx-v4'])
    args=parser.parse_args();prefix=args.prefix
    stage=ROOT/'artifacts/mod/HollowElements';evidence=ROOT/'artifacts/acceptance'
    assert json.loads((evidence/(prefix+'-runtime.json')).read_text(encoding='utf-8'))['passed']
    install=json.loads((evidence/(prefix+'-install.json')).read_text(encoding='utf-8'))
    assert install['passed'] and install['dllSha256']==sha(stage/'HollowElements.dll')
    clips=[json.loads(p.read_text()) for p in (stage/'Assets/Effects').glob('*/manifest.json')]
    assert len(clips)==13 and sum(c['frames'] for c in clips)==525 and all(c['fps']==60 for c in clips)
    assert all(len(c['samples'])==c['frames']==c['sourceFrames'] for c in clips)
    # Check that only the requested existing audio was replaced.
    with zipfile.ZipFile(ROOT/'artifacts/releases/mod_v0.1.3-fx2-60fps.zip') as previous:
        for p in (stage/'Assets/Audio').glob('*.wav'):
            old=previous.read('HollowElements/Assets/Audio/'+p.name)
            assert (old!=p.read_bytes()) if p.name=='overload_01.wav' else (old==p.read_bytes()),p.name
    name='mod_v0.1.3-'+prefix.replace('fx-v','fx');release=ROOT/'artifacts/releases'/name
    assert not release.exists() and not (release.parent/(name+'.zip')).exists()
    release.mkdir();mod=release/'HollowElements';mod.mkdir()
    for n in ('HollowElements.dll','HollowElements.pdb'):shutil.copy2(stage/n,mod/n)
    shutil.copytree(stage/'Assets',mod/'Assets');shutil.copy2(ROOT/'workspaces/mod/balance.defaults.json',mod/'balance.defaults.json')
    shutil.copy2(ROOT/'docs/结霜特效与音效修订.md',release/'本版说明.md')
    report_names=[prefix+'-runtime.json',prefix+'-install.json',prefix+'-build.log','fx-v3-audio.json','fx-v3-padding.json']
    if prefix=='fx-v4':report_names.append('fx-v3-runtime.json')
    reports=[dict(name=n,sha256=sha(evidence/n)) for n in report_names]
    metadata=dict(version='0.1.3',releaseName=name,game='1.5.78.11833',api=77,runtime='net472',language='zh-CN',effects=13,frames=525,fps=60,audio=7,icons=5,debugDefault=False,reports=reports)
    (release/'verification.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version='0.1.3',files=files),indent=2),encoding='utf-8')
    archive=release.parent/(name+'.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in release.rglob('*'):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    report=verify(archive);report['unusedAtlasCellsExcluded']=True
    (evidence/(prefix+'-package.json')).write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
