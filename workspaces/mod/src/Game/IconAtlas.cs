using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
namespace HollowElements.Game
{
    // Unity PackTextures layout exported as PNG + pixel rectangles for the game's Unity version.
    internal sealed class IconAtlas : IDisposable
    {
        internal sealed class Entry {public int x=0,y=0,width=0,height=0;}
        internal sealed class Layout {public Dictionary<string,Entry> sprites=new Dictionary<string,Entry>();}
        internal readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();
        private Texture2D texture;
        internal IconAtlas(string folder,string prefix)
        {
            try {
                texture=new Texture2D(2,2,TextureFormat.RGBA32,false) {name=prefix+"Atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                if(!texture.LoadImage(File.ReadAllBytes(Path.Combine(folder,"atlas.png")),true)) throw new InvalidDataException(folder);
                var layout=JsonConvert.DeserializeObject<Layout>(File.ReadAllText(Path.Combine(folder,"atlas.json")));
                foreach(var pair in layout.sprites) {
                    var r=pair.Value;
                    if(r.width<=0 || r.height<=0 || r.x<0 || r.y<0 || r.x+r.width>texture.width || r.y+r.height>texture.height) throw new InvalidDataException(pair.Key);
                    var sprite=Sprite.Create(texture,new Rect(r.x,r.y,r.width,r.height),new Vector2(.5f,.5f),100);
                    sprite.name=prefix+pair.Key;Sprites.Add(pair.Key,sprite);
                }
            } catch {Dispose();throw;}
        }
        public void Dispose() {foreach(var sprite in Sprites.Values) if(sprite) UnityEngine.Object.Destroy(sprite);Sprites.Clear();if(texture) UnityEngine.Object.Destroy(texture);texture=null;}
    }
}
