"""Xero phase activation: check real sprite bindings and native sword collisions."""
import hashlib
import json
import time
from runtime_acceptance import command, ROOT, SESSION, LOG
from boss_design_acceptance import enter, enemy, park
from incoming_attack_acceptance import catch, tests as contacts

OUT = ROOT / 'artifacts/acceptance/xero'


def inspect_swords(expected):
    shots = {p['name']: p for p in command('projectiles') if p['active']}
    assert {name: p['element'] for name, p in shots.items()} == expected, shots
    tints = {p['name']: p for p in command('tintDiagnostics')['tints'] if p['active']}
    for name, element in expected.items():
        tint = tints[name]
        assert shots[name]['tinted'] and tint['Element'] == element, tint
        # A tint component with zero bound sprites is the original regression.
        assert len(tint['sprites']) == 1 and tint['maxColourError'] < .001, tint
        assert tint['BindCount'] == 1 and tint['ancestorTints'] == 1, tint
        assert tint['sprites'][0]['actual'] != tint['sprites'][0]['original'], tint
    return dict(projectiles=shots, tints=tints)


def main():
    OUT.mkdir(exist_ok=True)
    command('load', slot=3)
    for _ in range(100):
        # Wait for the saved game's transition to finish before requesting GG.
        if command('element_guide')['canOpen']:
            break
        time.sleep(.2)
    command('ready')
    results = []
    for scene in ['GG_Ghost_Xero', 'GG_Ghost_Xero_V']:
        command('pause', value=False)
        boss = enemy(enter(scene), 'Ghost Warrior Xero')
        command('pause', value=True)
        first = inspect_swords({'Sword 1': 4, 'Sword 2': 3})
        command('enemyHp', targetId=boss['id'], value=100)
        command('enemyVulnerable', targetId=boss['id'])
        # Native HealthManager.Hit delivers TOOK DAMAGE and the original half-HP
        # summon logic. Do not directly force Attacking/Summon as the older test did.
        command('hit', targetId=boss['id'], source='Nail', element='None', damage=1)
        command('pause', value=False)
        for _ in range(100):
            shots = [p for p in command('projectiles') if p['active']]
            if len(shots) == 4:
                break
            time.sleep(.1)
        time.sleep(.1)
        command('pause', value=True)
        second = inspect_swords({'Sword 1': 4, 'Sword 2': 3, 'Sword 3': 2, 'Sword 4': 1})
        command('capture'); time.sleep(.3)
        (OUT / (scene + '-fixed.png')).write_bytes((SESSION / 'game.png').read_bytes())
        for name, element in [('Sword 1', 4), ('Sword 2', 3), ('Sword 3', 2), ('Sword 4', 1)]:
            command('pause', value=False); command('invincible', value=True)
            park(); command('pause', value=True)
            catch(second['projectiles'][name]['id'], element,
                  scene + ' ' + name + ' native collision aura', path=name, settle=False)
        results.append(dict(scene=scene, phaseOne=first, phaseTwo=second))
        print('PASS', scene, 'four swords bound, coloured, no repeated bind; four real collision auras', flush=True)
    command('pause', value=False); command('debugMode', value=False)
    report = dict(passed=True, version='0.1.6.2',
                  dllSha256=hashlib.sha256((ROOT / 'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(),
                  scenes=results, contacts=contacts, commands=LOG)
    (OUT / 'acceptance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    command('quit')


if __name__ == '__main__':
    main()
