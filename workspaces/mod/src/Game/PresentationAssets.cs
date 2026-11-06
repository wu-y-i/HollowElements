using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace HollowElements.Game
{
    internal sealed class FxAsset
    {
        public Sprite[] Frames;
        public float Fps, WorldSize;
        public bool Loop;
    }
    internal sealed class PresentationAssets : IDisposable
    {
        internal readonly Dictionary<string, FxAsset> Effects = new Dictionary<string, FxAsset>();
        internal readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        internal readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
        internal TMP_FontAsset Font, OutlineFont;
        internal Material OutlineMaterial;
        internal ElementGuideFonts SdfFonts;
        private IconAtlas iconAtlas;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal Sprite[][] LoadoutIcons()
        {
            // Inventory Sprite assets are already loaded with the hero. Borrow them;
            // never clone or destroy the game's textures/materials.
            var available = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>()) available[sprite.name] = sprite;
            var player = PlayerData.instance;
            string[] nails = { "Inv_0033_inv_nail_01", "nail_upgrade_0002_sharpened_nail", "nail_upgrade_0002_channel-nail 2", "nail_upgrade_03_coil_nail", "nail_upgrade_0000_pure-nail" };
            string[] names = {
                nails[Mathf.Clamp(player.nailSmithUpgrades, 0, 4)],
                "Inv_0025_spell_fireball_01", "Inv_0026_spell_quake_01", "Inv_0024_spell_scream_01",
                "Inv_0022_inv_uppercut_skill", "Inv_0021_inv_dash_strike_skill", "Inv_0023_inv_whirlwind_skill"
            };
            int[] levels={1,player.fireballLevel,player.quakeLevel,player.screamLevel,1,1,1};
            var result = new Sprite[names.Length][];
            for (int i = 0; i < names.Length; i++)
            {
                result[i]=new Sprite[i>=1 && i<=3 && levels[i]>1 ? 2 : 1];
                available.TryGetValue(names[i], out result[i][0]);
                if(result[i].Length==2) available.TryGetValue(names[i]+"_level2",out result[i][1]);
            }
            return result;
        }
        internal tk2dSpriteDefinition[] SwordArtIcons()
        {
            var result=new tk2dSpriteDefinition[3];
            string[] frames={"charge_slash_effect0002","dash_slash_effect0002","cyclone_slash_effect0004"};
            foreach(var collection in Resources.FindObjectsOfTypeAll<tk2dSpriteCollectionData>())
                if(collection.name=="Knight")
                    foreach(var definition in collection.inst.spriteDefinitions)
                        for(int i=0;i<frames.Length;i++) if(definition.name==frames[i]) result[i]=definition;
            return result;
        }
        internal static bool IsUnlocked(Rules.AttackSlot slot)
        {
            var player=PlayerData.instance;
            switch(slot)
            {
                case Rules.AttackSlot.Wave: return player.fireballLevel>0;
                case Rules.AttackSlot.Dive: return player.quakeLevel>0;
                case Rules.AttackSlot.Shriek: return player.screamLevel>0;
                case Rules.AttackSlot.GreatSlash: return player.hasUpwardSlash;
                case Rules.AttackSlot.DashSlash: return player.hasDashSlash;
                case Rules.AttackSlot.Cyclone: return player.hasCyclone;
                default: return true;
            }
        }
        internal void Load()
        {
            string directory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assets");
            foreach (string path in System.IO.Directory.GetDirectories(Path.Combine(directory, "Effects")))
            {
                var manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(Path.Combine(path, "manifest.json")));
                Texture2D atlas = Texture(Path.Combine(path, "atlas.png"));
                var asset = new FxAsset { Fps = manifest.fps, WorldSize = manifest.worldSize, Loop = manifest.loop, Frames = new Sprite[manifest.frames] };
                foreach (var sample in manifest.samples)
                {
                    var rect = new Rect(sample.x, atlas.height - sample.y - sample.height, sample.width, sample.height);
                    var sprite = Sprite.Create(atlas, rect, new Vector2(.5f, .5f), manifest.width / manifest.worldSize);
                    sprite.name=manifest.name+"_"+sample.index;
                    asset.Frames[sample.index] = sprite; owned.Add(sprite);
                }
                Effects.Add(manifest.name, asset);
            }
            iconAtlas=new IconAtlas(Path.Combine(directory,"Icons"),"HollowElements.ElementIcon.");
            foreach(var pair in iconAtlas.Sprites) Icons.Add(pair.Key,pair.Value);
            foreach (var path in System.IO.Directory.GetFiles(Path.Combine(directory, "Audio"), "*.wav"))
                Sounds.Add(Path.GetFileNameWithoutExtension(path).Replace("_01", ""), Wav(path));
            SdfFonts=new ElementGuideFonts(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"ElementGuide"));
            Font=SdfFonts.Primary;
            SdfFonts.Ensure("冻结结霜感电超导超载碎冰蒸发融化易伤加速分裂强化脆弱冲刺0123456789/.");
            OutlineFont=Font;
            var outline=new Material(Font.material);owned.Add(outline);outline.name="HollowElements.ElementGuideWhiteOutline.Material";
            outline.SetFloat("_OutlineWidth",.18f);outline.SetFloat("_OutlineSoftness",0);outline.SetColor("_OutlineColor",Color.white);
            OutlineMaterial=outline;
        }

        private Texture2D Texture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileName(path), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException("无法加载纹理：" + path);
            owned.Add(texture); return texture;
        }
        private AudioClip Wav(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException(path);
                reader.ReadInt32(); if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException(path);
                int channels = 0, rate = 0, bits = 0, format = 0; byte[] bytes = null;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string id = new string(reader.ReadChars(4)); int length = reader.ReadInt32(); long end = reader.BaseStream.Position + length;
                    if (length < 0 || end > reader.BaseStream.Length) throw new InvalidDataException(path);
                    if (id == "fmt ") { format = reader.ReadInt16(); channels = reader.ReadInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16(); }
                    else if (id == "data") bytes = reader.ReadBytes(length);
                    reader.BaseStream.Position = end + (length & 1);
                }
                if (format != 1 || bits != 16 || channels < 1 || rate < 8000 || bytes == null) throw new InvalidDataException("需要16位PCM WAV：" + path);
                var samples = new float[bytes.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
                var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), samples.Length / channels, channels, rate, false);
                clip.SetData(samples, 0); owned.Add(clip); return clip;
            }
        }
        public void Dispose() { iconAtlas?.Dispose();iconAtlas=null;foreach (var item in owned) if (item) UnityEngine.Object.Destroy(item); owned.Clear(); SdfFonts?.Dispose(); }
        private sealed class Manifest
        {
            public string name = ""; public int width = 256, frames = 0; public float fps = 30, worldSize = 3; public bool loop = false;
            public Sample[] samples = Array.Empty<Sample>();
        }
        private sealed class Sample { public int index = 0, x = 0, y = 0, width = 256, height = 256; }
    }
}
