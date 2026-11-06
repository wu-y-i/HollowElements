"""Actual 2160x1440 in-game texture A/B screenshots; immutable original frames."""
import json,hashlib,time,math
import numpy as np
from pathlib import Path
from PIL import Image
from runtime_acceptance import command,ROOT,SESSION
from import_reaction_fx import MAPPING,SOURCE
OUT=ROOT/'artifacts/acceptance/texture-optimization';PREVIEW=SESSION/'texture-preview';PREVIEW.mkdir(parents=True,exist_ok=True)

def capture(path):
 old=(SESSION/'game.png').stat().st_mtime_ns
 command('capture');time.sleep(.35);assert (SESSION/'game.png').stat().st_mtime_ns>old
 im=Image.open(SESSION/'game.png');assert im.size==(2160,1440),im.size
 path.write_bytes((SESSION/'game.png').read_bytes())
def main():
 command('resolution',width=2160,height=1440);time.sleep(1);command('pause',value=True);rows=[]
 for folder,name in MAPPING:
  files=sorted((SOURCE/folder).glob('*.png'));values=[]
  for path in files:
   with Image.open(path) as image:values.append(float(np.asarray(image.getchannel('A'),dtype=np.float32).sum()))
  index=int(np.argmax(values));before=Image.open(files[index]).convert('RGBA');manifest=json.loads((ROOT/'artifacts/mod/HollowElements/Assets/Effects'/name/'manifest.json').read_text(encoding='utf-8'))
  sample=manifest['samples'][index]
  with Image.open(ROOT/'artifacts/mod/HollowElements/Assets/Effects'/name/'atlas.png') as atlas:after=atlas.crop((sample['x'],sample['y'],sample['x']+sample['width'],sample['y']+sample['height'])).convert('RGBA')
  before.save(PREVIEW/'before.png');after.save(PREVIEW/'after.png');draw=command('textureCompare',visible=True);assert draw['screenWidth']==2160 and draw['screenHeight']==1440,draw
  capture(OUT/(name+'-2160x1440.png'))
  # Compare premultiplied pixels at the actual enlarged presentation size, black background.
  size=(round(draw['drawPixels']),)*2
  def shown(image):
   rgba=np.asarray(image.resize(size,Image.Resampling.BILINEAR),dtype=np.float32)/255;return rgba[:,:,:3]*rgba[:,:,3:]
  a,b=shown(before),shown(after);rmse=float(np.sqrt(np.mean((a-b)**2)));rows.append(dict(effect=name,index=index,original=list(before.size),release=list(after.size),draw=draw,rmse=rmse,psnr=None if rmse==0 else -20*math.log10(rmse)))
  print(name,before.size,after.size,round(draw['drawPixels']),round(rmse*255,3),flush=True)
 command('textureCompare',visible=False);command('pause',value=False)
 result=dict(passed=True,resolution=[2160,1440],framesAndTimingPreserved=True,comparisons=rows,dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest())
 (OUT/'quality.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
if __name__=='__main__':main()
