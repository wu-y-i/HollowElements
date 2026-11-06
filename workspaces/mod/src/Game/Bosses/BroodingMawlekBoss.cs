using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class BroodingMawlekBoss : BossBehaviour
    {
        internal const float CryoChance = .5f;
        internal int AcceptedHits { get; private set; }
        internal int CryoApplications { get; private set; }
        internal override void AcceptedHit()
        {
            AcceptedHits++;
            if (Random.value >= CryoChance) return;
            CryoApplications++; Runtime.ApplyAttachment(Health, Element.Cryo);
            Trace("受击自附冰 " + CryoApplications + "/" + AcceptedHits);
        }
        internal override void Projectile(GameObject projectile, FsmStateAction action)
        {
            if (action.Fsm.Name == "Mawlek Control" && action.State.Name == "Shoot")
                ElementalAttack.Set(projectile, Random.value < .5f ? Element.Pyro : Element.Electro, Health.GetInstanceID());
        }
    }
}
