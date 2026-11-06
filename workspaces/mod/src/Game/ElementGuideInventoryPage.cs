using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace HollowElements.Game
{
    // Add one real page to the existing inventory carousel; the native FSM still opens,
    // animates, closes and navigates every page. Every patch has an unload inverse.
    internal sealed class ElementGuideInventoryPage : IDisposable
    {
        internal PlayMakerFSM Inventory;
        internal GameObject Pane;
        internal int PageNumber;
        private readonly List<Action> undo = new List<Action>();
        private readonly ElementGuideWindow window;
        internal ElementGuideInventoryPage(ElementGuideWindow owner) { window = owner; }
        internal bool Visible => Inventory && Inventory.gameObject.activeInHierarchy && Inventory.ActiveStateName == "Opened" && Inventory.FsmVariables.FindFsmInt("Current Pane Num").Value == PageNumber;
        internal void Attach(PlayMakerFSM inventory)
        {
            Inventory = inventory;
            var loop = State("Loop Through").Actions.OfType<IntSwitch>().Single();
            int over = Array.FindIndex(loop.sendEvent, e => e.Name == "OVER");
            PageNumber = loop.compareTo[over].Value;
            if (PageNumber < 4 || PageNumber > 16) throw new InvalidOperationException("背包页面边界无法识别。");
            Pane = new GameObject("HollowElements.ElementGuidePane"); Pane.SetActive(false); Pane.transform.SetParent(inventory.transform, false);
            var fade = Pane.AddComponent<FadeGroup>(); fade.spriteRenderers = new SpriteRenderer[0]; fade.texts = new TMPro.TextMeshPro[0]; fade.animators = new InvAnimateUpAndDown[0];
            foreach (var pair in new[] { Tuple.Create("Check Current Pane", "Open inv"), Tuple.Create("Loop Through", "Next Inv"), Tuple.Create("Check R Pane", "Next Inv 2"), Tuple.Create("Check L Pane", "Next Inv 3") })
            {
                var state = State(pair.Item1); var selector = state.Actions.OfType<IntSwitch>().Single();
                var oldValues = selector.compareTo; var oldEvents = selector.sendEvent;
                selector.compareTo = oldValues.Select((v,i) => oldEvents[i].Name == "OVER" ? new FsmInt(PageNumber + 1) : v).Concat(new[] {new FsmInt(PageNumber)}).ToArray();
                selector.sendEvent = oldEvents.Concat(new[] { FsmEvent.GetFsmEvent("HOLLOW ELEMENTS ELEMENT GUIDE") }).ToArray();
                undo.Add(() => { selector.compareTo = oldValues; selector.sendEvent = oldEvents; });
                var originalTransitions = state.Transitions;
                string newName = "HE ElementGuide " + pair.Item1;
                state.Transitions = originalTransitions.Concat(new[] { new FsmTransition { FsmEvent = FsmEvent.GetFsmEvent("HOLLOW ELEMENTS ELEMENT GUIDE"), ToState = newName } }).ToArray();
                undo.Add(() => state.Transitions = originalTransitions);
                var template = State(pair.Item2);
                var created = new FsmState(inventory.Fsm) { Name = newName, Actions = template.Actions.Select(Clone).ToArray(),
                    Transitions = template.Transitions.Where(t => t.EventName == "FINISHED").Select(t => new FsmTransition { FsmEvent = t.FsmEvent, ToState = t.ToState }).ToArray() };
                inventory.Fsm.States = inventory.Fsm.States.Concat(new[] { created }).ToArray();
                foreach (var transition in created.Transitions) transition.ToFsmState = State(transition.ToState);
                state.Transitions.Last().ToFsmState = created;
                undo.Add(() => inventory.Fsm.States = inventory.Fsm.States.Where(s => s != created).ToArray());
            }
            foreach (string name in new[] { "Under", "Under 2", "Under 3" })
            {
                var action = State(name).Actions.OfType<SetIntValue>().Single(); var original = action.intValue;
                action.intValue = new FsmInt(PageNumber + 1); undo.Add(() => action.intValue = original);
            }
            // Even a fresh save has inventory + element_guide, so preserve the native arrow path.
            var single = State("Single Pane?"); var singleActions = single.Actions;
            single.Actions = new FsmStateAction[] { new ElementGuideCallback(() => {
                Inventory.FsmVariables.FindFsmBool("Single Pane").Value = false;
                foreach (var action in singleActions.OfType<ActivateGameObject>().Take(6))
                    if (action.gameObject.GameObject.Value) action.gameObject.GameObject.Value.SetActive(true);
            }) };
            undo.Add(() => single.Actions = singleActions);
            var opened = State("Opened"); var openedActions = opened.Actions;
            opened.Actions = openedActions.Select(a => a is ListenForMenuCancel ? new ElementGuideCancel(this, window) : a).ToArray();
            undo.Add(() => opened.Actions = openedActions);
        }
        private FsmState State(string name) => Inventory.FsmStates.Single(s => s.Name == name);
        private FsmStateAction Clone(FsmStateAction original)
        {
            if (original is GetLanguageString language) return new ElementGuideCallback(() => language.storeValue.Value = window.Title);
            var copy = (FsmStateAction)Activator.CreateInstance(original.GetType());
            foreach (var field in original.GetType().GetFields()) if (!field.IsInitOnly && !field.IsStatic) field.SetValue(copy, field.GetValue(original));
            if (copy is SetIntValue number) number.intValue = new FsmInt(PageNumber);
            if (copy is SetGameObject obj && obj.gameObject.Name == "Inventory Pane") obj.gameObject = new FsmGameObject { Value = Pane };
            return copy;
        }
        internal bool Open()
        {
            var hero = HeroController.SilentInstance;
            if (!Inventory || !hero || !hero.CanOpenInventory() || GameManager.instance.isPaused || WorkshopMenus.CapturesInput) return false;
            PlayerData.instance.currentInvPane = PageNumber;
            Inventory.SendEvent("BUTTON PRESSED");
            return true;
        }
        internal void Move(int direction)
        { window.ReleasePause(); Inventory.SendEvent(direction > 0 ? "MOVE PANE R" : "MOVE PANE L"); }
        internal void Close() { window.ReleasePause(); if (Inventory) Inventory.SendEvent("CLOSE"); }
        internal void RefreshTitle()
        {
            if(!Visible) return;
            var label=Inventory.FsmVariables.FindFsmGameObject("Pane Name Obj").Value;
            if(label) {var text=label.GetComponent<TMPro.TextMeshPro>();if(text) text.text=window.Title;}
        }
        public void Dispose()
        {
            window.ReleasePause();
            if (Inventory)
            {
                if (Pane && Inventory.gameObject.activeInHierarchy &&
                    (Inventory.FsmVariables.FindFsmGameObject("Current Pane").Value == Pane || Inventory.FsmVariables.FindFsmGameObject("Prev Pane").Value == Pane))
                {
                    Inventory.FsmVariables.FindFsmInt("Current Pane Num").Value = 0;
                    Inventory.SetState("Close");
                    Inventory.SetState("Regain Control");
                }
                for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
                if (PlayerData.instance != null && PlayerData.instance.currentInvPane == PageNumber) PlayerData.instance.currentInvPane = 0;
            }
            undo.Clear(); if (Pane) UnityEngine.Object.Destroy(Pane); Pane = null; Inventory = null;
        }
        private sealed class ElementGuideCallback : FsmStateAction
        {
            private readonly Action callback;
            internal ElementGuideCallback(Action value) { callback = value; }
            public override void OnEnter() { callback(); Finish(); }
        }
        private sealed class ElementGuideCancel : FsmStateAction
        {
            private readonly ElementGuideInventoryPage page; private readonly ElementGuideWindow view;
            internal ElementGuideCancel(ElementGuideInventoryPage owner, ElementGuideWindow window) { page = owner; view = window; }
            public override void OnUpdate()
            {
                if (GameManager.instance.isPaused || !InputHandler.Instance.inputActions.menuCancel.WasPressed) return;
                if (page.Visible && view.ReturnToIndex()) return;
                view.ReleasePause(); Fsm.Event("CLOSE");
            }
        }
    }
}
