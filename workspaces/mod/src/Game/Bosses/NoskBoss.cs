using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class NoskBoss : BossBehaviour
    {
        internal void Glob(GameObject obj)
        {
            if(!obj.name.StartsWith("Vomit Glob Nosk")) return;
            var marker=obj.GetComponent<ElementalAttack>();if(marker && marker.Element!=Element.None) return;
            ElementalAttack.Set(obj,ElementalAttack.RandomElement(),Health.GetInstanceID());
        }
        internal override void Projectile(GameObject projectile,FsmStateAction action) { Glob(projectile); }
    }
}
