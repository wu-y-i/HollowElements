using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class ElderHuBoss : BossBehaviour
    {
        internal bool Swapped { get; private set; }
        private bool chain;
        internal Element HammerElement => chain ? (Swapped ? Element.Electro : Element.Hydro) : (Swapped ? Element.Hydro : Element.Electro);
        internal override void Entering(FsmState state)
        {
            if(state.Fsm.Name!="Attacking") return;
            if(state.Name=="M 1") chain=true;
            else if(state.Name=="Place Rings" || state.Name=="Attack" || state.Name=="Ring Antic") chain=false;
        }
        internal void Hammer(UnityEngine.GameObject obj)
        { ElementalAttack.Set(obj,HammerElement,Health.GetInstanceID()); }
        internal static void PlayerHurt(GameObject source)
        {
            var attack=source ? source.GetComponentInParent<ElementalAttack>() : null;
            if(!attack) return;
            foreach(var boss in FindObjectsOfType<ElderHuBoss>())
                if(boss.Health.GetInstanceID()==attack.OwnerId)
                {boss.Swapped=!boss.Swapped;boss.Runtime.ShadowDashDisabledUntil=boss.Runtime.Now+3;break;}
        }
    }
}
