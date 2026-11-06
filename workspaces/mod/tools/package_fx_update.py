"""Package current branch with verified supplied effects; keep previous releases intact."""
import argparse,hashlib,json,shutil,zipfile
from pathlib import Path
from verify_release import verify
ROOT=Path(__file__).resolve().parents[3]
NAME='mod_v0.1.3-fx2'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--fps',type=int,choices=[60])
    args=parser.parse_args()
    name=NAME+('-60fps' if args.fps else '')
    stage=ROOT/'artifacts/mod/HollowElements';evidence=ROOT/'artifacts/acceptance'
    runtime=json.loads((evidence/'fx-v2-runtime.json').read_text(encoding='utf-8'));assert runtime['passed']
    normal=json.loads((evidence/'fx-v2-normal.json').read_text(encoding='utf-8'));assert normal['passed'] and normal['dllSha256']==sha(stage/'HollowElements.dll')
    if args.fps:
        # This revision changes only the playback rate of the previously tested
        # full-frame assets. Assert that code, images and all other assets match.
        with zipfile.ZipFile(ROOT/'artifacts/releases'/(NAME+'.zip')) as baseline:
            for path in stage.rglob('*'):
                if not path.is_file() or path.name not in ('HollowElements.dll','HollowElements.pdb') and 'Assets' not in path.relative_to(stage).parts: continue
                previous=baseline.read('HollowElements/'+path.relative_to(stage).as_posix())
                if path.name=='manifest.json' and 'Effects' in path.parts:
                    old=json.loads(previous);new=json.loads(path.read_text())
                    assert new['fps']==args.fps
                    old['fps']=args.fps
                    assert old==new,path
                else:assert previous==path.read_bytes(),path
    release=ROOT/'artifacts/releases'/name;assert not release.exists()
    release.mkdir();mod=release/'HollowElements';mod.mkdir()
    for n in ('HollowElements.dll','HollowElements.pdb'):shutil.copy2(stage/n,mod/n)
    shutil.copytree(stage/'Assets',mod/'Assets');shutil.copy2(ROOT/'workspaces/mod/balance.defaults.json',mod/'balance.defaults.json')
    shutil.copy2(ROOT/'docs/新反应特效接入.md',release/'本版说明.md')
    reports=[dict(name=n,sha256=sha(evidence/n)) for n in ('fx-v2-runtime.json','fx-v2-normal.json','fx-v2-build.log','fx-v2-final-build.log')]
    metadata=dict(version='0.1.3',releaseName=name,game='1.5.78.11833',api=77,runtime='net472',language='zh-CN',effects=10,frames=405,audio=7,icons=5,debugDefault=False,reports=reports)
    if args.fps:
        metadata.update(fps=args.fps,validationScope='Playback manifests changed only; native runtime evidence is from the previous 30 FPS revision.',baselineRelease=NAME)
    (release/'verification.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file()]
    (release/'manifest.json').write_text(json.dumps(dict(version='0.1.3',files=files),indent=2),encoding='utf-8')
    archive=release.parent/(name+'.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in release.rglob('*'):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    report=verify(archive);(evidence/('fx-v2-60fps-package.json' if args.fps else 'fx-v2-package.json')).write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
