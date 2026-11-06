using System;
using System.Collections.Generic;
using System.Linq;
namespace HollowElements.Settings
{
    public sealed class BossHintEntry
    {
        public string Id="",Name="",Text="";
        public bool Enabled;
        public string[] Scenes=Array.Empty<string>();
    }
    public sealed class BossHintSettings
    {
        public int Version=1;
        public List<BossHintEntry> Entries=new List<BossHintEntry>();
        public void Validate()
        {
            if(Version!=1 || Entries==null || Entries.Count>100) throw new ArgumentException("Boss 提示配置版本或数量无效。");
            var ids=new HashSet<string>();var scenes=new HashSet<string>();
            foreach(var e in Entries) {
                if(e==null || string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id) || e.Scenes==null || e.Scenes.Length==0 || e.Text==null || e.Text.Length>60 || e.Text.Contains("<") || e.Text.Contains(">") || e.Text.Contains("\n") || e.Enabled && string.IsNullOrWhiteSpace(e.Text)) throw new ArgumentException("Boss 提示条目或说明无效。");
                foreach(var s in e.Scenes) if(string.IsNullOrWhiteSpace(s) || !s.StartsWith("GG_") || !scenes.Add(s)) throw new ArgumentException("Boss 提示场景为空或重复。");
            }
        }
        public BossHintEntry ForScene(string scene) => Entries.FirstOrDefault(e=>e.Enabled && Array.IndexOf(e.Scenes,scene)>=0);
    }
}
