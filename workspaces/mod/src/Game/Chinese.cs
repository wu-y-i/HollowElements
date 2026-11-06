using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game
{
    internal static class Chinese
    {
        internal static readonly string[] Elements = { "无元素", "火", "水", "雷", "冰" };
        internal static readonly string[] Slots = { "普通攻击", "波", "砸地", "吼叫", "强力斩", "冲刺斩", "旋风斩" };
        internal static readonly Color[] Colors = { Color.white, new Color(1,.42f,.25f), new Color(.28f,.72f,1), new Color(.77f,.48f,1), new Color(.61f,.95f,1) };
        internal static string Reaction(Reaction reaction)
        {
            switch (reaction)
            {
                case Rules.Reaction.VaporizeForward: case Rules.Reaction.VaporizeReverse: return "蒸发";
                case Rules.Reaction.MeltForward: case Rules.Reaction.MeltReverse: return "融化";
                case Rules.Reaction.Overload: return "超载";
                case Rules.Reaction.ElectroCharged: return "感电";
                case Rules.Reaction.Freeze: return "冻结";
                case Rules.Reaction.Superconduct: return "超导";
                case Rules.Reaction.Shatter: return "碎冰";
                default: return "";
            }
        }
        internal static string Effect(Reaction reaction)
        {
            switch (reaction)
            {
                case Rules.Reaction.VaporizeForward: case Rules.Reaction.VaporizeReverse: return "vaporize";
                case Rules.Reaction.MeltForward: case Rules.Reaction.MeltReverse: return "melt";
                case Rules.Reaction.Overload: return "overload";
                case Rules.Reaction.ElectroCharged: return "electro-charged";
                case Rules.Reaction.Freeze: return "freeze";
                case Rules.Reaction.Superconduct: return "superconduct";
                case Rules.Reaction.Shatter: return "shatter";
                default: return null;
            }
        }
        internal static Element ReactionColor(Reaction reaction)
        {
            switch (reaction)
            {
                case Rules.Reaction.Overload: case Rules.Reaction.MeltForward: case Rules.Reaction.MeltReverse: case Rules.Reaction.VaporizeForward: case Rules.Reaction.VaporizeReverse: return Element.Pyro;
                case Rules.Reaction.ElectroCharged: case Rules.Reaction.Superconduct: return Element.Electro;
                default: return Element.Cryo;
            }
        }
    }
}
