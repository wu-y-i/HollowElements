using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class VengeflyKingBoss : BossBehaviour
    {
        internal Element CurrentElement { get; private set; }
        internal Element SummonElement { get; private set; }
        internal int RoarCount { get; private set; }

        internal override void Entering(FsmState state)
        {
            if (state.Fsm.Name != "Big Buzzer" || (state.Name != "Roar Left" && state.Name != "Roar Right")) return;
            // The intro uses "Intro Roar Left/Right" and deliberately never enters this branch.
            SummonElement = CurrentElement;
            Element excluded = Element.None;
            foreach (var other in FindObjectsOfType<VengeflyKingBoss>())
                if (other != this && other.Health && !other.Health.isDead) excluded = other.CurrentElement;
            var choices = new System.Collections.Generic.List<Element>();
            for (int i = 1; i <= 4; i++) if ((Element)i != excluded) choices.Add((Element)i);
            CurrentElement = choices[Random.Range(0, choices.Count)]; RoarCount++;
            Record.State.SetIntrinsicAura(CurrentElement); // Direct replacement: no damage/reaction event.
            Trace("吼叫 " + RoarCount + "：小怪=" + SummonElement + "，本体=" + CurrentElement);
        }
        internal override Element ContactElement() => CurrentElement;
        internal override void SyncAura(TargetState state) { if (CurrentElement != Element.None) state.SetIntrinsicAura(CurrentElement); }
    }

    internal sealed class SummonInheritance : MonoBehaviour
    {
        private EnemySpawner spawner;
        private Element snapshot;
        private int summoner;
        internal void Configure(EnemySpawner value, Element element, int owner)
        {
            Detach(); spawner = value; snapshot = element; summoner=owner; spawner.OnEnemySpawned += Spawned;
        }
        private void Spawned(GameObject enemy)
        {
            var marker = enemy.GetComponent<ElementalMinion>() ?? enemy.AddComponent<ElementalMinion>();
            marker.SetElement(snapshot,summoner); // Snapshot belongs to this particular summoning, not a nearby king.
        }
        internal void Detach() { if (spawner) spawner.OnEnemySpawned -= Spawned; spawner = null; }
        private void OnDestroy() { Detach(); }
    }

    internal sealed class ElementalMinion : MonoBehaviour
    {
        internal Element Element { get; private set; }
        internal int SummonerId { get; private set; }
        internal void SetElement(Element element, int summoner)
        {
            Element = element; SummonerId=summoner;
            (GetComponent<ElementOutline>() ?? gameObject.AddComponent<ElementOutline>()).SetElement(element);
        }
        private void OnDisable() { Element = Element.None; var outline = GetComponent<ElementOutline>(); if (outline) outline.Restore(); }
    }
}
