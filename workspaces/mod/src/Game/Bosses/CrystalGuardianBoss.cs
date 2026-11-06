using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class CrystalGuardianBoss : BossBehaviour
    {
        internal const int HeadHitsRequired = 8;
        internal const double RespawnSeconds = 15, ForcedNailSeconds = 6;
        internal bool Shielded { get; private set; } = true;
        internal int HeadHits { get; private set; }
        internal int Overloads { get; private set; }
        internal double ShieldReturnsAt { get; private set; }
        internal float BrokenAt { get; private set; } = -100;
        private void Awake() { gameObject.AddComponent<CrystalShieldVisual>(); }
        private void Update()
        {
            if(!Shielded && Runtime && Runtime.Now >= ShieldReturnsAt && Health && !Health.isDead) { Shielded=true; HeadHits=0; Overloads=0; Trace("护盾恢复"); }
        }
        internal void HeadHit()
        {
            if(!Shielded) return;
            if(++HeadHits>=HeadHitsRequired) BreakShield();
        }
        private void BreakShield()
        {
            if(!Shielded) return;
            Shielded=false; ShieldReturnsAt=Runtime.Now+RespawnSeconds; BrokenAt=Time.time; Trace("破盾，15秒后恢复");
        }
        internal override double DirectDamage(Element element,double amount) => Shielded ? 0 : amount;
        internal override bool AcceptZeroDamage(Element element) => Shielded;
        internal override int FilterSecondary(SecondaryDamage damage)
        {
            if(!Shielded) return damage.Damage;
            if(damage.Origin == DamageOrigin.Overload && ++Overloads >= (gameObject.scene.name=="GG_Crystal_Guardian_2"?2:1)) BreakShield();
            return 0;
        }
        internal static void PlayerHurt(GameObject source)
        {
            if(!source || source.GetComponent<HeroController>()) return;
            string scene=source.scene.name;
            if(scene!="GG_Crystal_Guardian" && scene!="GG_Crystal_Guardian_2") return;
            bool attack=source.GetComponentInParent<CrystalGuardianBoss>();
            for(var t=source.transform;t && !attack;t=t.parent) attack=t.name.StartsWith("Beam") || t.name.StartsWith("Laser Turret");
            if(!attack) return;
            foreach(var boss in FindObjectsOfType<CrystalGuardianBoss>())
                if(boss.gameObject.scene==source.scene && boss.Runtime) { boss.Runtime.ForceNail(Random.value<.5f ? Element.Pyro : Element.Electro,ForcedNailSeconds); break; }
        }
    }

    internal sealed class CrystalShieldVisual : MonoBehaviour
    {
        private CrystalGuardianBoss boss;
        private MeshRenderer sourceRenderer;
        private Material material;
        private readonly LineRenderer[] shards = new LineRenderer[8];
        private void Awake()
        {
            boss=GetComponent<CrystalGuardianBoss>(); sourceRenderer=GetComponent<MeshRenderer>(); material=new Material(Shader.Find("Sprites/Default"));
            for(int i=0;i<8;i++)
            {
                var obj=new GameObject("HollowElements.CrystalShield"); obj.transform.SetParent(transform,false);
                var line=obj.AddComponent<LineRenderer>(); line.sharedMaterial=material; line.useWorldSpace=false; line.loop=true;
                line.positionCount=4; line.startWidth=line.endWidth=.065f; shards[i]=line;
            }
        }
        private void LateUpdate()
        {
            float burst=Mathf.Clamp01((Time.time-boss.BrokenAt)/.35f);
            for(int i=0;i<8;i++)
            {
                var line=shards[i]; bool active=boss.Shielded ? i>=boss.HeadHits : burst<1;
                line.enabled=active && sourceRenderer.enabled; if(!active) continue;
                line.sortingLayerID=sourceRenderer.sortingLayerID; line.sortingOrder=sourceRenderer.sortingOrder+2;
                float angle=(i*45+Mathf.Sin(Time.time*.8f)*3)*Mathf.Deg2Rad, radius=boss.Shielded ? 2.15f : 2.15f+burst*.9f;
                Vector3 centre=new Vector3(Mathf.Cos(angle)*radius,1+Mathf.Sin(angle)*radius,-.02f);
                line.SetPosition(0,centre+Vector3.up*.27f); line.SetPosition(1,centre+Vector3.right*.13f); line.SetPosition(2,centre-Vector3.up*.27f); line.SetPosition(3,centre-Vector3.right*.13f);
                Color colour=new Color(.78f,.34f,.91f,boss.Shielded ? 1 : 1-burst); line.startColor=line.endColor=colour;
            }
        }
        private void OnDestroy() { foreach(var shard in shards) if(shard) Destroy(shard.gameObject); if(material) Destroy(material); }
        internal void Restore() { foreach(var shard in shards) if(shard) shard.enabled=false; }
    }
}
