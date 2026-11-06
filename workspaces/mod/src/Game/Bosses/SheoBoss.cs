using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class SheoBoss : BossBehaviour
    {
        internal override Element AttackElement(GameObject source)
        {
            if(source==gameObject) return Element.None;
            for(var t=source.transform;t && t!=transform;t=t.parent)
            {
                if(t.name.StartsWith("GS")) return Element.Electro;
                if(t.name=="Stab Effect") return Element.Hydro;
                if(t.name=="Stomp Effects") return Element.Pyro;
                if(t.name=="S1") return Element.Cryo;
            }
            return Element.None;
        }
        internal override void Projectile(GameObject obj,FsmStateAction action)
        {
            if(!obj.name.StartsWith("Paint Shot ")) return;
            string colour=obj.name.Substring("Paint Shot ".Length);
            Element element=colour.StartsWith("P") ? Element.Electro : colour.StartsWith("R") ? Element.Pyro : colour.StartsWith("B") ? Element.Cryo : Element.Hydro;
            ElementalAttack.Set(obj,element,Health.GetInstanceID(),outline:false);
        }
    }
}
