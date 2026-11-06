using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using HollowElements.Rules;

namespace HollowElements.Game
{
    internal sealed class WaterExposure : MonoBehaviour
    {
        private CombatRuntime runtime;
        private readonly HashSet<WaterAttachmentRegion> regions=new HashSet<WaterAttachmentRegion>();
        private bool wet;
        private double next;
        internal int Applications { get; private set; }
        internal void Initialize(CombatRuntime owner)
        { runtime=owner; UnitySceneManager.sceneLoaded+=Loaded; Discover(); }
        private void Loaded(Scene scene,LoadSceneMode mode) { regions.Clear(); wet=false; Discover(); }
        private static void Discover()
        { foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()) if(fsm.gameObject.scene.IsValid()) Register(fsm); }
        internal static void Register(PlayMakerFSM fsm)
        {
            if(!fsm) return;
            bool water=fsm.FsmName=="Surface Water Region" || fsm.FsmName=="Spa Region";
            bool acid=fsm.FsmName=="Acid Armour Check" && (fsm.name.StartsWith("Acid Box") || fsm.name.StartsWith("Surface Water Region"));
            if(!water && !acid) return;
            if(!fsm.GetComponent<Collider2D>()) return;
            var region=fsm.GetComponent<WaterAttachmentRegion>() ?? fsm.gameObject.AddComponent<WaterAttachmentRegion>();
            region.Acid=acid || fsm.GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="Acid Armour Check");
        }
        internal void Enter(WaterAttachmentRegion region) { regions.Add(region); Tick(); }
        internal void Leave(WaterAttachmentRegion region) { regions.Remove(region); Tick(); }
        private void Update() { Tick(); }
        private void Tick()
        {
            if(!runtime || !HeroController.SilentInstance || Time.timeScale==0) return;
            regions.RemoveWhere(r=>!r || !r.isActiveAndEnabled);
            bool active=regions.Any(r=>!r.Acid || !PlayerData.instance.hasAcidArmour);
            if(!active) { wet=false; return; }
            if(!wet || runtime.Now>=next)
            { wet=true; next=runtime.Now+1; Applications++; runtime.ApplyPlayerAttachment(Element.Hydro); }
        }
        private void OnDestroy() { UnitySceneManager.sceneLoaded-=Loaded; regions.Clear(); }
        internal static void RemoveRegions()
        { foreach(var region in Resources.FindObjectsOfTypeAll<WaterAttachmentRegion>()) Destroy(region); }
    }

    internal sealed class WaterAttachmentRegion : MonoBehaviour
    {
        internal bool Acid;
        private readonly HashSet<Collider2D> contacts=new HashSet<Collider2D>();
        private WaterExposure Exposure => CombatRuntime.Current ? CombatRuntime.Current.GetComponent<WaterExposure>() : null;
        private void OnTriggerEnter2D(Collider2D other)
        { if(other.GetComponentInParent<HeroController>()) { contacts.Add(other); Exposure?.Enter(this); } }
        private void OnTriggerStay2D(Collider2D other) { OnTriggerEnter2D(other); }
        private void OnTriggerExit2D(Collider2D other)
        { if(contacts.Remove(other) && contacts.Count==0) Exposure?.Leave(this); }
        private void OnDisable() { contacts.Clear(); Exposure?.Leave(this); }
        private void OnDestroy() { Exposure?.Leave(this); }
    }
}
