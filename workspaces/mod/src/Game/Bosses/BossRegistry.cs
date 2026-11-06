using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Registration is explicit and scoped to the requested Godhome encounters, including P5 variants.
    internal static class BossRegistry
    {
        internal static BossBehaviour Attach(GameObject obj)
        {
            if (!obj || !obj.scene.name.StartsWith("GG_")) return null;
            var existing = obj.GetComponent<BossBehaviour>();
            if (existing) return existing;
            string scene = obj.scene.name;
            foreach (var fsm in obj.GetComponents<PlayMakerFSM>())
            {
                if ((scene == "GG_Vengefly" || scene == "GG_Vengefly_V") && fsm.FsmName == "Big Buzzer") return obj.AddComponent<VengeflyKingBoss>();
                if ((scene == "GG_Gruz_Mother" || scene == "GG_Gruz_Mother_V") && fsm.FsmName == "Big Fly Control") return obj.AddComponent<GruzMotherBoss>();
                if ((scene == "GG_False_Knight" || scene=="GG_Failed_Champion") && fsm.FsmName == "FalseyControl") return obj.AddComponent<FalseKnightBoss>();
                if (scene == "GG_Mega_Moss_Charger" && fsm.FsmName == "Mossy Control") return obj.AddComponent<MassiveMossChargerBoss>();
                if (scene == "GG_Hornet_1" && fsm.FsmName == "Control" && obj.name == "Hornet Boss 1") return obj.AddComponent<HornetProtectorBoss>();
                if(scene=="GG_Hornet_2" && fsm.FsmName=="Control" && obj.name=="Hornet Boss 2") return obj.AddComponent<HornetSentinelBoss>();
                if ((scene == "GG_Ghost_Gorb" || scene == "GG_Ghost_Gorb_V") && fsm.FsmName == "Attacking") return obj.AddComponent<GorbBoss>();
                if ((scene == "GG_Dung_Defender" || scene=="GG_White_Defender") && fsm.FsmName == "Dung Defender") return obj.AddComponent<DungDefenderBoss>();
                if ((scene == "GG_Mage_Knight" || scene == "GG_Mage_Knight_V") && fsm.FsmName == "Mage Knight") return obj.AddComponent<SoulWarriorBoss>();
                if ((scene == "GG_Brooding_Mawlek" || scene == "GG_Brooding_Mawlek_V") && fsm.FsmName == "Mawlek Control") return obj.AddComponent<BroodingMawlekBoss>();
                if (scene == "GG_Nailmasters" && fsm.FsmName == "nailmaster") return obj.AddComponent<NailBrothersBoss>();
                if ((scene == "GG_Ghost_Xero" || scene == "GG_Ghost_Xero_V") && fsm.FsmName == "Sword Summon") return obj.AddComponent<XeroBoss>();
                if ((scene == "GG_Crystal_Guardian" || scene == "GG_Crystal_Guardian_2") && fsm.FsmName == "Beam Miner") return obj.AddComponent<CrystalGuardianBoss>();
                if ((scene == "GG_Soul_Master" || scene == "GG_Soul_Tyrant") && (fsm.FsmName == "Mage Lord" || fsm.FsmName == "Mage Lord 2")) return obj.AddComponent<SoulMasterBoss>();
                if (scene == "GG_Oblobbles" && fsm.FsmName == "Fatty Fly Attack") return obj.AddComponent<OblobblesBoss>();
                if ((scene == "GG_Mantis_Lords" || scene == "GG_Mantis_Lords_V") && fsm.FsmName == "Mantis Lord") return obj.AddComponent<MantisLordsBoss>();
                if ((scene=="GG_Ghost_Marmu" || scene=="GG_Ghost_Marmu_V") && obj.name=="Ghost Warrior Marmu" && fsm.FsmName=="Control") return obj.AddComponent<MarmuBoss>();
                if(scene=="GG_Flukemarm" && fsm.FsmName=="Fluke Mother") return obj.AddComponent<FlukemarmBoss>();
                if((scene=="GG_Broken_Vessel" || scene=="GG_Lost_Kin") && fsm.FsmName=="IK Control") return obj.AddComponent<BrokenVesselBoss>();
                if(scene=="GG_Ghost_Galien" && obj.name=="Ghost Warrior Galien" && fsm.FsmName=="Movement") return obj.AddComponent<GalienBoss>();
                if(scene=="GG_Painter" && fsm.FsmName=="nailmaster_sheo") return obj.AddComponent<SheoBoss>();
                if(scene=="GG_Hive_Knight" && obj.name=="Hive Knight" && fsm.FsmName=="Control") return obj.AddComponent<HiveKnightBoss>();
                if(scene=="GG_Ghost_Hu" && obj.name=="Ghost Warrior Hu" && fsm.FsmName=="Attacking") return obj.AddComponent<ElderHuBoss>();
                if((scene=="GG_Collector" || scene=="GG_Collector_V") && obj.name=="Jar Collector" && fsm.FsmName=="Control") return obj.AddComponent<CollectorBoss>();
                if(scene=="GG_God_Tamer" && (obj.name=="Lancer" || obj.name=="Lobster") && fsm.FsmName=="Control") return obj.AddComponent<GodTamerBoss>();
                if((scene=="GG_Grimm" || scene=="GG_Grimm_Nightmare") && (obj.name=="Grimm Boss" || obj.name=="Nightmare Grimm Boss") && fsm.FsmName=="Control") return obj.AddComponent<GrimmBoss>();
                if(scene=="GG_Watcher_Knights" && fsm.FsmName=="Black Knight") return obj.AddComponent<WatcherKnightsBoss>();
                if((scene=="GG_Uumuu" || scene=="GG_Uumuu_V") && fsm.FsmName=="Mega Jellyfish") return obj.AddComponent<UumuuBoss>();
                if((scene=="GG_Nosk" || scene=="GG_Nosk_V" || scene=="GG_Nosk_Hornet") && (fsm.FsmName=="Mimic Spider" || fsm.FsmName=="Hornet Nosk")) return obj.AddComponent<NoskBoss>();
                if(scene=="GG_Sly" && obj.name=="Sly Boss" && fsm.FsmName=="Control") return obj.AddComponent<SlyBoss>();
                if((scene=="GG_Ghost_No_Eyes" || scene=="GG_Ghost_No_Eyes_V") && obj.name=="Ghost Warrior No Eyes" && fsm.FsmName=="Movement") return obj.AddComponent<NoEyesBoss>();
                if(scene=="GG_Traitor_Lord" && obj.name=="Mantis Traitor Lord" && fsm.FsmName=="Mantis") return obj.AddComponent<TraitorLordBoss>();
                if((scene=="GG_Ghost_Markoth" || scene=="GG_Ghost_Markoth_V") && obj.name=="Ghost Warrior Markoth" && fsm.FsmName=="Movement") return obj.AddComponent<MarkothBoss>();
                if(scene=="GG_Grey_Prince_Zote" && obj.name=="Grey Prince" && fsm.FsmName=="Control") return obj.AddComponent<GreyPrinceBoss>();
                if(scene=="GG_Hollow_Knight" && obj.name=="HK Prime" && fsm.FsmName=="Control") return obj.AddComponent<PureVesselBoss>();
                if(scene=="GG_Radiance" && obj.name=="Absolute Radiance" && fsm.FsmName=="Control") return obj.AddComponent<RadianceBoss>();
            }
            return null;
        }

        internal static void Entering(FsmState state) { BossHooks.ActorState(state); if (state.Fsm.GameObject) {state.Fsm.GameObject.GetComponent<DungBallSplit>()?.Terrain(state);Attach(state.Fsm.GameObject)?.Entering(state);} }
        internal static void Entered(FsmState state) { if (state.Fsm.GameObject) state.Fsm.GameObject.GetComponent<BossBehaviour>()?.Entered(state); }

        internal static Element Contact(GameObject source, ref int damage)
        {
            if (!source) return Element.None;
            var shield=source.GetComponentInParent<MarkothShield>();if(shield) return shield.Element;
            var sly=source.GetComponentInParent<SlyAttack>();if(sly && sly.Owner) return sly.Owner.ElementFor(sly.Slot);
            var radiance=source.GetComponentInParent<RadianceAttack>();if(radiance && radiance.Owner) return radiance.Element;
            var attack = source.GetComponentInParent<ElementalAttack>();
            if (attack && attack.isActiveAndEnabled)
            {
                foreach(var owner in Object.FindObjectsOfType<BossBehaviour>())
                    if(owner.GetComponent<HealthManager>() && owner.GetComponent<HealthManager>().GetInstanceID()==attack.OwnerId) { damage=owner.ContactDamage(damage); break; }
                return GrimmDamage(source,attack.Element,ref damage);
            }
            var minion = source.GetComponentInParent<ElementalMinion>();
            if (minion && minion.isActiveAndEnabled) return minion.Element;
            var boss = source.GetComponentInParent<BossBehaviour>();
            if (!boss) return GrimmDamage(source,Element.None,ref damage);
            damage = boss.ContactDamage(damage);
            return GrimmDamage(source,boss.AttackElement(source),ref damage);
        }
        private static Element GrimmDamage(GameObject source,Element element,ref int damage)
        {
            string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if((scene!="GG_Grimm" && scene!="GG_Grimm_Nightmare") || !source.GetComponent<DamageHero>() || source.GetComponentInParent<HeroController>()) return element;
            var runtime=CombatRuntime.Current;
            if(scene!="GG_Grimm_Nightmare" && runtime && runtime.Player.Aura==Element.Pyro && runtime.Player.AuraUntil>runtime.Now) damage*=2;
            return Element.Pyro;
        }

        internal static void Spawned(On.GetNextPreSpawnedGameObject.orig_OnEnter orig, GetNextPreSpawnedGameObject action)
        {
            orig(action);
            var king = action.Fsm.GameObject ? action.Fsm.GameObject.GetComponent<VengeflyKingBoss>() : null;
            if (!king || action.State.Name != "GG Summon" || !action.storeObject.Value) return;
            var spawner = action.storeObject.Value.GetComponent<EnemySpawner>();
            if (spawner) (spawner.GetComponent<SummonInheritance>() ?? spawner.gameObject.AddComponent<SummonInheritance>()).Configure(spawner, king.SummonElement, king.GetComponent<HealthManager>().GetInstanceID());
        }

        internal static void RemoveAll()
        {
            foreach(var item in Resources.FindObjectsOfTypeAll<BarbElementRing>()) Object.Destroy(item);
            foreach (var item in Resources.FindObjectsOfTypeAll<EnemyAttackTint>()) { item.Restore(); Object.Destroy(item); }
            foreach (var item in Resources.FindObjectsOfTypeAll<CrystalShieldVisual>()) { item.Restore(); Object.Destroy(item); }
            foreach (var item in Resources.FindObjectsOfTypeAll<NailBrothersBoss>()) item.Restore();
            foreach (var item in Resources.FindObjectsOfTypeAll<NailBladeTint>()) { item.Restore(); Object.Destroy(item); }
            NailBladeTint.Release();
            foreach (var item in Resources.FindObjectsOfTypeAll<ElementalAttack>()) { item.Restore(); Object.Destroy(item); }
            foreach (var item in Resources.FindObjectsOfTypeAll<BossMotion>()) { item.Restore(); Object.Destroy(item); }
            foreach (var outline in Resources.FindObjectsOfTypeAll<ElementOutline>()) { outline.Restore(); Object.Destroy(outline); }
            foreach (var item in Resources.FindObjectsOfTypeAll<ElementalMinion>()) Object.Destroy(item);
            foreach (var item in Resources.FindObjectsOfTypeAll<EncounterAttack>()) Object.Destroy(item);
            foreach (var item in Resources.FindObjectsOfTypeAll<EncounterSummon>()) Object.Destroy(item);
            foreach (var item in Resources.FindObjectsOfTypeAll<HiveBee>()) Object.Destroy(item);
            BossActors.RemoveAll();
            foreach(var item in Resources.FindObjectsOfTypeAll<DungBallSplit>()) Object.Destroy(item);
            foreach (var item in Resources.FindObjectsOfTypeAll<SummonInheritance>()) { item.Detach(); Object.Destroy(item); }
            foreach (var item in Resources.FindObjectsOfTypeAll<BossBehaviour>()) Object.Destroy(item);
            ElementOutline.ReleaseShader();
        }
    }
}
