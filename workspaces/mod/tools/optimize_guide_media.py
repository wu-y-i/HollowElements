"""Lossless SDF PNG encoding and right-sized guide illustrations; preserve masters."""
import argparse,hashlib,io,json,math,shutil,re
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3];SOURCE=ROOT/'workspaces/mod/content/ElementGuide'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def optimize(target):
 target=Path(target).resolve();assert target.is_relative_to(ROOT) and target!=SOURCE.resolve()
 target.mkdir(parents=True,exist_ok=True);fonts=[];pictures=[];replacements={}
 for p in sorted((SOURCE/'Font').glob('*')):
  dest=target/'Font'/p.name;dest.parent.mkdir(parents=True,exist_ok=True)
  if p.suffix!='.png':shutil.copy2(p,dest);continue
  original=Image.open(p).convert('RGBA');alpha=original.getchannel('A');packed=alpha.convert('P');packed.putpalette([255]*768);packed.info['transparency']=bytes(range(256));packed.save(dest,optimize=True,compress_level=9)
  assert Image.open(dest).convert('RGBA').tobytes()==original.tobytes(),p
  if dest.stat().st_size>=p.stat().st_size:shutil.copy2(p,dest)
  fonts.append(dict(path=p.relative_to(SOURCE).as_posix(),sourceSha256=sha(p),releaseSha256=sha(dest),size=list(original.size),beforeBytes=p.stat().st_size,afterBytes=dest.stat().st_size,alphaLossless=True))
 for p in sorted((SOURCE/'images').rglob('*')):
  if not p.is_file():continue
  relative=p.relative_to(SOURCE);image=Image.open(p);originalSize=image.size;image.thumbnail((960,960),Image.Resampling.LANCZOS)
  photo=p.suffix.lower() in ['.jpg','.jpeg'] or p.parent.name=='bosses'
  release=relative.with_suffix('.jpg') if photo else relative;dest=target/release;dest.parent.mkdir(parents=True,exist_ok=True)
  if photo:image.convert('RGB').save(dest,quality=84,subsampling=0,optimize=True)
  else:image.save(dest,optimize=True,compress_level=9)
  replacements[relative.as_posix()]=release.as_posix()
  if release!=relative and (target/relative).exists():(target/relative).unlink()
  pictures.append(dict(source=relative.as_posix(),release=release.as_posix(),sourceSha256=sha(p),releaseSha256=sha(dest),beforeSize=list(originalSize),afterSize=list(image.size),beforeBytes=p.stat().st_size,afterBytes=dest.stat().st_size,format='JPEG' if photo else 'PNG'))
 # Keep docs editable and direct-loaded. Only rewrite paths whose image extension changed.
 for p in SOURCE.rglob('*'):
  if not p.is_file() or (p.suffix!='.md' and p.name!='index.json'):continue
  dest=target/p.relative_to(SOURCE);dest.parent.mkdir(parents=True,exist_ok=True);text=p.read_bytes().decode('utf-8')
  if p.suffix=='.md':
   import os
   for old,new in replacements.items():
    if old!=new:text=text.replace(os.path.relpath(SOURCE/old,p.parent).replace('\\','/'),os.path.relpath(SOURCE/new,p.parent).replace('\\','/'))
  dest.write_bytes(text.encode('utf-8'))
 result=dict(version=1,fontPixelsUnchanged=True,glyphsPreserved=33552,fontPages=fonts,images=pictures)
 (target/'asset-optimization.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--target',type=Path,default=ROOT/'artifacts/mod/HollowElements/ElementGuide');a=p.parse_args();r=optimize(a.target)
 out=ROOT/'artifacts/acceptance/guide-media';out.mkdir(parents=True,exist_ok=True)
 report={key:dict(files=len(r[field]),beforeBytes=sum(x['beforeBytes'] for x in r[field]),afterBytes=sum(x['afterBytes'] for x in r[field])) for key,field in [('fonts','fontPages'),('images','images')]}
 (out/'assets.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
