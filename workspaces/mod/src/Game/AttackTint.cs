using System.Collections.Generic;
using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game
{
    // Per-sprite vertex colour only: preserves black cores, native animation, alpha and shared materials.
    [DefaultExecutionOrder(10000)]
    internal sealed class AttackTint : MonoBehaviour
    {
        private readonly Dictionary<tk2dBaseSprite,ColourState> sprites=new Dictionary<tk2dBaseSprite,ColourState>();
        private readonly Dictionary<SpriteRenderer,ColourState> renderers=new Dictionary<SpriteRenderer,ColourState>();
        private AttackStamp stamp;
        private bool playerSource;
        internal int SpriteCount => sprites.Count+renderers.Count;
        internal bool Coloured { get; private set; }
        private sealed class ColourState { internal Color Original,Applied; internal bool Owned; }
        private void OnEnable()
        {
            stamp=GetComponent<AttackStamp>(); playerSource=BelongsToPlayer();
            sprites.Clear(); renderers.Clear();
            if(!playerSource) return;
            foreach(var sprite in GetComponentsInChildren<tk2dBaseSprite>(true)) sprites[sprite]=new ColourState();
            foreach(var sprite in GetComponentsInChildren<SpriteRenderer>(true)) renderers[sprite]=new ColourState();
            LateUpdate();
        }
        private bool BelongsToPlayer()
        {
            if(GetComponentInParent<HeroController>()) return true;
            if(GetComponentInChildren<DamageEnemies>(true)) return true;
            foreach(var fsm in GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if(fsm.FsmName=="damages_enemy") return true;
                foreach(var state in fsm.FsmStates)
                    foreach(var action in state.Actions)
                        if(action is HutongGames.PlayMaker.Actions.TakeDamage) return true;
            }
            return false;
        }
        private void LateUpdate()
        {
            var runtime=CombatRuntime.Current;
            if(!runtime || !stamp || !playerSource || !runtime.Mod.OnSaveGlobal().TintAttacks) { Restore(); return; }
            var element=runtime.AttackElement(stamp.Source);
            if(element==Element.None) { Restore(); return; }
            Color tint=Color.Lerp(Color.white,Chinese.Colors[(int)element],runtime.Mod.OnSaveGlobal().AttackTintStrength);
            foreach(var item in sprites) if(item.Key) { var colour=Apply(item.Key.color,item.Value,tint); if(item.Key.color!=colour) item.Key.color=colour; }
            foreach(var item in renderers) if(item.Key) { var colour=Apply(item.Key.color,item.Value,tint); if(item.Key.color!=colour) item.Key.color=colour; }
            Coloured=SpriteCount>0;
        }
        private static Color Apply(Color current,ColourState state,Color tint)
        {
            if(!state.Owned || !SameRgb(current,state.Applied)) state.Original=current;
            state.Original.a=current.a;
            state.Applied=new Color(state.Original.r*tint.r,state.Original.g*tint.g,state.Original.b*tint.b,current.a);
            state.Owned=true; return state.Applied;
        }
        private static bool SameRgb(Color a,Color b) { return Mathf.Abs(a.r-b.r)<.0001f && Mathf.Abs(a.g-b.g)<.0001f && Mathf.Abs(a.b-b.b)<.0001f; }
        private static Color Restore(Color current,ColourState state)
        {
            if(!state.Owned) return current;
            state.Owned=false;
            return SameRgb(current,state.Applied) ? new Color(state.Original.r,state.Original.g,state.Original.b,current.a) : current;
        }
        internal void Restore()
        {
            foreach(var item in sprites) if(item.Key) item.Key.color=Restore(item.Key.color,item.Value);
            foreach(var item in renderers) if(item.Key) item.Key.color=Restore(item.Key.color,item.Value);
            Coloured=false;
        }
        internal void RememberOriginals(Dictionary<tk2dBaseSprite,Color> tk,Dictionary<SpriteRenderer,Color> sr)
        {
            foreach(var item in sprites) if(item.Key) tk[item.Key]=item.Value.Owned?item.Value.Original:item.Key.color;
            foreach(var item in renderers) if(item.Key) sr[item.Key]=item.Value.Owned?item.Value.Original:item.Key.color;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
