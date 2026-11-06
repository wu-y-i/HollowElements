using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class BrokenVesselBoss : BossBehaviour
    {
        internal bool Dream => gameObject.scene.name=="GG_Lost_Kin";
        internal bool Empowered { get; private set; }
        internal int Overloads { get; private set; }
        internal int Knockdowns { get; private set; }
        private bool knockdown,down,recovering,recoveryProtected,attackStarted;
        internal override string Redirect(FsmState state)
        {
            return null;
        }
        internal override void Entering(FsmState state)
        {
            if(state.Fsm.Name!="IK Control") return;
            if(state.Name=="Stun Start")
            {
                Empowered=false;down=true;recovering=false;Overloads=0;knockdown=false;
                foreach(var fsm in GetComponents<PlayMakerFSM>()) if(fsm.FsmName=="Stun Control") fsm.SetState("Reset Counter");
            }
            if(state.Name=="Stun Recover" && down) recovering=true;
            if(recovering && state.Name!="Stun Recover" && state.Name!="Stunned" && state.Name!="Stun Start")
            { down=false; recovering=false; Empowered=true; recoveryProtected=true;attackStarted=false; Trace("起身后强化并在完成一次正常攻击前保护"); }
            if(recoveryProtected && (state.Name.Contains("Antic") || state.Name=="Dash" || state.Name=="Slash 1" || state.Name=="Jump")) attackStarted=true;
            if(recoveryProtected && attackStarted && (state.Name=="Idle" || state.Name=="Set Idle")) {recoveryProtected=false;attackStarted=false;}
        }
        internal override void SecondaryAccepted(SecondaryDamage damage)
        {
            if(damage.Origin!=DamageOrigin.Overload || down || knockdown || recoveryProtected) return;
            if(++Overloads >= (Dream ? 2 : 1)) knockdown=true;
        }
        private void LateUpdate()
        {
            if(!knockdown) return; knockdown=false;
            if(!Health || Health.isDead || Health.hp<=0) return;
            GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="IK Control").SetState("Stun Start");
            Knockdowns++; Trace("超载倒地 " + Knockdowns);
        }
        internal override double DirectDamage(Element element,double amount) => Empowered ? amount*1.5 : amount;
        internal override int SecondaryDamage(Element element,int amount) => Reactions.FinalDamage(DirectDamage(element,amount));
        internal override int ContactDamage(int original) => Empowered && original<9999 ? Mathf.Max(2,original) : original;
        internal override void Projectile(GameObject projectile,FsmStateAction action)
        {
            var spawner=projectile.GetComponent<EnemySpawner>();
            if(projectile.name.StartsWith("Parasite Balloon"))
            {
                var element=Random.value<.5f ? Element.Hydro : Element.Cryo;
                if(spawner)
                    (spawner.GetComponent<SummonInheritance>() ?? spawner.gameObject.AddComponent<SummonInheritance>())
                        .Configure(spawner,element,Health.GetInstanceID());
                else
                    // API 77 Godhome's misleadingly named "Spawner" is already the live balloon.
                    (projectile.GetComponent<ElementalMinion>() ?? projectile.AddComponent<ElementalMinion>())
                        .SetElement(element,Health.GetInstanceID());
            }
            else if(projectile.GetComponentInChildren<DamageHero>(true))
                ElementalAttack.Set(projectile,Element.None,Health.GetInstanceID(),outline:false);
        }
    }
}
