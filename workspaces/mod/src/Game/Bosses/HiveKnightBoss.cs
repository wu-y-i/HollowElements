using System;
using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class HiveKnightBoss : BossBehaviour
    {
        internal int Healing { get; private set; }
        internal int BeeHits { get; private set; }
        private double halfMask;
        private int maximum;
        private void Start()
        {
            maximum=Record.Maximum;
            var tint=gameObject.AddComponent<NailBladeTint>(); tint.ResourceSet="HiveBlades"; tint.Element=Element.Pyro;
            foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if(fsm.gameObject.scene==gameObject.scene && fsm.FsmName=="Control" && fsm.name.StartsWith("Hive Knight Glob"))
                    foreach(var damage in fsm.GetComponentsInChildren<DamageHero>(true))
                        if(damage.name.StartsWith("Stinger")) EncounterAttack.Bind(damage.gameObject,this,Element.Hydro,tint:true,outline:false);
            var swarm=transform.Find("Mouth Swarm"); if(swarm) EncounterAttack.Bind(swarm.gameObject,this,Element.Pyro,outline:false);
        }
        internal override Element AttackElement(GameObject source) => source==gameObject ? Element.None : Element.Pyro;
        internal void BeeStruck(int damage,Element element)
        {
            if(!Health || Health.isDead || Health.hp<=0 || damage<=0) return;
            var record=Record; halfMask+=damage*.5;
            int heal=(int)Math.Floor(halfMask); halfMask-=heal;
            maximum=Math.Max(maximum,record.Maximum);
            int before=Health.hp; Health.hp=Math.Min(maximum,Health.hp+heal);
            Healing+=Health.hp-before; BeeHits++;
            Runtime.ApplyAttachment(Health,element);
            Trace("小蜜蜂受击 " + damage + "，回血 " + (Health.hp-before) + "，附着 " + element);
        }
    }

    // Native Hivelings have no HP. Keep nail recoil and spell death; observe the accepted attack value.
    internal sealed class HiveBee : MonoBehaviour,IHitResponder
    {
        internal HiveKnightBoss Owner;
        internal Element DamageElement;
        private long lastCast=long.MinValue;
        private double lastHit=-100;
        private bool spellHit;
        internal void Born(HiveKnightBoss owner)
        {
            Owner=owner; DamageElement=ElementalAttack.RandomElement(); lastCast=long.MinValue; spellHit=false; lastHit=-100;
            (GetComponent<ElementalMinion>() ?? gameObject.AddComponent<ElementalMinion>()).SetElement(DamageElement,owner.GetComponent<HealthManager>().GetInstanceID());
        }
        public void Hit(HitInstance hit)
        {
            var runtime=CombatRuntime.Current;
            if(!runtime || !Owner || !hit.Source || hit.DamageDealt<=0) return;
            var control=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="Control");
            if(!control || (control.ActiveStateName!="Swarm" && control.ActiveStateName!="Spell Death")) return;
            AttackSource source; long cast;
            bool known=runtime.Casts.Resolve(hit,out source,out cast);
            bool spell=hit.AttackType==AttackTypes.Spell;
            if(spell && spellHit) return;
            if(known && lastCast==cast && (source!=AttackSource.Cyclone || runtime.Now-lastHit<.15)) return;
            if(!known && runtime.Now-lastHit<.15) return;
            lastCast=cast; lastHit=runtime.Now; if(spell) spellHit=true;
            Owner.BeeStruck(Mathf.Max(0,Mathf.RoundToInt(hit.DamageDealt*hit.Multiplier)),DamageElement);
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if(!other.CompareTag("Nail Attack") && !other.CompareTag("Hero Spell")) return;
            var damage=other.GetComponent<DamageEnemies>();
            if(damage)
            { Hit(new HitInstance {Source=other.gameObject,DamageDealt=damage.damageDealt,AttackType=damage.attackType,Multiplier=1}); return; }
            var fsm=other.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="damages_enemy");
            if(fsm)
                Hit(new HitInstance {Source=other.gameObject,DamageDealt=fsm.FsmVariables.GetFsmInt("damageDealt")?.Value ?? 0,
                    AttackType=(AttackTypes)(fsm.FsmVariables.GetFsmInt("attackType")?.Value ?? 2),Multiplier=1});
        }
    }
}
