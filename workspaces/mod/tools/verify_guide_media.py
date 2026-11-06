"""Validate optimized guide files against unchanged authoring masters."""
import hashlib,json,os
from pathlib import Path
from PIL import Image
import io
ROOT=Path(__file__).resolve().parents[3];SOURCE=ROOT/'workspaces/mod/content/ElementGuide'
def verify_media(read,names):
 manifest=json.loads(read('asset-optimization.json'));assert manifest['version']==1 and len(manifest['fontPages'])==32 and len(manifest['images'])==35
 font={r['path']:r for r in manifest['fontPages']};pictures={r['source']:r for r in manifest['images']};expected={'asset-optimization.json'}
 for p in SOURCE.rglob('*'):
  if not p.is_file():continue
  name=p.relative_to(SOURCE).as_posix();raw=p.read_bytes();release=name
  if name in pictures:
   r=pictures[name];release=r['release'];data=read(release);im=Image.open(io.BytesIO(data));assert list(im.size)==r['afterSize'] and max(im.size)<=960
   assert hashlib.sha256(raw).hexdigest()==r['sourceSha256'] and hashlib.sha256(data).hexdigest()==r['releaseSha256']
   assert im.format==r['format']
  elif name in font:
   data=read(name);r=font[name];assert hashlib.sha256(raw).hexdigest()==r['sourceSha256'] and hashlib.sha256(data).hexdigest()==r['releaseSha256']
   assert Image.open(io.BytesIO(data)).convert('RGBA').tobytes()==Image.open(io.BytesIO(raw)).convert('RGBA').tobytes()
  elif p.suffix=='.md':
   text=raw.decode('utf-8')
   for old,r in pictures.items():
    new=r['release']
    if old!=new:text=text.replace(os.path.relpath(SOURCE/old,p.parent).replace('\\','/'),os.path.relpath(SOURCE/new,p.parent).replace('\\','/'))
   assert read(name)==text.encode('utf-8'),name
  else:assert read(name)==raw,name
  expected.add(release)
 assert set(names)==expected,(set(names)-expected,expected-set(names))
 return dict(passed=True,fontPages=32,glyphs=33552,alphaLossless=True,images=35)
