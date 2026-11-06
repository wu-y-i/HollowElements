"""Verify the distributable itself, independently of the staging directory."""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import sys
import wave
import zipfile
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]


def verify_atlas_payload(names):
    files=[n.replace('\\','/').removeprefix('HollowElements/') for n in names]
    groups=['Assets/Charms/','Assets/Icons/']
    for prefix in groups:
        actual={n for n in files if n.startswith(prefix)}
        assert actual=={prefix+'atlas.png',prefix+'atlas.json'},('Unexpected loose icon/source files',actual)
    effects={n.split('/')[2] for n in files if n.startswith('Assets/Effects/')}
    assert len(effects)==13
    for name in effects:
        prefix='Assets/Effects/'+name+'/'
        assert {n for n in files if n.startswith(prefix)}=={prefix+'atlas.png',prefix+'manifest.json'},('Unexpected loose effect frames',name)
    return dict(passed=True,atlases=15,looseSourceImages=0,effects=len(effects))

def verify(path):
    with zipfile.ZipFile(path) as archive:
        names=[name for name in archive.namelist() if not name.endswith("/")]
        assert len(names)==len(set(names))
        for name in names:
            assert not name.startswith(("/","\\")) and ":" not in name and ".." not in PurePosixPath(name).parts
            assert "development-session.json" not in name and not name.endswith("Assembly-CSharp.dll")
        if 'HollowElements/Assets/Charms/atlas.json' in names: verify_atlas_payload(names)
        manifest=json.loads(archive.read("manifest.json").decode("utf-8-sig"))
        expected={"manifest.json"}
        for item in manifest["files"]:
            name=item["path"].replace("\\","/"); expected.add(name)
            data=archive.read(name)
            assert len(data)==item["bytes"] and hashlib.sha256(data).hexdigest()==item["sha256"],name
        assert expected==set(names)
        dll=archive.read("HollowElements/HollowElements.dll")
        assert dll==(ROOT/"artifacts/mod/HollowElements/HollowElements.dll").read_bytes()
        metadata=json.loads(archive.read("verification.json").decode("utf-8-sig"))
        if tuple(map(int,metadata['version'].split('.'))) >= (0,1,10,0):
            assert not any(n.lower().endswith(('.xlsx','.xls')) for n in names),'Authoring workbook leaked into release'
            assert archive.read('HollowElements/BossHints.json')==(ROOT/'workspaces/mod/content/BossHints.json').read_bytes()
            hints=json.loads(archive.read('HollowElements/BossHints.json'))
            assert hints['Version']==1 and len(hints['Entries'])==42
        expected_version=path.stem.removeprefix("mod_v")
        if metadata.get("releaseName"):
            assert metadata["releaseName"]==path.stem
            expected_version=expected_version.split("-",1)[0]
        if expected_version.count(".")==1: expected_version+=".0"
        assert metadata["version"]==manifest["version"]==expected_version and metadata["game"]=="1.5.78.11833" and metadata["api"]==77
        assert metadata["debugDefault"] is False
        if (0,1,9,5) <= tuple(map(int,metadata['version'].split('.'))) < (0,1,9,6):
            assert not any('/Codex/' in name for name in names)
            guide='HollowElements/ElementGuide/'
            assert not any(name.startswith(guide) and (name.endswith('.md') or name.endswith('/index.json') or '.content-state' in name or '.incoming' in name) for name in names)
            with zipfile.ZipFile(io.BytesIO(archive.read(guide+'content.zip'))) as content:
                assert content.namelist()==['documents.json']
                documents=json.loads(content.read('documents.json'))
            source=ROOT/'workspaces/mod/content/ElementGuide'
            expected_text={p.relative_to(source).as_posix():p.read_bytes().decode('utf-8') for p in source.rglob('*') if p.is_file() and (p.suffix=='.md' or p==source/'index.json')}
            assert documents==expected_text and len(documents)==53
        if (0,1,9,6) <= tuple(map(int,metadata['version'].split('.'))) < (0,1,9,8):
            guide='HollowElements/ElementGuide/'
            assert guide+'content.zip' not in names and guide+'.content-state.json' not in names
            source=ROOT/'workspaces/mod/content/ElementGuide'
            for p in source.rglob('*'):
                if p.is_file():assert archive.read(guide+p.relative_to(source).as_posix())==p.read_bytes()
        if tuple(map(int,metadata['version'].split('.'))) >= (0,1,9,8):
            from verify_guide_media import verify_media
            guide='HollowElements/ElementGuide/'
            verify_media(lambda name:archive.read(guide+name),[n[len(guide):] for n in names if n.startswith(guide)])
        frame_count=0; effect_count=0
        for name in names:
            if "/Effects/" not in name or not name.endswith("/manifest.json"): continue
            effect_count+=1; clip=json.loads(archive.read(name)); frame_count+=clip["frames"]
            if metadata.get('fps'): assert clip['fps']==metadata['fps']
            assert len(clip["samples"])==clip["frames"] and clip["alpha"]=="straight"
            assert sorted(s["index"] for s in clip["samples"])==list(range(clip["frames"]))
            atlas=Image.open(io.BytesIO(archive.read(name.replace("manifest.json","atlas.png")))).convert("RGBA")
            for sample in clip["samples"]:
                assert 0<=sample["x"] and 0<=sample["y"] and sample["x"]+sample["width"]<=atlas.width and sample["y"]+sample["height"]<=atlas.height
            if clip.get('sourceFrames'):
                from import_reaction_fx import SOURCE,MAPPING
                folder=next(folder for folder,effect in MAPPING if effect==clip['name'])
                originals=sorted((SOURCE/folder).glob('*.png'))
                assert len(originals)==clip['frames']
                for sample,original in zip(clip['samples'],originals):
                    assert hashlib.sha256(original.read_bytes()).hexdigest()==sample['sourceSha256']
                    expected=Image.open(original).convert('RGBA')
                    processing=clip.get('textureProcessing')
                    if processing:
                        from optimize_runtime_textures import resized
                        assert processing['version']==1 and processing['resample']=='premultiplied-alpha-lanczos'
                        assert processing['sourceWidth']==processing['sourceHeight']==512 and processing['frameSize'] in (128,192,256,320,384,448,512)
                        assert sample['width']==sample['height']==processing['frameSize']==clip['width']==clip['height']
                        expected=resized(expected,(sample['width'],sample['height']))
                    assert atlas.crop((sample['x'],sample['y'],sample['x']+sample['width'],sample['y']+sample['height'])).tobytes()==expected.tobytes()
            if not clip["loop"] and not clip.get("sourceFrames"):
                s=clip["samples"][-1]
                assert atlas.crop((s["x"],s["y"],s["x"]+s["width"],s["y"]+s["height"])).getchannel("A").getbbox() is None
            if clip["name"]=="electro-state" or clip["name"]=="electro-charged" and not clip.get("sourceFrames"):
                assert clip["trigger"]=="electro-charged-damage" and not clip["loop"]
        assert effect_count==metadata['effects'] and effect_count in (10,13) and frame_count==metadata['frames'] and frame_count in (249,405,525)
        sounds=[name for name in names if "/Audio/" in name and name.endswith(".wav")]
        assert len(sounds)==7
        for name in sounds:
            data=archive.read(name)
            assert data==(ROOT/"workspaces/art/source/audio/generated"/Path(name).name).read_bytes()
            with wave.open(io.BytesIO(data)) as audio:
                assert audio.getsampwidth()==2 and audio.getframerate()==48000 and audio.getnchannels()==2 and audio.getnframes()>0
        if "HollowElements/Assets/Icons/atlas.json" in names:
            from optimize_runtime_textures import resized
            for group,source,count,size in [('Icons',ROOT/'workspaces/art/source/icons/elements',5,128),('Charms',ROOT/'workspaces/art/source/charms',20,192)]:
                prefix='HollowElements/Assets/'+group+'/'
                layout=json.loads(archive.read(prefix+'atlas.json'))
                assert layout['packer']=='Unity Texture2D.PackTextures' and len(layout['sprites'])==count
                atlas=Image.open(io.BytesIO(archive.read(prefix+'atlas.png'))).convert('RGBA')
                assert atlas.size==(layout['width'],layout['height'])
                assert [n for n in names if n.startswith(prefix) and n.endswith('.png')]==[prefix+'atlas.png']
                for key,r in layout['sprites'].items():
                    assert r['width']==r['height']==size
                    y=atlas.height-r['y']-r['height']
                    cropped=atlas.crop((r['x'],y,r['x']+r['width'],y+r['height']))
                    assert cropped.tobytes()==resized(Image.open(source/(key+'.png')),(size,size)).tobytes()
            icons=set(json.loads(archive.read('HollowElements/Assets/Icons/atlas.json'))['sprites'])
        else:
            icons={Path(name).stem for name in names if "/Icons/" in name and name.endswith(".png")}
        assert icons=={"pyro","hydro","electro","cryo","geo"}
        font=json.loads(archive.read("HollowElements/Assets/Font/glyphs.json"))
        ids=[g["id"] for g in font["glyphs"]]; assert len(ids)==len(set(ids))
        assert all(ord(c) in ids for c in "蒸发融化超载感电冻结结霜超导碎冰长椅配装调试")
        font_atlas=Image.open(io.BytesIO(archive.read("HollowElements/Assets/Font/atlas.png")))
        assert font_atlas.size==(font["face"]["AtlasWidth"],font["face"]["AtlasHeight"])
        if tuple(map(int,metadata["version"].split("."))) >= (0,1,2):
            from PIL import ImageChops, ImageFilter
            base=font_atlas.convert("RGBA").getchannel("A")
            outline=Image.open(io.BytesIO(archive.read("HollowElements/Assets/Font/outline.png"))).convert("RGBA").getchannel("A")
            assert outline.size==base.size and outline.getbbox()
            assert ImageChops.difference(outline,ImageChops.subtract(base.filter(ImageFilter.MaxFilter(5)),base)).getbbox() is None
    return dict(passed=True,files=len(names),effects=effect_count,frames=frame_count,audio=len(sounds),glyphs=len(ids),dllSha256=hashlib.sha256(dll).hexdigest(),zipSha256=hashlib.sha256(path.read_bytes()).hexdigest())


if __name__=="__main__":
    if len(sys.argv)>1 and sys.argv[1]=='--atlas-only':
        stage=Path(sys.argv[2]);print(json.dumps(verify_atlas_payload([p.relative_to(stage).as_posix() for p in stage.rglob('*') if p.is_file()])))
    else:
        path=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/"artifacts/releases/mod_v0.1.3.zip"
        report=verify(path)
        (ROOT/"artifacts/acceptance/package-verification.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
        print(json.dumps(report,indent=2))
