using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class SlyBoss : BossBehaviour
    {
        internal AttackSlot CurrentSlot { get; private set; }=AttackSlot.Nail;
        private NailBladeTint blade;
        internal Element ElementFor(AttackSlot slot) => Runtime.Mod.OnSaveLocal().Get(slot);
        private void Start()
        {
            blade=gameObject.AddComponent<NailBladeTint>();blade.ResourceSet="SlyTrails";
            foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if(fsm.gameObject.scene==gameObject.scene && (fsm.name=="Spin Tink" || fsm.name=="Cyclone Tink" || fsm.name=="Shockwave")) Link(fsm.gameObject);
        }
        internal void Link(GameObject obj)
        {
            var link=obj.GetComponent<SlyAttack>() ?? obj.AddComponent<SlyAttack>();link.Owner=this;
            link.Slot=obj.name=="Spin Tink" || obj.name=="Cyclone Tink" ? AttackSlot.Cyclone : AttackSlot.GreatSlash;
        }
        internal override void Entering(FsmState state)
        {
            if(state.Fsm.Name!="Control") return;
            string name=state.Name;
            if(name.StartsWith("GSlash")) CurrentSlot=AttackSlot.GreatSlash;
            else if(name.StartsWith("Dash Charge") || name=="Dash Charged" || name.StartsWith("D Slash") || name.StartsWith("DSlash")) CurrentSlot=AttackSlot.DashSlash;
            else if(name.StartsWith("Cyc") || name.StartsWith("Spin Slash") || name.StartsWith("Rage Slash")) CurrentSlot=AttackSlot.Cyclone;
            else if(name.StartsWith("Slash S") || name.StartsWith("BSlash") || name.StartsWith("Stomp") || name.StartsWith("Dstab")) CurrentSlot=AttackSlot.Nail;
        }
        internal override Element AttackElement(GameObject source) => source==gameObject ? Element.None : ElementFor(CurrentSlot);
        private void LateUpdate()
        {
            if(blade) blade.Element=ElementFor(CurrentSlot);
            foreach(string name in new[]{"NA Charge","NA Charged","Sharp Flash","GSlash Effect","DSlash Effect","Slam Effect","Shockwave"})
            {
                var child=transform.Find(name);if(!child) continue;
                var element=ElementFor(name=="GSlash Effect" || name=="Shockwave" ? AttackSlot.GreatSlash : name=="DSlash Effect" ? AttackSlot.DashSlash : CurrentSlot);
                var tint=child.GetComponent<EnemyAttackTint>() ?? child.gameObject.AddComponent<EnemyAttackTint>();
                if(tint.Element!=element) tint.Set(element);
            }
        }
    }
    internal sealed class SlyAttack : MonoBehaviour
    { internal SlyBoss Owner;internal AttackSlot Slot; }
}
