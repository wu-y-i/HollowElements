using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using HollowElements.Game.Bosses;
using HollowElements.Rules;
using HollowElements.Settings;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game
{
    internal sealed class EnemyRecord
    {
        internal HealthManager Health;
        internal TargetState State = new TargetState();
        internal int Maximum;
        internal EnemyClock Clock;
        internal bool Boss;
    }

    internal sealed partial class CombatRuntime : MonoBehaviour
    {
        internal static CombatRuntime Current;
        internal readonly AttackTracking Casts = new AttackTracking();
        internal readonly Dictionary<HealthManager, EnemyRecord> Enemies = new Dictionary<HealthManager, EnemyRecord>();
        internal PlayerState Player = new PlayerState();
        internal Element ForcedNailElement;
        internal double ForcedNailUntil;
        internal double ShadowDashDisabledUntil;
        internal Element AttackElement(AttackSource source) => source == AttackSource.Nail && ForcedNailUntil > Now ? ForcedNailElement : source==AttackSource.Nail && Charms.Has(CharmId.C01)?charmNail:Mod.OnSaveLocal().Get(RuleConfig.SlotOf(source));
        internal void ForceNail(Element element, double seconds) { ForcedNailElement = element; ForcedNailUntil = Now + seconds; LogTrace("普攻强制元素 " + element + " 至 " + ForcedNailUntil); }
        internal HollowElementsMod Mod;
        internal SettingsStore Settings;
        internal string Directory;
        internal EnemyRules EnemyRules;
        internal PlayerRules PlayerRules;
        internal RuleConfig Config;
        internal event Action<Vector3, int, Element, Reaction, bool, bool> Feedback;
        internal bool DeferredOverloadFeedback;
        internal float OverloadFeedbackScale=1;
        internal event Action<Vector3, DamageOrigin, bool> Pulse;
        internal event Action Cleared;
        // Capture the old control kind before committing the shatter-cleared state.
        private void EnemyFeedback(EnemyRecord record, TargetState previous, Vector3 position, int damage, Element element, Reaction reaction,HitResult result=null)
        {
            bool frost= reaction==Reaction.Freeze ? !record.State.HardFrozen : reaction==Reaction.Shatter && previous.FrozenUntil>Now && !previous.HardFrozen;
            bool oldDeferred=DeferredOverloadFeedback;float oldScale=OverloadFeedbackScale;
            DeferredOverloadFeedback=false;OverloadFeedbackScale=1;
            if(result!=null) foreach(var secondary in result.Secondary) if(secondary.Origin==DamageOrigin.Overload) {DeferredOverloadFeedback=secondary.Delay>0;OverloadFeedbackScale=(float)(secondary.Radius/Config.Reactions.OverloadRadius);break;}
            try {Feedback?.Invoke(position,damage,element,reaction,false,frost);}
            finally {DeferredOverloadFeedback=oldDeferred;OverloadFeedbackScale=oldScale;}
        }
        internal event Action<GameObject, Element, int, int> PlayerDamageObserved;
        internal readonly Queue<string> Trace = new Queue<string>();
        private int secondaryDepth;
        private int encounterDepth;
        private int playerExtraDepth;
        private int heroHitDepth;
        private bool ownPulse;
        private bool nightmareHit;
        private int shieldSequence;
        private Element incoming;
        private Element incomingPrelude;
        private float nextScan;
        private static readonly FieldInfo Evasion = typeof(HealthManager).GetField("evasionByHitRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
        internal double Now => Time.time;

        internal void Initialize(HollowElementsMod mod, SettingsStore settings, string directory)
        {
            Current = this; Mod = mod; Settings = settings; Directory = directory;
            ResetCharms();
            RefreshRules(); Casts.Trace = LogTrace;
            On.HealthManager.TakeDamage += EnemyHit;
            On.HealthManager.Hit += RouteEnemyHit;
            On.HealthManager.ApplyExtraDamage += NativeExtraDamage;
            On.ExtraDamageable.ApplyExtraDamageToHealthManager += PlayerExtraDamage;
            On.HealthManager.OnEnable += EnemyEnabled;
            On.HeroController.TakeDamage += HeroHit;
            On.PlayerData.TakeHealth += TakeHealth;
            On.HeroController.Move += Move;
            On.HeroController.DoAttack += Attack;
            On.HeroController.HeroDash += Dash;
            On.HeroController.CanCast += CanCast;
            On.HeroController.CanFocus += CanFocus;
            On.HeroController.CanInput += CanInput;
            On.NailSlash.StartSlash += Nail;
            On.HutongGames.PlayMaker.FsmState.OnEnter += StateEntered;
            Modding.ModHooks.GetPlayerIntHook += SpellLevel;
            EnemyClock.Install();
            On.GetNextPreSpawnedGameObject.OnEnter += BossRegistry.Spawned;
            BossHooks.Install();
            gameObject.AddComponent<WaterExposure>().Initialize(this);
        }

        internal void RefreshRules() { Config = Settings.Snapshot(); EnemyRules = new EnemyRules(Config); PlayerRules = new PlayerRules(Config); }
        internal void LogTrace(string message)
        {
            if (!Mod.OnSaveGlobal().DebugEnabled) return;
            Trace.Enqueue(Now.ToString("F3") + " " + message); while (Trace.Count > 160) Trace.Dequeue();
        }
        internal EnemyRecord Track(HealthManager health)
        {
            EnemyRecord record;
            if (!Enemies.TryGetValue(health, out record))
            {
                record = new EnemyRecord { Health = health, Maximum = Math.Max(1, health.hp), Boss = IsBoss(health) };
                record.Clock = health.gameObject.GetComponent<EnemyClock>() ?? health.gameObject.AddComponent<EnemyClock>();
                record.Clock.Record = record;
                Enemies.Add(health, record);
            }
            record.Maximum = Math.Max(record.Maximum, health.hp);
            record.Boss = IsBoss(health);
            BossRegistry.Attach(health.gameObject)?.SyncAura(record.State);
            EncounterSummon.Discover(health);
            return record;
        }
        private static bool IsBoss(HealthManager health)
        {
            if(health.gameObject.scene.name=="GG_Grey_Prince_Zote") return health.name=="Grey Prince";
            if(health.gameObject.scene.name=="GG_Hollow_Knight") return health.name=="HK Prime";
            if(BossRegistry.Attach(health.gameObject)) return true;
            if (BossSceneController.Instance && BossSceneController.Instance.BossHealthLookup != null && BossSceneController.Instance.BossHealthLookup.ContainsKey(health)) return true;
            // Boss scenes are validated by runtime inspection; summons retain their own small bars.
            return false;
        }
        private void EnemyEnabled(On.HealthManager.orig_OnEnable orig, HealthManager health)
        {
            if(health.name=="Head") health.GetComponentInParent<FalseKnightBoss>()?.ResetHead();
            EnemyRecord old;
            if (Enemies.TryGetValue(health, out old)) { if (old.Clock) { old.Clock.Release(); old.Clock.Record = null; } Enemies.Remove(health); }
            orig(health);
            EncounterSummon.Discover(health);
        }
        private static readonly MethodInfo NativeClink=typeof(HealthManager).GetMethod("Invincible",BindingFlags.Instance|BindingFlags.NonPublic);
        private void RouteEnemyHit(On.HealthManager.orig_Hit orig,HealthManager health,HitInstance hit)
        {
            var boss=BossRegistry.Attach(health.gameObject);
            if(secondaryDepth==0 && boss && boss.BlocksHit(hit))
            {
                if(health.isDead || hit.DamageDealt<=0 || (Evasion!=null && (float)Evasion.GetValue(health)>0)) return;
                NativeClink.Invoke(health,new object[]{hit});
                if(boss.BlockedHitGivesSoul && hit.AttackType==AttackTypes.Nail) HeroController.SilentInstance?.SoulGain();
                ProtectedHit(health,hit,boss); return;
            }
            orig(health,hit);
        }
        private void ProtectedHit(HealthManager health,HitInstance hit,BossBehaviour boss)
        {
            AttackSource source;long cast;
            if(!Casts.Resolve(hit,out source,out cast)) return;
            int amount=Mathf.RoundToInt(hit.DamageDealt*hit.Multiplier);if(amount<=0) return;
            var record=Track(health);var recoil=health.GetComponent<Recoil>();
            var context=new AttackContext {Source=source,CastId=cast,Element=AttackElement(source),Damage=amount,
                BaseNailDamage=Math.Max(1,PlayerData.instance.nailDamage),SkipSuperconduct=true,TargetCanRecoil=recoil && !recoil.freezeInPlace && health.GetComponent<Rigidbody2D>()};
            boss.PrepareHit(context);var result=EnemyRules.Prepare(record.State,context,Now,Mod.RuleModifier,Charms);
            boss.FilterResult(result);result.Damage=0;var previous=record.State;NoDamageFourPhase(result,previous);record.State=result.NextState;Charms=result.NextCharms;boss.CommitAura(record.State);
            var position=health.transform.position;
            foreach(var pre in result.PreludeReactions) EnemyFeedback(record,previous,position,0,Element.Cryo,pre);
            EnemyFeedback(record,previous,position,0,context.Element,result.Reaction,result);
            foreach(var extra in result.Secondary)
            {
                if(extra.Origin==DamageOrigin.Overload && extra.Delay>0) {QueueExplosion(health,position,extra,hit.Direction);continue;}
                if(extra.Origin==DamageOrigin.Overload)
                {
                    foreach(var target in FindObjectsOfType<HealthManager>())
                        if(!target.isDead && Vector2.Distance(target.transform.position,position)<=extra.Radius) Extra(Track(target),extra,hit.Direction,false);
                }
                else Extra(record,extra,hit.Direction,false);
            }
            if(health && !health.isDead) {foreach(var pre in result.PreludeReactions) boss.Reacted(pre);if(result.Reaction!=Reaction.None) boss.Reacted(result.Reaction);}
            LogTrace("护甲有效附着 " + health.name + " " + context.Element + " " + result.Reaction);
        }
        private void StateEntered(On.HutongGames.PlayMaker.FsmState.orig_OnEnter orig, FsmState state)
        {
            var boss = state.Fsm.GameObject ? BossRegistry.Attach(state.Fsm.GameObject) : null;
            string redirect = boss?.Redirect(state);
            if (redirect != null) { state.Fsm.SetState(redirect); return; }
            Casts.StateEntered(state); BossRegistry.Entering(state); orig(state); BossRegistry.Entered(state);
        }
        private void Nail(On.NailSlash.orig_StartSlash orig, NailSlash slash)
        {
            if(Charms.Has(CharmId.C01)) charmNail=elementDeck.Next();
            Casts.Begin(AttackSlot.Nail); Casts.Stamp(slash.gameObject, true); orig(slash);
        }
        private void EnemyHit(On.HealthManager.orig_TakeDamage orig, HealthManager health, HitInstance hit)
        {
            if (secondaryDepth > 0) { orig(health, hit); return; }
            var summon=health.GetComponent<EncounterSummon>(); summon?.CaptureMaximum();
            AttackSource source; long cast;
            bool tagged=Casts.Resolve(hit,out source,out cast);
            bool auxiliary=!tagged && PlayerOwnedDamage(hit);
            if(auxiliary) {source=AttackSource.NativeAuxiliary;cast=Casts.NextNativeCast();}
            if (!tagged && !auxiliary)
            {
                var unknownBoss = BossRegistry.Attach(health.gameObject);
                if (Mathf.RoundToInt(hit.DamageDealt * hit.Multiplier) > 0)
                {
                    unknownBoss?.PrepareUnknownHit(ref hit);
                    if (unknownBoss) { hit.DamageDealt = Reactions.FinalDamage(unknownBoss.DirectDamage(Element.None, hit.DamageDealt * (double)hit.Multiplier)); hit.Multiplier = 1; }
                }
                LogTrace("未知来源 " + (hit.Source ? hit.Source.name : "null") + " / " + hit.AttackType);
                var neutralDummy = health.GetComponent<TrainingDummy>();
                if (neutralDummy) { neutralDummy.Accept(hit); Feedback?.Invoke(health.transform.position, hit.DamageDealt, Element.None, Reaction.None, false,false); }
                else
                {
                    int beforeUnknown = health.hp; orig(health, hit);
                    if (health && health.hp < beforeUnknown)
                    {
                        var controllerUnknown=BossRegistry.Attach(health.gameObject);
                        controllerUnknown?.DirectHitAccepted(Element.None,Reaction.None,Math.Min(beforeUnknown,beforeUnknown-health.hp));
                        if(!health.isDead) controllerUnknown?.AcceptedHit();
                    }
                }
                return;
            }
            var record = Track(health);
            double effectiveDamage=hit.DamageDealt*(double)hit.Multiplier;
            if(effectiveDamage<=0)
            {
                var zeroDummy=health.GetComponent<TrainingDummy>();
                if(zeroDummy) zeroDummy.Accept(hit); else orig(health,hit);
                return;
            }
            var recoil = health.GetComponent<Recoil>();
            var context = new AttackContext { Source = source, CastId = cast, Element = auxiliary?Element.None:AttackElement(source),CanApplyAura=!auxiliary,
                Damage = effectiveDamage, BaseNailDamage = Math.Max(1, PlayerData.instance.nailDamage),
                TargetCanRecoil = recoil && !recoil.freezeInPlace && health.GetComponent<Rigidbody2D>() };
            var controller = BossRegistry.Attach(health.gameObject);
            context.AnySuperconductTarget=HasSuperconductTarget();
            if(Charms.Has(CharmId.C03) && Charms.InsulatedUntil>Now && !context.AnySuperconductTarget && IsSword(source)) Charms.InsulatedUntil=0;
            if(Charms.Has(CharmId.C03) && Charms.InsulatedUntil>Now && record.State.SuperconductStacks>0 && record.State.SuperconductUntil>Now && IsSword(source)) context.Element=Element.None;
            controller?.PrepareHit(context);
            if(controller && controller.DirectDamage(context.Element,1)<=0) context.SkipSuperconduct=true;
            var result = EnemyRules.Prepare(record.State, context, Now, Mod.RuleModifier,Charms);
            controller?.FilterResult(result);
            if (controller) result.PreciseDamage = controller.DirectDamage(result.ResolvedElement, result.PreciseDamage);
            // TakeDamage is reached only after native Hit accepted direction/evasion. Custom shields
            // and elemental immunity must still commit aura at zero HP loss, without fake chip damage.
            bool commitZero = result.Damage == 0 && controller && controller.AcceptZeroDamage(context.Element) && !health.IsBlockingByDirection(DirectionUtils.GetCardinalDirection(hit.GetActualDirection(health.transform)),hit.AttackType) && !health.isDead;
            int before = health.hp;
            hit.DamageDealt = result.Damage; hit.Multiplier = 1;
            var dummy = health.GetComponent<TrainingDummy>();
            if (dummy) dummy.Accept(hit); else orig(health, hit);
            bool accepted = !health || health.hp < before || health.isDead || commitZero;
            LogTrace(source + " #" + cast + " " + context.Element + " " + before + " -> " + (health ? health.hp : 0) + " " + result.Reaction);
            if (!accepted) return;
            var previous=record.State;
            if(health && health.hp>=before) NoDamageFourPhase(result,previous);
            if(result.NextCharms!=null) Charms=result.NextCharms;
            record.State = result.NextState;
            if(health && health.name=="Head") health.GetComponentInParent<FalseKnightBoss>()?.RememberHead(record.State);
            controller?.CommitAura(record.State);
            Vector3 position = health ? health.transform.position : record.Clock.transform.position;
            foreach(var pre in result.PreludeReactions) EnemyFeedback(record,previous,position,0,Element.Cryo,pre);
            EnemyFeedback(record,previous,position, result.Damage, result.ResolvedElement, result.Reaction,result);
            if(health && health.hp<before) controller?.DirectHitAccepted(context.Element,result.Reaction,Math.Min(before,before-health.hp));
            foreach (var extra in result.Secondary)
            {
                if(extra.Origin==DamageOrigin.Overload && extra.Delay>0) {QueueExplosion(health,position,extra,hit.Direction);continue;}
                if (extra.Origin == DamageOrigin.Overload)
                {
                    foreach (var target in FindObjectsOfType<HealthManager>())
                        if (!target.isDead && Vector2.Distance(target.transform.position, position) <= extra.Radius)
                        {
                            Vector2 offset = target.transform.position - position;
                            float direction = offset.sqrMagnitude < .001f ? hit.Direction : Mathf.Atan2(offset.y,offset.x)*Mathf.Rad2Deg;
                            Extra(Track(target), extra, direction, false);
                        }
                }
                else Extra(record, extra, hit.Direction, false);
            }
            if (health && !health.isDead)
            {
                var boss = BossRegistry.Attach(health.gameObject);
                foreach(var pre in result.PreludeReactions) boss?.Reacted(pre);
                if (result.Reaction != Reaction.None) boss?.Reacted(result.Reaction);
                boss?.AcceptedHit();
            }
        }

        internal void ApplyAttachment(HealthManager health, Element element, double units = 1)
        {
            if (!health || health.isDead || health.hp<=0) return;
            var record = Track(health); var recoil = health.GetComponent<Recoil>();
            var result = EnemyRules.Attach(record.State, element, units, Math.Max(1, PlayerData.instance.nailDamage), recoil && !recoil.freezeInPlace && health.GetComponent<Rigidbody2D>(), Now);
            var controller=BossRegistry.Attach(health.gameObject); controller?.FilterResult(result);
            var previous=record.State;
            record.State = result.NextState;
            if(health && health.name=="Head") health.GetComponentInParent<FalseKnightBoss>()?.RememberHead(record.State);
            controller?.CommitAura(record.State);
            if (result.Reaction == Reaction.None) return;
            Vector3 position = health.transform.position;
            EnemyFeedback(record,previous,position, 0, element, result.Reaction);
            foreach (var extra in result.Secondary)
            {
                if (extra.Origin == DamageOrigin.Overload)
                {
                    foreach (var target in FindObjectsOfType<HealthManager>())
                        if (!target.isDead && Vector2.Distance(target.transform.position, position) <= extra.Radius) Extra(Track(target), extra, 0, false);
                }
                else Extra(record, extra, 0, false);
            }
            if (health && !health.isDead) BossRegistry.Attach(health.gameObject)?.Reacted(result.Reaction);
        }

        private bool Extra(EnemyRecord target, SecondaryDamage damage, float direction, bool periodic)
        {
            var health = target.Health;
            if (!health || !health.isActiveAndEnabled || health.isDead || health.hp<=0) return false;
            if (periodic && Evasion != null && (float)Evasion.GetValue(health) > 0) return false;
            var controller=BossRegistry.Attach(health.gameObject);
            if (health.IsBlockingByDirection(DirectionUtils.GetCardinalDirection(direction), AttackTypes.Spell) && !(controller?.AllowsSecondaryThroughArmour(damage) ?? false)) return false;
            int amount = controller?.FilterSecondary(damage) ?? damage.Damage;
            if(amount>0 && target.State.Charms.FragileUntil>Now) amount=Reactions.Damage(amount*1.5);
            if(amount>0 && damage.Element!=Element.None && Charms.Has(CharmId.C06) && Charms.FourUntil>Now) amount=Reactions.Damage(amount*(1+.05*Charms.Bonuses[0]));
            // A permanent immunity/shield resolves a zero-damage tick; ordinary native
            // invulnerability above still defers it. Otherwise immune electro-charged never expires.
            if (amount <= 0) return controller && controller.AcceptZeroDamage(damage.Element);
            int before = health.hp;
            secondaryDepth++;
            try
            {
                // Accepted extra damage bypasses the original hit's short evasion, adds no soul or aura.
                var dummy = health.GetComponent<TrainingDummy>();
                if (dummy) dummy.AcceptExtra(amount); else health.ApplyExtraDamage(amount);
                if (damage.HeavyRecoil && health && health.hp>0 && !health.isDead)
                {
                    var recoil = health.GetComponent<Recoil>();
                    if (recoil) { recoil.CancelRecoil(); recoil.RecoilByDirection(DirectionUtils.GetCardinalDirection(direction), (float)damage.RecoilMultiplier); }
                }
            }
            finally { secondaryDepth--; }
            bool success = !health || health.hp < before || health.isDead;
            if (success)
            {
                if(damage.Soul) HeroController.SilentInstance?.SoulGain();
                controller?.SecondaryHitAccepted(damage,health ? Math.Min(before,Math.Max(0,before-health.hp)) : before);
                LogTrace(damage.Origin + " 追加伤害 " + amount + " → " + health.name);
                Vector3 p = target.Clock.transform.position;
                Feedback?.Invoke(p, amount, damage.Element, Reaction.None, false,false);
                Pulse?.Invoke(p, damage.Origin, false);
            }
            return success;
        }
        private void NativeExtraDamage(On.HealthManager.orig_ApplyExtraDamage orig,HealthManager health,int amount)
        {
            if(secondaryDepth==0 && encounterDepth==0)
            {
                // Original charm clouds enter through ExtraDamageable. A bare
                // ApplyExtraDamage also serves Ooma/scene mechanics and other mods.
                double scaled=amount*(playerExtraDepth>0 ? Config.DirectDamageMultiplier : 1);
                amount=Reactions.FinalDamage(BossRegistry.Attach(health.gameObject)?.DirectDamage(Element.None,scaled) ?? scaled);
            }
            if(amount<=0) return;
            var summon=health.GetComponent<EncounterSummon>(); summon?.CaptureMaximum();
            int before=health.hp; orig(health,amount);
            if(secondaryDepth==0 && encounterDepth==0 && health && health.hp<before)
            {
                BossRegistry.Attach(health.gameObject)?.DirectHitAccepted(Element.None,Reaction.None,Math.Min(before,before-health.hp));
                Feedback?.Invoke(health.transform.position,Math.Min(before,before-health.hp),Element.None,Reaction.None,false,false);
            }
        }
        private void PlayerExtraDamage(On.ExtraDamageable.orig_ApplyExtraDamageToHealthManager orig,ExtraDamageable source,int amount)
        { playerExtraDepth++;try {orig(source,amount);}finally {playerExtraDepth--;} }
        internal void NeutralEncounterDamage(HealthManager health,int amount,Element element=Element.None)
        {
            if(!health || health.isDead || amount<=0) return;
            int before=health.hp; var position=health.transform.position;
            encounterDepth++;try {health.ApplyExtraDamage(amount);}finally {encounterDepth--;}
            Feedback?.Invoke(position,health ? Math.Min(before,Math.Max(0,before-health.hp)) : before,element,Reaction.None,false,false);
        }

        private static bool PlayerOwnedDamage(HitInstance hit)
        {
            var source=hit.Source;if(!source) return false;
            if(source.GetComponentInParent<BossBehaviour>() || source.GetComponentInParent<ElementalAttack>() || source.GetComponentInParent<ElementalMinion>()) return false;
            if(source.GetComponent<DamageEnemies>() || source.GetComponentInParent<HeroController>()) return true;
            if(source.GetComponentInParent<HealthManager>()) return false;
            return
                hit.AttackType==AttackTypes.Nail || hit.AttackType==AttackTypes.NailBeam || hit.AttackType==AttackTypes.Spell;
        }

        private void HeroHit(On.HeroController.orig_TakeDamage orig, HeroController hero, GameObject source, CollisionSide side, int amount, int hazard)
        {
            Element previous = incoming;
            bool previousNightmare=nightmareHit;
            nightmareHit=source && source!=hero.gameObject && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="GG_Grimm_Nightmare" && (source.GetComponentInParent<GrimmBoss>() || source.GetComponentInParent<ElementalAttack>() || source.GetComponent<DamageHero>());
            Element previousPrelude=incomingPrelude;
            incomingPrelude=RadianceBoss.Prelude(source);
            if (source && source != hero.gameObject) incoming = BossRegistry.Contact(source, ref amount);
            if(nightmareHit) amount=Math.Max(2,amount)+(Player.Aura==Element.Pyro && Player.AuraUntil>Now?1:0);
            Element attackElement = incoming;
            int before = PlayerData.instance.health + PlayerData.instance.healthBlue;
            int beforeShield=shieldSequence;
            heroHitDepth++;
            try { orig(hero, source, side, amount, hazard); }
            finally
            {
                heroHitDepth--; incoming = previous; incomingPrelude=previousPrelude;nightmareHit=previousNightmare;
                int after = PlayerData.instance.health + PlayerData.instance.healthBlue;
                if (after < before || shieldSequence!=beforeShield)
                {
                    CrystalGuardianBoss.PlayerHurt(source);
                    ElderHuBoss.PlayerHurt(source);
                    LogTrace("玩家有效受伤 " + (source ? source.name : "null") + " " + attackElement + " " + before + " -> " + after + "，附着 " + Player.Aura);
                    PlayerDamageObserved?.Invoke(source, attackElement, before, after);
                }
            }
        }
        private void TakeHealth(On.PlayerData.orig_TakeHealth orig, PlayerData data, int amount)
        {
            if (amount <= 0 || heroHitDepth == 0 || amount >= 9999) { orig(data, amount); return; }
            if (ownPulse) { if(Charms.Masks>0) {Charms.Masks--;shieldSequence++;} else orig(data, 1); return; }
            // Enemy hits are independent applications. Only outgoing player attacks use AttackProfile ICD.
            var initial=Player;
            if(incomingPrelude!=Element.None)
            {
                initial=Player.Copy();initial.Aura=Element.None;initial.AuraUnits=0;initial.AuraUntil=0;initial.AuraDuration=0;
                initial=PlayerRules.Attach(initial,incomingPrelude,1,Now,Charms).NextState;
            }
            var result = PlayerRules.Prepare(initial, amount, incoming, 1, Now,Charms);
            int before = data.health + data.healthBlue;
            bool shield=Charms.Masks>0;
            if(shield) {result.NextCharms.Masks--;shieldSequence++;} else orig(data, result.Damage);
            if (!shield && data.health + data.healthBlue >= before) return;
            Charms=result.NextCharms;
            Player = result.NextState;
            if(result.Reaction==Reaction.Freeze) FreezeMaskPenalty(data,orig);
            if (HeroController.SilentInstance)
            {
                Feedback?.Invoke(HeroController.SilentInstance.transform.position, 0, incoming, result.Reaction, true,false);
                if (result.Reaction == Reaction.Superconduct && !Charms.Suppressed(Now))
                    HeroController.SilentInstance.spellControl.SendEvent("CANCEL");
            }
        }
        internal bool HurtPlayer(Element element, int amount, bool pulse = false)
        {
            var hero = HeroController.SilentInstance;
            if (!hero) return false;
            int before = PlayerData.instance.health + PlayerData.instance.healthBlue;
            int beforeShield=shieldSequence;
            Element saved = incoming; bool savedPulse = ownPulse;
            incoming = element; ownPulse = pulse;
            try { hero.TakeDamage(hero.gameObject, CollisionSide.left, amount, 1); }
            finally { incoming = saved; ownPulse = savedPulse; }
            return PlayerData.instance.health + PlayerData.instance.healthBlue < before || shieldSequence!=beforeShield;
        }
        internal void ApplyPlayerAttachment(Element element,double units=1)
        {
            var hero=HeroController.SilentInstance;
            if(!hero || hero.cState.dead || PlayerData.instance.health+PlayerData.instance.healthBlue<=0) return;
            var result=PlayerRules.Attach(Player,element,units,Now,Charms); Player=result.NextState;Charms=result.NextCharms;
            if(result.Reaction==Reaction.Freeze) FreezeMaskPenalty(PlayerData.instance,(data,amount)=>data.TakeHealth(amount));
            Feedback?.Invoke(hero.transform.position,0,element,result.Reaction,true,false);
            if(result.Reaction==Reaction.Superconduct && !Charms.Suppressed(Now)) hero.spellControl.SendEvent("CANCEL");
            LogTrace("环境附着 " + element + " " + units + "U " + result.Reaction);
        }
        private void Move(On.HeroController.orig_Move orig, HeroController hero, float direction)
        { orig(hero, Player.FrozenUntil > Now && !Charms.Suppressed(Now) ? direction * (float)Player.MoveScale : direction); }
        private void Dash(On.HeroController.orig_HeroDash orig,HeroController hero)
        {
            if(ShadowDashDisabledUntil<=Now) {orig(hero);return;}
            // Native HeroDash reads the raw field. Restrict only this call so
            // no ability/save data is changed for the three-second debuff.
            var data=PlayerData.instance;bool original=data.hasShadowDash;
            try {data.hasShadowDash=false;orig(hero);} finally {data.hasShadowDash=original;}
        }
        private void Attack(On.HeroController.orig_DoAttack orig, HeroController hero)
        {
            if (Player.FrozenUntil <= Now || Charms.Suppressed(Now)) { orig(hero); return; }
            float normal = hero.ATTACK_COOLDOWN_TIME, quick = hero.ATTACK_COOLDOWN_TIME_CH;
            try { hero.ATTACK_COOLDOWN_TIME /= (float)Player.AttackSpeedScale; hero.ATTACK_COOLDOWN_TIME_CH /= (float)Player.AttackSpeedScale; orig(hero); }
            finally { hero.ATTACK_COOLDOWN_TIME = normal; hero.ATTACK_COOLDOWN_TIME_CH = quick; }
        }
        private bool CanCast(On.HeroController.orig_CanCast orig, HeroController hero) { return (Player.SuperconductUntil <= Now || Charms.Suppressed(Now)) && orig(hero); }
        private bool CanFocus(On.HeroController.orig_CanFocus orig, HeroController hero) { return (Player.SuperconductUntil <= Now || Charms.Suppressed(Now)) && orig(hero); }
        private bool CanInput(On.HeroController.orig_CanInput orig, HeroController hero) { return !WorkshopMenus.CapturesInput && !ElementGuideWindow.CapturesInput && orig(hero); }
        private int SpellLevel(string field, int value)
        {
            if (Player.FrozenUntil > Now && !Charms.Suppressed(Now) && (field == "fireballLevel" || field == "quakeLevel" || field == "screamLevel")) return Math.Min(1, value);
            return value;
        }

        private void Update()
        {
            if (Time.timeScale == 0) return;
            var hero = HeroController.SilentInstance;
            Charms.Expire(Now);
            if(hero) Casts.PrepareHero(hero);
            if (hero && (hero.cState.dead || PlayerData.instance.health + PlayerData.instance.healthBlue <= 0)) { Player = new PlayerState(); ResetCharms(); ForcedNailUntil = 0; ForcedNailElement = Element.None; return; }
            UpdateExplosions();
            PlayerRules.Tick(Player, Now, () => {
                if(Charms.Suppressed(Now)) return true;
                bool accepted = HurtPlayer(Element.None, 1, true);
                if (accepted) LogTrace("玩家感电实际跳伤");
                if (accepted && HeroController.SilentInstance) Pulse?.Invoke(HeroController.SilentInstance.transform.position, DamageOrigin.ElectroCharged, true);
                return accepted;
            });
            var remove = new List<HealthManager>();
            foreach (var pair in new List<KeyValuePair<HealthManager, EnemyRecord>>(Enemies))
            {
                var record = pair.Value;
                // Special-death bosses can keep isDead=false through the entire exit FSM.
                // Retrying a DOT on zero HP re-sends ZERO HP every frame and restarts it.
                if (!pair.Key || !pair.Key.isActiveAndEnabled || pair.Key.isDead || pair.Key.hp<=0) { if(pair.Key && pair.Key.name=="Head") pair.Key.GetComponentInParent<FalseKnightBoss>()?.RememberHead(record.State); if (record.Clock) { record.Clock.Release(); record.Clock.Record = null; } remove.Add(pair.Key); continue; }
                record.State.Expire(Now);
                var ec = record.State.ElectroCharged;
                if (ec != null && ec.NextTick <= Now)
                {
                    var damage = new SecondaryDamage { Origin = DamageOrigin.ElectroCharged, Element = Element.Electro, Damage = ec.Damage };
                    if (Extra(record, damage, 0, true))
                    { ec.RemainingTicks--; ec.NextTick = Now + ec.Interval; if (ec.RemainingTicks <= 0) record.State.ElectroCharged = null; }
                }
            }
            foreach (var key in remove) Enemies.Remove(key);
            if (Time.unscaledTime >= nextScan && HeroController.SilentInstance)
            {
                nextScan = Time.unscaledTime + 0.35f;
                foreach (var health in FindObjectsOfType<HealthManager>()) if (!health.isDead && health.hp>0) Track(health);
            }
        }
        internal void Clear(bool preserveMasks=false)
        {
            int masks=preserveMasks?Math.Min(1,Charms.Masks):0;double maskReady=preserveMasks?Charms.MaskReadyAt:0;ResetCharms();Charms.Masks=masks;Charms.MaskReadyAt=maskReady;
            foreach(var boss in FindObjectsOfType<GodTamerBoss>()) boss.ClearSharedAura();
            foreach (var record in Enemies.Values) if (record.Clock) { record.Clock.Release(); record.Clock.Record = null; }
            Enemies.Clear(); Player = new PlayerState(); ForcedNailUntil = 0; ShadowDashDisabledUntil=0; ForcedNailElement = Element.None; Casts.Clear(); Trace.Clear();
            Cleared?.Invoke();
        }
        private void OnDestroy()
        {
            Clear();
            On.HealthManager.TakeDamage -= EnemyHit; On.HealthManager.OnEnable -= EnemyEnabled;
            On.HealthManager.Hit -= RouteEnemyHit;
            On.HealthManager.ApplyExtraDamage -= NativeExtraDamage;
            On.ExtraDamageable.ApplyExtraDamageToHealthManager -= PlayerExtraDamage;
            On.HeroController.TakeDamage -= HeroHit; On.PlayerData.TakeHealth -= TakeHealth;
            On.HeroController.Move -= Move; On.HeroController.DoAttack -= Attack;
            On.HeroController.HeroDash -= Dash;
            On.HeroController.CanCast -= CanCast; On.HeroController.CanFocus -= CanFocus;
            On.HeroController.CanInput -= CanInput;
            On.NailSlash.StartSlash -= Nail;
            On.HutongGames.PlayMaker.FsmState.OnEnter -= StateEntered;
            Modding.ModHooks.GetPlayerIntHook -= SpellLevel;
            EnemyClock.Uninstall(); Current = null;
            On.GetNextPreSpawnedGameObject.OnEnter -= BossRegistry.Spawned;
            BossHooks.Uninstall();
            BossRegistry.RemoveAll();
            WaterExposure.RemoveRegions();
            foreach(var hold in Resources.FindObjectsOfTypeAll<EnemyPhysicsHold>()) Destroy(hold);
            foreach (var clock in Resources.FindObjectsOfTypeAll<EnemyClock>()) { clock.Release(); Destroy(clock); }
            foreach(var tint in Resources.FindObjectsOfTypeAll<AttackTint>()) { tint.Restore(); Destroy(tint); }
            foreach(var stamp in Resources.FindObjectsOfTypeAll<AttackStamp>()) Destroy(stamp);
            foreach (var dummy in FindObjectsOfType<TrainingDummy>()) Destroy(dummy.gameObject);
        }
    }
}
