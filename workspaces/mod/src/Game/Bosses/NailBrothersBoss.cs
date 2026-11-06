using System.Collections.Generic;
using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class NailBrothersBoss : BossBehaviour
    {
        internal Element Element { get; private set; }
        internal bool Dual => Element != Element.None;
        private readonly Dictionary<tk2dBaseSprite, Color> original = new Dictionary<tk2dBaseSprite, Color>();
        internal override void Entering(FsmState state)
        {
            if (state.Fsm.Name != "nailmaster" || state.Name != "Activate" || name != "Mato" || Dual) return;
            var brothers = FindObjectsOfType<NailBrothersBoss>();
            var other = brothers.FirstOrDefault(b => b != this && b.gameObject.scene == gameObject.scene);
            if (!other)
                foreach (var health in FindObjectsOfType<HealthManager>())
                    if (health.gameObject.scene == gameObject.scene && health.name == "Oro") other = BossRegistry.Attach(health.gameObject) as NailBrothersBoss;
            if (!other) return;
            Element = Element.Pyro; other.Element = Element.Hydro;
            (GetComponent<NailBladeTint>() ?? gameObject.AddComponent<NailBladeTint>()).Element = Element;
            (other.GetComponent<NailBladeTint>() ?? other.gameObject.AddComponent<NailBladeTint>()).Element = other.Element;
            Trace("双人阶段 " + Element + " / " + other.Element);
        }
        internal override Element AttackElement(GameObject source)
        {
            // Native weapon hitboxes are separate children; the root DamageHero is body contact.
            return source != gameObject ? Element : Element.None;
        }
        private void LateUpdate()
        {
            if (!Dual) return;
            foreach (var sprite in GetComponentsInChildren<tk2dBaseSprite>(true))
            {
                string text = sprite.name.ToLowerInvariant();
                if (sprite.gameObject == gameObject || (!text.Contains("slash") && !text.Contains("nail") && !text.Contains("cyclone"))) continue;
                if (!original.ContainsKey(sprite)) original.Add(sprite, sprite.color);
                Color tint = Chinese.Colors[(int)Element]; tint.a = sprite.color.a; sprite.color = tint;
            }
        }
        internal void Restore() { foreach (var pair in original) if (pair.Key) { Color colour = pair.Value; colour.a = pair.Key.color.a; pair.Key.color = colour; } original.Clear(); }
        internal void RememberOriginals(Dictionary<tk2dBaseSprite,Color> colours) { foreach(var pair in original) if(pair.Key) colours[pair.Key] = pair.Value; }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
