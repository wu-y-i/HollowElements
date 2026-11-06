using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using MonoMod.RuntimeDetour;
using HollowElements.Rules;
using HollowElements.Game.Bosses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace HollowElements.Game
{
    // Opt-in local acceptance harness. The session marker is never included in release packages.
    // All test sessions suppress game saving, and deployment also makes a full save backup.
    internal sealed class DevelopmentBridge : MonoBehaviour
    {
        private CombatRuntime runtime;
        private string directory;
        private float nextPoll;
        private bool isolatedSave;
        private readonly List<float> profileFrames=new List<float>();
        private bool profiling;
        private Hook savePathHook;
        private MenuScreen settingsScreen;
        private GameObject textureComparison;
        private readonly List<UnityEngine.Object> comparisonAssets=new List<UnityEngine.Object>();
        private readonly Dictionary<int,int> deathCalls=new Dictionary<int,int>();
        private readonly List<object> playerHits = new List<object>();
        private int playerHitSequence;
        private delegate string OriginalSavePath(int slot);
        internal void Initialize(CombatRuntime value)
        {
            runtime = value;
            string marker = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "development-session.json");
            if (!File.Exists(marker)) { Destroy(this); return; }
            directory = JObject.Parse(File.ReadAllText(marker)).Value<string>("directory");
            if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory) || !System.IO.Directory.Exists(directory)) throw new InvalidDataException("开发会话目录无效。");
            On.GameManager.SaveGame_int_Action1 += SuppressSave;
            On.HealthManager.Die += CountDeath;
            On.DesktopPlatform.ReadSaveSlot += ReadIsolatedSave;
            On.DesktopPlatform.WriteSaveSlot += WriteIsolatedSave;
            savePathHook = new Hook(typeof(GameManager).GetMethod("ModdedSavePath",BindingFlags.Static|BindingFlags.NonPublic),
                new Func<OriginalSavePath,int,string>((orig,slot) => isolatedSave ? Path.Combine(directory,"user"+slot+".modded.json") : orig(slot)));
            runtime.Mod.OnSaveGlobal().DebugEnabled = true;
            runtime.PlayerDamageObserved += ObservePlayerDamage;
            Application.runInBackground = true;
            File.WriteAllText(Path.Combine(directory, "connected.json"), JsonConvert.SerializeObject(new { pid = System.Diagnostics.Process.GetCurrentProcess().Id, saving = "suppressed", version = runtime.Mod.GetVersion() }));
        }
        private void ObservePlayerDamage(GameObject source, Element element, int before, int after)
        {
            playerHits.Add(new { sequence=++playerHitSequence,time=runtime.Now,source=source ? PathOf(source.transform) : null,
                element,before,after,state=runtime.Player.Copy() });
            if(playerHits.Count>200) playerHits.RemoveAt(0);
        }
        private void CountDeath(On.HealthManager.orig_Die orig,HealthManager health,float? direction,AttackTypes type,bool ignore)
        {
            int id=health.GetInstanceID();int count;deathCalls.TryGetValue(id,out count);deathCalls[id]=count+1;
            orig(health,direction,type,ignore);
        }
        // Moves the test player into a real enabled attack collider. Damage must still travel through
        // Unity physics -> HeroBox -> native damage/i-frames -> our health hook; no injected TakeDamage.
        private IEnumerator InterceptAttack(int id, float seconds, float yOffset)
        {
            var obj=Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==id);
            var hero=HeroController.SilentInstance;
            double until=runtime.Now+seconds; int before=playerHitSequence;
            while(obj && hero && runtime.Now<until && before==playerHitSequence)
            {
                var collider=obj.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.enabled && c.gameObject.activeInHierarchy &&
                    (c.GetComponent<DamageHero>() || c.GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="damages_hero")))
                    ?? obj.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.enabled && c.gameObject.activeInHierarchy);
                if(collider)
                {
                    Physics2D.SyncTransforms();
                    var box=hero.GetComponentInChildren<HeroBox>();
                    var heroCollider=box ? box.GetComponent<Collider2D>() : null;
                    Vector3 offset=heroCollider ? box.transform.TransformPoint(heroCollider.offset)-hero.transform.position : Vector3.zero;
                    Vector3 point=collider.bounds.center;
                    var owner=obj.GetComponentInParent<BossBehaviour>();
                    if(owner && owner.gameObject!=obj)
                        point.x+=Mathf.Sign(point.x-owner.transform.position.x)*Mathf.Max(0,collider.bounds.extents.x-.15f);
                    point=collider.ClosestPoint(point); // Bounds corners can lie outside triangular/curved slash polygons.
                    hero.transform.position=new Vector3(point.x-offset.x,point.y-offset.y+yOffset,hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().velocity=Vector2.zero;
                }
                yield return new WaitForFixedUpdate();
            }
        }
        private void SuppressSave(On.GameManager.orig_SaveGame_int_Action1 orig, GameManager manager, int slot, Action<bool> callback)
        { if(isolatedSave) orig(manager,slot,callback); else { callback?.Invoke(false); runtime.LogTrace("开发验收会话：已阻止写入存档 " + slot); } }
        private void WriteIsolatedSave(On.DesktopPlatform.orig_WriteSaveSlot orig, DesktopPlatform platform, int slot, byte[] bytes, Action<bool> callback)
        { if(!isolatedSave) { orig(platform,slot,bytes,callback); return; } File.WriteAllBytes(Path.Combine(directory,"user"+slot+".dat"),bytes); callback?.Invoke(true); }
        private void ReadIsolatedSave(On.DesktopPlatform.orig_ReadSaveSlot orig, DesktopPlatform platform, int slot, Action<byte[]> callback)
        { if(!isolatedSave) orig(platform,slot,callback); else callback(File.ReadAllBytes(Path.Combine(directory,"user"+slot+".dat"))); }
        private void Update()
        {
            if(profiling && profileFrames.Count<3600) profileFrames.Add(Time.unscaledDeltaTime*1000);
            if (directory == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .15f;
            string path = Path.Combine(directory, "command.json");
            if (!File.Exists(path)) return;
            JObject command = null;
            try
            {
                command = JObject.Parse(File.ReadAllText(path)); File.Delete(path);
                object result = Execute(command);
                File.WriteAllText(Path.Combine(directory, "response.json"), JsonConvert.SerializeObject(new { id = command.Value<string>("id"), ok = true, result }, Formatting.Indented));
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(directory, "response.json"), JsonConvert.SerializeObject(new { id = command?.Value<string>("id"), ok = false, error = e.ToString() }, Formatting.Indented));
                runtime.Mod.LogError(e);
            }
        }
        private object Execute(JObject c)
        {
            string name = c.Value<string>("command");
            var hero = HeroController.SilentInstance;
            switch (name)
            {
                case "profileStart": profileFrames.Clear();profiling=true;return "recording frame times";
                case "profileStop":
                    profiling=false;var frames=profileFrames.OrderBy(x=>x).ToArray();return new {count=frames.Length,meanMs=frames.Length>0?frames.Average():0,p95Ms=frames.Length>0?frames[(int)((frames.Length-1)*.95)]:0,p99Ms=frames.Length>0?frames[(int)((frames.Length-1)*.99)]:0,maxMs=frames.Length>0?frames.Last():0,over33ms=frames.Count(x=>x>33.34f)};
                case "tintDiagnostics": return new {tints=Resources.FindObjectsOfTypeAll<EnemyAttackTint>().Where(t=>t.gameObject.scene==UnitySceneManager.GetActiveScene() || (t.GetComponent<RadianceAttack>()?.Owner && t.GetComponent<RadianceAttack>().Owner.gameObject.scene==UnitySceneManager.GetActiveScene())).Select(t=>t.Diagnostics()).ToArray(),attacks=Resources.FindObjectsOfTypeAll<RadianceAttack>().Count(t=>t.Owner && t.Owner.gameObject.scene==UnitySceneManager.GetActiveScene())};
                case "resolution": Screen.SetResolution(c.Value<int>("width"),c.Value<int>("height"),false);return "requested";
                case "fixDiagnostics": return new {ui=GetComponent<CombatPresentation>().Diagnostics(),splits=FindObjectsOfType<DungBallSplit>().Select(b=>new {id=b.gameObject.GetInstanceID(),b.Small,b.Triggered,b.Count,scale=b.transform.localScale.ToString(),velocity=b.GetComponent<Rigidbody2D>().velocity.ToString()}).ToArray()};
                case "packIcons":
                    var exports=new List<object>();
                    foreach(var spec in new[]{new {folder="Charms",size=192,max=1024},new {folder="Icons",size=128,max=512}}) {
                        string folder=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Assets",spec.folder);
                        var files=Directory.GetFiles(folder,"*.png").Where(p=>Path.GetFileName(p)!="atlas.png").OrderBy(p=>p).ToArray();
                        var inputs=new List<Texture2D>();Texture2D packed=null;
                        try {
                            foreach(var file in files) {
                                var original=new Texture2D(2,2);original.LoadImage(File.ReadAllBytes(file));
                                var small=new Texture2D(spec.size,spec.size,TextureFormat.RGBA32,false);
                                for(int y=0;y<spec.size;y++) for(int x=0;x<spec.size;x++) small.SetPixel(x,y,original.GetPixelBilinear((x+.5f)/spec.size,(y+.5f)/spec.size));
                                small.Apply();inputs.Add(small);Destroy(original);
                            }
                            packed=new Texture2D(2,2,TextureFormat.RGBA32,false);
                            var rects=packed.PackTextures(inputs.ToArray(),4,spec.max,false);
                            var sprites=new Dictionary<string,object>();
                            for(int i=0;i<files.Length;i++) sprites.Add(Path.GetFileNameWithoutExtension(files[i]),new {x=Mathf.RoundToInt(rects[i].x*packed.width),y=Mathf.RoundToInt(rects[i].y*packed.height),width=Mathf.RoundToInt(rects[i].width*packed.width),height=Mathf.RoundToInt(rects[i].height*packed.height)});
                            string dest=Path.Combine(directory,"icon-atlas",spec.folder);Directory.CreateDirectory(dest);
                            File.WriteAllBytes(Path.Combine(dest,"atlas.png"),packed.EncodeToPNG());
                            File.WriteAllText(Path.Combine(dest,"atlas.json"),JsonConvert.SerializeObject(new {packer="Unity Texture2D.PackTextures",width=packed.width,height=packed.height,sprites},Formatting.Indented));
                            exports.Add(new {spec.folder,packed.width,packed.height,count=files.Length});
                        } finally {foreach(var packedInput in inputs) Destroy(packedInput);if(packed) Destroy(packed);}
                    }
                    return exports;
                case "bossHint": return GetComponent<BossMechanicBanner>().Diagnostics();
                case "uiLayers": return new {
                    version=runtime.Mod.GetVersion(),
                    cameras=Resources.FindObjectsOfTypeAll<Camera>().Where(x=>x.gameObject.scene.IsValid()).Select(x=>new {x.name,x.depth,x.cullingMask,x.orthographicSize,position=x.transform.position.ToString(),x.nearClipPlane,x.farClipPlane}),
                    canvases=Resources.FindObjectsOfTypeAll<Canvas>().Where(x=>x.gameObject.scene.IsValid()).Select(x=>new {path=PathOf(x.transform),x.renderMode,camera=x.worldCamera?x.worldCamera.name:null,x.sortingLayerID,x.sortingOrder,x.planeDistance,active=x.gameObject.activeInHierarchy}),
                    hud=GameCameras.instance && GameCameras.instance.hudCanvas ? PathOf(GameCameras.instance.hudCanvas.transform) : null,
                    hudRenderers=GameCameras.instance && GameCameras.instance.hudCanvas ? GameCameras.instance.hudCanvas.GetComponentsInChildren<Renderer>(true).Take(25).Select(x=>new {path=PathOf(x.transform),x.sortingLayerID,x.sortingOrder,layer=x.gameObject.layer,position=x.transform.position.ToString()}) : null,
                    sprites=Resources.FindObjectsOfTypeAll<tk2dSprite>().Where(x=>x.gameObject.scene.IsValid() && (x.name=="Full" || x.name.Contains("Soul Orb"))).Select(x=>new {path=PathOf(x.transform),layer=x.gameObject.layer,position=x.transform.position.ToString(),sorting=x.GetComponent<Renderer>()?.sortingOrder,sortingLayer=x.GetComponent<Renderer>()?.sortingLayerID})
                };
                case "status": return Status();
                case "deathCalls": return deathCalls;
                case "settingsMenu":
                    if(c.Value<string>("action")=="open")
                    {
                        if(!settingsScreen) settingsScreen=runtime.Mod.GetMenuScreen(UIManager.instance.optionsMenuScreen,null);
                        UIManager.instance.UIGoToDynamicMenu(settingsScreen);
                    }
                    var options=settingsScreen.GetComponentsInChildren<UnityEngine.UI.MenuOptionHorizontal>(true);
                    if(c.Value<string>("action")=="select") UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(options[c.Value<int>("index")].gameObject);
                    if(c.Value<string>("action")=="move")
                    {
                        var events=UnityEngine.EventSystems.EventSystem.current;
                        var move=new UnityEngine.EventSystems.AxisEventData(events) {moveDir=c.Value<int>("direction")<0?UnityEngine.EventSystems.MoveDirection.Up:UnityEngine.EventSystems.MoveDirection.Down};
                        UnityEngine.EventSystems.ExecuteEvents.Execute(events.currentSelectedGameObject,move,UnityEngine.EventSystems.ExecuteEvents.moveHandler);
                    }
                    return new {selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.name,
                        scrolls=settingsScreen.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Select(s=>new {y=s.content.anchoredPosition.y,hidden=s.content.rect.height-s.viewport.rect.height,normalized=s.verticalNormalizedPosition}),
                        options=options.Select(o=>new {o.name,up=o.navigation.selectOnUp?.name,down=o.navigation.selectOnDown?.name,
                            texts=o.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t=>new {t.name,t.text,t.fontSize,width=t.rectTransform.rect.width,height=t.rectTransform.rect.height})})};
                case "externalBar":
                    var externalTarget=Target(c);var externalType=Type.GetType("EnemyHPBar."+(c.Value<string>("kind")=="boss"?"BossHPBar":"HPBar")+", EnemyHPBar");
                    if(externalType==null) throw new InvalidOperationException("EnemyHPBar is not installed.");
                    var externalComponent=externalTarget.GetComponent(externalType);
                    if(!externalComponent && c.Value<bool?>("attach")==true) externalComponent=externalTarget.gameObject.AddComponent(externalType);
                    var externalImage=(UnityEngine.UI.Image)externalType.GetField("health_bar").GetValue(externalComponent);
                    var externalCorners=new Vector3[4];externalImage.rectTransform.GetWorldCorners(externalCorners);
                    var externalTop=RectTransformUtility.WorldToScreenPoint(externalImage.canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:Camera.main,(externalCorners[1]+externalCorners[2])*.5f);
                    return new {externalImage.canvas.renderMode,alpha=externalImage.canvasRenderer.GetAlpha(),x=externalTop.x,y=externalTop.y,scale=GetComponent<CombatPresentation>().CanvasRoot.GetComponent<Canvas>().scaleFactor};
                case "inventoryProbe":
                    return Resources.FindObjectsOfTypeAll<PlayMakerFSM>().Where(f=>f.FsmName=="Inventory Control").Select(f=>new {
                        id=f.gameObject.GetInstanceID(),path=PathOf(f.transform),f.ActiveStateName,active=f.gameObject.activeInHierarchy,
                        variables=f.FsmVariables.GetAllNamedVariables().Select(v=>new{v.Name,value=SafeValue(v)}),
                        children=Enumerable.Range(0,f.transform.childCount).Select(i=>new{name=f.transform.GetChild(i).name,components=f.transform.GetChild(i).GetComponents<Component>().Select(x=>x.GetType().FullName)}),
                        states=f.FsmStates.Select(s=>new{s.Name,transitions=s.Transitions.Select(t=>new{t.EventName,t.ToState}),actions=s.Actions.Select(a=>new{type=a.GetType().Name,fields=a.GetType().GetFields().ToDictionary(x=>x.Name,x=>ProbeValue(x.GetValue(a)))})})});
                case "guideFontDecode":
                    string guideFontDir=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"ElementGuide","Font");
                    var guideBook=JObject.Parse(File.ReadAllText(Path.Combine(guideFontDir,"font.json")));
                    string guideFile=guideBook["pages"][c.Value<int>("index")].Value<string>("file");
                    var guidePageTex=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    try {
                        if(!guidePageTex.LoadImage(File.ReadAllBytes(Path.Combine(guideFontDir,guideFile)))) throw new InvalidDataException(guideFile);
                        var guidePixels=guidePageTex.GetPixels32();var guideAlpha=new byte[guidePixels.Length];for(int i=0;i<guidePixels.Length;i++) guideAlpha[i]=guidePixels[i].a;
                        using(var guideHash=System.Security.Cryptography.SHA256.Create()) return new {file=guideFile,width=guidePageTex.width,height=guidePageTex.height,alphaSha256=BitConverter.ToString(guideHash.ComputeHash(guideAlpha)).Replace("-","").ToLowerInvariant()};
                    } finally {Destroy(guidePageTex);}
                case "element_guideFonts":
                    var element_guideText=string.Join("",System.IO.Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"ElementGuide"),"*.md",SearchOption.AllDirectories).Select(File.ReadAllText));
                    var element_guideCharacters=element_guideText.Where(ch=>ch>127 && !char.IsWhiteSpace(ch)).Distinct().ToArray();
                    return Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Select(f=>new{f.name,type=f.fontAssetType.ToString(),glyphs=f.characterDictionary.Count,missing=element_guideCharacters.Count(ch=>!f.characterDictionary.ContainsKey(ch)),fallbacks=f.fallbackFontAssets?.Select(a=>a?a.name:null).ToArray()});
                case "inventoryEvent":
                    Resources.FindObjectsOfTypeAll<PlayMakerFSM>().Single(f=>f.FsmName=="Inventory Control").SendEvent(c.Value<string>("event"));return "sent";
                case "charms":
                    var charmPage=GetComponent<ElementGuideWindow>();
                    if(c.Value<string>("action")=="show") charmPage.OpenCharms();
                    if(c["charm"]!=null) charmPage.SelectCharm(Parse<CharmId>(c,"charm"));
                    if(c.Value<string>("action")=="toggle") charmPage.ToggleSelectedCharm();
                    if(c.Value<string>("action")=="navigate") charmPage.NavigateCharms(c.Value<int?>("vertical")??0,c.Value<int?>("horizontal")??0,c.Value<bool?>("submit")??false);
                    return charmPage.CharmDiagnostics();
                case "element_guide":
                    var element_guide=GetComponent<ElementGuideWindow>();
                    switch(c.Value<string>("action"))
                    {
                        case "open": element_guide.Native.Open();break;
                        case "close": element_guide.Native.Close();break;
                        case "move": element_guide.Native.Move(c.Value<int>("direction"));break;
                        case "navigate":element_guide.Navigate(c.Value<int?>("direction")??0,c.Value<bool?>("submit")??false);break;
                        case "back":element_guide.ReturnToIndex();break;
                        case "scroll":element_guide.Scroll(c.Value<float>("pixels"));break;
                        case "reload":element_guide.Reload();break;
                        case "configure":element_guide.ChangeConfiguration(c.Value<int?>("step")??1);break;
                    }
                    return element_guide.Diagnostics();
                case "sceneNames": return Enumerable.Range(0,UnitySceneManager.sceneCountInBuildSettings).Select(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex).ToArray();
                case "water": return new {applications=runtime.GetComponent<WaterExposure>().Applications,acidArmour=PlayerData.instance.hasAcidArmour,regions=Resources.FindObjectsOfTypeAll<WaterAttachmentRegion>().Select(r=>new {id=r.gameObject.GetInstanceID(),path=PathOf(r.transform),r.Acid,active=r.gameObject.activeInHierarchy,colliders=r.GetComponents<Collider2D>().Select(x=>new {x.enabled,centre=x.bounds.center.ToString(),size=x.bounds.size.ToString()})})};
                case "acidArmour": PlayerData.instance.hasAcidArmour=c.Value<bool>("value"); return PlayerData.instance.hasAcidArmour;
                case "absoluteInvincible": PlayerData.instance.isInvincible=c.Value<bool>("value"); return PlayerData.instance.isInvincible;
                case "bees": return Resources.FindObjectsOfTypeAll<HiveBee>().Select(b=>new {id=b.gameObject.GetInstanceID(),path=PathOf(b.transform),element=b.DamageElement,healthManagers=b.GetComponents<HealthManager>().Length,fsms=b.GetComponents<PlayMakerFSM>().Select(f=>new {f.FsmName,f.ActiveStateName})}).ToArray();
                case "markothShields": return Resources.FindObjectsOfTypeAll<MarkothShield>().Select(s=>new {id=s.gameObject.GetInstanceID(),path=PathOf(s.transform),active=s.gameObject.activeInHierarchy,element=s.Element,state=s.State,rotation=s.transform.parent.eulerAngles.z,position=s.transform.position.ToString(),clock=s.GetComponentInParent<EnemyClock>()?.Scale}).ToArray();
                case "hitActor":
                    var actorHitTarget=Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId"));
                    var actorSource=new GameObject("HollowElements.DevelopmentActorHit"); var actorStamp=actorSource.AddComponent<AttackStamp>();
                    actorStamp.Source=Parse<AttackSource>(c,"source"); actorStamp.Cast=c.Value<long?>("cast") ?? runtime.Casts.Begin(RuleConfig.SlotOf(actorStamp.Source));
                    if(c["element"]!=null) runtime.Mod.OnSaveLocal().Set(RuleConfig.SlotOf(actorStamp.Source),Parse<Element>(c,"element"));
                    HitTaker.Hit(actorHitTarget,new HitInstance {Source=actorSource,DamageDealt=c.Value<int>("damage"),Multiplier=1,AttackType=c.Value<bool?>("spell")??false ? AttackTypes.Spell : AttackTypes.Nail});
                    Destroy(actorSource); return Status();
                case "playerHits": return playerHits.ToArray();
                case "clearPlayerHits": playerHits.Clear(); return playerHitSequence;
                case "interceptAttack": StartCoroutine(InterceptAttack(c.Value<int>("objectId"),c.Value<float?>("seconds")??2,c.Value<float?>("yOffset")??0)); return "intercepting native physics collider";
                case "attackDetails":
                    var attackObject=Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId"));
                    return attackObject.GetComponentsInChildren<Transform>(true).Select(t=>new {id=t.gameObject.GetInstanceID(),path=PathOf(t),active=t.gameObject.activeInHierarchy,position=t.position.ToString(),
                        components=t.GetComponents<Component>().Select(x=>x.GetType().Name).ToArray(),
                        colliders=t.GetComponents<Collider2D>().Select(x=>new {x.enabled,x.isTrigger,centre=x.bounds.center.ToString(),size=x.bounds.size.ToString()}),
                        damage=t.GetComponents<DamageHero>().Select(x=>new {x.damageDealt,x.hazardType}),
                        fsms=t.GetComponents<PlayMakerFSM>().Select(x=>new {x.FsmName,x.ActiveStateName,states=x.FsmStates.Select(s=>new {s.Name,actions=s.Actions.Select(a=>new {type=a.GetType().FullName,fields=a.GetType().GetFields().ToDictionary(field=>field.Name,field=>SafeValue(field.GetValue(a)))})})})}).ToArray();
                case "shieldVisuals": return FindObjectsOfType<CrystalShieldVisual>().Select(v=>new {v.name,shards=v.GetComponentsInChildren<LineRenderer>().Select(l=>new {l.enabled,l.sortingLayerID,l.sortingOrder,position=l.GetPosition(0).ToString(),shader=l.sharedMaterial.shader.name,bounds=l.bounds.ToString()})}).ToArray();
                case "healthBars":
                    var healthBars=(System.Collections.IDictionary)typeof(CombatPresentation).GetField("bars",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(GetComponent<CombatPresentation>());
                    var barStatus=new List<object>();
                    foreach(System.Collections.DictionaryEntry pair in healthBars)
                    {
                        var hp=(HealthManager)pair.Key;var barRoot=(RectTransform)pair.Value.GetType().GetField("Root",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(pair.Value);
                        if(hp && barRoot.gameObject.activeSelf) barStatus.Add(new {id=hp.GetInstanceID(),objectId=hp.gameObject.GetInstanceID(),hp.name});
                    }
                    return barStatus;
                case "objectActive": Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId")).SetActive(c.Value<bool>("value")); return "object activation changed for native acceptance";
                case "animateOffscreen":
                    var animated=Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId"));
                    foreach(var animator in animated.GetComponentsInChildren<Animator>(true)) animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    return "native animators update outside the test camera";
                case "objectState":
                    var actor=Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId")); actor.GetComponents<PlayMakerFSM>().First(f=>f.FsmName==c.Value<string>("fsm")).SetState(c.Value<string>("state")); return "native state entered";
                case "soul": PlayerData.instance.MPCharge=c.Value<int>("value"); return PlayerData.instance.MPCharge;
                case "dashTest":
                    PlayerData.instance.hasShadowDash=true;hero.cState.shadowDashing=false;
                    typeof(HeroController).GetField("shadowDashTimer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(hero,0f);
                    typeof(HeroController).GetMethod("HeroDash",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hero,null);
                    return new {shadow=hero.cState.shadowDashing,ability=PlayerData.instance.hasShadowDash,disabledUntil=runtime.ShadowDashDisabledUntil,now=runtime.Now};
                case "sceneObjects": return Resources.FindObjectsOfTypeAll<Transform>().Where(t=>t.gameObject.scene==UnitySceneManager.GetActiveScene()).Select(t=>new {id=t.gameObject.GetInstanceID(),path=PathOf(t),active=t.gameObject.activeInHierarchy,components=t.GetComponents<Component>().Select(x=>x.GetType().Name).ToArray()}).ToArray();
                case "dumpAll": return Resources.FindObjectsOfTypeAll<PlayMakerFSM>().Where(f=>f.gameObject.scene==UnitySceneManager.GetActiveScene()).Select(f=>new {obj=f.name,path=PathOf(f.transform),f.FsmName,f.ActiveStateName,states=f.FsmStates.Select(s=>new {s.Name,transitions=s.Transitions.Select(t=>new {t.EventName,t.ToState}),actions=s.Actions.Select(a=>new {type=a.GetType().FullName,fields=a.GetType().GetFields().ToDictionary(field=>field.Name,field=>SafeValue(field.GetValue(a)))})})}).ToArray();
                case "projectileDisable": Resources.FindObjectsOfTypeAll<ElementalAttack>().First(a=>a.gameObject.GetInstanceID()==c.Value<int>("objectId")).gameObject.SetActive(false); return "disabled for pool-lifetime acceptance";
                case "exportEnemySprites": return ExportEnemySprites(Target(c));
                case "enemyHp": Target(c).hp=c.Value<int>("value"); return Target(c).hp;
                case "hierarchy": return Target(c).GetComponentsInChildren<Transform>(true).Select(t=>new {path=PathOf(t),active=t.gameObject.activeSelf,components=t.GetComponents<Component>().Select(x=>x.GetType().Name).ToArray(),scale=t.localScale.ToString()}).ToArray();
                case "projectiles": return Resources.FindObjectsOfTypeAll<ElementalAttack>().Select(a=>new {id=a.gameObject.GetInstanceID(),a.name,active=a.gameObject.activeInHierarchy,element=a.Element,owner=a.OwnerId,ring=a.Ring,scale=a.transform.localScale.ToString(),position=a.transform.position.ToString(),rotation=a.transform.eulerAngles.z,clock=a.GetComponent<EnemyClock>() ? new {a.GetComponent<EnemyClock>().AcceptedFrames,a.GetComponent<EnemyClock>().SkippedFrames,a.GetComponent<EnemyClock>().AcceptedPhysics,a.GetComponent<EnemyClock>().SkippedPhysics} : null,clockScale=a.GetComponent<EnemyClock>() ? a.GetComponent<EnemyClock>().Scale : 1,tinted=a.GetComponent<EnemyAttackTint>() && a.GetComponent<EnemyAttackTint>().Element!=Element.None,outlineVisible=a.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name=="HollowElements.ElementOutline" && r.enabled && r.gameObject.activeInHierarchy)}).ToArray();
                case "weaponVisuals": return FindObjectsOfType<NailBladeTint>().Select(t=>new {t.name,t.ResourceSet,element=t.Element,frame=t.GetComponent<tk2dBaseSprite>().GetCurrentSpriteDef().name,overlayVisible=t.transform.Find("HollowElements.NailBladeTint").GetComponent<MeshRenderer>().enabled}).ToArray();
                case "nativeExtra":
                    var extraTarget=Target(c);
                    if(c.Value<bool?>("player")??false)
                    {
                        var extra=extraTarget.GetComponent<ExtraDamageable>() ?? extraTarget.gameObject.AddComponent<ExtraDamageable>();
                        typeof(ExtraDamageable).GetMethod("ApplyExtraDamageToHealthManager",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(extra,new object[]{c.Value<int>("damage")});
                    }
                    else extraTarget.ApplyExtraDamage(c.Value<int>("damage"));
                    return Status();
                case "unclassifiedHit":
                    var nativeTarget=Target(c);int nativeBefore=nativeTarget.hp;
                    var nativeSource=c.Value<bool?>("player")??true ? hero.gameObject : new GameObject("World Damage Acceptance");
                    nativeTarget.Hit(new HitInstance {Source=nativeSource,AttackType=AttackTypes.Generic,DamageDealt=c.Value<int>("damage"),Multiplier=1,IgnoreInvulnerable=true});
                    if(nativeSource!=hero.gameObject) Destroy(nativeSource);
                    return new {before=nativeBefore,after=nativeTarget.hp,state=runtime.Track(nativeTarget).State};
                case "enemyState":
                    var enemyFsm=Target(c).GetComponents<PlayMakerFSM>().First(f=>f.FsmName==c.Value<string>("fsm"));
                    enemyFsm.SetState(c.Value<string>("state"));
                    var enemyBody=enemyFsm.GetComponent<Rigidbody2D>();
                    return new {state=enemyFsm.ActiveStateName,x=enemyBody?enemyBody.velocity.x:0,y=enemyBody?enemyBody.velocity.y:0};
                case "contactHit":
                    int contactBefore=PlayerData.instance.health+PlayerData.instance.healthBlue;
                    var contactSource=c["objectId"]!=null ? Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId")) : Target(c).gameObject;
                    if(c["child"]!=null) contactSource=contactSource.transform.Find(c.Value<string>("child")).gameObject;
                    hero.TakeDamage(contactSource,GlobalEnums.CollisionSide.left,c.Value<int?>("damage")??1,1);
                    return new {before=contactBefore,after=PlayerData.instance.health+PlayerData.instance.healthBlue,player=runtime.Player};
                case "enemyVulnerable":
                    typeof(HealthManager).GetField("invincible",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Target(c),false);
                    return "invincibility cleared in memory for acceptance";
                case "startGodhome": GameManager.instance.StartNewGame(false, true); return "starting";
                case "load": GameManager.instance.LoadGameFromUI(c.Value<int?>("slot") ?? 3); return "loading with save writes suppressed";
                case "ready":
                    GameManager.instance.SetState(GlobalEnums.GameState.PLAYING); GameManager.instance.FadeSceneIn();
                    PlayerData.instance.atBench = false;
                    if (hero) { hero.RegainControl(); hero.StartAnimationControl(); hero.SetDamageMode(GlobalEnums.DamageMode.FULL_DAMAGE); }
                    return "ready";
                case "position":
                    var moved = c.Value<string>("target") == "hero" ? hero.transform : c["objectId"]!=null ? Resources.FindObjectsOfTypeAll<GameObject>().First(o=>o.GetInstanceID()==c.Value<int>("objectId")).transform : Target(c).transform;
                    moved.position = new Vector3(c.Value<float>("x"), c.Value<float>("y"), moved.position.z);
                    var movedBody = moved.GetComponent<Rigidbody2D>(); if (movedBody) movedBody.velocity = Vector2.zero;
                    return moved.position.ToString();
                case "spellLevel":
                    PlayerData.instance.fireballLevel = c.Value<int?>("wave") ?? 2; PlayerData.instance.quakeLevel = c.Value<int?>("dive") ?? 2; PlayerData.instance.screamLevel = c.Value<int?>("shriek") ?? 2;
                    return "spell levels changed in memory only";
                case "artUnlocks":
                    PlayerData.instance.hasUpwardSlash=c.Value<bool?>("great") ?? true;
                    PlayerData.instance.hasDashSlash=c.Value<bool?>("dash") ?? true;
                    PlayerData.instance.hasCyclone=c.Value<bool?>("cyclone") ?? true;
                    PlayerData.instance.hasNailArt=PlayerData.instance.hasUpwardSlash || PlayerData.instance.hasDashSlash || PlayerData.instance.hasCyclone;
                    return "sword art unlocks changed in memory only";
                case "bench": PlayerData.instance.atBench = c.Value<bool?>("value") ?? true; return "bench state changed for UI acceptance";
                case "face": if(c.Value<bool?>("right") ?? true) hero.FaceRight(); else hero.FaceLeft(); return "facing changed";
                case "invincible": PlayerData.instance.invinciTest = c.Value<bool?>("value") ?? true; return PlayerData.instance.invinciTest;
                case "blueHealth": PlayerData.instance.health=c.Value<int?>("red") ?? 0; PlayerData.instance.healthBlue=c.Value<int?>("blue") ?? 8; return Status();
                case "pool":
                    var pooled=Target(c); pooled.gameObject.SetActive(false); pooled.gameObject.SetActive(true); return runtime.Track(pooled).State;
                case "unloadAudit":
                    var probe=new GameObject("HollowElements.UnloadAudit").AddComponent<UnloadAudit>();
                    probe.Begin(runtime.Mod,directory,c.Value<bool?>("reload") ?? true); runtime.Mod.Unload(); return "audit queued";
                case "loadout":
                    foreach (AttackSlot slot in Enum.GetValues(typeof(AttackSlot)))
                        if(c[slot.ToString()] != null) runtime.Mod.OnSaveLocal().Set(slot,Parse<Element>(c,slot.ToString()));
                    return runtime.Mod.OnSaveLocal();
                case "closeMenu": typeof(WorkshopMenus).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GetComponent<WorkshopMenus>(), null); return "closed";
                case "menuClick":
                    var panel = GetComponent<WorkshopMenus>();
                    var menuButtons = (List<UnityEngine.UI.Button>)typeof(WorkshopMenus).GetField("buttons",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
                    menuButtons[c.Value<int>("index")].onClick.Invoke(); return "clicked";
                case "menuNavigate": GetComponent<WorkshopMenus>().Navigate(c.Value<int?>("vertical")??0,c.Value<int?>("horizontal")??0,c.Value<bool?>("submit")??false,c.Value<bool?>("cancel")??false); return MenuStatus();
                case "menuStatus": return MenuStatus();
                case "menuSetting":
                    var entry=runtime.Mod.GetMenuData(null).First(e=>e.Name==c.Value<string>("name"));
                    entry.Saver(c.Value<int>("index")); return new {selected=entry.Loader(),settings=runtime.Mod.OnSaveGlobal()};
                case "modMenu": return runtime.Mod.GetMenuData(null).Select(e=>new {e.Name,e.Values,selected=e.Loader()});
                case "globalSettings":
                    if(c["settings"]!=null) runtime.Mod.OnLoadGlobal(c["settings"].ToObject<Settings.GlobalSettings>());
                    return runtime.Mod.OnSaveGlobal();
                case "presentationStatus":
                    var presentation=GetComponent<CombatPresentation>();
                    return new {settings=runtime.Mod.OnSaveGlobal(),effects=presentation.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.name=="反应特效" && s.enabled).Select(s=>new {frame=s.sprite.name,scale=s.transform.localScale.x,width=s.bounds.size.x}).ToArray(),numbers=presentation.CanvasRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(t=>t.transform.parent.name=="血条").Select(t=>new {t.text,visible=t.gameObject.activeInHierarchy,width=t.rectTransform.sizeDelta.x,height=t.transform.parent.GetComponent<RectTransform>().sizeDelta.y}).ToArray()};
                case "menuJson":
                    var input=(TMPro.TMP_InputField)typeof(WorkshopMenus).GetField("json",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(GetComponent<WorkshopMenus>());
                    input.text=c.Value<string>("text"); return "updated";
                case "pause": Time.timeScale = c.Value<bool?>("value") ?? true ? 0 : 1; return Time.timeScale;
                case "nativePause": GameManager.instance.StartCoroutine(GameManager.instance.PauseGameToggle()); return "pause toggle requested";
                case "debugMode": runtime.Mod.OnSaveGlobal().DebugEnabled=c.Value<bool?>("value") ?? false; return runtime.Mod.OnSaveGlobal().DebugEnabled;
                case "spriteCatalog": return new { sprites=Resources.FindObjectsOfTypeAll<Sprite>().Select(s=>s.name).ToArray(), collections=Resources.FindObjectsOfTypeAll<tk2dSpriteCollectionData>().Select(collection=>new {collection=collection.name,sprites=collection.spriteDefinitions.Where(d=>d!=null).Select(d=>d.name).ToArray()}).ToArray() };
                case "slashGallery":
                    var galleryView=GetComponent<CombatPresentation>();
                    var gallery=galleryView.Rect("HollowElements.SlashGallery",galleryView.CanvasRoot,Vector2.zero,new Vector2(1080,620));
                    gallery.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.025f,.035f,.055f,1);
                    var knight=Resources.FindObjectsOfTypeAll<tk2dSpriteCollectionData>().First(s=>s.name=="Knight").inst;
                    for(int i=0;i<9;i++)
                    {
                        string frame="dash_slash_effect"+i.ToString("0000");
                        var definition=knight.spriteDefinitions.First(s=>s.name==frame);
                        Vector2 pos=new Vector2((i%3-1)*320,175-(i/3)*180);
                        var graphic=galleryView.Rect(frame,gallery,pos,new Vector2(280,110)).gameObject.AddComponent<NativeSlashGraphic>();
                        graphic.Definition=definition; graphic.raycastTarget=false; graphic.SetAllDirty();
                        galleryView.Text(frame,gallery,pos+new Vector2(0,-75),new Vector2(300,35),20);
                    }
                    return "native dash frames rendered";
                case "attackVisuals": return Resources.FindObjectsOfTypeAll<AttackTint>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>new {name=t.name,source=t.GetComponent<AttackStamp>().Source.ToString(),t.SpriteCount,t.Coloured,colours=t.GetComponentsInChildren<tk2dBaseSprite>(true).Select(s=>s.color.ToString()).ToArray()}).ToArray();
                case "bossLevel": BossSceneController.Instance.BossLevel = c.Value<int>("value"); return BossSceneController.Instance.BossLevel;
                case "measurePlayer": return MeasurePlayer(hero);
                case "saveRoundTrip": return SaveRoundTrip(c.Value<int?>("slot") ?? 3);
                case "menu":
                    var menus = GetComponent<WorkshopMenus>();
                    typeof(WorkshopMenus).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menus, new object[] { c.Value<bool?>("debug") ?? true }); return "opened";
                case "setState":
                    var controlledFsm = hero.GetComponents<PlayMakerFSM>().First(x => x.FsmName == c.Value<string>("fsm")); controlledFsm.SetState(c.Value<string>("state")); return controlledFsm.ActiveStateName;
                case "goto":
                    if(!Application.CanStreamedLevelBeLoaded(c.Value<string>("scene"))) throw new InvalidOperationException("场景不存在："+c.Value<string>("scene"));
                    if((c.Value<string>("scene")??"").StartsWith("GG_"))
                    {
                        PlayerData.instance.dreamReturnScene="GG_Workshop";
                        PlayerData.instance.bossReturnEntryGate="door_dreamReturnGG_GG_Statue_Gruz";
                    }
                    GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo { SceneName = c.Value<string>("scene"), EntryGateName = c.Value<string>("gate") ?? "door_dreamEnter", Visualization = GameManager.SceneLoadVisualizations.Default, WaitForSceneTransitionCameraFade = false });
                    return "transitioning";
                case "dummy":
                    if (!hero || !UnitySceneManager.GetActiveScene().name.StartsWith("GG_")) throw new InvalidOperationException("木桩仅可在神居生成。");
                    var dummy = TrainingDummy.Spawn(hero.transform.position + new Vector3(c.Value<float?>("offset") ?? 4, 0), c.Value<bool?>("recoilable") ?? true);
                    runtime.Track(dummy.Health); return dummy.name;
                case "enemyAura":
                    var target = Target(c); var record = runtime.Track(target);
                    record.State.Aura = Parse<Element>(c, "element"); record.State.AuraUnits = c.Value<double?>("units") ?? 1; record.State.AuraUntil = runtime.Now + 60;
                    record.State.AuraDuration = 60;
                    return target.name;
                case "enemyControl":
                    var controlled = runtime.Track(Target(c)); controlled.State.FrozenUntil = runtime.Now + (c.Value<double?>("seconds") ?? 3);
                    controlled.State.FrozenDuration = c.Value<double?>("seconds") ?? 3; controlled.State.FrozenSpeed = runtime.Config.Reactions.FrostSpeed;
                    controlled.State.HardFrozen = c.Value<bool?>("hard") ?? false;controlled.State.HardFrozenUntil=controlled.State.HardFrozen?controlled.State.FrozenUntil:0;controlled.State.FrostAfterSpeed=runtime.Config.Reactions.FrostSpeed; return controlled.Health.name;
                case "hit":
                    var health = Target(c); var source = new GameObject("HollowElements.DevelopmentHit");
                    source.transform.position = hero ? hero.transform.position : health.transform.position + Vector3.left;
                    var stamp = source.AddComponent<AttackStamp>(); stamp.Source = Parse<AttackSource>(c, "source"); stamp.Cast = c.Value<long?>("cast") ?? runtime.Casts.Begin(RuleConfig.SlotOf(stamp.Source));
                    runtime.Mod.OnSaveLocal().Set(RuleConfig.SlotOf(stamp.Source), Parse<Element>(c, "element"));
                    int before = health.hp;
                    health.Hit(new HitInstance { Source = source, AttackType = c["attackType"]!=null ? Parse<AttackTypes>(c,"attackType") : AttackTypes.Nail, DamageDealt = c.Value<int?>("damage") ?? 10, Multiplier = c.Value<float?>("multiplier")??1, Direction = c.Value<float?>("direction")??0, IgnoreInvulnerable = true, MagnitudeMultiplier = 1 });
                    if(c.Value<bool?>("capture") ?? false) StartCoroutine(CaptureAfter(c.Value<float?>("delay") ?? .1f));
                    var hitBody=health.GetComponent<Rigidbody2D>();
                    Destroy(source); return new { before, after = health ? health.hp : 0, state = runtime.Track(health).State,velocityX=hitBody?hitBody.velocity.x:0,velocityY=hitBody?hitBody.velocity.y:0 };
                case "textureCompare": return TextureComparison(c.Value<bool?>("visible")??true);
                case "audioDiagnostics":
                    var ownAudio=GetComponent<CombatPresentation>().GetComponent<AudioSource>();var nativeAudio=hero?hero.GetComponent<HeroAudioController>():null;
                    return new {output=ownAudio.outputAudioMixerGroup?ownAudio.outputAudioMixerGroup.name:null,mixer=ownAudio.outputAudioMixerGroup?ownAudio.outputAudioMixerGroup.audioMixer.name:null,reference=nativeAudio && nativeAudio.takeHit && nativeAudio.takeHit.outputAudioMixerGroup?nativeAudio.takeHit.outputAudioMixerGroup.name:null,referenceId=nativeAudio && nativeAudio.takeHit && nativeAudio.takeHit.outputAudioMixerGroup?nativeAudio.takeHit.outputAudioMixerGroup.GetInstanceID():0,outputId=ownAudio.outputAudioMixerGroup?ownAudio.outputAudioMixerGroup.GetInstanceID():0,gain=runtime.Mod.OnSaveGlobal().SoundVolume*.5f,chain=AudioChain(ownAudio.outputAudioMixerGroup),referenceChain=AudioChain(nativeAudio && nativeAudio.takeHit?nativeAudio.takeHit.outputAudioMixerGroup:null),groups=Resources.FindObjectsOfTypeAll<UnityEngine.Audio.AudioMixerGroup>().Select(g=>new{g.name,mixer=g.audioMixer.name}).ToArray()};
                case "fontDiagnostics":
                    return GetComponent<CombatPresentation>().CanvasRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(t=>!string.IsNullOrEmpty(t.text)).Select(t=>new {t.text,font=t.font?t.font.name:null,size=t.fontSize,channels=t.canvas?t.canvas.additionalShaderChannels.ToString():null,material=t.fontSharedMaterial?t.fontSharedMaterial.name:null,texture=t.fontSharedMaterial && t.fontSharedMaterial.mainTexture?t.fontSharedMaterial.mainTexture.name:null,submeshes=t.GetComponentsInChildren<TMPro.TMP_SubMeshUI>(true).Select(m=>new{font=m.fontAsset?m.fontAsset.name:null,atlas=m.fontAsset && m.fontAsset.atlas?m.fontAsset.atlas.GetInstanceID():0,texture=m.sharedMaterial && m.sharedMaterial.mainTexture?m.sharedMaterial.mainTexture.GetInstanceID():0})}).ToArray();
                case "playerAttach": runtime.ApplyPlayerAttachment(Parse<Element>(c,"element"));return Status();
                case "playerAura": runtime.Player.Aura = Parse<Element>(c, "element"); runtime.Player.AuraUnits = 1; runtime.Player.AuraUntil = runtime.Now + 60; runtime.Player.AuraDuration = 60; return Status();
                case "hurt": return runtime.HurtPlayer(Parse<Element>(c, "element"), c.Value<int?>("damage") ?? 1);
                case "heal": hero.MaxHealth(); hero.AddMPCharge(99); return Status();
                case "clear": runtime.Clear(); return Status();
                case "attack": typeof(HeroController).GetMethod("DoAttack", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hero, null); return "attacked";
                case "fsmEvent":
                    var fsm = hero.GetComponents<PlayMakerFSM>().First(x => x.FsmName == c.Value<string>("fsm")); fsm.SendEvent(c.Value<string>("event")); return fsm.ActiveStateName;
                case "dump": return Dump();
                case "capture": ScreenCapture.CaptureScreenshot(Path.Combine(directory, "game.png")); return "queued";
                case "damageReductionMenu":
                    var reductionMenu=runtime.Mod.GetMenuData(null).Single(entry=>entry.Name=="难度");
                    if(c["index"]!=null) reductionMenu.Saver(c.Value<int>("index"));
                    var savedBalance=new Settings.SettingsStore();string savedMessage;
                    bool loadedBalance=savedBalance.TryReload(Path.Combine(runtime.Directory,"balance.json"),out savedMessage);
                    return new {reductionMenu.Name,reductionMenu.Values,index=reductionMenu.Loader(),multiplier=runtime.Config.DirectDamageMultiplier,
                        saved=loadedBalance ? (double?)savedBalance.Snapshot().DirectDamageMultiplier : null};
                case "quit": Application.Quit(); return "quitting";
                default: throw new InvalidOperationException("未知开发命令：" + name);
            }
        }
        private IEnumerator CaptureAfter(float delay) { yield return new WaitForSeconds(delay); ScreenCapture.CaptureScreenshot(Path.Combine(directory,"reaction.png")); }
        private object TextureComparison(bool visible)
        {
            if(textureComparison) Destroy(textureComparison);foreach(var asset in comparisonAssets) if(asset) Destroy(asset);comparisonAssets.Clear();
            if(!visible) return "closed";
            var view=GetComponent<CombatPresentation>();float width=view.CanvasRoot.rect.width*.94f,height=view.CanvasRoot.rect.height*.92f;
            var root=view.Image("HollowElements.TextureComparison",view.CanvasRoot,Vector2.zero,new Vector2(width,height),new Color(.035f,.04f,.055f,1));textureComparison=root.gameObject;
            root.transform.SetAsLastSibling();float tile=Mathf.Min((width-100)/2,height-150);
            for(int i=0;i<2;i++)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};comparisonAssets.Add(texture);
                texture.LoadImage(File.ReadAllBytes(Path.Combine(directory,"texture-preview",i==0?"before.png":"after.png")));
                var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));comparisonAssets.Add(sprite);
                float x=(i==0?-1:1)*(width/4);var image=view.Image("对比",root.transform,new Vector2(x,-15),new Vector2(tile,tile),Color.white);image.sprite=sprite;image.preserveAspect=true;
                view.Text(i==0?"原图":"发布版",root.transform,new Vector2(x,height/2-45),new Vector2(width/2,50),30);
            }
            return new {screenWidth=Screen.width,screenHeight=Screen.height,drawPixels=tile*Screen.width/view.CanvasRoot.rect.width};
        }
        private static object AudioChain(UnityEngine.Audio.AudioMixerGroup group)
        {
            var chain=new List<object>();var visited=new HashSet<int>();
            while(group && visited.Add(group.GetInstanceID())) {chain.Add(new {group=group.name,mixer=group.audioMixer.name});group=group.audioMixer.outputAudioMixerGroup;}
            return chain;
        }
        private object MenuStatus()
        {
            var ui=GetComponent<WorkshopMenus>(); var type=typeof(WorkshopMenus);
            var list=(List<UnityEngine.UI.Button>)type.GetField("buttons",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            var status=(TMPro.TextMeshProUGUI)type.GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            var benches=(List<KeyValuePair<PlayMakerFSM,bool>>)type.GetField("pausedBenches",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            var panel=(RectTransform)type.GetField("panel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            return new { open=type.GetField("open",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui),selected=type.GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui),
                captures=WorkshopMenus.CapturesInput,pausedBenches=benches.Count,status=status?status.text:"",buttons=list.Select(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>().text).ToArray(),
                slots=new string[0],icons=new string[0][],
                title=panel?panel.name:null,pending=(Settings.Loadout)null,loadout=runtime.Mod.OnSaveLocal(),config=runtime.Settings.Snapshot() };
        }
        private object SaveRoundTrip(int slot)
        {
            bool saved=false,loaded=false;
            string before=JsonConvert.SerializeObject(runtime.Mod.OnSaveLocal());
            isolatedSave=true;
            try
            {
                GameManager.instance.SaveGame(slot,ok => saved=ok);
                if(!saved) throw new InvalidOperationException("隔离存档写入失败。");
                runtime.Mod.OnLoadLocal(new Settings.Loadout());
                GameManager.instance.LoadGame(slot,ok => loaded=ok);
                if(HeroController.SilentInstance) typeof(HeroController).GetField("playerData",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(HeroController.SilentInstance,PlayerData.instance);
                string after=JsonConvert.SerializeObject(runtime.Mod.OnSaveLocal());
                return new { saved,loaded,matched=before==after,before,after,path=Path.Combine(directory,"user"+slot+".modded.json") };
            }
            finally { isolatedSave=false; }
        }
        private object MeasurePlayer(HeroController hero)
        {
            var move=typeof(HeroController).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic);
            var body=hero.GetComponent<Rigidbody2D>(); var velocity=body.velocity; var state=runtime.Player;
            float frozen=0,normal=0;
            try
            {
                move.Invoke(hero,new object[]{1f}); frozen=body.velocity.x;
                runtime.Player=new PlayerState(); move.Invoke(hero,new object[]{1f}); normal=body.velocity.x;
            }
            finally { runtime.Player=state; body.velocity=velocity; }
            typeof(HeroController).GetMethod("CancelAttack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hero,null);
            var cooldown=typeof(HeroController).GetField("attack_cooldown",BindingFlags.Instance|BindingFlags.NonPublic); cooldown.SetValue(hero,0f);
            typeof(HeroController).GetMethod("DoAttack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hero,null);
            float expected=PlayerData.instance.equippedCharm_32 ? hero.ATTACK_COOLDOWN_TIME_CH : hero.ATTACK_COOLDOWN_TIME;
            return new { frozenVelocity=frozen,normalVelocity=normal,moveRatio=normal==0?0:frozen/normal,cooldown=cooldown.GetValue(hero),baseCooldown=expected,state=runtime.Player };
        }
        private HealthManager Target(JObject command)
        {
            if(command["targetId"]!=null) return FindObjectsOfType<HealthManager>().First(x=>x.GetInstanceID()==command.Value<int>("targetId"));
            string text = command.Value<string>("target") ?? "反应木桩";
            return FindObjectsOfType<HealthManager>().First(x => x.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        private static T Parse<T>(JObject c, string key) where T : struct { return (T)Enum.Parse(typeof(T), c.Value<string>(key) ?? "None", true); }
        private object Status()
        {
            var hero = HeroController.SilentInstance;
            return new { scene = UnitySceneManager.GetActiveScene().name, time = runtime.Now, timeScale = Time.timeScale,forcedNail=runtime.ForcedNailElement,forcedNailUntil=runtime.ForcedNailUntil,
                hero = hero ? new { position = hero.transform.position.ToString(), health = PlayerData.instance.health, blue = PlayerData.instance.healthBlue, soul = PlayerData.instance.MPCharge,
                    nail = PlayerData.instance.nailDamage, canCast = hero.CanCast(), canFocus = hero.CanFocus(), canTakeDamage=typeof(HeroController).GetMethod("CanTakeDamage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hero,null),isInvincible=PlayerData.instance.isInvincible,atBench = PlayerData.instance.atBench,
                    spellLevels = new { wave = GameManager.instance.GetPlayerDataInt("fireballLevel"), dive = GameManager.instance.GetPlayerDataInt("quakeLevel"), shriek = GameManager.instance.GetPlayerDataInt("screamLevel") },
                    rawSpellLevels = new { wave=PlayerData.instance.fireballLevel,dive=PlayerData.instance.quakeLevel,shriek=PlayerData.instance.screamLevel },
                    state = runtime.Player, fsm = hero.GetComponents<PlayMakerFSM>().Select(f => new { f.FsmName, f.ActiveStateName }).ToArray() } : null,
                enemies = FindObjectsOfType<HealthManager>().Select(h => new { id=h.GetInstanceID(),objectId=h.gameObject.GetInstanceID(), h.name, h.hp, h.isDead, h.IsInvincible,h.InvincibleFromDirection,position = h.transform.position.ToString(), state = runtime.Track(h).State,
                    components=h.GetComponents<Component>().Select(x=>x.GetType().Name).ToArray(),mechanic=BossStatus(h),velocity=h.GetComponent<Rigidbody2D>() ? h.GetComponent<Rigidbody2D>().velocity.ToString() : "none",gravity=h.GetComponent<Rigidbody2D>() ? h.GetComponent<Rigidbody2D>().gravityScale : 0,
                    clock = ClockStatus(runtime.Track(h).Clock), boss = runtime.Track(h).Boss,
                    fsms = h.GetComponents<PlayMakerFSM>().Select(f => new { f.FsmName, f.ActiveStateName }).ToArray() }).ToArray(),
                trace = runtime.Trace.ToArray() };
        }
        private static object ClockStatus(EnemyClock c) { return new { scale = c.Scale, c.AcceptedFrames, c.SkippedFrames, c.AcceptedPhysics, c.SkippedPhysics, c.AnimationInput, c.AnimationOutput }; }
        private static object BossStatus(HealthManager health)
        {
            var watcher=health.GetComponent<WatcherKnightsBoss>();if(watcher) return new {kind="Watcher",armoured=watcher.Armoured,vulnerableUntil=watcher.VulnerableUntil};
            var uumuu=health.GetComponent<UumuuBoss>();if(uumuu) return new {kind="Uumuu",shielded=uumuu.Shielded,shatterBreaks=uumuu.ShatterBreaks,nodes=uumuu.Nodes,exposure=uumuu.ExposureMultiplier};
            var hu=health.GetComponent<ElderHuBoss>();if(hu) return new {kind="Hu",element=hu.HammerElement,swapped=hu.Swapped,shadowDisabledUntil=CombatRuntime.Current.ShadowDashDisabledUntil};
            var markoth=health.GetComponent<MarkothBoss>();if(markoth) return new {kind="Markoth",stoppedUntil=markoth.StoppedUntil,readyAt=markoth.ReadyAt,clears=markoth.Clears,burst=markoth.Bursting};
            var traitor=health.GetComponent<TraitorLordBoss>();if(traitor) return new {kind="Traitor",pending=traitor.WavePending,replacements=traitor.Replacements};
            var pure=health.GetComponent<PureVesselBoss>();if(pure) return new {kind="PureVessel",pending=pure.TendrilPending,replacements=pure.Replacements};
            var radiance=health.GetComponent<RadianceBoss>();if(radiance) return new {kind="Radiance",colour=radiance.Colour,followup=radiance.Followup,until=radiance.ColouredUntil};
            var sly=health.GetComponent<SlyBoss>();if(sly) return new {kind="Sly",slot=sly.CurrentSlot,element=sly.ElementFor(sly.CurrentSlot)};
            var marmu=health.GetComponent<MarmuBoss>(); if(marmu) return new {kind="Marmu",restUntil=marmu.RestUntil};
            var vessel=health.GetComponent<BrokenVesselBoss>(); if(vessel) return new {kind="Vessel",dream=vessel.Dream,empowered=vessel.Empowered,overloads=vessel.Overloads,knockdowns=vessel.Knockdowns};
            var hive=health.GetComponent<HiveKnightBoss>(); if(hive) return new {kind="Hive",healing=hive.Healing,beeHits=hive.BeeHits};
            var collector=health.GetComponent<CollectorBoss>(); if(collector) return new {kind="Collector",summons=collector.ImmediateSummons,jumps=collector.NeutralJumps};
            var summon=health.GetComponent<EncounterSummon>(); if(summon) return new {kind="CollectorSummon",maximum=summon.Maximum,owner=summon.Owner ? summon.Owner.name : null};
            var shield=health.GetComponent<CrystalGuardianBoss>(); if(shield) return new {kind="CrystalGuardian",shield=shield.Shielded,headHits=shield.HeadHits,returnsAt=shield.ShieldReturnsAt};
            var oblob=health.GetComponent<OblobblesBoss>(); if(oblob) return new {kind="Oblobble",aura=oblob.AuraElement,bullets=oblob.BulletElement,enraged=oblob.Enraged};
            var mantis=health.GetComponent<MantisLordsBoss>(); if(mantis) return new {kind="Mantis",element=mantis.Element,parent=mantis.transform.parent.name};
            var hornet=health.GetComponent<HornetProtectorBoss>(); if(hornet) return new {kind="Hornet",stacks=hornet.Reactions,vulnerability=hornet.Vulnerability,tempo=hornet.Tempo,last=hornet.LastElement,repeated=hornet.RepeatedHits};
            var gorb=health.GetComponent<GorbBoss>(); if(gorb) return new {kind="Gorb",ring=gorb.Ring,element=gorb.RingElement,suppressedUntil=gorb.SuppressedUntil};
            var dung=health.GetComponent<DungDefenderBoss>(); if(dung) return new {kind="Dung",ball=dung.Ball,tempo=dung.Tempo,knockdowns=dung.ForcedKnockdowns,surface=dung.GroundY,stacks=dung.SplitStacks,overloads=dung.OverloadProgress,readyAt=dung.KnockdownReadyAt};
            var mawlek=health.GetComponent<BroodingMawlekBoss>(); if(mawlek) return new {kind="Mawlek",hits=mawlek.AcceptedHits,cryo=mawlek.CryoApplications};
            var brother=health.GetComponent<NailBrothersBoss>(); if(brother) return new {kind="Nailmaster",dual=brother.Dual,element=brother.Element};
            var king=health.GetComponent<VengeflyKingBoss>();
            if(king) return new {kind="VengeflyKing",element=king.CurrentElement,summonElement=king.SummonElement,roars=king.RoarCount};
            var gruz=health.GetComponent<GruzMotherBoss>();
            if(gruz) return new {kind="GruzMother",element=gruz.CurrentElement,collisions=gruz.CollisionCount};
            var shell=health.GetComponent<FalseKnightBoss>();
            if(shell) return new {kind="FalseKnight",element=shell.ShellElement};
            var moss=health.GetComponent<MassiveMossChargerBoss>();
            if(moss) return new {kind="Moss",frosted=moss.Frosted};
            var minion=health.GetComponent<ElementalMinion>();
            if(minion) return new {kind=health.name.StartsWith("Fluke Fly") ? "Flukefey" : health.name.StartsWith("Parasite Balloon") ? "Balloon" : "Vengefly",element=minion.Element,summonerId=minion.SummonerId,outline=health.transform.Find("HollowElements.ElementOutline")!=null};
            return null;
        }
        private object Dump()
        {
            return FindObjectsOfType<PlayMakerFSM>().Select(f => new { obj = f.name, f.FsmName, f.ActiveStateName,
                states = f.FsmStates.Select(s => new { s.Name, transitions = s.Transitions.Select(t => new { t.EventName, t.ToState }),
                    actions = s.Actions.Select(a => new { type = a.GetType().FullName, fields = a.GetType().GetFields().ToDictionary(field => field.Name, field => SafeValue(field.GetValue(a))) }) }) }).ToArray();
        }
        private static string SafeValue(object value)
        {
            if (value == null) return "null";
            var owner=value as HutongGames.PlayMaker.FsmOwnerDefault;
            if(owner!=null) return owner.OwnerOption+":"+(owner.GameObject.Value ? PathOf(owner.GameObject.Value.transform) : "null");
            try { var property = value.GetType().GetProperty("Value"); if (property != null) value = property.GetValue(value, null); return value?.ToString() ?? "null"; }
            catch { return value.GetType().Name; }
        }
        private static object ProbeValue(object value)
        {
            if(value is Array array) return array.Cast<object>().Select(ProbeValue).ToArray();
            if(value is HutongGames.PlayMaker.NamedVariable variable) return new {variable.Name,value=SafeValue(variable)};
            if(value is HutongGames.PlayMaker.FsmEvent evt) return evt.Name;
            return SafeValue(value);
        }
        private static string PathOf(Transform t) { return t.parent ? PathOf(t.parent)+"/"+t.name : t.name; }
        private object ExportEnemySprites(HealthManager health)
        {
            string output=Path.Combine(directory,"native-sprites"); System.IO.Directory.CreateDirectory(output);
            var animator=health.GetComponent<tk2dSpriteAnimator>(); var sprite=health.GetComponent<tk2dBaseSprite>();
            var texture=sprite.GetCurrentSpriteDef().material.mainTexture;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32); var previous=RenderTexture.active;
            var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try { Graphics.Blit(texture,rt); RenderTexture.active=rt; copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); copy.Apply(); File.WriteAllBytes(Path.Combine(output,health.name+".png"),copy.EncodeToPNG()); }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Destroy(copy); }
            var manifest=new {width=texture.width,height=texture.height,clips=animator.Library.clips.Select(clip=>new {clip.name,clip.fps,frames=clip.frames.Select(frame=>new {frame.spriteId,name=frame.spriteCollection.spriteDefinitions[frame.spriteId].name,uvs=frame.spriteCollection.spriteDefinitions[frame.spriteId].uvs.Select(v=>new[]{v.x,v.y}),positions=frame.spriteCollection.spriteDefinitions[frame.spriteId].positions.Select(v=>new[]{v.x,v.y})})})};
            File.WriteAllText(Path.Combine(output,health.name+".json"),JsonConvert.SerializeObject(manifest)); return output;
        }
        private void OnDestroy()
        {
            if (directory==null) return;
            if(textureComparison) Destroy(textureComparison);foreach(var asset in comparisonAssets) if(asset) Destroy(asset);comparisonAssets.Clear();
            runtime.PlayerDamageObserved -= ObservePlayerDamage;
            On.HealthManager.Die -= CountDeath;
            if(settingsScreen) Destroy(settingsScreen.gameObject);
            On.GameManager.SaveGame_int_Action1 -= SuppressSave; On.DesktopPlatform.ReadSaveSlot -= ReadIsolatedSave; On.DesktopPlatform.WriteSaveSlot -= WriteIsolatedSave;
            savePathHook?.Dispose();
        }
    }
}
