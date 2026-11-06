"""Synchronize the editable book, including removal of orphaned image files."""
import shutil,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SOURCE=ROOT/'workspaces/mod/content/ElementGuide'
def sync(target):
    target=Path(target).resolve();assert target.name=='ElementGuide' and target.parent.name=='HollowElements' and target!=SOURCE.resolve()
    expected={p.relative_to(SOURCE) for p in SOURCE.rglob('*') if p.is_file()}
    for relative in expected:
        destination=target/relative;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(SOURCE/relative,destination)
    removed=0;images=target/'images'
    for p in images.rglob('*'):
        if p.is_file() and p.relative_to(target) not in expected:
            assert p.resolve().is_relative_to(images.resolve());p.unlink();removed+=1
    print('Synced',len(expected),'files; removed',removed,'old images:',target)
if __name__=='__main__':
    for value in sys.argv[1:]:sync(value)
