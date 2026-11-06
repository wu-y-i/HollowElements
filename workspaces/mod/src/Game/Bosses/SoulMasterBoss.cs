using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class SoulMasterBoss : BossBehaviour
    {
        private bool phaseStarted;
        private SoulMasterBoss second;
        private void OnEnable() {phaseStarted=false;second=null;}
        internal bool PhaseTwo => GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="Mage Lord 2");
        internal override void Entering(FsmState state)
        {
            if(state.Fsm.Name=="Mage Lord 2" && (state.Name=="Wait" || state.Name=="Quake Antic" || state.Name=="Shot Antic")) phaseStarted=true;
        }
        internal bool BarVisible
        {
            get
            {
                if(PhaseTwo)
                {
                    return phaseStarted;
                }
                if(!second) second=FindObjectsOfType<SoulMasterBoss>().FirstOrDefault(b=>b!=this && b.PhaseTwo);
                return !second || !second.phaseStarted;
            }
        }
        internal void Orb(GameObject orb)
        { ElementalAttack.Set(orb,Random.value<.5f ? Element.Hydro : Element.Cryo,Health.GetInstanceID()); }
        internal override void Projectile(GameObject projectile,FsmStateAction action)
        {
            if(projectile.name.StartsWith("Mage Orb")) Orb(projectile);
            else if(projectile.name.StartsWith("Shockwave Wave")) ElementalAttack.Set(projectile,Element.Hydro,Health.GetInstanceID(),tint:true,outline:false);
        }
    }
}
