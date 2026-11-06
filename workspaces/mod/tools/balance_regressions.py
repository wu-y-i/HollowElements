"""Current-build regressions for incoming damage, vanilla mechanics and coloured formations."""
import hashlib,json,time,traceback
from runtime_acceptance import ROOT,LOG
import balance_acceptance as balance
import boss_fixes_acceptance as fixes
import boss_fix_regressions as original
fixes.OUT=balance.OUT
def original_mechanics():
    stagger=original.native_stagger();uumuu=original.uumuu_original()
    assert all(row['afterOoma']['hp']==250 for row in uumuu),uumuu
    return dict(stagger=stagger,uumuu=uumuu,oomaOriginalDamage=100)
CASES={'incoming':lambda:dict(attunedAscended=fixes.grimm(),physics=fixes.grimm_physics()),
       'original':original_mechanics,
       'radiance':fixes.radiance_tinted,'galien':fixes.perf}
if __name__=='__main__':
    for name,run in CASES.items():
        try:r=dict(passed=True,evidence=run());print('PASS regression',name,flush=True)
        except Exception as error:r=dict(passed=False,error=repr(error));traceback.print_exc()
        r.update(dllSha256=hashlib.sha256((ROOT/'artifacts/mod/HollowElements/HollowElements.dll').read_bytes()).hexdigest(),commands=LOG[:]);LOG.clear()
        (balance.OUT/('regression-'+name+'.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
