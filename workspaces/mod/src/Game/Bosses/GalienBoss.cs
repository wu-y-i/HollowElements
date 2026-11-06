using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class GalienBoss : BossBehaviour
    {
        private void Start()
        {
            foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if(fsm.gameObject.scene==gameObject.scene && fsm.name=="Galien Hammer") Gear(fsm.gameObject);
        }
        internal void Gear(GameObject obj)
        {
            bool small=obj.name.StartsWith("Galien Mini Hammer");
            if(obj.GetComponent<EncounterAttack>()) return;
            EncounterAttack.Bind(obj,this,small ? Element.Pyro : Element.Electro,tint:small,outline:!small,followFrost:true);
        }
        internal override void Projectile(GameObject obj,FsmStateAction action)
        { if(obj.name.StartsWith("Galien Mini Hammer")) Gear(obj); }
    }
}
