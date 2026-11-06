"""Install and package the approved electro tick, preserving the fx4 payload."""
import array,hashlib,json,shutil,subprocess,sys,wave,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'local/tools/audio-python'))
import imageio_ffmpeg
from verify_release import verify

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    sound='electro_tick_01.wav';name='mod_v0.1.3-fx5'
    base=ROOT/'artifacts/releases/mod_v0.1.3-fx4'
    release=ROOT/'artifacts/releases'/name;archive=release.parent/(name+'.zip')
    assert not release.exists() and not archive.exists(),'Release already exists'
    stage=ROOT/'artifacts/mod/HollowElements'
    game=Path(json.loads((ROOT/'local/paths.json').read_text())['gameDirectory'])
    installed=game/'hollow_knight_Data/Managed/Mods/HollowElements'
    assert not (installed/'development-session.json').exists()
    source=ROOT/'workspaces/art/source/audio/sonilo/electro-tick-653511db'
    original=source/'electro-tick.m4a'
    result=json.loads((source/'result.json').read_text(encoding='utf-8'))
    assert result['status']=='succeeded' and original.stat().st_size==result['audio']['file_size']
    payload=[p for p in (base/'HollowElements').rglob('*') if p.is_file()]
    installed_before={p.relative_to(installed):sha(p) for p in installed.rglob('*') if p.is_file()}
    for p in payload:
        relative=p.relative_to(base/'HollowElements')
        # Old deployments can retain an older reference defaults file; preserve
        # that file and the actual user configuration rather than updating it.
        if relative.as_posix()!='balance.defaults.json':assert p.read_bytes()==(installed/relative).read_bytes(),relative
        if (stage/relative).exists():assert p.read_bytes()==(stage/relative).read_bytes(),relative
    target=ROOT/'workspaces/art/source/audio/generated'/sound
    backup=source/('previous-'+sound)
    assert not backup.exists(),'Audio replacement already started'
    shutil.copy2(target,backup)
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-i',str(original),'-vn','-ac','2','-ar','48000','-c:a','pcm_s16le',str(target)],check=True)
    with wave.open(str(target)) as audio:
        assert audio.getframerate()==48000 and audio.getnchannels()==2 and audio.getsampwidth()==2
        samples=array.array('h',audio.readframes(audio.getnframes()))
        assert any(samples),'Silent audio'
        audio_info=dict(rate=48000,channels=2,sampleBytes=2,seconds=audio.getnframes()/48000,peak=max(abs(s) for s in samples)/32768,sha256=sha(target))
    for directory in (stage,installed):shutil.copy2(target,directory/'Assets/Audio'/sound)
    assert target.read_bytes()==(installed/'Assets/Audio'/sound).read_bytes()==(stage/'Assets/Audio'/sound).read_bytes()
    # All previous runtime files except the requested WAV remain byte-identical.
    for relative,old_hash in installed_before.items():
        expected=sha(target) if relative.as_posix()=='Assets/Audio/'+sound else old_hash
        assert expected==sha(installed/relative),relative
    shutil.copytree(base,release)
    shutil.copy2(target,release/'HollowElements/Assets/Audio'/sound)
    shutil.copy2(ROOT/'docs/感电音效修订.md',release/'本版说明.md')
    evidence=ROOT/'artifacts/acceptance'
    report=dict(passed=True,sourceTask=result['task_id'],audio=audio_info,backup=str(backup),replacedFile='Assets/Audio/'+sound,unchangedPayloadFiles=len(payload)-1,dllSha256=sha(stage/'HollowElements.dll'),nativeRuntimeRepeated=False)
    report_path=evidence/'fx-v5-audio.json'
    report_path.write_text(json.dumps(report,indent=2),encoding='utf-8')
    metadata=json.loads((base/'verification.json').read_text())
    metadata.update(releaseName=name,baselineRelease=base.name,validationScope='Only electro tick WAV replaced; native runtime evidence retained from fx4.')
    metadata['reports'].append(dict(name=report_path.name,sha256=sha(report_path)))
    (release/'verification.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    files=[dict(path=p.relative_to(release).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(release.rglob('*')) if p.is_file() and p!=release/'manifest.json']
    (release/'manifest.json').write_text(json.dumps(dict(version='0.1.3',files=files),indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in release.rglob('*'):
            if p.is_file():z.write(p,p.relative_to(release).as_posix())
    checked=verify(archive)
    (evidence/'fx-v5-package.json').write_text(json.dumps(checked,indent=2),encoding='utf-8')
    result['usage']='已替换游戏感电跳伤音效，随 mod_v0.1.3-fx5 导出'
    (source/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(replacement=report,package=checked),indent=2))

if __name__=='__main__':main()
