"""HUD projection, inventory occlusion, resolution and unload/reload checks.

Run after ui_menu_acceptance.py, while its settings screen is paused.
"""
import hashlib
import json
import time

from runtime_acceptance import ROOT, SESSION, command
from ui_menu_acceptance import OUT, capture, guide


def main():
    command('nativePause')
    time.sleep(.8)
    command('dummy', recoilable=True, offset=3)
    command('enemyAura', element='Hydro', units=1)
    command('playerAura', element='Hydro')
    time.sleep(.15)
    capture('combat-hud')
    ui = command('fixDiagnostics')['ui']
    assert any(bar['visible'] and bar['statuses'] for bar in ui['bars']), ui
    command('nativePause')
    time.sleep(.7)
    capture('combat-hud-pause')
    command('nativePause')
    time.sleep(.7)
    resolutions = []
    for width, height in [(1024, 768), (2560, 1080), (1280, 720)]:
        command('resolution', width=width, height=height)
        time.sleep(.7)
        ui = command('fixDiagnostics')['ui']
        for point in ui['playerCorners']:
            assert -1 <= point['x'] <= ui['screen']['width'] + 1, ui
            assert -1 <= point['y'] <= ui['screen']['height'] + 1, ui
        guide(action='open')
        time.sleep(.7)
        state = guide()
        assert state['visible'] and state['cursorVisible'] and state['panelWidth'] > 600, state
        capture(f'guide-{width}x{height}')
        resolutions.append(dict(screen=ui['screen'], corners=ui['playerCorners'], panelWidth=state['panelWidth']))
        guide(action='close')
        time.sleep(.6)
    guide(action='open')
    time.sleep(.7)
    audit_path = SESSION / 'unload-audit.json'
    previous = audit_path.stat().st_mtime_ns if audit_path.exists() else 0
    command('unloadAudit', reload=True)
    for _ in range(100):
        if audit_path.exists() and audit_path.stat().st_mtime_ns != previous:
            break
        time.sleep(.1)
    audit = json.loads(audit_path.read_text(encoding='utf-8-sig'))
    assert audit['passed'] and audit['remainingHud'] == 0, audit
    for _ in range(50):
        state = guide()
        if state['attached']:
            break
        time.sleep(.2)
    assert state['attached'] and not state['visible'] and state['timeScale'] == 1, state
    guide(action='open')
    time.sleep(.7)
    assert guide()['visible'] and guide()['cursorVisible']
    guide(action='close')
    time.sleep(.7)
    closed = guide()
    assert not closed['visible'] and not closed['cursorVisible'] and closed['timeScale'] == 1, closed
    guide(action='open')
    time.sleep(.7)
    result = dict(passed=True, resolutions=resolutions, unload=audit, cursorRestored=True,
                  dllSha256=hashlib.sha256((ROOT / 'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest())
    (OUT / 'lifecycle.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PASS enemy/player HUD, pause/inventory, 3 resolutions, both canvases unload, cursor restore and reopen')


if __name__ == '__main__':
    main()
