using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class GreyPrinceBoss : BossBehaviour
    {
        internal override void Projectile(GameObject obj,FsmStateAction action)
        {
            if(obj.name.StartsWith("Shockwave Wave Z"))
            {
                var wave=obj.GetComponent<ZoteWave>() ?? obj.AddComponent<ZoteWave>();wave.Owner=this;wave.ScaleChanged();
            }
        }
        internal void Explosion(GameObject obj)
        { if(obj.name.StartsWith("Gas Explosion M")) ElementalAttack.Set(obj,Element.Pyro,Health.GetInstanceID(),outline:false); }
    }
    internal sealed class ZoteWave : MonoBehaviour
    {
        internal GreyPrinceBoss Owner;
        internal void ScaleChanged()
        {
            if(!Owner) return;
            // The native emitter stores spurt height in its X scale (1.2–1.6 vs 2.3–2.6).
            var element=Mathf.Abs(transform.localScale.x)>=2 ? Element.Hydro : Element.Electro;
            ElementalAttack.Set(gameObject,element,Owner.GetComponent<HealthManager>().GetInstanceID(),tint:true,outline:false);
        }
    }
}
