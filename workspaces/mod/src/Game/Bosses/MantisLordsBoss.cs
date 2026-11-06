using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class MantisLordsBoss : BossBehaviour
    {
        internal Element Element => name.EndsWith("S1") ? Element.Electro : name.EndsWith("S2") ? Element.Hydro : Element.Cryo;
        private void Awake() { var tint=gameObject.AddComponent<NailBladeTint>(); tint.ResourceSet="MantisBlades"; tint.Element=Element; }
        internal override Element AttackElement(GameObject source)
        {
            if(source!=gameObject) return Element;
            var fsm=GetComponent<PlayMakerFSM>(); var hero=HeroController.SilentInstance;
            return fsm && fsm.ActiveStateName=="Dstab Attack" && hero && hero.transform.position.y<transform.position.y-.4f ? Element : Element.None;
        }
        internal override void Projectile(GameObject projectile,FsmStateAction action)
        { if(projectile.name.StartsWith("Shot Mantis Lord")) ElementalAttack.Set(projectile,Element,Health.GetInstanceID(),tint:true,outline:false); }
    }
}
