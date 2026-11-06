"""Archive supplied PNG sequences and pack lossless atlases for the old game."""
import hashlib,json,math,shutil
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
SOURCE=ROOT/'workspaces/art/source/reaction-fx-v2'
MAPPING=[('01_vaporize','vaporize'),('02_melt','melt'),('03_overloaded','overload'),('04_superconduct','superconduct'),('05_electro_charged','electro-charged'),('06_frozen','freeze'),('07_electro_charged_dot','electro-state'),('08_frozen_loop','frost-state'),('09_shatter','shatter'),('10_superconduct_dot','superconduct-state'),('11_frost','frost'),('12_frost_loop','soft-frost-state'),('13_frost_shatter','frost-shatter')]
def pack():
    for folder,name in MAPPING:
        frames=sorted((SOURCE/folder).glob('*.png')); assert frames,folder
        cols=min(8,len(frames)); rows=math.ceil(len(frames)/cols)
        atlas=Image.new('RGBA',(cols*512,rows*512));samples=[]
        for i,path in enumerate(frames):
            frame=Image.open(path); assert frame.size==(512,512) and frame.mode=='RGBA'
            x=i%cols*512;y=i//cols*512;atlas.paste(frame,(x,y))
            samples.append(dict(index=i,x=x,y=y,width=512,height=512,sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        dest=ROOT/'artifacts/reaction-fx-v2'/name;dest.mkdir(parents=True,exist_ok=True)
        atlas.save(dest/'atlas.png')
        manifest=dict(schema=1,name=name,width=512,height=512,frames=len(frames),fps=60,worldSize=3.8,loop=name in ('frost-state','soft-frost-state','superconduct-state'),alpha='straight',trigger='electro-charged-damage' if name=='electro-state' else 'reaction',sourceFrames=len(frames),samples=samples)
        (dest/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(f'Packed {len(MAPPING)} supplied effects / {sum(len(list((SOURCE/folder).glob("*.png"))) for folder,_ in MAPPING)} original frames.')
if __name__=='__main__': pack()
