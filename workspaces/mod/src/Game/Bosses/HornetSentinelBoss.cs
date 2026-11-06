using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class HornetSentinelBoss : HornetProtectorBoss
    {
        internal void Barb(GameObject obj)
        {
            if(!obj.name.StartsWith("Hornet Barb")) return;
            var marker=obj.GetComponent<ElementalAttack>();if(marker && marker.Element!=Element.None) return;
            ElementalAttack.Set(obj,ElementalAttack.RandomElement(),GetComponent<HealthManager>().GetInstanceID(),outline:false);
            (obj.GetComponent<BarbElementRing>() ?? obj.AddComponent<BarbElementRing>()).Initialize();
        }
        internal override void Projectile(GameObject projectile,FsmStateAction action) { Barb(projectile); }
    }
    // Barbs use a stable circular contour rather than expanding their spiky mesh
    // against atlas UVs. No texture readback or per-frame mask uploads are needed.
    internal sealed class BarbElementRing : MonoBehaviour
    {
        private LineRenderer line;
        private Material material;
        private Renderer source;
        internal void Initialize()
        {
            if(line && material) return;
            source=GetComponentInChildren<MeshRenderer>(true);
            var existing=transform.Find("HollowElements.BarbRing");
            var child=existing ? existing.gameObject : new GameObject("HollowElements.BarbRing");child.transform.SetParent(transform,false);
            line=child.GetComponent<LineRenderer>() ?? child.AddComponent<LineRenderer>();material=new Material(Shader.Find("Sprites/Default"));
            line.sharedMaterial=material;line.useWorldSpace=false;line.loop=true;line.positionCount=32;line.startWidth=line.endWidth=.055f;
            float radius=.65f;
            var sprite=GetComponentInChildren<tk2dBaseSprite>(true);
            if(sprite) {var bounds=sprite.GetCurrentSpriteDef().boundsData;radius=Mathf.Clamp(Mathf.Max(bounds[1].x,bounds[1].y)*.55f,.3f,1.2f);}
            for(int i=0;i<32;i++) {float angle=i*Mathf.PI/16;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,-.01f));}
        }
        private void LateUpdate()
        {
            if(!line) return;var attack=GetComponent<ElementalAttack>();
            line.enabled=attack && attack.Element!=Element.None && source && source.enabled;
            if(!line.enabled) return;
            line.sortingLayerID=source.sortingLayerID;line.sortingOrder=source.sortingOrder+1;
            var colour=Chinese.Colors[(int)attack.Element];line.startColor=line.endColor=colour;
        }
        private void OnDisable() {if(line) line.enabled=false;}
        private void OnDestroy() {if(line) Destroy(line.gameObject);if(material) Destroy(material);}
    }
}
