"""Native element_guide open latency, bounded caching and external-file invalidation.

Run only in the backed-up, save-suppressed development session. The edit checks
temporarily change installed ElementGuide files and restore their exact original bytes.
"""
import hashlib
import json
import time
from pathlib import Path
from runtime_acceptance import command, ROOT
from balance_acceptance import room
from element_guide_update_acceptance import open_page

OUT = ROOT / 'artifacts/acceptance/element_guide-performance'
BOOK = Path('E:/SteamLibrary/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Mods/HollowElements/ElementGuide')


def close():
    command('element_guide', action='close')
    time.sleep(.7)
    assert not command('element_guide')['visible']


def select(entry_id):
    index = json.loads((BOOK / 'index.json').read_text(encoding='utf-8'))
    ids = ['loadout','charms'] + [e['Id'] for c in index['Categories'] for e in c['Entries']]
    current = command('element_guide')['selected']
    command('element_guide', action='navigate', direction=ids.index(entry_id) - ids.index(current))
    return command('element_guide')


def opens():
    if not command('status')['hero']:
        command('load', slot=3)
        for _ in range(120):
            time.sleep(.2)
            if command('element_guide')['canOpen']:
                break
    room(); command('ready')
    rows = []
    for n in range(4):
        command('profileStart'); start = time.perf_counter()
        command('element_guide', action='open')
        for _ in range(50):
            r = command('element_guide')
            if r['visible']:
                break
            time.sleep(.05)
        elapsed = (time.perf_counter() - start) * 1000
        assert r['visible'] and not r['error'], r
        time.sleep(.4)
        rows.append(dict(open=n, wallMs=elapsed, frames=command('profileStop'),
                         selected=r['selected'], images=r['images'], performance=r['performance']))
        close()
    first = rows[0]['performance']
    for row in rows[1:]:
        p = row['performance']
        assert (p['builds'], p['renders'], p['imageDecodes']) == (first['builds'], first['renders'], first['imageDecodes']), rows
    report = dict(passed=True, dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(), opens=rows)
    (OUT/'optimized.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2), flush=True)


def edits():
    open_page(); select('start')
    result = []
    def edited(path, transform, check):
        original = path.read_bytes()
        try:
            close(); path.write_bytes(transform(original)); open_page()
            r = command('element_guide'); check(r)
            result.append(dict(file=path.relative_to(BOOK).as_posix(), error=r['error'], performance=r['performance']))
        finally:
            path.write_bytes(original)
            close(); open_page()

    def contains(marker):
        return lambda r: require(any(marker in t for t in r['text']), r)
    edited(BOOK/'basics/start.md', lambda b: b + '\n\n缓存刷新验收。\n'.encode(), contains('缓存刷新验收'))
    edited(BOOK/'index.json', lambda b: b.replace('元素系统'.encode(), '图鉴缓存测试'.encode(), 1), lambda r: require(r['title']=='图鉴缓存测试', r))
    # Failed author reload must retain the last complete valid book.
    edited(BOOK/'index.json', lambda b: b'{broken', lambda r: require(bool(r['error']) and r['entries']==51, r))
    assert not command('element_guide')['error']
    select('vaporize'); first = command('element_guide')['performance']['imageDecodes']
    select('start'); select('vaporize')
    assert command('element_guide')['performance']['imageDecodes']==first
    edited(BOOK/'images/vaporize.jpg', lambda b: b + b'\n', lambda r: require(r['performance']['imageDecodes']>first and r['images']==1, r))
    # Explicit reload handles replacements preserving file metadata too.
    before = command('element_guide')['performance']['imageDecodes']
    command('element_guide', action='reload')
    assert command('element_guide')['performance']['imageDecodes']>before
    command('resolution', width=1024, height=768); time.sleep(.8)
    r=command('element_guide'); assert not r['error'] and r['bodyHeight']>r['viewportHeight'], r
    command('element_guide', action='scroll', pixels=99999); r=command('element_guide')
    assert abs(r['scroll']-max(0,r['bodyHeight']-r['viewportHeight']))<1, r
    command('resolution', width=1280, height=720); time.sleep(.8)
    close()
    (OUT/'edits.json').write_text(json.dumps(dict(passed=True, checks=result, resizeAndScroll=True), ensure_ascii=False, indent=2), encoding='utf-8')
    print('PASS external Markdown/index/picture edits, invalid-file fallback, image reuse, explicit reload, resize and scroll', flush=True)


def require(value, evidence):
    assert value, evidence


if __name__=='__main__':
    import sys
    OUT.mkdir(parents=True, exist_ok=True)
    {'opens': opens, 'edits': edits}[sys.argv[1]]()
