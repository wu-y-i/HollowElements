"""Focused UI regression through the opt-in, save-suppressed game bridge.

Run after real pointer/wheel checks; does not synthesize combat or player input.
"""
import hashlib
import json
import time

from runtime_acceptance import ROOT, SESSION, command

OUT = ROOT / 'artifacts/acceptance/ui-v1'


def guide(**values):
    return command('element_guide', **values)


def capture(name):
    command('capture')
    time.sleep(.25)
    (OUT / (name + '.png')).write_bytes((SESSION / 'game.png').read_bytes())


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    guide(action='open')
    time.sleep(.8)
    for _ in range(5):
        if guide()['visible']:
            break
        guide(action='move', direction=1)
        time.sleep(.6)
    state = guide()
    assert state['visible'] and state['cursorVisible'] and state['timeScale'] == 0, state
    catalog = json.loads((ROOT / 'workspaces/mod/content/ElementGuide/index.json').read_text(encoding='utf-8-sig'))
    ids = [e['Id'] for c in catalog['Categories'] for e in c['Entries']]
    position = {'loadout': -2, 'charms': -1}.get(state['selected'])
    if position is None:
        position = ids.index(state['selected'])
    guide(action='navigate', direction=-position)
    pages = []
    for index, identifier in enumerate(ids):
        state = guide(action='navigate', direction=0 if index == 0 else 1, submit=True)
        assert state['selected'] == identifier and state['visible'] and state['cursorVisible'], state
        assert state['error'] == '' and state['text'] and not state['missingCharacters'], state
        assert not any('[图片未加载]' in t or '[未知参数' in t for t in state['text']), identifier
        pages.append(identifier)
    guide(action='navigate', direction=-(len(ids) - 1), submit=True)
    guide(action='scroll', pixels=99999)
    state = guide()
    assert 0 < state['scroll'] <= state['bodyHeight'] - state['viewportHeight'] + 1, state
    capture('guide-scrolled')
    guide(action='navigate', direction=-2, submit=True)
    assert guide()['selected'] == 'loadout'
    guide(action='navigate', direction=1, submit=True)
    assert guide()['selected'] == 'charms'
    guide(action='navigate', direction=1, submit=True)

    carousel = []
    for _ in range(5):
        guide(action='move', direction=1)
        time.sleep(.6)
        state = guide()
        assert state['timeScale'] == (0 if state['visible'] else 1), state
        carousel.append(dict(visible=state['visible'], timeScale=state['timeScale']))
    assert sum(row['visible'] for row in carousel) == 1, carousel
    guide(action='close')
    time.sleep(.7)

    command('playerAura', element='Hydro')
    layers = command('uiLayers')
    hud = next(c for c in layers['canvases'] if c['path'].endswith('/HollowElements.HUD'))
    native = next(r for r in layers['hudRenderers'] if r['path'].endswith('/HUD_frame'))
    assert hud['renderMode'] == 1 and hud['camera'] == 'HudCamera', hud
    assert (hud['sortingLayerID'], hud['sortingOrder']) == (native['sortingLayerID'], native['sortingOrder'])
    capture('hud')
    command('nativePause')
    time.sleep(.8)
    capture('pause')
    command('settingsMenu', action='open')
    time.sleep(.8)
    settings = command('settingsMenu')
    # Reopening reuses the previous scroll offset. Reset through directional
    # events: ordinary programmatic/pointer selection deliberately cannot scroll.
    settings = command('settingsMenu', action='select', index=len(settings['options']) - 1)
    for _ in range(len(settings['options']) - 1):
        settings = command('settingsMenu', action='move', direction=-1)
    assert settings['scrolls'][0]['y'] < 1, settings
    moves = []
    for _ in range(len(settings['options']) - 1):
        settings = command('settingsMenu', action='move', direction=1)
        moves.append(settings['scrolls'][0]['y'])
    assert moves[-1] > 0, moves
    capture('settings-keyboard-end')
    for _ in range(len(settings['options']) - 1):
        settings = command('settingsMenu', action='move', direction=-1)
    assert settings['scrolls'][0]['y'] < 1, settings
    result = dict(passed=True, pages=pages, carousel=carousel, layers=layers,
                  settingsNavigation=dict(down=moves, returnedY=settings['scrolls'][0]['y']),
                  dllSha256=hashlib.sha256((ROOT / 'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest())
    (OUT / 'runtime.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PASS 51 articles, both configuration pages, native carousel, HUD layer, pause, settings directional scrolling')


if __name__ == '__main__':
    main()
