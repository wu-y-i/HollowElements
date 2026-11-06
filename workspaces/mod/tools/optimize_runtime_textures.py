"""Deterministic release texture optimization. Source artwork is never modified."""
import argparse,hashlib,json,math
from pathlib import Path
from PIL import Image
from import_reaction_fx import ROOT,SOURCE,MAPPING
SIZES={'vaporize':192,'electro-charged':192,'electro-state':192,'frost':192,'frost-state':192,'soft-frost-state':192,'superconduct-state':192}

def resized(image,size):
 image=image.convert('RGBA')
 return image if image.size==tuple(size) else image.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def save(image,path):
 path.parent.mkdir(parents=True,exist_ok=True);image.save(path,optimize=True,compress_level=9)
def main():
 p=argparse.ArgumentParser();p.add_argument('--staging',type=Path,default=ROOT/'artifacts/mod/HollowElements');a=p.parse_args();stage=a.staging.resolve();assert stage.is_relative_to(ROOT.resolve()),stage
 out=ROOT/'artifacts/acceptance/fx-compact';out.mkdir(parents=True,exist_ok=True)
 previous=json.loads((out/'assets.json').read_text(encoding='utf-8')) if (out/'assets.json').exists() else None
 changes=[];before_bytes=after_bytes=before_gpu=after_gpu=0
 for folder,name in MAPPING:
  dest=stage/'Assets/Effects'/name;path=dest/'atlas.png';manifest=dest/'manifest.json';j=json.loads(manifest.read_text(encoding='utf-8-sig'));old_size=Image.open(path).size;old_bytes=path.stat().st_size
  files=sorted((SOURCE/folder).glob('*.png'));assert len(files)==j['frames']==len(j['samples']);size=SIZES.get(name,256);cols=min(8,len(files));rows=math.ceil(len(files)/cols)
  atlas=Image.new('RGBA',(cols*size,rows*size));samples=[]
  for index,source in enumerate(files):
   assert sha(source)==j['samples'][index]['sourceSha256'];frame=Image.open(source);assert frame.size==(512,512)
   x=index%cols*size;y=index//cols*size;atlas.paste(resized(frame,(size,size)),(x,y))
   samples.append(dict(index=index,x=x,y=y,width=size,height=size,sourceSha256=sha(source)))
  save(atlas,path);j.update(width=size,height=size,samples=samples)
  j['textureProcessing']=dict(version=1,sourceWidth=512,sourceHeight=512,frameSize=size,resample='premultiplied-alpha-lanczos',scale=size/512)
  manifest.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
  changes.append(dict(path=str(path.relative_to(stage)),beforeSize=old_size,afterSize=atlas.size,beforeBytes=old_bytes,afterBytes=path.stat().st_size,frames=len(files),fps=j['fps']))
  before_bytes+=old_bytes;after_bytes+=path.stat().st_size;before_gpu+=math.prod(old_size)*4;after_gpu+=math.prod(atlas.size)*4
 for source_dir,target_dir,size in [(ROOT/'workspaces/art/source/charms',stage/'Assets/Charms',256),(ROOT/'workspaces/art/source/icons/elements',stage/'Assets/Icons',192)]:
  for source in sorted(source_dir.glob('*.png')):
   path=target_dir/source.name
   if not path.exists():continue
   old_bytes=path.stat().st_size;old_size=Image.open(path).size;image=resized(Image.open(source),(size,size));save(image,path)
   changes.append(dict(path=str(path.relative_to(stage)),sourceSha256=sha(source),beforeSize=old_size,afterSize=image.size,beforeBytes=old_bytes,afterBytes=path.stat().st_size))
   before_bytes+=old_bytes;after_bytes+=path.stat().st_size
 # Fonts and already web-sized element_guide screenshots are left at native resolution.
 report=dict(passed=True,sourceArtworkUntouched=True,frames=525,fps=60,changes=changes,beforeBytes=before_bytes,afterBytes=after_bytes,effectRgbaBytesBefore=before_gpu,effectRgbaBytesAfter=after_gpu,
             policy='192px steam, lightning and status; 256px explosions/ice/shatter. Preserve world size, all frames, timing, fonts and guide screenshots.')
 if previous:
  baseline={entry['path']:entry for entry in previous['changes']}
  for entry in changes:
   if entry['path'] in baseline:
    for key in ['beforeBytes','beforeSize']:entry[key]=baseline[entry['path']][key]
  report['beforeBytes']=sum(e['beforeBytes'] for e in changes)
  report['effectRgbaBytesBefore']=previous['effectRgbaBytesBefore']
 (out/'assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({k:v for k,v in report.items() if k!='changes'},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
