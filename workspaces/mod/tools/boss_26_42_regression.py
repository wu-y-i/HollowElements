"""Run existing acceptance tools serially after the complete 26–42 implementation.

One bridge mailbox means these subprocesses must never run in parallel.
Every result and console log is preserved under this batch's evidence directory.
"""
import json
import os
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'artifacts/acceptance/boss-26-42/regression'

CASES = [
    ('runtime', 'runtime_acceptance.py', []),
    ('boss01-04', 'boss_design_acceptance.py', []),
    ('boss05-10', 'boss_05_10_acceptance.py', []),
    ('boss11-15', 'boss_11_15_acceptance.py', []),
] + [
    ('boss16-25-' + case, 'boss_16_25_acceptance.py', [case])
    for case in ['marmu', 'fluke', 'vessel', 'galien', 'sheo', 'hive', 'hu',
                 'collector', 'tamer', 'grimm', 'water', 'acid', 'lifecycle']
] + [
    ('incoming-' + case, 'incoming_attack_acceptance.py', [case])
    for case in ['gorb', 'warrior', 'dung', 'mawlek', 'xero', 'oblobble',
                 'master', 'tyrant', 'brothers', 'mantis', 'sisters']
] + [('attachment-config', 'attachment_config_acceptance.py', [])]


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    os.environ['HOLLOW_ELEMENTS_SKIP_CAPTURE']='1'
    results = []
    if '--resume' in sys.argv and (OUT/'results.json').exists():
        results=[r for r in json.loads((OUT/'results.json').read_text(encoding='utf8'))['cases'] if r['passed']]
    for name, script, args in CASES:
        if any(r['case']==name for r in results):continue
        print('RUN', name, flush=True)
        start = time.monotonic()
        try:
            run = subprocess.run([sys.executable, '-X', 'utf8', str(HERE / script), *args],
                                 cwd=ROOT, capture_output=True, encoding='utf8', timeout=360)
            output = run.stdout + run.stderr
            result = dict(case=name, passed=run.returncode == 0, exitCode=run.returncode,
                          seconds=round(time.monotonic()-start, 2))
        except subprocess.TimeoutExpired as error:
            output = str(error)
            result = dict(case=name, passed=False, error=output,
                          seconds=round(time.monotonic()-start, 2))
        (OUT / (name+'.log')).write_text(output, encoding='utf8')
        results.append(result)
        print('PASS' if result['passed'] else 'FAIL', name, result['seconds'], flush=True)
        if not result['passed']:
            print(output[-3500:], flush=True)
        (OUT/'results.json').write_text(json.dumps(dict(passed=all(r['passed'] for r in results),
            complete=len(results)==len(CASES), cases=results), ensure_ascii=False, indent=2), encoding='utf8')
    raise SystemExit(0 if all(r['passed'] for r in results) else 1)
