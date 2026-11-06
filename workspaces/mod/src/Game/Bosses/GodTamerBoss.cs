using HollowElements.Rules;

namespace HollowElements.Game.Bosses
{
    internal sealed class GodTamerBoss : BossBehaviour
    {
        private TargetState shared;
        internal override void DirectHitAccepted(Element element,Reaction reaction,int damage)
        { if(Reactions.Amplifying(reaction)) Mirror(damage,element); }
        internal override void SecondaryHitAccepted(SecondaryDamage damage,int actual) { Mirror(actual,damage.Element); }
        private void Mirror(int amount,Element element)
        {
            if(amount<=0) return;
            foreach(var other in FindObjectsOfType<GodTamerBoss>())
                if(other!=this && other.gameObject.scene==gameObject.scene && other.Health && !other.Health.isDead)
                    Runtime.NeutralEncounterDamage(other.Health,Reactions.FinalDamage(amount*1.5),element);
        }
        private void FindShared()
        {
            if(shared!=null) return;
            foreach(var other in FindObjectsOfType<GodTamerBoss>())
                if(other!=this && other.gameObject.scene==gameObject.scene && other.shared!=null) { shared=other.shared; return; }
            shared=new TargetState();
        }
        private static void CopyAura(TargetState from,TargetState to)
        { to.Aura=from.Aura; to.AuraUnits=from.AuraUnits; to.AuraUntil=from.AuraUntil; to.AuraDuration=from.AuraDuration; }
        internal override void SyncAura(TargetState state)
        { FindShared(); shared.Expire(Runtime.Now); CopyAura(shared,state); }
        internal override void CommitAura(TargetState state)
        {
            FindShared(); CopyAura(state,shared);
            foreach(var pair in Runtime.Enemies)
                if(pair.Key && pair.Key.gameObject.scene==gameObject.scene && pair.Key.GetComponent<GodTamerBoss>()) CopyAura(shared,pair.Value.State);
        }
        internal void ClearSharedAura() { if(shared!=null) CopyAura(new TargetState(),shared); }
    }
}
