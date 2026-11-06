using System;
using System.IO;
using System.Reflection;
using HollowElements.Rules;
using UnityEngine;
namespace HollowElements.Game
{
    internal sealed class CharmIconAssets : IDisposable
    {
        private IconAtlas atlas;
        internal Sprite Get(CharmId id)
        {
            if(id==CharmId.None) return null;
            if(atlas==null) atlas=new IconAtlas(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Assets","Charms"),"HollowElements.CharmIcon.");
            Sprite sprite;return atlas.Sprites.TryGetValue(id.ToString(),out sprite)?sprite:null;
        }
        public void Dispose() {atlas?.Dispose();atlas=null;}
    }
}
