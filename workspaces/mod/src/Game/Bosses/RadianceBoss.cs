using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class RadianceBoss : BossBehaviour
    {
        internal const double ColourSeconds=10;
        private Element colour;
        internal Element Followup { get; private set; }
        internal double ColouredUntil { get; private set; }
        internal Element Colour
        {
            get
            {
                if(colour!=Element.None && Runtime.Now>=ColouredUntil)
                {
                    colour=Element.None;Followup=Element.None;ColouredUntil=0;
                    GetComponent<EnemyAttackTint>()?.Restore();
                    EnemyRecord record;if(Runtime.Enemies.TryGetValue(Health,out record)) record.State.SetIntrinsicAura(Element.None);
                }
                return colour;
            }
        }
        internal override void SyncAura(TargetState state)
        { var value=Colour;if(value!=Element.None) state.SetIntrinsicAura(value);else if(state.IntrinsicAura!=Element.None) state.SetIntrinsicAura(Element.None); }
        internal override void DirectHitAccepted(Element element,Reaction reaction,int damage)
        {
            if(element==Element.None || damage<=0) return;
            if(Colour==Element.None)
            {
                colour=element;Followup=Element.None;ColouredUntil=Runtime.Now+ColourSeconds;
                (GetComponent<EnemyAttackTint>() ?? gameObject.AddComponent<EnemyAttackTint>()).Set(element,false);
                Record.State.SetIntrinsicAura(element);Trace("染色10秒 "+element);
            }
            if(Followup==Element.None && reaction!=Reaction.None)
            { Followup=element;Trace("记录首次反应后手 "+element); }
        }
        private void LateUpdate() { var unused=Colour; }
        internal void Attack(GameObject obj)
        {
            // Formation containers share the projectile prefix. Tint only the
            // actual damage actor, never a parent containing multiple swords.
            if(!obj || !obj.GetComponent<DamageHero>()) return;
            RadianceAttack.Kind kind;
            if(obj.name.StartsWith("Radiant Orb")) kind=RadianceAttack.Kind.Orb;
            else if(obj.name.StartsWith("Radiant Beam R")) kind=RadianceAttack.Kind.Wall;
            else if(obj.name.StartsWith("Radiant Nail") || obj.name.StartsWith("Radiant Beam") || obj.name=="Ascend Beam") kind=RadianceAttack.Kind.Reflected;
            else return;
            var attack=obj.GetComponent<RadianceAttack>() ?? obj.AddComponent<RadianceAttack>();attack.Bind(this,kind);
        }
        internal override void Projectile(GameObject obj,FsmStateAction action) { Attack(obj); }
        private void Start()
        {
            foreach(var damage in Resources.FindObjectsOfTypeAll<DamageHero>())
                if(damage.gameObject.scene==gameObject.scene) Attack(damage.gameObject);
        }
        internal static Element Prelude(GameObject source)
        {
            var attack=source ? source.GetComponentInParent<RadianceAttack>() : null;
            return attack && attack.Owner && attack.Type==RadianceAttack.Kind.Reflected && attack.Owner.Colour!=Element.None ? attack.Owner.Followup : Element.None;
        }
    }
    internal sealed class RadianceAttack : MonoBehaviour
    {
        internal enum Kind { Orb,Wall,Reflected }
        internal RadianceBoss Owner;
        internal Kind Type;
        private Element fixedElement,last;
        internal Element Element => Type==Kind.Reflected ? (Owner ? Owner.Colour : Element.None) : fixedElement;
        internal void Bind(RadianceBoss owner,Kind type)
        {
            if(Owner==owner && Type==type) return;
            Owner=owner;Type=type;Born();
        }
        private void Born()
        { fixedElement=Type==Kind.Orb ? ElementalAttack.RandomElement() : Type==Kind.Wall ? Element.Pyro : Element.None;last=(Element)(-1);Apply(); }
        private void OnEnable() { if(Owner) Born(); }
        private void Apply()
        {
            if(!Owner) return;var element=Element;
            if(last==element) return;last=element;
            ElementalAttack.Set(gameObject,element,Owner.GetComponent<HealthManager>().GetInstanceID(),tint:Type==Kind.Reflected,outline:Type==Kind.Orb);
        }
        private void LateUpdate() { Apply(); }
    }
}
