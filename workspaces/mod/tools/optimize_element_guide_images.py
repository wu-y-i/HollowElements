"""Bring retained screenshots under the same 1 MB limit, preserving their full frame."""
import io,json,os
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
CONTENT=ROOT/'workspaces/mod/content/ElementGuide'
def main():
    report_path=ROOT/'artifacts/acceptance/ui-balance/images.json'
    report=json.loads(report_path.read_text(encoding='utf-8'));changed=[]
    for path in sorted((CONTENT/'images').rglob('*.png')):
        if path.stat().st_size<1_000_000:continue
        with Image.open(path) as image:
            image=image.convert('RGB');image.thumbnail((1280,1280),Image.Resampling.LANCZOS)
            for quality in range(92,69,-2):
                buffer=io.BytesIO();image.save(buffer,format='JPEG',quality=quality,subsampling=0,optimize=True)
                if len(buffer.getvalue())<1_000_000:break
            else:raise ValueError(path)
            target=path.with_suffix('.jpg');assert not target.exists();target.write_bytes(buffer.getvalue())
        for page in CONTENT.rglob('*.md'):
            text=page.read_text(encoding='utf-8');old=os.path.relpath(path,page.parent).replace('\\','/');new=os.path.relpath(target,page.parent).replace('\\','/')
            if old in text:page.write_text(text.replace(old,new),encoding='utf-8')
        assert path.resolve().is_relative_to((CONTENT/'images').resolve());path.unlink()
        changed.append(dict(target=target.relative_to(CONTENT).as_posix(),bytes=target.stat().st_size))
    images=[p for p in (CONTENT/'images').rglob('*') if p.is_file()]
    assert all(p.stat().st_size<1_000_000 for p in images)
    report.update(optimizedRetained=changed,allImagesBelow1MB=True,allImagesMaxBytes=max(p.stat().st_size for p in images))
    report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('Optimized',len(changed),'retained screenshots; all',len(images),'images below 1 MB')
if __name__=='__main__':main()
