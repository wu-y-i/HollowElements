"""Crop the user's recording overlay at the right edge, resize and import faithful screenshots."""
import hashlib,io,json,re,shutil
from datetime import datetime
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
CONTENT=ROOT/'workspaces/mod/content/ElementGuide'
SOURCE=Path('E:/User/Downloads/images')

def main():
    backup=ROOT/('local/backups/element_guide-images-'+datetime.now().strftime('%Y%m%d-%H%M%S'))
    shutil.copytree(CONTENT,backup)
    rows=[]
    for path in sorted(SOURCE.glob('*.png')):
        folder='images/bosses' if path.stem.isdigit() else 'images'
        target=CONTENT/folder/(path.stem+'.jpg')
        with Image.open(path) as original:
            assert original.size==(1920,1080),path
            cropped=original.convert('RGB').crop((0,0,1880,1080))
            final=cropped.resize((1280,round(cropped.height*1280/cropped.width)),Image.Resampling.LANCZOS)
            for quality in range(92,69,-2):
                data=io.BytesIO();final.save(data,format='JPEG',quality=quality,subsampling=0,optimize=True)
                if len(data.getvalue())<1_000_000:break
            else:raise ValueError('Cannot meet image size limit: '+path.name)
            target.parent.mkdir(exist_ok=True);target.write_bytes(data.getvalue())
            rows.append(dict(source=path.name,target=target.relative_to(CONTENT).as_posix(),sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),crop=[0,0,1880,1080],size=final.size,quality=quality,bytes=target.stat().st_size))
        if path.stem.isdigit():
            page=CONTENT/'bosses'/(path.stem+'.md');text=page.read_text(encoding='utf-8')
            old='../images/bosses/'+path.stem+'.png';new='../images/bosses/'+path.stem+'.jpg'
            if old in text:text=text.replace(old,new)
            elif new not in text:
                lines=text.splitlines();bullets=[i for i,line in enumerate(lines) if line.startswith('- ')]
                position=bullets[min(1,len(bullets)-1)]+1 if bullets else 1
                title=lines[0].lstrip('# ').strip()
                lines[position:position]=['',f'![实机：{title}]({new})','']
                text='\n'.join(lines)+'\n'
            page.write_text(text,encoding='utf-8')
        elif path.stem not in ['freeze_01','freeze_02']:
            page=CONTENT/'reactions'/(path.stem+'.md');text=page.read_text(encoding='utf-8')
            old='../images/'+path.stem+'.png';assert old in text or '../images/'+path.stem+'.jpg' in text,page
            page.write_text(text.replace(old,'../images/'+path.stem+'.jpg'),encoding='utf-8')
    page=CONTENT/'reactions/freeze.md';text=page.read_text(encoding='utf-8')
    text=re.sub(r'^!\[[^\]]*\]\(\.\./images/freeze\.png\)\n?','',text,flags=re.M)
    if '../images/freeze_01.jpg' not in text:
        lines=text.splitlines();output=[]
        for line in lines:
            output.append(line)
            if line.startswith('- 可被击退的目标'):output+=['','![硬冻结：完整冰块](../images/freeze_01.jpg)','']
            if line.startswith('- 不能被击退的目标'):output+=['','![结霜：减速状态](../images/freeze_02.jpg)','']
        text='\n'.join(output)+'\n'
    page.write_text(text,encoding='utf-8')
    book=json.loads((CONTENT/'index.json').read_text(encoding='utf-8'))
    pages=[CONTENT/e['File'] for c in book['Categories'] for e in c['Entries']]
    referenced=set()
    for page in pages:
        for value in re.findall(r'!\[[^\]]*\]\(([^)]+)\)',page.read_text(encoding='utf-8')):
            image=(page.parent/value).resolve();assert image.is_file() and image.is_relative_to(CONTENT.resolve()),image
            referenced.add(image)
    # Keep JSON-defined covers/icons too, if the editable catalog later uses them.
    def strings(value):
        if isinstance(value,str):yield value
        elif isinstance(value,dict):
            for v in value.values():yield from strings(v)
        elif isinstance(value,list):
            for v in value:yield from strings(v)
    for value in strings(book):
        if Path(value).suffix.lower() in ['.png','.jpg','.jpeg']:
            candidate=(CONTENT/value).resolve()
            if candidate.is_file():referenced.add(candidate)
    removed=[];images=(CONTENT/'images').resolve()
    for path in images.rglob('*'):
        if path.is_file() and path.suffix.lower() in ['.png','.jpg','.jpeg'] and path.resolve() not in referenced:
            assert path.resolve().is_relative_to(images)
            removed.append(path.relative_to(CONTENT).as_posix());path.unlink()
    assert all((CONTENT/r['target']).is_file() for r in rows)
    report=dict(passed=True,backup=str(backup),imported=rows,removed=removed,referencedImages=len(referenced),maxBytes=max(r['bytes'] for r in rows))
    (ROOT/'artifacts/acceptance/ui-balance/images.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(imported=len(rows),removed=len(removed),referencedImages=len(referenced),maxBytes=report['maxBytes']),indent=2))

if __name__=='__main__':main()
