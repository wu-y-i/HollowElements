using System;
using System.Collections.Generic;
using HollowElements.Rules;
using UnityEngine;

namespace HollowElements.Game
{
    internal sealed partial class CombatRuntime
    {
        internal CharmState Charms=new CharmState();
        private ElementDeck elementDeck=new ElementDeck(Environment.TickCount);
        private Element charmNail=Element.Pyro;
        private bool charmDirty;
        private PlayerData charmPlayer;
        private Settings.Loadout charmOwner;
        private readonly List<CharmExplosion> charmExplosions=new List<CharmExplosion>();
        internal int PendingCharmExplosions => charmExplosions.Count;
        private void NoDamageFourPhase(HitResult result,TargetState previous)
        {
            if(result.NextCharms==null || !Charms.Has(CharmId.C06)) return;
            result.NextCharms.FourUntil=Charms.FourUntil;result.NextCharms.Epoch=Charms.Epoch;result.NextCharms.Bonuses=(int[])Charms.Bonuses.Clone();
            result.NextState.Charms.Epoch=previous.Charms.Epoch;result.NextState.Charms.Elements=previous.Charms.Elements;result.NextState.Charms.FourStarted=previous.Charms.FourStarted;
        }
        internal Element NextNailElement => elementDeck.Peek();
        internal bool CanConfigureCharms => HeroController.SilentInstance && !HeroController.SilentInstance.cState.dead && PlayerData.instance.atBench;
        private bool HasSuperconductTarget()
        {
            foreach(var pair in Enemies) if(pair.Key && pair.Key.hp>0 && !pair.Key.isDead && pair.Key.isActiveAndEnabled && pair.Value.State.SuperconductStacks>0 && pair.Value.State.SuperconductUntil>Now) return true;
            return false;
        }
        private void FreezeMaskPenalty(PlayerData data,On.PlayerData.orig_TakeHealth take)
        {
            if(!Charms.Has(CharmId.C40) || Charms.Masks<=0) return;
            int amount=Charms.SacrificeMasks(data.health+data.healthBlue);
            if(amount>0) take(data,amount);
        }
        private static bool IsSword(AttackSource source) => source==AttackSource.GreatSlash || source==AttackSource.DashSlash || source==AttackSource.Cyclone;
        private void ResetCharms()
        {
            var equipment=Mod?.OnSaveLocal()?.Charms ?? new CharmEquipment();equipment.Normalize();
            Charms=new CharmState {Equipment=equipment.Copy()};elementDeck=new ElementDeck(Environment.TickCount);charmNail=elementDeck.Peek();
            charmExplosions.Clear();
        }
        internal bool ToggleCharm(CharmId id)
        {
            if(!CanConfigureCharms) return false;
            var candidate=Mod.OnSaveLocal().Charms.Copy();if(!candidate.Toggle(id)) return false;
            Mod.OnSaveLocal().Charms=candidate;ResetCharms();
            foreach(var record in Enemies.Values) record.State.Charms=new TargetCharmState();
            charmDirty=true;charmPlayer=PlayerData.instance;charmOwner=Mod.OnSaveLocal();return true;
        }
        internal void SaveCharms()
        {
            bool save=charmDirty && ReferenceEquals(charmPlayer,PlayerData.instance) && ReferenceEquals(charmOwner,Mod.OnSaveLocal());
            charmDirty=false;charmPlayer=null;charmOwner=null;
            if(save && HeroController.SilentInstance && !HeroController.SilentInstance.cState.dead && GameManager.instance)
                GameManager.instance.SaveGame(ok=>{if(!ok) Mod.Log("护符配置已应用；将随下一次存档保存。");});
        }
        private sealed class CharmExplosion
        {
            internal HealthManager Follow;internal Vector3 Position,Velocity;internal double Due,Started;internal float Direction;internal bool Moving;internal SecondaryDamage Damage;
        }
        private void QueueExplosion(HealthManager target,Vector3 position,SecondaryDamage damage,float direction)
        {
            charmExplosions.Add(new CharmExplosion {Follow=target,Position=position,Due=Now+damage.Delay,Started=Now,Damage=damage.Copy(),Direction=direction});
        }
        private void UpdateExplosions()
        {
            for(int i=charmExplosions.Count-1;i>=0;i--)
            {
                var item=charmExplosions[i];
                if(item.Follow) item.Position=item.Follow.transform.position;
                if(Now<item.Due) continue;
                charmExplosions.RemoveAt(i);
                Vector3 position=item.Position;
                if(item.Moving) {float t=(float)(Now-item.Started);position+=item.Velocity*t+Vector3.down*(3.5f*t*t);}
                GetComponent<CombatPresentation>()?.CharmExplosion(position,(float)(item.Damage.Radius/Config.Reactions.OverloadRadius));
                foreach(var target in FindObjectsOfType<HealthManager>())
                    if(!target.isDead && target.hp>0 && target.isActiveAndEnabled && Vector2.Distance(target.transform.position,position)<=item.Damage.Radius)
                    {
                        Vector2 offset=target.transform.position-position;
                        Extra(Track(target),item.Damage,offset.sqrMagnitude<.001f?item.Direction:Mathf.Atan2(offset.y,offset.x)*Mathf.Rad2Deg,false);
                    }
                if(!item.Damage.Sparks) continue;
                for(int n=0;n<3;n++)
                {
                    float angle=(30+60*n)*Mathf.Deg2Rad;var velocity=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*4.5f;
                    var small=item.Damage.Copy();small.Damage=Math.Max(1,small.Damage/2);small.Radius*=.5;small.Sparks=false;small.Delay=0;
                    charmExplosions.Add(new CharmExplosion {Position=position,Velocity=velocity,Started=Now,Due=Now+1,Moving=true,Damage=small,Direction=item.Direction});
                    GetComponent<CombatPresentation>()?.CharmSpark(position,velocity);
                }
            }
        }
    }
}
