using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class PureVesselBoss : BossBehaviour
    {
        internal bool TendrilPending { get; private set; }
        internal int Replacements { get; private set; }
        private readonly ReactionAttackGate gate=new ReactionAttackGate();
        private static bool Antic(string name) => name=="SmallShot Antic" || name=="Slash1 Antic" || name=="Dstab Antic" || name=="Counter Antic" || name=="Dash Antic" || name=="Focus Charge" || name=="Tendril Antic";
        internal override void Entering(FsmState state) { if(state.Fsm.Name=="Control") gate.Enter(state.Name,Antic(state.Name)); }
        internal override void Reacted(Reaction reaction) { if(gate.Ready) TendrilPending=true; }
        internal override string Redirect(FsmState state)
        {
            if(state.Fsm.Name!="Control" || !TendrilPending) return null;
            string[] attacks={"SmallShot Antic","Slash1 Antic","Dstab Antic","Counter Antic","Dash Antic","Focus Charge","Tendril Antic"};
            if(!attacks.Contains(state.Name)) return null;
            TendrilPending=false;gate.Forced();Replacements++;Trace("下一招替换为虚空触手");
            return state.Name=="Tendril Antic" ? null : "Tendril Antic";
        }
        internal override Element AttackElement(GameObject source) => source.name=="Focus Hit" ? Element.Electro : Element.None;
        internal override void Projectile(GameObject obj,FsmStateAction action)
        {
            // Bind only the damage actor, never a sword-formation container.
            if(!obj) return;
            if(obj.name.StartsWith("Shot HK")) BindActor(obj,Element.Hydro);
            if(obj.name.StartsWith("HK Plume Prime")) BindActor(obj,Element.Pyro);
        }
        internal void Blast(GameObject obj)
        {
            if(obj.name.StartsWith("HK Prime Blast") || obj.name=="Focus Hit") BindActor(obj,Element.Electro);
            if(obj.name=="Focus Hit") {var visual=transform.Find("Focus Blast");if(visual) BindActor(visual.gameObject,Element.Electro);}
        }
        private void BindActor(GameObject actor,Element element)
        {
            // Damage and visible graphics are siblings in these native prefabs.
            // Give the collider its aura, but tint each actual drawable separately.
            foreach(var damage in actor.GetComponentsInChildren<DamageHero>(true))
                EncounterAttack.Bind(damage.gameObject,this,element,outline:false);
            foreach(var sprite in actor.GetComponentsInChildren<tk2dBaseSprite>(true))
                EncounterAttack.Bind(sprite.gameObject,this,element,tint:true,outline:false);
            foreach(var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true))
                EncounterAttack.Bind(sprite.gameObject,this,element,tint:true,outline:false);
        }
        private void Start()
        {
            foreach(var damage in Resources.FindObjectsOfTypeAll<DamageHero>())
                if(damage.gameObject.scene==gameObject.scene)
                    for(var t=damage.transform;t;t=t.parent) if(t.name.StartsWith("HK Prime Blast") || t.name=="Focus Hit") { Blast(t.gameObject);break; }
        }
    }
}
