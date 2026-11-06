using System;
using System.Collections.Generic;
using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Compatible overlay: cache each animated frame's alpha contour, then move a highlight around it.
    // Uses the game's Sprite shader; no recolouring, shared-material edits or new Editor dependency.
    [DefaultExecutionOrder(11000)]
    internal sealed class ElementOutline : MonoBehaviour
    {
        private sealed class Mask
        {
            internal Texture2D Texture;
            internal Color32[] Pixels;
            internal readonly List<int> Indices = new List<int>();
            internal readonly List<float> Angles = new List<float>();
            internal readonly List<byte> Alpha = new List<byte>();
            internal float LastUpdate = -1;
            internal int Width, Height;
        }
        private static readonly Dictionary<string, Mask> cache = new Dictionary<string, Mask>();
        private tk2dBaseSprite sprite;
        private MeshFilter sourceMesh, mesh;
        private MeshRenderer sourceRenderer, overlay;
        private Mesh ownedMesh;
        private Material material;
        private Element element;
        private int lastSprite = -1, lastCollection;
        private Mask mask;
        internal void SetElement(Element value) { if(value!=element) {mask=null;lastSprite=-1;}element = value; if (value == Element.None) Restore(); }
        private void LateUpdate()
        {
            if (element == Element.None) return;
            if (!sprite)
            {
                sprite = GetComponent<tk2dBaseSprite>(); sourceMesh = GetComponent<MeshFilter>(); sourceRenderer = GetComponent<MeshRenderer>();
                if (!sprite || !sourceMesh || !sourceRenderer) return;
            }
            if (!overlay)
            {
                var child = new GameObject("HollowElements.ElementOutline"); child.transform.SetParent(transform, false);
                child.transform.localPosition = new Vector3(0, 0, -.001f);
                mesh = child.AddComponent<MeshFilter>(); overlay = child.AddComponent<MeshRenderer>();
                material = new Material(Shader.Find("Sprites/Default")); overlay.sharedMaterial = material;
                ownedMesh = new Mesh(); mesh.sharedMesh = ownedMesh;
            }
            var def = sprite.GetCurrentSpriteDef();
            if (sprite.spriteId != lastSprite || sprite.Collection.GetInstanceID() != lastCollection || mask == null)
            {
                lastSprite = sprite.spriteId; lastCollection = sprite.Collection.GetInstanceID();
                string key = lastCollection + ":" + lastSprite + ":" + element;
                Vector2 min = def.uvs[0], max = def.uvs[0];
                foreach (var uv in def.uvs) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
                if (!cache.TryGetValue(key, out mask))
                {
                    mask = CreateMask(sourceRenderer.sharedMaterial.mainTexture, min, max);
                    cache.Add(key, mask);
                }
                var source = sourceMesh.sharedMesh; var bounds = source.bounds;
                Vector3[] vertices = source.vertices; Vector2[] uvs = source.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].x += Mathf.Sign(vertices[i].x-bounds.center.x)*bounds.size.x*2/mask.Width;
                    vertices[i].y += Mathf.Sign(vertices[i].y-bounds.center.y)*bounds.size.y*2/mask.Height;
                    // The source UVs can be rotated in the atlas: normalization preserves that rotation.
                    float u = (uvs[i].x-min.x)/(max.x-min.x), v = (uvs[i].y-min.y)/(max.y-min.y);
                    uvs[i] = new Vector2((u*mask.Width+2+Mathf.Sign(u-.5f)*2)/(mask.Width+4),
                        (v*mask.Height+2+Mathf.Sign(v-.5f)*2)/(mask.Height+4));
                }
                ownedMesh.Clear(); ownedMesh.vertices = vertices; ownedMesh.uv = uvs; ownedMesh.triangles = source.triangles;
                var white = new Color[vertices.Length]; for (int i=0;i<white.Length;i++) white[i]=Color.white;
                ownedMesh.colors=white; ownedMesh.RecalculateBounds(); material.mainTexture = mask.Texture;
            }
            overlay.enabled = sourceRenderer.enabled && gameObject.activeInHierarchy;
            overlay.sortingLayerID = sourceRenderer.sortingLayerID; overlay.sortingOrder = sourceRenderer.sortingOrder + 1;
            material.color = new Color(1,1,1,sprite.color.a);
            if (Time.time-mask.LastUpdate < .05f) return;
            mask.LastUpdate = Time.time;
            Color color = Chinese.Colors[(int)element];
            for (int i=0;i<mask.Indices.Count;i++)
            {
                float phase = Mathf.Repeat(mask.Angles[i]-Time.time*.65f,1);
                Color c = Color.Lerp(color,Color.white,Mathf.Pow(1-phase,8)*.7f);
                c.a = mask.Alpha[i]/255f; mask.Pixels[mask.Indices[i]] = c;
            }
            mask.Texture.SetPixels32(mask.Pixels); mask.Texture.Apply(false,false);
        }
        private static Mask CreateMask(Texture texture, Vector2 min, Vector2 max)
        {
            int w = Math.Max(1,Mathf.RoundToInt((max.x-min.x)*texture.width)), h = Math.Max(1,Mathf.RoundToInt((max.y-min.y)*texture.height));
            // Bound readback, dilation and uploads for large animated gears.
            float scale=Mathf.Min(1,128f/Mathf.Max(w,h));
            w=Math.Max(1,Mathf.RoundToInt(w*scale));h=Math.Max(1,Mathf.RoundToInt(h*scale));
            var rt = RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
            var crop = new Texture2D(w,h,TextureFormat.RGBA32,false);
            Color32[] source;
            try
            {
                Graphics.Blit(texture,rt,max-min,min); RenderTexture.active=rt;
                crop.ReadPixels(new Rect(0,0,w,h),0,0); crop.Apply(); source=crop.GetPixels32();
            }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Destroy(crop); }
            var result = new Mask { Width=w,Height=h,Pixels=new Color32[(w+4)*(h+4)],Texture=new Texture2D(w+4,h+4,TextureFormat.RGBA32,false) };
            result.Texture.name="HollowElements.ContourMask"; result.Texture.wrapMode=TextureWrapMode.Clamp; result.Texture.filterMode=FilterMode.Bilinear;
            for(int y=-2;y<h+2;y++) for(int x=-2;x<w+2;x++)
            {
                int alpha=x>=0 && x<w && y>=0 && y<h ? source[y*w+x].a : 0, near=0;
                for(int dy=-2;dy<=2;dy++) for(int dx=-2;dx<=2;dx++)
                {
                    if(dx*dx+dy*dy>5) continue;
                    int sx=x+dx,sy=y+dy;
                    if(sx>=0 && sx<w && sy>=0 && sy<h) near=Math.Max(near,source[sy*w+sx].a);
                }
                if(near<=alpha) continue;
                result.Indices.Add((y+2)*(w+4)+x+2); result.Alpha.Add((byte)(near-alpha));
                result.Angles.Add(Mathf.Atan2(y-h*.5f,x-w*.5f)/(2*Mathf.PI));
            }
            return result;
        }
        internal void Restore() { element=Element.None; mask=null; lastSprite=-1; if(overlay) overlay.enabled=false; }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { if(material) Destroy(material); if(ownedMesh) Destroy(ownedMesh); if(overlay) Destroy(overlay.gameObject); }
        internal static void ReleaseShader() { foreach(var mask in cache.Values) if(mask.Texture) Destroy(mask.Texture); cache.Clear(); }
    }
}
