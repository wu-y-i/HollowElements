using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowElements.Game.Bosses
{
    // Only Collector-spawned enemies in its own arena participate. No species-wide behaviour changes.
    internal sealed class EncounterSummon : MonoBehaviour
    {
        internal CollectorBoss Owner;
        internal int Maximum { get; private set; }
        private bool notified;
        private HealthManager health;
        private void Awake()
        { health=GetComponent<HealthManager>(); health.OnDeath+=Died; }
        private void CheckDeath()
        {
            // Some native effects recycle/destroy before SendDeathEvent or the Die hook returns.
            // hp/isDead are managed fields and remain readable during component destruction.
            if(!ReferenceEquals(health,null) && (health.hp<=0 || health.isDead)) Died();
        }
        private void OnDisable() { CheckDeath(); }
        private void OnDestroy() { CheckDeath(); if(!ReferenceEquals(health,null)) health.OnDeath-=Died; }
        internal static void Discover(HealthManager health)
        {
            string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if(!health || (scene!="GG_Collector" && scene!="GG_Collector_V") || health.name=="Jar Collector" || health.GetComponent<TrainingDummy>()) return;
            if(!health.GetComponent<EnemyDreamnailReaction>()) return;
            var owner=Object.FindObjectsOfType<CollectorBoss>().FirstOrDefault(b=>b.gameObject.scene.name==scene);
            if(!owner) return;
            var item=health.GetComponent<EncounterSummon>() ?? health.gameObject.AddComponent<EncounterSummon>();
            if(item.Owner==owner) return;
            item.Owner=owner; item.Maximum=0; item.notified=false;
        }
        private void OnEnable() { Owner=null; Maximum=0; notified=false; Discover(GetComponent<HealthManager>()); }
        internal void CaptureMaximum() { var hp=GetComponent<HealthManager>(); if(Maximum==0 && hp && hp.hp>0) Maximum=hp.hp; }
        private void LateUpdate() { CaptureMaximum(); }
        internal void Died()
        { if(notified) return; notified=true; if(Owner && Maximum>0) Owner.SummonDied(Maximum); }
    }
}
