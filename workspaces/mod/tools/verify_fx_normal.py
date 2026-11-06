"""Record clean startup, final installed resources and unchanged user saves."""
import argparse,hashlib,json,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser()
parser.add_argument('--backup',default='development-20261023-131746')
parser.add_argument('--prefix',default='fx-v2')
parser.add_argument('--version',default='0.1.3')
args=parser.parse_args()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
game=Path(json.loads((ROOT/'local/paths.json').read_text())['gameDirectory'])
installed=game/'hollow_knight_Data/Managed/Mods/HollowElements'
stage=ROOT/'artifacts/mod/HollowElements'
saves=Path(os.environ['USERPROFILE'])/'AppData/LocalLow/Team Cherry/Hollow Knight'
backup=ROOT/'local/backups'/args.backup/'saves'
assert not (installed/'development-session.json').exists()
assert not (stage/'development-session.json').exists()
compared=[]
for source in stage.rglob('*'):
    if source.is_file():
        relative=source.relative_to(stage)
        assert sha(source)==sha(installed/relative),relative
        compared.append(str(relative))
before={p.name:sha(p) for p in backup.glob('user*') if p.is_file()}
after={p.name:sha(p) for p in saves.glob('user*') if p.is_file()}
assert before==after,'User save files changed'
settings=json.loads((saves/'HollowElementsMod.GlobalSettings.json').read_text(encoding='utf-8-sig'))
assert settings['DebugEnabled'] is False
log=(saves/'ModLog.txt').read_text(encoding='utf-8-sig')
assert 'HollowElements : '+args.version in log and 'Modding API: 1.5.78.11833-77' in log
assert '四元素运行层已加载' in log and '资源加载失败' not in log
assert '[ERROR]' not in log,log
pid=int((ROOT/('local/game-session/'+args.prefix+'-normal.pid')).read_text().strip())
assert json.loads((ROOT/'local/game-session/connected.json').read_text(encoding='utf-8-sig'))['pid']!=pid
report=dict(passed=True,pid=pid,dllSha256=sha(stage/'HollowElements.dll'),installedFilesCompared=len(compared),unchangedSaveFiles=len(before),debugEnabled=False,developmentMarker=False,log=log)
(ROOT/('artifacts/acceptance/'+args.prefix+'-install.json' if args.prefix!='fx-v2' else 'artifacts/acceptance/fx-v2-normal.json')).write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='log'},indent=2))
