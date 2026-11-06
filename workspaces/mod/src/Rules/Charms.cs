using System;
using System.Collections.Generic;
using System.Linq;

namespace HollowElements.Rules
{
    public enum CharmId { None, C01, C06, C07, C31, C05, C08, C09, C12, C13, C14, C17, C19, C21, C03, C27, C28, C35, C36, C39, C40 }
    public sealed class CharmDefinition
    {
        public CharmId Id; public int Category; public string Name, Description;
        public CharmDefinition(CharmId id,int category,string name,string description) { Id=id;Category=category;Name=name;Description=description; }
    }
    public static class CharmCatalog
    {
        public static readonly string[] Categories={"通用","增幅","剧变","生存"};
        public static readonly int[] Capacities={1,1,2,1};
        public static readonly CharmDefinition[] All={
            new CharmDefinition(CharmId.C01,0,"万象骨钉","挥舞骨钉，令四种元素轮流流转。"),
            new CharmDefinition(CharmId.C06,0,"四相回路","以三种元素命中同一敌人，短暂唤醒越战越强的元素之力。"),
            new CharmDefinition(CharmId.C07,0,"余烬回响","让火元素附着更深，也更难消散。"),
            new CharmDefinition(CharmId.C31,0,"三式轮转","交替施展各类法术，蓄积能连续引发反应的法术力量。"),
            new CharmDefinition(CharmId.C05,1,"交织棱镜","交替使用不同招式，耗尽底元素时留下新的附着。"),
            new CharmDefinition(CharmId.C08,1,"蒸馏瓶","逆向蒸发与融化消耗更少的底元素。"),
            new CharmDefinition(CharmId.C09,1,"尽相刻印","耗尽普通底元素，换取更猛烈的蒸发与融化。"),
            new CharmDefinition(CharmId.C12,1,"猛火炉心","剑技增幅后留下特殊余火，让后续水与冰攻击更有威力。"),
            new CharmDefinition(CharmId.C13,2,"聚爆导体","超载的爆炸更广、更强，失去击退之力。"),
            new CharmDefinition(CharmId.C14,2,"延时引信","超载稍后引爆，并散出会再次爆炸的火花。"),
            new CharmDefinition(CharmId.C17,2,"引雷针","剑技提前释放感电余威，随后电流逐渐减弱。"),
            new CharmDefinition(CharmId.C19,2,"续流电池","以水与雷持续命中，让尚未散去的感电延续，并减轻引雷后的衰减。"),
            new CharmDefinition(CharmId.C21,2,"刻痕回路","击破超导破绽，额外汲取灵魂，并短暂强化破绽伤害。"),
            new CharmDefinition(CharmId.C03,2,"绝缘刃","超导唤醒绝缘之刃，转化剑技，或以更多次元素攻击兑现破绽。"),
            new CharmDefinition(CharmId.C27,2,"缓霜凝结","冰封维持更久，霜冻更加迟缓；反应破冰后，敌人变得脆弱。"),
            new CharmDefinition(CharmId.C28,2,"冰棱余势","碎冰留下冰棱，使接下来的水元素附着先唤来寒意。"),
            new CharmDefinition(CharmId.C35,3,"绝缘薄膜","抵御超载后患并减少感电跳伤，代价是反应当下更痛、电流更短。"),
            new CharmDefinition(CharmId.C36,3,"熄流护印","超载的伤害加重不再接连生效，来袭的增幅反应变得温和。"),
            new CharmDefinition(CharmId.C39,3,"逆境帷幕","接连引发反应，短暂压制自身的反应负面效果。"),
            new CharmDefinition(CharmId.C40,3,"碎晶护壳","从碎冰中凝成护面，抵挡来袭的伤害；自身结霜时，护面也会刺痛。")
        };
        public static CharmDefinition Find(CharmId id) => All.FirstOrDefault(c=>c.Id==id);
    }
    public sealed class CharmEquipment
    {
        public CharmId General, Amplification, TransformA, TransformB, Survival;
        public bool Has(CharmId id) => id!=CharmId.None && (General==id || Amplification==id || TransformA==id || TransformB==id || Survival==id);
        public CharmEquipment Copy() => (CharmEquipment)MemberwiseClone();
        public void Normalize()
        {
            General=Valid(General,0);Amplification=Valid(Amplification,1)!=CharmId.None?Amplification:Valid(Amplification,2);TransformA=Valid(TransformA,2);TransformB=Valid(TransformB,2);Survival=Valid(Survival,3);
            if(TransformA==Amplification) TransformA=CharmId.None;
            if(TransformB==TransformA || TransformB==Amplification) TransformB=CharmId.None;
        }
        private static CharmId Valid(CharmId id,int category) => CharmCatalog.Find(id)?.Category==category ? id : CharmId.None;
        public bool Toggle(CharmId id)
        {
            var definition=CharmCatalog.Find(id); if(definition==null) return false;
            switch(definition.Category)
            {
                case 0: General=General==id?CharmId.None:id;return true;
                case 1:
                    if(CharmCatalog.Find(Amplification)?.Category==2) return false;
                    Amplification=Amplification==id?CharmId.None:id;return true;
                case 3: Survival=Survival==id?CharmId.None:id;return true;
                default:
                    if(Amplification==id) Amplification=CharmId.None;
                    else if(TransformA==id) TransformA=CharmId.None;
                    else if(TransformB==id) TransformB=CharmId.None;
                    else if(TransformA==CharmId.None) TransformA=id;
                    else if(TransformB==CharmId.None) TransformB=id;
                    else if(Amplification==CharmId.None) Amplification=id;
                    else return false;
                    return true;
            }
        }
        public CharmId[] Slots => new[]{General,Amplification,TransformA,TransformB,Survival};
    }
    public sealed class CharmState
    {
        public CharmEquipment Equipment=new CharmEquipment();
        public double FourUntil, InsulatedUntil, VeilUntil, ScarUntil, LastReaction=-1000, MaskReadyAt;
        public int Epoch, SpellCharges, Masks;
        public bool SkipOverload;
        public int[] Bonuses=new int[5];
        public Dictionary<long,double> SpellCasts=new Dictionary<long,double>();
        public bool Has(CharmId id) => Equipment.Has(id);
        public bool Suppressed(double now) => Has(CharmId.C39) && VeilUntil>now;
        public CharmState Copy()
        {
            var result=(CharmState)MemberwiseClone();result.Bonuses=(int[])Bonuses.Clone();result.SpellCasts=new Dictionary<long,double>(SpellCasts);return result;
        }
        public int SacrificeMasks(int health) {int damage=Math.Min(Masks,Math.Max(0,health-1));Masks=0;return damage;}
        public void Expire(double now)
        {
            if(FourUntil>0 && FourUntil<=now) {FourUntil=0;Array.Clear(Bonuses,0,Bonuses.Length);Epoch++;}
            foreach(long cast in SpellCasts.Where(c=>now-c.Value>30).Select(c=>c.Key).ToArray()) SpellCasts.Remove(cast);
        }
        public void Reaction(Reaction reaction,double now)
        {
            if(reaction==Rules.Reaction.None) return;
            if(Has(CharmId.C03) && reaction==Rules.Reaction.Superconduct) InsulatedUntil=now+12;
            if(Has(CharmId.C40) && reaction==Rules.Reaction.Shatter && Masks==0 && now>=MaskReadyAt) {Masks=1;MaskReadyAt=now+5;}
            if(Has(CharmId.C39) && VeilUntil<=now)
            {
                if(now-LastReaction<=5) {VeilUntil=now+8;LastReaction=-1000;}
                else LastReaction=now;
            }
        }
    }
    public sealed class TargetCharmState
    {
        public int Epoch=-1, Elements, Spells, LastKind=-1, FrostCharges;
        public double FourStarted=-1000, SpellStarted=-1000, FireUnits, FireUntil, FireDuration, FragileUntil;
        public TargetCharmState Copy() => (TargetCharmState)MemberwiseClone();
    }
}
