using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;

namespace HollowElements.Game.Bosses
{
    internal sealed class UumuuBoss : BossBehaviour
    {
        internal bool Shielded => Health.IsInvincible;
        internal int ShatterBreaks { get; private set; }
        internal int Nodes { get; private set; }=2;
        internal double ExposureMultiplier { get; private set; }=1;
        private bool wasShielded,breakQueued;
        internal override bool BlockedHitGivesSoul => true;
        internal override void Reacted(Reaction reaction)
        {
            if(!Shielded || breakQueued || reaction==Reaction.None || Reactions.Amplifying(reaction)) return;
            if(reaction==Reaction.Shatter) ShatterBreaks++;
            if(--Nodes<=0) breakQueued=true;
        }
        internal override void Entering(HutongGames.PlayMaker.FsmState state)
        {
            if(state.Fsm.Name!="Mega Jellyfish") return;
            if(state.Name=="Explode") ExposureMultiplier=breakQueued ? 1.5 : .5;
            if(state.Name=="Recover") {Nodes=2;ExposureMultiplier=1;}
        }
        private void LateUpdate()
        {
            bool shield=Shielded;
            if(shield && !wasShielded && !breakQueued) Nodes=2;
            wasShielded=shield;
            if(!breakQueued) return;
            if(shield) GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Mega Jellyfish").SetState("Explode");
            breakQueued=false;
        }
        internal override bool BlocksHit(HitInstance hit) => Shielded && (hit.AttackType==AttackTypes.Nail || hit.AttackType==AttackTypes.NailBeam || hit.AttackType==AttackTypes.Spell);
        internal override bool AcceptZeroDamage(Element element) => Shielded;
        internal override double DirectDamage(Element element,double amount) => Shielded ? 0 : amount*ExposureMultiplier;
        internal override int SecondaryDamage(Element element,int amount) => Reactions.FinalDamage(DirectDamage(element,amount));
        internal override int FilterSecondary(SecondaryDamage damage)
        {
            return SecondaryDamage(damage.Element,damage.Damage);
        }
    }
}
