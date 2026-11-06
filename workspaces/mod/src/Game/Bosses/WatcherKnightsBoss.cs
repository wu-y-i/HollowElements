using HollowElements.Rules;
using System.Linq;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class WatcherKnightsBoss : BossBehaviour
    {
        internal bool Armoured { get; private set; }=true;
        internal double VulnerableUntil { get; private set; }
        private PlayMakerFSM control;
        private void Awake() {control=GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Black Knight");}
        internal bool BarVisible => control && control.ActiveStateName!="" && control.ActiveStateName!="Init" && control.ActiveStateName!="Rest" && control.ActiveStateName!="Set Battle Inert";
        private bool Superconduct => VulnerableUntil>Runtime.Now;
        internal override bool BlocksHit(HitInstance hit) => Armoured && (hit.AttackType==AttackTypes.Nail || hit.AttackType==AttackTypes.NailBeam);
        internal override bool BlockedHitGivesSoul => true;
        internal override void Reacted(Reaction reaction)
        {
            if(reaction==Reaction.Superconduct) VulnerableUntil=Runtime.Now+Runtime.Config.Reactions.WatcherSuperconductSeconds;
        }
        internal override double DirectDamage(Element element,double amount) => Superconduct && amount>0 ? amount*1.5 : amount;
        internal override int SecondaryDamage(Element element,int amount) => Reactions.FinalDamage(DirectDamage(element,amount));
        internal override void SecondaryAccepted(SecondaryDamage damage)
        { if(damage.Origin==DamageOrigin.Overload && Armoured) { Armoured=false;Trace("超载永久解除骨钉护甲"); } }
    }
}
