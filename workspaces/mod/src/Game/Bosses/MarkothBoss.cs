using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class MarkothBoss : BossBehaviour
    {
        internal const double StopSeconds=3,CooldownSeconds=6;
        internal double StoppedUntil { get; private set; }
        internal double ReadyAt { get; private set; }
        internal int Clears { get; private set; }
        internal bool Stopped => Runtime && Runtime.Now<StoppedUntil;
        internal bool Bursting => ReadyAt>0 && Runtime && Runtime.Now>=StoppedUntil && Runtime.Now<StoppedUntil+1;
        internal void ShieldRoot(GameObject root)
        {
            if(!root.name.StartsWith("Markoth Shield")) return;
            var orbit=root.GetComponent<MarkothOrbit>() ?? root.AddComponent<MarkothOrbit>();orbit.Owner=this;
            if(!root.GetComponent<EnemyClock>()) root.AddComponent<EnemyClock>();
            foreach(var tink in root.GetComponentsInChildren<TinkEffect>(true))
                (tink.GetComponent<MarkothShield>() ?? tink.gameObject.AddComponent<MarkothShield>())
                    .Configure(this,tink.name=="Shield 2" ? Element.Hydro : Element.Pyro);
        }
        internal bool AmplifiedShield()
        {
            if(Runtime.Now<ReadyAt) return false;
            ReadyAt=Runtime.Now+CooldownSeconds;StoppedUntil=Runtime.Now+StopSeconds;Clears++;
            foreach(var marker in FindObjectsOfType<ElementalAttack>())
                if(marker.OwnerId==Health.GetInstanceID() && marker.name.StartsWith("Shot Markoth Nail")) marker.gameObject.Recycle();
            Trace("清除剑阵，盾停转3秒，随后加速1秒；6秒冷却");return true;
        }
        internal void Nail(GameObject obj)
        {
            if(!obj.name.StartsWith("Shot Markoth Nail")) return;
            var marker=obj.GetComponent<ElementalAttack>();if(marker && marker.Element!=Element.None) return;
            ElementalAttack.Set(obj,Random.value<.5f ? Element.Electro : Element.Cryo,Health.GetInstanceID());
        }
        internal override void Projectile(GameObject obj,FsmStateAction action) { if(obj.name.StartsWith("Markoth Shield")) ShieldRoot(obj);else Nail(obj); }
        private void Start()
        {
            foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if(fsm.gameObject.scene==gameObject.scene && fsm.name.StartsWith("Markoth Shield") && fsm.FsmName=="Control") ShieldRoot(fsm.gameObject);
        }
    }
    internal sealed class MarkothOrbit : MonoBehaviour
    {
        private float burstAngle;
        private Quaternion burstStart;
        private bool wasBurst;
        internal MarkothBoss Owner;
        internal float Scale => Owner && Owner.Stopped ? 0 : 1;
        private void LateUpdate()
        {
            if(Owner && Owner.Stopped)
            { var p=Owner.transform.position;transform.position=new Vector3(p.x,p.y,transform.position.z); }
            bool burst=Owner && Owner.Bursting;
            if(burst)
            {if(!wasBurst) {burstStart=transform.localRotation;burstAngle=0;}burstAngle+=720*Time.deltaTime;transform.localRotation=burstStart*Quaternion.Euler(0,0,burstAngle);}
            else burstAngle=0;
            wasBurst=burst;
        }
    }
    internal sealed class MarkothShield : MonoBehaviour,IHitResponder
    {
        internal MarkothBoss Owner;
        internal Element Element;
        internal TargetState State=new TargetState();
        internal void Configure(MarkothBoss owner,Element element)
        {
            if(Owner==owner && Element==element) return;
            Owner=owner;Element=element;ResetAura();
        }
        private void ResetAura()
        { State=new TargetState();State.SetIntrinsicAura(Element);if(Element!=Element.None) (GetComponent<ElementOutline>() ?? gameObject.AddComponent<ElementOutline>()).SetElement(Element); }
        private void OnEnable() { if(Owner) ResetAura(); }
        public void Hit(HitInstance hit)
        {
            var runtime=CombatRuntime.Current;if(!runtime || !Owner || hit.AttackType==AttackTypes.Spell || !hit.Source || hit.DamageDealt<=0) return;
            AttackSource source;long cast;if(!runtime.Casts.Resolve(hit,out source,out cast)) return;
            var result=runtime.EnemyRules.Prepare(State,new AttackContext {Source=source,CastId=cast,Element=runtime.AttackElement(source),Damage=hit.DamageDealt,
                BaseNailDamage=PlayerData.instance.nailDamage,TargetCanRecoil=false},runtime.Now,runtime.Mod.RuleModifier);
            State=result.NextState;State.FrozenUntil=0;State.SuperconductUntil=0;State.ElectroCharged=null;
            if(Reactions.Amplifying(result.Reaction)) Owner.AmplifiedShield();
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if(!other.CompareTag("Nail Attack")) return;
            var damage=other.GetComponent<DamageEnemies>();
            Hit(new HitInstance {Source=other.gameObject,AttackType=AttackTypes.Nail,Multiplier=1,DamageDealt=damage ? damage.damageDealt : PlayerData.instance.nailDamage});
        }
    }
}
