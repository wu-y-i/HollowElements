using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class SoulWarriorBoss : BossBehaviour
    {
        internal const float OrbSize = 1.3f;
        internal override void Projectile(GameObject projectile, FsmStateAction action)
        {
            if (action.Fsm.Name == "Mage Knight" && action.State.Name == "Shoot" && projectile.name.StartsWith("Mage Orb"))
                ElementalAttack.Set(projectile, Random.value < .5f ? Element.Cryo : Element.Electro, Health.GetInstanceID(), size: OrbSize);
        }
    }
}
