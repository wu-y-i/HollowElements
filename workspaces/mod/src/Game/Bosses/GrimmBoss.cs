using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class GrimmBoss : BossBehaviour
    {
        internal override Element ContactElement() => Element.Pyro;
        internal override double DirectDamage(Element element,double amount)
        { return amount<=0 ? amount : amount*(element==Element.Pyro ? .5 : element==Element.Hydro ? 1.5 : 1); }
        internal override int SecondaryDamage(Element element,int amount) => Reactions.FinalDamage(DirectDamage(element,amount));
        internal override void FilterResult(HitResult result)
        {
            result.NextState.FrozenUntil=0; result.NextState.FrozenDuration=0;
            result.NextState.FrozenSpeed=1; result.NextState.HardFrozen=false;
        }
        internal override void Projectile(GameObject obj,FsmStateAction action)
        { if(obj.GetComponentInChildren<DamageHero>(true)) ElementalAttack.Set(obj,Element.Pyro,Health.GetInstanceID(),outline:false); }
    }
}
