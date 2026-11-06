"""Read native FSMs/assets only. No acceptance assertions or regression runs."""
import json
import time
from runtime_acceptance import command, ROOT

OUT=ROOT/'local/research/boss-26-42'
SCENES=['GG_Watcher_Knights','GG_Uumuu','GG_Uumuu_V','GG_Nosk_Hornet','GG_Nosk','GG_Nosk_V',
        'GG_Sly','GG_Hornet_2','GG_Ghost_No_Eyes','GG_Ghost_No_Eyes_V','GG_Traitor_Lord',
        'GG_White_Defender','GG_Ghost_Markoth','GG_Ghost_Markoth_V','GG_Grey_Prince_Zote',
        'GG_Failed_Champion','GG_Hollow_Knight','GG_Radiance']

def save(name,data): (OUT/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    for scene in SCENES:
        command('pause',value=False);command('invincible',value=True);command('goto',scene=scene)
        deadline=time.monotonic()+30
        while time.monotonic()<deadline:
            s=command('status')
            if s['scene']==scene and s['hero']:break
            time.sleep(.2)
        else:raise TimeoutError(scene)
        time.sleep(3);command('ready');command('pause',value=True)
        objs=command('sceneObjects');save(scene+'-objects.json',objs)
        save(scene+'-all.json',command('dumpAll'));s=command('status');save(scene+'-status.json',s)
        ids={o['id'] for o in objs}
        for e in s['enemies']:
            if e['objectId'] not in ids:continue
            save(scene+'-'+str(e['objectId'])+'-detail.json',command('attackDetails',objectId=e['objectId']))
            if scene=='GG_Sly' and 'Sly' in e['name']:command('exportEnemySprites',targetId=e['id'])
        print('READ',scene,[(e['name'],e['hp']) for e in s['enemies'] if e['objectId'] in ids],flush=True)
    command('pause',value=False)
