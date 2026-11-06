using System.Collections.Generic;
using System.Linq;
using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game
{
    internal sealed partial class CombatPresentation
    {
        private RectTransform charmMasks;
        private tk2dSpriteDefinition nativeMask;
        internal void CharmExplosion(Vector3 position,float scale)
        {
            var effect=Play("overload",position,false,null);if(effect!=null) effect.ExtraScale=scale;
            Sound("overload");
        }
        internal void CharmSpark(Vector3 position,Vector3 velocity)
        {
            var obj=new GameObject("HollowElements.CharmSpark");obj.transform.SetParent(transform,false);
            obj.transform.position=position;obj.transform.localScale=new Vector3(.16f,.32f,1);
            var sprite=obj.AddComponent<SpriteRenderer>();sprite.sprite=ring;sprite.color=new Color(1,.5f,.15f,.9f);sprite.sortingOrder=61;
            var motion=obj.AddComponent<CharmSparkMotion>();motion.Position=position;motion.Velocity=velocity;motion.Started=Time.time;
        }
        private void AddCharmIcons(List<IconEntry> entries,double now)
        {
            var charms=Runtime.Charms;
            if(charms.Has(CharmId.C01)) entries.Add(new IconEntry {Element=Runtime.NextNailElement,Icon=CharmIcons.Get(CharmId.C01),Label="",Fraction=1});
            if(charms.FourUntil>now) entries.Add(new IconEntry {Element=Element.Pyro,Label="",Icon=CharmIcons.Get(CharmId.C06),Stacks=charms.Bonuses.Sum(),Fraction=(float)((charms.FourUntil-now)/10)});
            if(charms.ScarUntil>now) entries.Add(new IconEntry {Element=Element.Electro,Label="",Icon=CharmIcons.Get(CharmId.C21),Fraction=(float)((charms.ScarUntil-now)/10)});
            if(charms.SpellCharges>0) entries.Add(new IconEntry {Element=Element.Hydro,Label="",Icon=CharmIcons.Get(CharmId.C31),Stacks=charms.SpellCharges,Fraction=1});
            if(charms.InsulatedUntil>now) entries.Add(new IconEntry {Element=Element.Cryo,Label="",Icon=CharmIcons.Get(CharmId.C03),Fraction=(float)((charms.InsulatedUntil-now)/12)});
            if(charms.VeilUntil>now) entries.Add(new IconEntry {Element=Element.Cryo,Label="",Icon=CharmIcons.Get(CharmId.C39),Fraction=(float)((charms.VeilUntil-now)/8)});
        }
        private void UpdateCharmMasks()
        {
            bool active=HeroController.SilentInstance && Runtime.Charms.Masks>0;
            if(!active) {if(charmMasks) charmMasks.gameObject.SetActive(false);return;}
            if(!charmMasks)
            {
                var full=Resources.FindObjectsOfTypeAll<tk2dSprite>().FirstOrDefault(s=>s.name=="Full" && s.transform.parent && s.transform.parent.name.StartsWith("Health"));
                if(full) nativeMask=full.GetCurrentSpriteDef();
                charmMasks=Rect("HollowElements.CharmMasks",CanvasRoot,Vector2.zero,new Vector2(110,50),new Vector2(0,1));
                for(int i=0;i<2;i++)
                {
                    var rect=Rect("临时面具",charmMasks,new Vector2((i-.5f)*52,0),new Vector2(48,48));
                    if(nativeMask!=null) {var graphic=rect.gameObject.AddComponent<NativeSlashGraphic>();graphic.Definition=nativeMask;graphic.color=new Color(.55f,.85f,1);graphic.raycastTarget=false;}
                    else {var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=CharmIcons.Get(CharmId.C40);image.preserveAspect=true;image.raycastTarget=false;}
                }
            }
            charmMasks.gameObject.SetActive(true);charmMasks.anchoredPosition=playerIcons.anchoredPosition+new Vector2(0,-74);
            for(int i=0;i<2;i++) charmMasks.GetChild(i).gameObject.SetActive(i<Runtime.Charms.Masks);
        }
    }
    internal sealed class CharmSparkMotion : MonoBehaviour
    {
        internal Vector3 Position,Velocity;internal float Started;
        private void Update()
        {
            float t=Time.time-Started;if(t>=1) {Destroy(gameObject);return;}
            transform.position=Position+Velocity*t+Vector3.down*(3.5f*t*t);
            transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(Velocity.y-7*t,Velocity.x)*Mathf.Rad2Deg-90);
        }
    }
}
