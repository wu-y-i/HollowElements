using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace HollowElements.Game
{
    // All pages use the supplied Genshin font. Common element_guide characters share the
    // first atlas; other BMP glyphs load on demand, using the same SDF metrics.
    internal sealed class ElementGuideFonts : IDisposable
    {
        internal TMP_FontAsset Primary { get; private set; }
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly HashSet<char> seen = new HashSet<char>();
        private readonly Dictionary<int, int> pages = new Dictionary<int, int>();
        private readonly HashSet<int> loaded = new HashSet<int>();
        private readonly string directory;
        private readonly FontBook book;
        private readonly Hook submeshHook;
        private readonly Hook materialHook;
        private readonly Dictionary<long,Material> fallbackMaterials=new Dictionary<long,Material>();
        private delegate Material OriginalFallback(Material source,Material target);
        private delegate void OriginalEnable(TMP_SubMeshUI self);
        internal ElementGuideFonts(string root)
        {
            directory = Path.Combine(root, "Font");
            book = JsonConvert.DeserializeObject<FontBook>(File.ReadAllText(Path.Combine(directory, "font.json")));
            if(book == null || book.pages == null || book.pages.Length == 0) throw new InvalidDataException("图鉴后备字体无效。");
            for(int i=0;i<book.pages.Length;i++) foreach(var glyph in book.pages[i].glyphs) pages[glyph.id]=i;
            // HK's old TMP creates fallback submeshes without CanvasRenderer. Add
            // it before OnEnable, scoped strictly to our document text.
            submeshHook=new Hook(typeof(TMP_SubMeshUI).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic),new Action<OriginalEnable,TMP_SubMeshUI>((original,self)=>{
                var label=self.GetComponentInParent<TextMeshProUGUI>();
                if(label && label.font && label.font.name.StartsWith("HollowElements.ElementGuide"))
                {
                    if(label.canvas) label.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
                    if(!self.GetComponent<CanvasRenderer>()) self.gameObject.AddComponent<CanvasRenderer>();
                }
                original(self);
            }));
            materialHook=new Hook(typeof(TMP_MaterialManager).GetMethod("GetFallbackMaterial",BindingFlags.Public|BindingFlags.Static),new Func<OriginalFallback,Material,Material,Material>((original,source,target)=>{
                if(!source || !target || !source.name.StartsWith("HollowElements.ElementGuide")) return original(source,target);
                long key=((long)source.GetInstanceID()<<32)|(uint)target.mainTexture.GetInstanceID();
                Material match;
                if(fallbackMaterials.TryGetValue(key,out match) && match) return match;
                match=new Material(source) {name="HollowElements.ElementGuideFallback.Material",mainTexture=target.mainTexture};
                foreach(string property in new[]{"_TextureWidth","_TextureHeight","_GradientScale","_WeightNormal","_WeightBold"}) match.SetFloat(property,target.GetFloat(property));
                owned.Add(match);fallbackMaterials[key]=match;return match;
            }));
            try {Primary=Load(0);Primary.name="HollowElements.ElementGuideGenshin";Primary.ReadFontDefinition();}
            catch {Dispose();throw;}
        }
        internal bool Supports(char value) => Primary && (Primary.HasCharacter(value) || Primary.fallbackFontAssets.Any(f=>f.HasCharacter(value)));
        internal void Ensure(string text)
        {
            foreach(char ch in text)
            {
                if(char.IsWhiteSpace(ch) || !seen.Add(ch) || Supports(ch)) continue;
                int page;if(!pages.TryGetValue(ch,out page) || loaded.Contains(page)) continue;
                var fallback=Load(page);Primary.fallbackFontAssets.Add(fallback);
            }
        }
        // Prepare directory glyphs before the inventory opens. At most one atlas
        // is decoded per frame, using the unscaled coroutine scheduler.
        internal IEnumerator Warm(string text)
        {
            foreach (int page in text.Select(ch => pages.TryGetValue(ch, out int index) ? index : -1).Where(i => i >= 0).Distinct())
            {
                if (loaded.Contains(page)) continue;
                var fallback = Load(page); Primary.fallbackFontAssets.Add(fallback);
                yield return null;
            }
        }
        internal int LoadedPages => loaded.Count;
        private TMP_FontAsset Load(int index)
        {
            var page=book.pages[index];
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!source.LoadImage(File.ReadAllBytes(Path.Combine(directory,page.file)))) throw new InvalidDataException("图鉴字库纹理无效。");
                var pixels=source.GetPixels32();var alpha=new byte[pixels.Length];for(int i=0;i<pixels.Length;i++) alpha[i]=pixels[i].a;
                var atlas=new Texture2D(source.width,source.height,TextureFormat.Alpha8,false) {name="HollowElements.ElementGuideSdfAtlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(atlas);
                atlas.LoadRawTextureData(alpha);atlas.Apply(false,true);
                var font=ScriptableObject.CreateInstance<TMP_FontAsset>();owned.Add(font);font.name="HollowElements.ElementGuideSdfPage."+index;
                font.fontAssetType=TMP_FontAsset.FontAssetTypes.SDF;font.atlas=atlas;
                var material=new Material(Shader.Find("TextMeshPro/Distance Field"));owned.Add(material);material.name=font.name+".Material";material.mainTexture=atlas;
                material.SetFloat("_TextureWidth",atlas.width);material.SetFloat("_TextureHeight",atlas.height);material.SetFloat("_GradientScale",book.padding+1);
                material.SetFloat("_FaceDilate",0);material.SetFloat("_OutlineSoftness",0);material.EnableKeyword("RATIOS_ON");
                material.SetFloat("_WeightNormal",0);material.SetFloat("_WeightBold",.75f);material.SetFloat("_ScaleX",1);material.SetFloat("_ScaleY",1);material.SetColor("_FaceColor",Color.white);
                var face=JsonConvert.DeserializeObject<FaceInfo>(JsonConvert.SerializeObject(book.face));face.AtlasWidth=atlas.width;face.AtlasHeight=atlas.height;face.CharacterCount=page.glyphs.Length;
                font.material=material;font.AddFaceInfo(face);font.AddGlyphInfo(page.glyphs);font.AddKerningInfo(new KerningTable());font.fallbackFontAssets=new List<TMP_FontAsset>();font.ReadFontDefinition();loaded.Add(index);return font;
            }
            finally {UnityEngine.Object.Destroy(source);}
        }
        public void Dispose()
        {
            submeshHook?.Dispose();materialHook?.Dispose();fallbackMaterials.Clear();
            // The game's older TMP can retain a cloned font material after all
            // labels are destroyed. Release only clones using our owned atlases.
            var atlases=owned.OfType<Texture2D>().ToArray();
            foreach(var material in Resources.FindObjectsOfTypeAll<Material>())
                if(material && material.name.StartsWith("HollowElements.ElementGuide") && !owned.Contains(material) && atlases.Any(a=>a && material.mainTexture==a))
                    UnityEngine.Object.Destroy(material);
            foreach(var item in owned) if(item) UnityEngine.Object.Destroy(item);owned.Clear();
        }
        private sealed class FontBook { public FaceInfo face=new FaceInfo(); public int padding=8; public Page[] pages=Array.Empty<Page>(); }
        private sealed class Page { public string file=""; public TMP_Glyph[] glyphs=Array.Empty<TMP_Glyph>(); }
    }
}
