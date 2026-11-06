using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Deliberately small: each boss owns its state; the existing combat hooks call these few entry points.
    internal abstract class BossBehaviour : MonoBehaviour
    {
        protected CombatRuntime Runtime => CombatRuntime.Current;
        protected HealthManager Health => GetComponent<HealthManager>();
        protected EnemyRecord Record => Runtime.Track(Health);
        internal virtual void Entering(FsmState state) { }
        internal virtual void Entered(FsmState state) { }
        internal virtual string Redirect(FsmState state) => null;
        internal virtual void Reacted(Reaction reaction) { }
        internal virtual void AcceptedHit() { }
        internal virtual void DirectHitAccepted(Element element, Reaction reaction, int damage) { }
        internal virtual void SecondaryAccepted(SecondaryDamage damage) { }
        internal virtual void SecondaryHitAccepted(SecondaryDamage damage,int actual)
        { if(damage.Origin!=DamageOrigin.Superconduct) SecondaryAccepted(damage); }
        internal virtual void FilterResult(HitResult result) { }
        internal virtual void CommitAura(TargetState state) { }
        internal virtual float ControlScale => 1;
        internal virtual float Tempo => 1;
        internal virtual float ActionTempo(FsmStateAction action) => Tempo;
        internal virtual void Projectile(GameObject projectile, FsmStateAction action) { }
        internal virtual Element AttackElement(GameObject source) => ContactElement();
        internal virtual void PrepareHit(AttackContext hit) { }
        internal virtual void PrepareUnknownHit(ref HitInstance hit) { }
        internal virtual double DirectDamage(Element element, double amount) => amount;
        internal virtual bool AcceptZeroDamage(Element element) => false;
        internal virtual bool BlocksHit(HitInstance hit) => false;
        internal virtual bool BlockedHitGivesSoul => false;
        internal virtual bool AllowsSecondaryThroughArmour(SecondaryDamage damage) => false;
        internal virtual int FilterSecondary(SecondaryDamage damage) => SecondaryDamage(damage.Element, damage.Damage);
        internal virtual int SecondaryDamage(Element element, int original) => original;
        internal virtual Element ContactElement() => Element.None;
        internal virtual int ContactDamage(int original) => original;
        internal virtual void SyncAura(TargetState state) { }
        protected void Trace(string text) { if (Runtime) Runtime.LogTrace(GetType().Name + ": " + text); }
    }
}
