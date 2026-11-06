using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HollowElements.Rules;
using Newtonsoft.Json;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Only the weapon/trail pixels are overlaid. Native body textures and materials are borrowed unchanged.
    [DefaultExecutionOrder(12000)]
    internal sealed class NailBladeTint : MonoBehaviour
    {
        internal Element Element;
        internal string ResourceSet = "NailBlades";
        private Dictionary<string, Frame> currentFrames;
        private sealed class Frame { public float x { get; set; } public float y { get; set; } public float w { get; set; } public float h { get; set; } }
        private static readonly Dictionary<string, Dictionary<string, Frame>> frameSets = new Dictionary<string, Dictionary<string, Frame>>();
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private tk2dBaseSprite sprite;
        private MeshRenderer original, renderer;
        private Mesh mesh;
        private Material material;
        private string last;
        private void Awake()
        {
            sprite = GetComponent<tk2dBaseSprite>(); original = GetComponent<MeshRenderer>();
            var child = new GameObject("HollowElements.NailBladeTint"); child.transform.SetParent(transform, false); child.transform.localPosition = new Vector3(0,0,-.002f);
            mesh = new Mesh(); child.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = child.AddComponent<MeshRenderer>(); material = new Material(Shader.Find("Sprites/Default")); renderer.sharedMaterial = material;
        }
        private void LateUpdate()
        {
            if(currentFrames == null)
            {
                if(!frameSets.TryGetValue(ResourceSet,out currentFrames))
                {
                    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceSet+".frames.json"))
                    using(var reader=new StreamReader(stream)) currentFrames=JsonConvert.DeserializeObject<Dictionary<string,Frame>>(reader.ReadToEnd());
                    frameSets.Add(ResourceSet,currentFrames);
                }
            }
            string name = sprite.GetCurrentSpriteDef().name;
            string key = ResourceSet+":"+name;
            Frame frame;
            if (Element == Element.None || !currentFrames.TryGetValue(name, out frame)) { renderer.enabled = false; return; }
            if (last != name)
            {
                last = name; Texture2D texture;
                if (!textures.TryGetValue(key, out texture))
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceSet+"." + name + ".png"))
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes); texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
                        texture.name = "HollowElements.BladeMask"; texture.LoadImage(bytes.ToArray()); texture.wrapMode = TextureWrapMode.Clamp;
                        textures.Add(key, texture);
                    }
                }
                material.mainTexture = texture; mesh.Clear();
                mesh.vertices = new[] {new Vector3(frame.x,frame.y),new Vector3(frame.x+frame.w,frame.y),new Vector3(frame.x,frame.y+frame.h),new Vector3(frame.x+frame.w,frame.y+frame.h)};
                mesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.up,Vector2.one}; mesh.triangles = new[] {0,2,1,1,2,3};
                mesh.colors = new[] {Color.white,Color.white,Color.white,Color.white}; mesh.RecalculateBounds();
            }
            renderer.transform.localScale = new Vector3(sprite.FlipX ? -1 : 1,sprite.FlipY ? -1 : 1,1);
            Color colour = Color.Lerp(Color.white,Chinese.Colors[(int)Element],.65f); colour.a = sprite.color.a; material.color = colour;
            renderer.sortingLayerID = original.sortingLayerID; renderer.sortingOrder = original.sortingOrder+1;
            renderer.enabled = original.enabled && gameObject.activeInHierarchy;
        }
        internal void Restore() { if (renderer) renderer.enabled = false; }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { if(mesh) Destroy(mesh); if(material) Destroy(material); if(renderer) Destroy(renderer.gameObject); }
        internal static void Release() { foreach (var texture in textures.Values) if(texture) Destroy(texture); textures.Clear(); frameSets.Clear(); }
    }
}
