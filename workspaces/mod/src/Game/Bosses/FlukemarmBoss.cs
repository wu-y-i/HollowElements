using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // The mother's native behaviour is unchanged; only the released Flukefey receive a marker.
    internal sealed class FlukemarmBoss : BossBehaviour
    {
        internal static void Released(FsmState state)
        {
            var obj=state.Fsm.GameObject;
            if(!obj || state.Fsm.Name!="Fluke Fly" || state.Name!="Spawn" || obj.scene.name!="GG_Flukemarm") return;
            var marker=obj.GetComponent<ElementalMinion>() ?? obj.AddComponent<ElementalMinion>();
            marker.SetElement(Element.Pyro,0);
        }
    }
}
