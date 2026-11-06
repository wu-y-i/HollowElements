using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Persistent native attack actors re-enable without being spawned from a pool.
    internal sealed class EncounterAttack : MonoBehaviour
    {
        internal BossBehaviour Owner;
        internal Element Element;
        internal bool Tint,Outline,FollowFrost;
        internal void Apply()
        {
            if(!Owner) return;
            var hp=Owner.GetComponent<HealthManager>(); if(!hp) return;
            ElementalAttack.Set(gameObject,Element,hp.GetInstanceID(),tint:Tint,outline:Outline);
            if(FollowFrost)
            {
                var clock=GetComponent<EnemyClock>() ?? gameObject.AddComponent<EnemyClock>();
                clock.ControlOwner=hp; clock.KeepMovingWhenFrozen=true;
            }
        }
        internal static void Bind(GameObject obj,BossBehaviour owner,Element element,bool tint=false,bool outline=true,bool followFrost=false)
        {
            var link=obj.GetComponent<EncounterAttack>() ?? obj.AddComponent<EncounterAttack>();
            link.Owner=owner; link.Element=element; link.Tint=tint; link.Outline=outline; link.FollowFrost=followFrost;
            if(obj.activeInHierarchy) link.Apply();
        }
        private void OnEnable() { Apply(); }
    }
}
