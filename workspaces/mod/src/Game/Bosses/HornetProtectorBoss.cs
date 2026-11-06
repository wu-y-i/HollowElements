using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal class HornetProtectorBoss : BossBehaviour
    {
        internal const double PerReaction = .1;
        internal int Reactions { get; private set; }
        internal Element LastElement { get; private set; }
        internal int RepeatedHits { get; private set; }
        private Element pendingElement;
        private int pendingHits;
        internal double Vulnerability => 1 + Reactions * PerReaction;
        internal int SpeedStacks => Mathf.Min(Reactions,10);
        internal override float Tempo => 1 + SpeedStacks * (this is HornetSentinelBoss ? .05f : .1f);
        protected virtual void Awake() { gameObject.AddComponent<BossMotion>(); }
        internal override float ActionTempo(FsmStateAction action) => action.Fsm.Name == "Control" ? Tempo : 1;
        internal override void Reacted(Reaction reaction)
        {
            LastElement=Element.None;RepeatedHits=0;
            float before = Tempo; Reactions++;
            var body = GetComponent<Rigidbody2D>(); if (body) body.velocity *= Tempo / before;
            Trace("反应层数=" + Reactions + " 易伤=" + Vulnerability + " 速度=" + Tempo);
        }
        internal override void PrepareHit(AttackContext hit)
        { pendingElement=hit.Element;pendingHits=hit.Element!=Element.None && hit.Element==LastElement ? RepeatedHits+1 : 0;hit.Damage *= Vulnerability; }
        internal override void FilterResult(HitResult result)
        {
            if(result.Reaction!=Reaction.None) pendingHits=0;
            if(pendingElement!=Element.None && result.PreciseDamage>0) result.PreciseDamage*=System.Math.Pow(.5,System.Math.Min(30,pendingHits));
        }
        internal override void DirectHitAccepted(Element element,Reaction reaction,int damage)
        {
            if(reaction!=Reaction.None) { LastElement=Element.None;RepeatedHits=0;return; }
            if(element!=Element.None) {LastElement=element;RepeatedHits=pendingHits;}
        }
        internal override void PrepareUnknownHit(ref HitInstance hit) { hit.Multiplier *= (float)Vulnerability; }
        internal override int SecondaryDamage(Element element, int original)
        {
            int repeats=element!=Element.None && element==LastElement ? RepeatedHits+1 : 0;
            return original<=0?0:Rules.Reactions.Damage(original*Vulnerability*System.Math.Pow(.5,System.Math.Min(30,repeats)));
        }
        internal override void SecondaryAccepted(SecondaryDamage damage)
        {
            if(damage.Element==Element.None) return;
            RepeatedHits=damage.Element==LastElement?RepeatedHits+1:0;LastElement=damage.Element;
        }
    }
}
