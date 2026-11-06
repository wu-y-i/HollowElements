using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class CollectorBoss : BossBehaviour
    {
        internal int ImmediateSummons { get; private set; }
        internal int NeutralJumps { get; private set; }
        private int summonDue;
        private bool jumpDue;
        internal override void DirectHitAccepted(Element element,Reaction reaction,int damage)
        {
            if(damage<=0) return;
            if(Reactions.Amplifying(reaction)) summonDue++;
            if(element==Element.None) jumpDue=true;
        }
        internal override void SecondaryAccepted(SecondaryDamage damage)
        {
            summonDue++;
            if(damage.Element==Element.None) jumpDue=true;
        }
        private void SummonOne(PlayMakerFSM control)
        {
            var spawn=control.FsmStates.First(s=>s.Name=="Spawn");
            var action=spawn.Actions.OfType<SpawnObjectFromGlobalPool>().First();
            var bounds=spawn.Actions.OfType<RandomFloat>().First();
            var choices=control.FsmStates.Where(s=>s.Name=="Buzzer" || s.Name=="Spitter" || s.Name=="Roller")
                .SelectMany(s=>s.Actions).OfType<SetSpawnJarContents>().ToArray();
            var contents=choices[Random.Range(0,choices.Length)];
            var jar=action.gameObject.Value.Spawn(new Vector3(Random.Range(bounds.min.Value,bounds.max.Value),transform.position.y,0));
            jar.GetComponent<SpawnJarControl>().SetEnemySpawn(contents.enemyPrefab.Value,contents.enemyHealth.Value);
            ImmediateSummons++; Trace("原地召唤一罐 " + contents.enemyPrefab.Value.name);
        }
        private void LateUpdate()
        {
            if(!Health || Health.isDead || Health.hp<=0) { summonDue=0; jumpDue=false; return; }
            var control=GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Control");
            while(summonDue>0) { summonDue--; SummonOne(control); }
            if(!jumpDue) return; jumpDue=false;
            string state=control.ActiveStateName;
            if(state=="Jump" || state=="Jump Antic" || state=="Roof" || state=="Spawn" || state=="Summon?") return;
            control.SetState("Jump Antic"); NeutralJumps++; Trace("无元素伤害触发上跳召唤");
        }
        internal void SummonDied(int maximum)
        { if(Health && !Health.isDead && Health.hp>1) Runtime.NeutralEncounterDamage(Health,System.Math.Min(maximum,Health.hp-1)); }
    }
}
