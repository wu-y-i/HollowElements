using System;
using System.Collections.Generic;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game
{
    internal sealed class AttackStamp : MonoBehaviour
    {
        internal AttackSource Source;
        internal long Cast;
        private void OnEnable()
        {
            CombatRuntime.Current?.Casts.Refresh(this);
            if(!GetComponent<AttackTint>()) gameObject.AddComponent<AttackTint>();
        }
    }

    internal sealed class AttackTracking
    {
        private long sequence;
        internal long NextNativeCast() => ++sequence;
        private readonly Dictionary<AttackSlot,long> casts = new Dictionary<AttackSlot,long>();
        private HeroController preparedHero;
        internal bool BlackDive;
        internal Action<string> Trace;
        internal long Begin(AttackSlot slot) { long id=++sequence; casts[slot]=id; return id; }
        internal long Current(AttackSlot slot) { long id; return casts.TryGetValue(slot,out id) ? id : Begin(slot); }
        internal void PrepareHero(HeroController hero)
        {
            if(preparedHero==hero) return;
            preparedHero=hero;
            foreach(var fsm in hero.GetComponentsInChildren<PlayMakerFSM>(true)) Stamp(fsm.gameObject);
            foreach(var slash in hero.GetComponentsInChildren<NailSlash>(true)) Stamp(slash.gameObject);
            foreach(var damage in hero.GetComponentsInChildren<DamageEnemies>(true)) Stamp(damage.gameObject);
        }
        internal void StateEntered(FsmState state)
        {
            var fsm=state.Fsm;
            if(fsm==null || !fsm.GameObject) return;
            var hero=HeroController.SilentInstance;
            if(hero && fsm.GameObject==hero.gameObject)
            {
                Trace?.Invoke("FSM "+fsm.Name+" / "+state.Name);
                // Verified on 1.5.78.11833. Intermediate Level Check, landing and extend states
                // intentionally keep the cast. Only a fresh attack's entry allocates an ID.
                if(fsm.Name=="Spell Control")
                {
                    if(state.Name=="Fireball Antic") Begin(AttackSlot.Wave);
                    if(state.Name=="Quake Antic") Begin(AttackSlot.Dive);
                    if(state.Name=="Scream Antic1" || state.Name=="Scream Antic2" || state.Name=="SG Antic") Begin(AttackSlot.Shriek);
                    if(state.Name=="Quake1 Down") BlackDive=false;
                    if(state.Name=="Quake2 Down") BlackDive=true;
                }
                if(fsm.Name=="Nail Arts")
                {
                    if(state.Name=="Cyclone Start") Begin(AttackSlot.Cyclone);
                    if(state.Name=="G Slash") Begin(AttackSlot.GreatSlash);
                    if(state.Name=="Dash Slash") Begin(AttackSlot.DashSlash);
                }
            }
            Stamp(fsm.GameObject);
        }
        internal void Refresh(AttackStamp stamp)
        {
            AttackSource kind;
            if(Classify(stamp.gameObject,out kind)) stamp.Source=kind;
            stamp.Cast=Current(RuleConfig.SlotOf(stamp.Source));
        }
        internal void Stamp(GameObject source,bool force=false)
        {
            if(!source) return;
            AttackSource kind;
            if(!Classify(source,out kind)) return;
            var stamp=source.GetComponent<AttackStamp>();
            if(stamp && !force) return;
            if(!stamp) stamp=source.AddComponent<AttackStamp>();
            stamp.Source=kind; stamp.Cast=Current(RuleConfig.SlotOf(kind));
        }
        internal bool Resolve(HitInstance hit,out AttackSource source,out long cast)
        {
            source=AttackSource.Nail; cast=0;
            if(!hit.Source || hit.IsExtraDamage) return false;
            var stamp=hit.Source.GetComponent<AttackStamp>();
            if(!stamp) { Stamp(hit.Source); stamp=hit.Source.GetComponent<AttackStamp>(); }
            if(!stamp) return false;
            source=stamp.Source; cast=stamp.Cast; return true;
        }
        private bool Classify(GameObject obj,out AttackSource source)
        {
            source=AttackSource.Nail;
            string name=obj.name.ToLowerInvariant();
            var parent=obj.transform.parent;
            for(int i=0;i<3 && parent && parent.name!="Knight";i++,parent=parent.parent) name+="/"+parent.name.ToLowerInvariant();
            if(name.Contains("great slash")) source=AttackSource.GreatSlash;
            else if(name.Contains("dash slash")) source=AttackSource.DashSlash;
            else if(name.Contains("cyclone slash")) source=AttackSource.Cyclone;
            else if(obj.GetComponent<NailSlash>()) source=AttackSource.Nail;
            else if(name.Contains("fireball")) source=name.Contains("fireball2") ? AttackSource.BlackWave : AttackSource.WhiteWave;
            else if(name.Contains("scr heads") || name.Contains("scr base")) source=name.Contains("heads 2") || name.Contains("base 2") ? AttackSource.BlackShriek : AttackSource.WhiteShriek;
            else if(name.Contains("q fall damage")) source=BlackDive ? AttackSource.BlackDiveBody : AttackSource.WhiteDiveBody;
            else if(name.StartsWith("q ") || name.Contains("/q ") || name.Contains("q1 pillar"))
            {
                bool black=name.Contains("mega") || name.Contains("slam 2") || name.Contains("pillar") && !name.Contains("q1 pillar");
                source=black ? AttackSource.BlackDiveShock : AttackSource.WhiteDiveShock;
            }
            else return false;
            return true;
        }
        internal void Clear() { casts.Clear(); BlackDive=false; preparedHero=null; }
    }
}
