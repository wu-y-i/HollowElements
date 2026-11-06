using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class FalseKnightBoss : BossBehaviour
    {
        internal const double NeutralShellMultiplier = .75;
        internal Element ShellElement { get; private set; }
        private TargetState lastHead;
        internal void RememberHead(TargetState state) { lastHead=state.Copy(); }
        internal void ResetHead() {lastHead=null;}
        private void Inherit(TargetState state)
        {
            state.Expire(Runtime.Now);
            if(state.Aura!=Element.None) ShellElement=state.Aura;
        }
        internal override void PrepareHit(AttackContext hit)
        {
            // Elemental damage still deals damage; it simply cannot attach to an uninfused shell.
            hit.CanApplyAura = ShellElement != Element.None;
            if (hit.Element == Element.None) hit.Damage *= NeutralShellMultiplier;
        }
        internal override void PrepareUnknownHit(ref HitInstance hit)
        { hit.Multiplier *= (float)NeutralShellMultiplier; }
        internal override int SecondaryDamage(Element element, int original)
        { return ShellElement!=Element.None && element==ShellElement ? 0 : element == Element.None ? Reactions.Damage(original * NeutralShellMultiplier) : original; }
        internal override double DirectDamage(Element element,double amount) => ShellElement!=Element.None && element==ShellElement ? 0 : amount;
        internal override bool AcceptZeroDamage(Element element) => ShellElement!=Element.None && element==ShellElement;

        internal override void Entering(FsmState state)
        {
            if (state.Fsm.Name != "FalseyControl" || (state.Name != "Recover" && state.Name != "Stun Fail")) return;
            bool found=false;
            foreach (var head in GetComponentsInChildren<HealthManager>(true))
            {
                if (head == Health || head.name != "Head") continue;
                EnemyRecord record;
                if (!Runtime.Enemies.TryGetValue(head, out record)) continue;
                found=true;
                Inherit(record.State);
                // No aura on the head leaves the shell's previous permanent infusion unchanged.

            }
            if(!found && lastHead!=null) Inherit(lastHead);
            if (ShellElement != Element.None) Record.State.SetIntrinsicAura(ShellElement);
            Trace("回壳：" + ShellElement);
        }
        internal override void SyncAura(TargetState state) { if (ShellElement != Element.None) state.SetIntrinsicAura(ShellElement); }
    }
}
