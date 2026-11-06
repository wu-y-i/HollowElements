"""Rebuild release PNGs from Unity PackTextures layouts; preserve original artwork."""
import json,argparse
from pathlib import Path
from PIL import Image
from optimize_runtime_textures import resized
ROOT=Path(__file__).resolve().parents[3]
def main():
 p=argparse.ArgumentParser();p.add_argument('--staging',type=Path,default=ROOT/'artifacts/mod/HollowElements');args=p.parse_args();stage=args.staging.resolve();assert stage.is_relative_to(ROOT)
 rows=[]
 for group,source in [('Charms',ROOT/'workspaces/art/source/charms'),('Icons',ROOT/'workspaces/art/source/icons/elements')]:
  layout=json.loads((ROOT/'workspaces/art/source/ui-atlas'/(group+'.json')).read_text(encoding='utf-8-sig'));folder=stage/'Assets'/group;folder.mkdir(parents=True,exist_ok=True)
  old=sum(p.stat().st_size for p in folder.glob('*.png'));atlas=Image.new('RGBA',(layout['width'],layout['height']))
  for name,r in layout['sprites'].items():
   image=resized(Image.open(source/(name+'.png')),(r['width'],r['height']));atlas.paste(image,(r['x'],atlas.height-r['y']-r['height']))
  atlas.save(folder/'atlas.png',optimize=True,compress_level=9)
  (folder/'atlas.json').write_text(json.dumps(layout,indent=2),encoding='utf-8')
  # Only derived staging files; never delete original art.
  for name in layout['sprites']:
   path=folder/(name+'.png')
   if path.exists():path.unlink()
  rows.append(dict(group=group,count=len(layout['sprites']),width=atlas.width,height=atlas.height,iconSizes=sorted(set(r['width'] for r in layout['sprites'].values())),beforeBytes=old,afterBytes=(folder/'atlas.png').stat().st_size))
 out=ROOT/'artifacts/acceptance/cyclone-shell-atlas';out.mkdir(parents=True,exist_ok=True)
 (out/'textures.json').write_text(json.dumps(dict(packer='Unity Texture2D.PackTextures',sourceArtworkUntouched=True,groups=rows),indent=2),encoding='utf-8');print(json.dumps(rows))
if __name__=='__main__':main()
