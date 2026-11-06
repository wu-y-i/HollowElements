using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class GruzMotherBoss : BossBehaviour
    {
        internal Element CurrentElement { get; private set; }
        internal int CollisionCount { get; private set; }
        private readonly System.Collections.Generic.Dictionary<int,float> contactSteps = new System.Collections.Generic.Dictionary<int,float>();
        private readonly System.Collections.Generic.HashSet<int> playerContacts = new System.Collections.Generic.HashSet<int>();

        private void Roll(int contact)
        {
            // HeroBox and the native physics callback can report the same collision in either order.
            float step;
            if (contactSteps.TryGetValue(contact,out step) && step == Time.fixedTime) return;
            contactSteps[contact] = Time.fixedTime;
            CurrentElement = (Element)Random.Range(1, 5); CollisionCount++;
            Trace("碰撞 " + CollisionCount + "：" + CurrentElement);
        }
        private void OnCollisionEnter2D(Collision2D collision) { Collision(collision.collider); }
        private void OnTriggerEnter2D(Collider2D collider) { Collision(collider); }
        private void OnCollisionExit2D(Collision2D collision) { playerContacts.Remove(collision.collider.GetInstanceID()); }
        private void OnTriggerExit2D(Collider2D collider) { playerContacts.Remove(collider.GetInstanceID()); }
        private void Collision(Collider2D collider)
        {
            if (!collider) return;
            // Ignore nail hitboxes, trigger zones, other monsters and roar effects.
            int layer = collider.gameObject.layer;
            var hero=collider.GetComponentInParent<HeroController>();
            if (layer == 8 || layer == 25) Roll(collider.GetInstanceID());
            else if (layer == 9 && hero)
            {
                if (playerContacts.Count == 0) Roll(hero.GetInstanceID());
                playerContacts.Add(collider.GetInstanceID());
            }
        }
        internal override Element ContactElement()
        {
            if (playerContacts.Count == 0) Roll(HeroController.SilentInstance ? HeroController.SilentInstance.GetInstanceID() : 0);
            return CurrentElement;
        }
        private void OnDisable() { playerContacts.Clear(); contactSteps.Clear(); }
    }
}
