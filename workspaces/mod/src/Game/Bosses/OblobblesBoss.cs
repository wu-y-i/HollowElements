using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class OblobblesBoss : BossBehaviour
    {
        internal Element AuraElement => name=="Mega Fat Bee" ? Element.Pyro : Element.Electro;
        internal Element BulletElement => AuraElement==Element.Pyro ? Element.Electro : Element.Pyro;
        internal bool Enraged { get; private set; }
        internal override Element ContactElement() => AuraElement;
        internal override void SyncAura(TargetState state) { state.SetIntrinsicAura(AuraElement); }
        internal override void Entering(FsmState state)
        { if(state.Fsm.Name=="Set Rage" && state.Name=="Set") { Enraged=true; Trace("狂暴，免疫 "+AuraElement); } }
        internal override double DirectDamage(Element element,double amount) => Enraged && element==AuraElement ? 0 : amount;
        internal override bool AcceptZeroDamage(Element element) => Enraged && element==AuraElement;
        internal override int SecondaryDamage(Element element,int amount) => Reactions.FinalDamage(DirectDamage(element,amount));
        internal override void Projectile(GameObject projectile,FsmStateAction action)
        {
            if(!projectile.name.StartsWith("Oblobble Shot")) return;
            var element=Enraged ? (Random.value<.5f ? Element.Pyro : Element.Electro) : BulletElement;
            ElementalAttack.Set(projectile,element,Health.GetInstanceID(),tint:true,outline:false);
        }
    }
}
