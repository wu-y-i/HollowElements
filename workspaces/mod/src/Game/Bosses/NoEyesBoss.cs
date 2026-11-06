using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class NoEyesBoss : BossBehaviour
    {
        internal void Ghost(GameObject obj)
        {
            if(!obj.name.StartsWith("No Eyes Head")) return;
            var marker=obj.GetComponent<ElementalAttack>();if(marker && marker.Element!=Element.None) return;
            ElementalAttack.Set(obj,ElementalAttack.RandomElement(),Health.GetInstanceID(),tint:true,outline:false);
        }
        internal override void Projectile(GameObject projectile,FsmStateAction action) { Ghost(projectile); }
    }
}
