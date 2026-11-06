using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Lifetime belongs to one pooled spawn, never to the shared prefab.
    internal sealed class ElementalAttack : MonoBehaviour
    {
        internal Element Element { get; private set; }
        internal int OwnerId, Ring;
        internal float Size { get; private set; } = 1;
        private Vector3 originalScale;
        private bool scaled;
        private bool tinted,outlined;
        internal Vector3 NativeScale => scaled ? originalScale : transform.localScale;
        internal static Element RandomElement() => (Element)Random.Range(1, 5);
        internal static void Set(GameObject obj, Element element, int owner, int ring = 0, float size = 1, bool tint = false, bool outline = true)
        {
            if (!obj) return;
            var marker = obj.GetComponent<ElementalAttack>() ?? obj.AddComponent<ElementalAttack>();
            if(marker.Element==element && marker.OwnerId==owner && marker.Ring==ring && marker.Size==size && marker.tinted==tint && marker.outlined==outline) return;
            marker.Restore(); marker.Element = element; marker.OwnerId = owner; marker.Ring = ring;
            marker.tinted=tint;marker.outlined=outline;
            marker.Size = size; marker.originalScale = obj.transform.localScale; marker.scaled = size != 1;
            if (marker.scaled) obj.transform.localScale = marker.originalScale * size;
            if(tint) (obj.GetComponent<EnemyAttackTint>() ?? obj.AddComponent<EnemyAttackTint>()).Set(element);
            if(outline) foreach (var sprite in obj.GetComponentsInChildren<tk2dBaseSprite>(true))
                    (sprite.GetComponent<ElementOutline>() ?? sprite.gameObject.AddComponent<ElementOutline>()).SetElement(element);
        }
        internal void Restore()
        {
            if (scaled) transform.localScale = originalScale;
            scaled = false; Size = 1; Element = Element.None; OwnerId = 0; Ring = 0;
            foreach (var outline in GetComponentsInChildren<ElementOutline>(true)) outline.Restore();
            GetComponent<EnemyAttackTint>()?.Restore();
        }
        internal void NativeScaleSet()
        {
            if (Size == 1) return;
            originalScale = transform.localScale; scaled = true; transform.localScale = originalScale * Size;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
