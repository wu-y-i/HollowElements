using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class GorbBoss : BossBehaviour
    {
        internal const double SuppressionSeconds = 6;
        internal double SuppressedUntil { get; private set; }
        internal int Ring { get; private set; }
        internal Element RingElement { get; private set; }
        private bool fourthPending, fourthEntering;
        internal override void Reacted(Reaction reaction) { SuppressedUntil = Runtime.Now + SuppressionSeconds; Trace("两圈窗口至 " + SuppressedUntil); }
        internal override string Redirect(FsmState state)
        {
            if (state.Fsm.Name != "Attacking") return null;
            if (state.Name == "Triple" && Runtime.Now < SuppressedUntil) { fourthPending = fourthEntering = false; return "Recover"; }
            if (state.Name == "Recover" && fourthPending)
            {
                fourthPending = false;
                if (Runtime.Now < SuppressedUntil || Health.isDead) return null;
                fourthEntering = true; return "Triple Pause";
            }
            return null;
        }
        internal override void Entering(FsmState state)
        {
            if (state.Fsm.Name != "Attacking") return;
            if (state.Name == "Attack") { Ring = 0; fourthPending = fourthEntering = false; }
            if (state.Name != "Attack" && state.Name != "Double" && state.Name != "Triple") return;
            Ring++; RingElement = ElementalAttack.RandomElement();
            if (state.Name == "Triple") { fourthPending = !fourthEntering; fourthEntering = false; }
            Trace("剑阵 " + Ring + " " + RingElement);
        }
        internal override void Projectile(GameObject projectile, FsmStateAction action)
        {
            if (action.Fsm.Name == "Attacking" && projectile.name.StartsWith("Shot Slug Spear"))
                ElementalAttack.Set(projectile, RingElement, Health.GetInstanceID(), Ring, tint:true, outline:false);
        }
    }
}
