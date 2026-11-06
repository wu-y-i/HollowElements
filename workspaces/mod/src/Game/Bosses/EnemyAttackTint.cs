using System.Collections.Generic;
using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Sprite-instance colour, including alpha changes made by native animations.
    [DefaultExecutionOrder(10500)]
    internal sealed class EnemyAttackTint : MonoBehaviour
    {
        [System.Serializable]
        private sealed class NativeColour
        {
            public tk2dBaseSprite Sprite;
            public SpriteRenderer Renderer;
            public Color Colour;
        }
        // Native pools expand by Instantiate of live actors. Unity copies these
        // baselines and remaps sprite references; dictionaries alone lose them.
        [SerializeField] private List<NativeColour> nativeColours=new List<NativeColour>();
        private readonly Dictionary<tk2dBaseSprite,Color> originals = new Dictionary<tk2dBaseSprite,Color>();
        private readonly Dictionary<SpriteRenderer,Color> renderers = new Dictionary<SpriteRenderer,Color>();
        internal Element Element;
        private bool children;
        internal int BindCount { get; private set; }
        internal void Set(Element element,bool includeChildren=true)
        {
            if(Element==element && children==includeChildren && (originals.Count>0 || renderers.Count>0)) return;
            Restore(); Element = element;
            BindCount++;
            children=includeChildren;
            foreach(var sprite in includeChildren ? GetComponentsInChildren<tk2dBaseSprite>(true) : GetComponents<tk2dBaseSprite>())
                if(sprite.GetComponentInParent<EnemyAttackTint>()==this)
                {
                    Color original=sprite.color;
                    for(var t=transform.parent;t;t=t.parent) {var owner=t.GetComponent<EnemyAttackTint>();if(owner && owner.originals.TryGetValue(sprite,out original)) break;original=sprite.color;}
                    var saved=nativeColours.Find(c=>c.Sprite==sprite);
                    if(saved!=null) original=saved.Colour;
                    else nativeColours.Add(new NativeColour {Sprite=sprite,Colour=original});
                    originals[sprite]=original;
                }
            foreach(var sprite in includeChildren ? GetComponentsInChildren<SpriteRenderer>(true) : GetComponents<SpriteRenderer>())
                if(sprite.GetComponentInParent<EnemyAttackTint>()==this)
                {
                    Color original=sprite.color;
                    for(var t=transform.parent;t;t=t.parent) {var owner=t.GetComponent<EnemyAttackTint>();if(owner && owner.renderers.TryGetValue(sprite,out original)) break;original=sprite.color;}
                    var saved=nativeColours.Find(c=>c.Renderer==sprite);
                    if(saved!=null) original=saved.Colour;
                    else nativeColours.Add(new NativeColour {Renderer=sprite,Colour=original});
                    renderers[sprite]=original;
                }
        }
        private void LateUpdate()
        {
            if(Element == Element.None) return;
            Color tint = Color.Lerp(Color.white,Chinese.Colors[(int)Element],.75f);
            foreach(var pair in originals) if(pair.Key && pair.Key.GetComponentInParent<EnemyAttackTint>()==this)
            { var c=new Color(pair.Value.r*tint.r,pair.Value.g*tint.g,pair.Value.b*tint.b,pair.Key.color.a);if(pair.Key.color!=c) pair.Key.color=c; }
            foreach(var pair in renderers) if(pair.Key && pair.Key.GetComponentInParent<EnemyAttackTint>()==this)
            { var c=new Color(pair.Value.r*tint.r,pair.Value.g*tint.g,pair.Value.b*tint.b,pair.Key.color.a);if(pair.Key.color!=c) pair.Key.color=c; }
        }
        internal void Restore()
        {
            foreach(var pair in originals) if(pair.Key && pair.Key.GetComponentInParent<EnemyAttackTint>()==this) pair.Key.color = new Color(pair.Value.r,pair.Value.g,pair.Value.b,pair.Key.color.a);
            foreach(var pair in renderers) if(pair.Key && pair.Key.GetComponentInParent<EnemyAttackTint>()==this) pair.Key.color = new Color(pair.Value.r,pair.Value.g,pair.Value.b,pair.Key.color.a);
            foreach(var saved in nativeColours)
            {
                if(saved.Sprite && !originals.ContainsKey(saved.Sprite) && saved.Sprite.GetComponentInParent<EnemyAttackTint>()==this)
                    saved.Sprite.color=new Color(saved.Colour.r,saved.Colour.g,saved.Colour.b,saved.Sprite.color.a);
                if(saved.Renderer && !renderers.ContainsKey(saved.Renderer) && saved.Renderer.GetComponentInParent<EnemyAttackTint>()==this)
                    saved.Renderer.color=new Color(saved.Colour.r,saved.Colour.g,saved.Colour.b,saved.Renderer.color.a);
            }
            originals.Clear(); renderers.Clear(); Element = Element.None;
        }
        internal void RememberOriginals(Dictionary<tk2dBaseSprite,Color> tk,Dictionary<SpriteRenderer,Color> sr)
        { foreach(var pair in originals) if(pair.Key) tk[pair.Key]=pair.Value; foreach(var pair in renderers) if(pair.Key) sr[pair.Key]=pair.Value; }
        internal object Diagnostics()
        {
            Color tint=Color.Lerp(Color.white,Chinese.Colors[(int)Element],.75f);
            var sprites=new List<object>();float error=0;
            foreach(var pair in originals) if(pair.Key && pair.Key.GetComponentInParent<EnemyAttackTint>()==this)
            {
                var expected=new Color(pair.Value.r*tint.r,pair.Value.g*tint.g,pair.Value.b*tint.b,pair.Key.color.a);
                var actual=pair.Key.color;
                error=Mathf.Max(error,Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),Mathf.Abs(expected.b-actual.b));
                sprites.Add(new {name=pair.Key.name,original=pair.Value.ToString(),actual=actual.ToString(),expected=expected.ToString()});
            }
            foreach(var pair in renderers) if(pair.Key && pair.Key.GetComponentInParent<EnemyAttackTint>()==this)
            {
                var expected=new Color(pair.Value.r*tint.r,pair.Value.g*tint.g,pair.Value.b*tint.b,pair.Key.color.a);
                var actual=pair.Key.color;
                error=Mathf.Max(error,Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),Mathf.Abs(expected.b-actual.b));
                sprites.Add(new {name=pair.Key.name,original=pair.Value.ToString(),actual=actual.ToString(),expected=expected.ToString()});
            }
            return new {id=gameObject.GetInstanceID(),name,active=gameObject.activeInHierarchy,Element,BindCount,
                ancestorTints=GetComponentsInParent<EnemyAttackTint>(true).Length,maxColourError=error,sprites};
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
