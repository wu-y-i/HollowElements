using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class XeroBoss : BossBehaviour
    {
        internal void Sword(GameObject sword)
        {
            // Swords 3/4 are inactive until phase two. Their native FSM calls us
            // on activation; binding earlier skips the inactive sprite owner.
            if (!sword || !sword.activeInHierarchy) return;
            Element element = sword.name == "Sword 1" ? Element.Cryo : sword.name == "Sword 2" ? Element.Electro : sword.name == "Sword 3" ? Element.Hydro : sword.name == "Sword 4" ? Element.Pyro : Element.None;
            if(element == Element.None) return;
            var marker = sword.GetComponent<ElementalAttack>();
            if(!marker || marker.Element != element) ElementalAttack.Set(sword,element,Health.GetInstanceID(),tint:true,outline:false);
        }
        internal override void Entered(FsmState state)
        {
            if(state.Fsm.Name != "Attacking") return;
            foreach(var variable in state.Fsm.Variables.GameObjectVariables)
                if(variable.Value && variable.Value.name.StartsWith("Sword ")) Sword(variable.Value);
        }
    }
}
