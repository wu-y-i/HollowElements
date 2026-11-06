"""Bake the supplied font's BMP glyphs into lazy-loaded TMP SDF fallback pages."""
from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'local/tools/font-python'))
import numpy as np
from scipy.ndimage import distance_transform_edt
from fontTools.ttLib import TTFont
from PIL import Image,ImageDraw,ImageFont
FONT=ROOT/'workspaces/art/source/fonts/zh-cn.ttf';OUT=ROOT/'workspaces/mod/content/ElementGuide/Font'
SIZE=48;PAD=8;ATLAS=2048
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 font=ImageFont.truetype(str(FONT),SIZE);ascent,descent=font.getmetrics()
 available=set(i for i in TTFont(str(FONT)).getBestCmap() if 32<=i<=65535 and not 0xd800<=i<=0xdfff)
 priority=set(range(32,127))
 for folder,pattern in [(ROOT/'workspaces/mod/content/ElementGuide','*.md'),(ROOT/'workspaces/mod/src','*.cs')]:
  for path in folder.rglob(pattern):
   if not any(part in ('bin','obj') for part in path.parts):priority.update(map(ord,path.read_text(encoding='utf-8-sig')))
 ids=sorted(available&priority)+sorted(available-priority)
 pages=[];glyphs=[];size=4096;atlas=Image.new('L',(size,size));x=y=0;row=0
 def finish():
  nonlocal atlas,glyphs,x,y,row,size
  filename=f'sdf-{len(pages):02}.png';image=Image.new('RGBA',atlas.size,'white');image.putalpha(atlas);image.save(OUT/filename)
  pages.append(dict(file=filename,glyphs=glyphs));print('SDF page',len(pages),'glyphs',len(glyphs),flush=True)
  size=ATLAS;atlas=Image.new('L',(size,size));glyphs=[];x=y=row=0
 for code in ids:
  ch=chr(code);left,top,right,bottom=font.getbbox(ch,anchor='ls');width=max(1,right-left);height=max(1,bottom-top);w=width+PAD*2;h=height+PAD*2
  if x+w>size:x=0;y+=row+2;row=0
  if y+h>size:finish()
  mask=Image.new('L',(w,h));ImageDraw.Draw(mask).text((PAD-left,PAD-top),ch,font=font,anchor='ls',fill=255)
  alpha=np.asarray(mask,dtype=np.float32)/255;inside=alpha>=.5
  if inside.any():
   distance=np.where(inside,distance_transform_edt(inside)-.5,-distance_transform_edt(~inside)+.5)
   sdf=np.clip(.5+distance/(PAD*2),0,1);sdf=(sdf*255).astype('uint8')
  else:sdf=np.zeros((h,w),dtype='uint8')
  atlas.paste(Image.fromarray(sdf),(x,y))
  glyphs.append(dict(id=code,x=x+PAD,y=y+PAD,width=width,height=height,xOffset=left,yOffset=-top,xAdvance=font.getlength(ch),scale=1))
  x+=w+2;row=max(row,h)
 if glyphs:finish()
 face=dict(Name='HollowElements.GenshinSDF',PointSize=SIZE,Scale=1,CharacterCount=len(ids),LineHeight=ascent+descent,Baseline=0,Ascender=ascent,CapHeight=SIZE,Descender=-descent,CenterLine=0,SuperscriptOffset=20,SubscriptOffset=-10,SubSize=.5,Underline=-4,UnderlineThickness=2,TabWidth=SIZE,Padding=PAD,AtlasWidth=ATLAS,AtlasHeight=ATLAS)
 (OUT/'font.json').write_text(json.dumps(dict(face=face,padding=PAD,pages=pages),ensure_ascii=False,separators=(',',':')),encoding='utf-8')
 for path in OUT.glob('sdf-*.png'):
  if path.name not in {p['file'] for p in pages} and path.resolve().parent==OUT.resolve():path.unlink()
 print('Baked',len(ids),'glyphs in',len(pages),'pages',flush=True)
if __name__=='__main__':main()
