using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using HollowElements.Game.Bosses;

namespace HollowElements.Game
{
    internal sealed class UnloadAudit : MonoBehaviour
    {
        private HollowElementsMod mod;
        private string directory;
        private bool reload;
        private readonly Dictionary<Rigidbody2D,RigidbodyConstraints2D> bodies=new Dictionary<Rigidbody2D,RigidbodyConstraints2D>();
        private readonly Dictionary<tk2dBaseSprite,Color> spriteColours=new Dictionary<tk2dBaseSprite,Color>();
        private readonly Dictionary<SpriteRenderer,Color> rendererColours=new Dictionary<SpriteRenderer,Color>();
        private readonly Dictionary<Rigidbody2D,float> gravity=new Dictionary<Rigidbody2D,float>();
        private readonly Dictionary<Transform,Vector3> sizes=new Dictionary<Transform,Vector3>();
        internal void Begin(HollowElementsMod owner,string path,bool reinitialize)
        {
            mod=owner; directory=path; reload=reinitialize; DontDestroyOnLoad(gameObject);
            foreach(var clock in Resources.FindObjectsOfTypeAll<EnemyClock>())
            {
                var body=clock.GetComponent<Rigidbody2D>(); if(!body) continue;
                var type=typeof(EnemyClock); bool held=(bool)type.GetField("held",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(clock);
                bodies[body]=held ? (RigidbodyConstraints2D)type.GetField("constraints",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(clock) : body.constraints;
            }
            foreach(var tint in Resources.FindObjectsOfTypeAll<AttackTint>()) tint.RememberOriginals(spriteColours,rendererColours);
            foreach(var tint in Resources.FindObjectsOfTypeAll<EnemyAttackTint>()) tint.RememberOriginals(spriteColours,rendererColours);
            foreach(var boss in Resources.FindObjectsOfTypeAll<NailBrothersBoss>()) boss.RememberOriginals(spriteColours);
            foreach(var motion in Resources.FindObjectsOfTypeAll<BossMotion>()) if(motion.GetComponent<Rigidbody2D>()) gravity[motion.GetComponent<Rigidbody2D>()]=motion.NativeGravity;
            foreach(var attack in Resources.FindObjectsOfTypeAll<ElementalAttack>()) sizes[attack.transform]=attack.NativeScale;
        }
        private IEnumerator Start()
        {
            yield return null; yield return new WaitForEndOfFrame();
            bool restored=true; foreach(var body in bodies) if(body.Key && body.Key.constraints!=body.Value) restored=false;
            bool currentCleared=CombatRuntime.Current==null;
            bool coloursRestored=true;
            foreach(var sprite in spriteColours) if(sprite.Key && !RgbEqual(sprite.Key.color,sprite.Value)) coloursRestored=false;
            foreach(var sprite in rendererColours) if(sprite.Key && !RgbEqual(sprite.Key.color,sprite.Value)) coloursRestored=false;
            int clocks=Resources.FindObjectsOfTypeAll<EnemyClock>().Length+Resources.FindObjectsOfTypeAll<EnemyPhysicsHold>().Length;
            int dummies=FindObjectsOfType<TrainingDummy>().Length;
            int tints=Resources.FindObjectsOfTypeAll<AttackTint>().Length;
            int stamps=Resources.FindObjectsOfTypeAll<AttackStamp>().Length;
            int hud=0;
            int bosses=Resources.FindObjectsOfTypeAll<BossBehaviour>().Length;
            int minions=Resources.FindObjectsOfTypeAll<ElementalMinion>().Length;
            int inheritance=Resources.FindObjectsOfTypeAll<SummonInheritance>().Length;
            int outlines=Resources.FindObjectsOfTypeAll<ElementOutline>().Length;
            int enemyTints=Resources.FindObjectsOfTypeAll<EnemyAttackTint>().Length;
            int shields=Resources.FindObjectsOfTypeAll<CrystalShieldVisual>().Length;
            int shards=0; foreach(var line in Resources.FindObjectsOfTypeAll<LineRenderer>()) if(line.name=="HollowElements.CrystalShield") shards++;
            int attacks=Resources.FindObjectsOfTypeAll<ElementalAttack>().Length;
            int encounterBindings=Resources.FindObjectsOfTypeAll<EncounterAttack>().Length+Resources.FindObjectsOfTypeAll<EncounterSummon>().Length+Resources.FindObjectsOfTypeAll<HiveBee>().Length+Resources.FindObjectsOfTypeAll<WaterAttachmentRegion>().Length+BossActors.BindingCount;
            int motions=Resources.FindObjectsOfTypeAll<BossMotion>().Length;
            int blades=Resources.FindObjectsOfTypeAll<NailBladeTint>().Length;
            bool gravityRestored=true, sizesRestored=true;
            foreach(var pair in gravity) if(pair.Key && Mathf.Abs(pair.Key.gravityScale-pair.Value)>.001f) gravityRestored=false;
            foreach(var pair in sizes) if(pair.Key && (pair.Key.localScale-pair.Value).sqrMagnitude>.0001f) sizesRestored=false;
            int bladeMasks=0; foreach(var texture in Resources.FindObjectsOfTypeAll<Texture2D>()) if(texture.name=="HollowElements.BladeMask") bladeMasks++;
            int masks=0; foreach(var texture in Resources.FindObjectsOfTypeAll<Texture2D>()) if(texture.name=="HollowElements.ContourMask") masks++;
            foreach(var canvas in Resources.FindObjectsOfTypeAll<Canvas>()) if(canvas.name=="HollowElements.HUD" || canvas.name=="HollowElements.Menus") hud++;
            bool element_guideRestored=!ElementGuideWindow.CapturesInput && Resources.FindObjectsOfTypeAll<ElementGuideWindow>().Length==0
                && !Resources.FindObjectsOfTypeAll<GameObject>().Any(o=>o.name=="HollowElements.ElementGuidePane")
                && !Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Any(f=>f.name.StartsWith("HollowElements.ElementGuide"))
                && !Resources.FindObjectsOfTypeAll<Material>().Any(m=>m.name.StartsWith("HollowElements.ElementGuide"))
                && !Resources.FindObjectsOfTypeAll<Texture2D>().Any(t=>t.name=="HollowElements.ElementGuideImage" || t.name=="HollowElements.ElementGuideSdfAtlas")
                && !Resources.FindObjectsOfTypeAll<PlayMakerFSM>().Where(f=>f.FsmName=="Inventory Control" && f.Fsm.Initialized).Any(f=>f.FsmStates.Any(s=>s.Name.StartsWith("HE ElementGuide ") || s.Transitions.Any(t=>t.EventName=="HOLLOW ELEMENTS ELEMENT GUIDE")));
            int charmAssets=Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name.StartsWith("HollowElements.CharmIcon"))+Resources.FindObjectsOfTypeAll<Sprite>().Count(t=>t.name.StartsWith("HollowElements.CharmIcon"))+Resources.FindObjectsOfTypeAll<CharmSparkMotion>().Length;
            var report=new { passed=charmAssets==0 && element_guideRestored && currentCleared && restored && coloursRestored && gravityRestored && sizesRestored && encounterBindings==0 && enemyTints==0 && shields==0 && shards==0 && attacks==0 && motions==0 && blades==0 && bladeMasks==0 && clocks==0 && dummies==0 && hud==0 && tints==0 && stamps==0 && bosses==0 && minions==0 && inheritance==0 && outlines==0 && masks==0,element_guideRestored,
                element_guideRemaining=new {windows=Resources.FindObjectsOfTypeAll<ElementGuideWindow>().Length,panes=Resources.FindObjectsOfTypeAll<GameObject>().Where(o=>o.name=="HollowElements.ElementGuidePane").Select(o=>o.name).ToArray(),
                    fonts=Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Where(f=>f.name.StartsWith("HollowElements.ElementGuide")).Select(f=>f.name).ToArray(),
                    materials=Resources.FindObjectsOfTypeAll<Material>().Where(m=>m.name.StartsWith("HollowElements.ElementGuide")).Select(m=>m.name).ToArray(),
                    textures=Resources.FindObjectsOfTypeAll<Texture2D>().Where(t=>t.name=="HollowElements.ElementGuideImage" || t.name=="HollowElements.ElementGuideSdfAtlas").Select(t=>t.name).ToArray(),
                    states=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().Where(f=>f.FsmName=="Inventory Control" && f.Fsm.Initialized).SelectMany(f=>f.FsmStates).Where(s=>s.Name.StartsWith("HE ElementGuide ") || s.Transitions.Any(t=>t.EventName=="HOLLOW ELEMENTS ELEMENT GUIDE")).Select(s=>s.Name).ToArray()},
                remainingCharmAssets=charmAssets,currentCleared,remainingEncounterBindings=encounterBindings,constraintsRestored=restored,coloursRestored,sizesRestored,gravityRestored,remainingEnemyTints=enemyTints,remainingShields=shields,remainingShards=shards,remainingAttacks=attacks,remainingMotions=motions,remainingBlades=blades,remainingBladeMasks=bladeMasks,checkedSprites=spriteColours.Count+rendererColours.Count,remainingClocks=clocks,remainingDummies=dummies,remainingHud=hud,remainingTints=tints,remainingStamps=stamps,
                remainingBosses=bosses,remainingMinions=minions,remainingInheritance=inheritance,remainingOutlines=outlines,remainingMasks=masks,
                spellLevel=GameManager.instance.GetPlayerDataInt("fireballLevel"),rawSpellLevel=PlayerData.instance.fireballLevel };
            File.WriteAllText(Path.Combine(directory,"unload-audit.json"),JsonConvert.SerializeObject(report,Formatting.Indented));
            if(reload) mod.Initialize();
            Destroy(gameObject);
        }
        private static bool RgbEqual(Color a,Color b) { return Mathf.Abs(a.r-b.r)<.001f && Mathf.Abs(a.g-b.g)<.001f && Mathf.Abs(a.b-b.b)<.001f; }
    }
}
