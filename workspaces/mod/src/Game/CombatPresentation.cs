using System;
using System.Collections.Generic;
using System.Linq;
using HollowElements.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HollowElements.Game
{
    [DefaultExecutionOrder(5000)]
    internal sealed partial class CombatPresentation : MonoBehaviour
    {
        internal PresentationAssets Assets;
        internal readonly CharmIconAssets CharmIcons=new CharmIconAssets();
        internal RectTransform CanvasRoot;
        internal RectTransform MenuCanvasRoot;
        private Canvas hudCanvas;
        private Renderer nativeHudFrame;
        internal CombatRuntime Runtime;
        private AudioSource audio;
        private readonly Dictionary<string, float> soundTimes = new Dictionary<string, float>();
        private readonly List<FxInstance> effects = new List<FxInstance>();
        private readonly List<Popup> popups = new List<Popup>();
        private readonly Dictionary<HealthManager, Bar> bars = new Dictionary<HealthManager, Bar>();
        private readonly Dictionary<string, FxInstance> states = new Dictionary<string, FxInstance>();
        private RectTransform playerIcons;
        private Sprite ring;
        internal Sprite CharmSlotPattern => ring;

        internal void Initialize(CombatRuntime runtime)
        {
            Runtime = runtime; Assets = new PresentationAssets(); Assets.Load();
            CanvasRoot=CreateCanvas("HollowElements.HUD",false);
            hudCanvas=CanvasRoot.GetComponent<Canvas>();hudCanvas.renderMode=RenderMode.ScreenSpaceCamera;
            BindHudLayer();
            MenuCanvasRoot=CreateCanvas("HollowElements.Menus",true);
            MenuCanvasRoot.GetComponent<Canvas>().sortingOrder=2000;
            audio = gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
            ring = Ring();
            playerIcons = Rect("玩家元素状态", CanvasRoot, new Vector2(560, -175), new Vector2(450, 48), new Vector2(0, 1));
            runtime.Feedback += Feedback; runtime.Pulse += Pulse; runtime.Cleared += Clear;
        }
        private RectTransform CreateCanvas(string name,bool interactive)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            obj.layer=LayerMask.NameToLayer("UI");obj.transform.SetParent(transform,false);
            var canvas=obj.GetComponent<Canvas>();canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            if(interactive) obj.AddComponent<GraphicRaycaster>();
            var scaler=obj.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            return (RectTransform)obj.transform;
        }
        private void BindHudLayer()
        {
            var cameras=GameCameras.instance;
            if(!cameras || !cameras.hudCamera || !cameras.hudCanvas) {hudCanvas.enabled=false;return;}
            if(!nativeHudFrame)
            {
                var frame=cameras.hudCanvas.transform.Find("Soul Orb/HUD_frame");
                if(frame) nativeHudFrame=frame.GetComponent<Renderer>();
            }
            if(!nativeHudFrame) {hudCanvas.enabled=false;return;}
            // Render alongside the native soul/mask HUD, before inventory and
            // pause UI. The guide/workshop keep their independent menu canvas.
            hudCanvas.worldCamera=cameras.hudCamera;
            hudCanvas.sortingLayerID=nativeHudFrame.sortingLayerID;hudCanvas.sortingOrder=nativeHudFrame.sortingOrder;
            hudCanvas.planeDistance=Mathf.Clamp(Vector3.Dot(nativeHudFrame.transform.position-cameras.hudCamera.transform.position,cameras.hudCamera.transform.forward),cameras.hudCamera.nearClipPlane+.1f,cameras.hudCamera.farClipPlane-.1f);
            hudCanvas.enabled=cameras.hudCanvas.activeInHierarchy;
        }
        internal RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer=parent.gameObject.layer;rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        internal UnityEngine.UI.Image Image(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(name, parent, pos, size).gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        internal TextMeshProUGUI Text(string value, Transform parent, Vector2 pos, Vector2 size, float fontSize = 24)
        {
            var text = Rect("文字", parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            Assets.SdfFonts.Ensure(value);text.font = Assets.Font;text.fontSharedMaterial=Assets.Font.material; text.fontSize = fontSize; text.color = Color.white; text.text = value;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.enableWordWrapping = false;
            return text;
        }
        private void Feedback(Vector3 position, int damage, Element element, Reaction reaction, bool player, bool frost)
        {
            if (!player && damage > 0) ShowPopup(position, damage.ToString(), Chinese.Colors[(int)element], false, false);
            if (reaction == Reaction.None) return;
            ShowPopup(position + Vector3.up * .9f, Chinese.Reaction(reaction), Chinese.Colors[(int)Chinese.ReactionColor(reaction)], true, player);
            if(!player && reaction==Reaction.Overload && Runtime.DeferredOverloadFeedback) return;
            string sound = Chinese.Effect(reaction);
            // Player freeze slows movement/attacks; it does not immobilize the
            // knight. Reserve the ice block for enemies with HardFrozen set.
            frost |= player && reaction == Reaction.Freeze;
            string effect = frost && reaction == Reaction.Freeze ? "frost" : frost && reaction == Reaction.Shatter ? "frost-shatter" : sound;
            if (effect != null) { var fx=Play(effect, position, player, EffectTarget(position,player)); if(fx!=null && !player && reaction==Reaction.Overload) fx.ExtraScale=Runtime.OverloadFeedbackScale; Sound(sound); }
        }
        private void Pulse(Vector3 position, DamageOrigin origin, bool player)
        {
            if (origin != DamageOrigin.ElectroCharged) return;
            Play("electro-state", position, player, EffectTarget(position,player)); Sound("electro_tick");
        }
        private Transform EffectTarget(Vector3 position,bool player)
        {
            if(player) return HeroController.SilentInstance ? HeroController.SilentInstance.transform : null;
            Transform nearest=null; float distance=.1f;
            foreach(var record in Runtime.Enemies.Values) if(record.Health && record.Health.gameObject.activeInHierarchy)
            {
                float d=(record.Health.transform.position-position).sqrMagnitude;
                if(d<distance) { distance=d;nearest=record.Health.transform; }
            }
            return nearest;
        }
        private void PlaceEffect(FxInstance instance)
        {
            float bodyScale=instance.BodyScale;
            if(instance.Follow)
            {
                var collider=instance.Follow.GetComponent<BoxCollider2D>() as Collider2D ?? instance.Follow.GetComponent<Collider2D>();
                if(collider && collider.enabled)
                {
                    instance.Renderer.transform.position=collider.bounds.center;
                    bodyScale=Mathf.Clamp(Mathf.Sqrt(Mathf.Max(collider.bounds.size.x,collider.bounds.size.y)/2f),.9f,2.2f);
                }
                else instance.Renderer.transform.position=instance.Follow.position;
            }
            instance.BodyScale=bodyScale;
            var settings=Runtime.Mod.OnSaveGlobal();
            instance.Renderer.transform.localScale=Vector3.one*(instance.Player?settings.PlayerFxScale:settings.EnemyFxScale)*settings.ReactionFxScale*1.2f*bodyScale*instance.ExtraScale*(instance.Name=="vaporize"?1.5f:1f);
        }
        private void Sound(string name)
        {
            AudioClip clip; if (!Assets.Sounds.TryGetValue(name, out clip)) return;
            float last; if (soundTimes.TryGetValue(name, out last) && Time.unscaledTime - last < .06f) return;
            if(!audio.outputAudioMixerGroup && GameManager.instance && GameManager.instance.actorSnapshotUnpaused)
            {
                var mixer=GameManager.instance.actorSnapshotUnpaused.audioMixer;
                audio.outputAudioMixerGroup=mixer.FindMatchingGroups("Actors").FirstOrDefault(g=>g.name=="Actors");
            }
            // Native actor sounds follow the SFX volume and pause/damage snapshots.
            // Hero takeHit is intentionally on the UI mixer, so it is not a suitable combat bus.
            if(!audio.outputAudioMixerGroup) return;
            soundTimes[name] = Time.unscaledTime; audio.PlayOneShot(clip, Runtime.Mod.OnSaveGlobal().SoundVolume*.5f);
        }
        private FxInstance Play(string name, Vector3 position, bool player, Transform follow)
        {
            FxAsset asset; if (!Assets.Effects.TryGetValue(name, out asset)) return null;
            FxInstance instance = null;
            foreach (var item in effects) if (!item.Renderer.enabled) { instance = item; break; }
            if (instance == null)
            {
                if (effects.Count >= 64) return null;
                var obj = new GameObject("反应特效"); obj.transform.SetParent(transform, false);
                instance = new FxInstance { Renderer = obj.AddComponent<SpriteRenderer>() }; instance.Renderer.sortingOrder = 60; effects.Add(instance);
            }
            instance.Asset = asset; instance.Name=name; instance.BodyScale=1;instance.ExtraScale=1; instance.Start = Time.time; instance.Follow = follow; instance.Player = player;
            instance.Renderer.enabled = true; instance.Renderer.transform.position = position; instance.Renderer.sprite = asset.Frames[0];
            PlaceEffect(instance);
            return instance;
        }
        private void ShowPopup(Vector3 position, string value, Color color, bool reaction, bool italic)
        {
            Assets.SdfFonts.Ensure(value);
            int nearby=0;
            if(!reaction) foreach(var item in popups)
                if(item.Text.gameObject.activeSelf && !item.Reaction && Time.time-item.Start<.65f && Vector2.Distance(item.World,position)<.35f) nearby++;
            Popup popup = null;
            foreach (var item in popups) if (!item.Text.gameObject.activeSelf) { popup = item; break; }
            if (popup == null)
            {
                if (popups.Count >= 96) return;
                var outline=Text("",CanvasRoot,Vector2.zero,new Vector2(240,56));
                outline.font=Assets.OutlineFont;outline.fontSharedMaterial=Assets.OutlineMaterial; outline.name="跳字白描边";
                popup = new Popup { Outline=outline,Text = Text("", CanvasRoot, Vector2.zero, new Vector2(240, 56)) }; popup.Text.name="跳字"; popups.Add(popup);
            }
            popup.Start = Time.time; popup.World = position; popup.Reaction=reaction;
            popup.Offset=reaction ? 0 : nearby%4==0 ? -36 : nearby%4==1 ? 36 : nearby%4==2 ? -84 : 84;
            popup.Text.gameObject.SetActive(true);
            popup.Text.text = value; popup.Text.color = color;
            SizePopup(popup);
            popup.Text.fontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
            popup.Outline.gameObject.SetActive(true); popup.Outline.text=value; popup.Outline.color=Color.white;
            popup.Outline.fontSize=popup.Text.fontSize; popup.Outline.fontStyle=popup.Text.fontStyle;
        }
        private void SizePopup(Popup popup)
        {
            float scale=Runtime.Mod.OnSaveGlobal().PopupScale;
            popup.Text.fontSize=(popup.Reaction?60:48)*scale;popup.Outline.fontSize=popup.Text.fontSize;
            popup.Text.rectTransform.sizeDelta=popup.Outline.rectTransform.sizeDelta=new Vector2(480*scale,112*scale);
        }
        private Vector2 Screen(Vector3 position, out bool visible)
        {
            var camera = Camera.main;
            if (!camera) { visible = false; return Vector2.zero; }
            var p = camera.WorldToScreenPoint(position);visible=p.z>0 && camera.pixelRect.Contains(p);
            Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRoot,p,hudCanvas.worldCamera,out local);return local;
        }
        private void LateUpdate()
        {
            if (Runtime == null || Assets == null) return;
            BindHudLayer();
            foreach (var instance in effects)
            {
                if (!instance.Renderer.enabled) continue;
                int index = Mathf.FloorToInt((Time.time - instance.Start) * instance.Asset.Fps);
                if (instance.Asset.Loop) index %= instance.Asset.Frames.Length;
                else if (index >= instance.Asset.Frames.Length) { instance.Renderer.enabled = false; continue; }
                instance.Renderer.sprite = instance.Asset.Frames[Mathf.Max(0, index)];
                PlaceEffect(instance);
            }
            foreach (var popup in popups)
            {
                if (!popup.Text.gameObject.activeSelf) continue;
                SizePopup(popup);
                float age = Time.time - popup.Start;
                if (age > .85f) { popup.Text.gameObject.SetActive(false); popup.Outline.gameObject.SetActive(false); continue; }
                bool visible; var p = Screen(popup.World + Vector3.up * .65f, out visible);
                popup.Text.rectTransform.anchoredPosition = p + new Vector2(popup.Offset, age * 75);
                var color = popup.Text.color; color.a = visible ? Mathf.Clamp01((.85f-age) * 4) : 0; popup.Text.color = color;
                popup.Outline.rectTransform.anchoredPosition=popup.Text.rectTransform.anchoredPosition;
                popup.Outline.color=new Color(1,1,1,color.a);
            }
            var activeStates = new HashSet<string>();
            var removed = new List<HealthManager>(); int bosses = 0;
            foreach (var entry in bars) if (!entry.Key || !Runtime.Enemies.ContainsKey(entry.Key)) removed.Add(entry.Key);
            foreach (var key in removed) { Destroy(bars[key].Root.gameObject); bars.Remove(key); }
            foreach (var record in Runtime.Enemies.Values)
            {
                if (!record.Health) continue;
                var soul=record.Health.GetComponent<HollowElements.Game.Bosses.SoulMasterBoss>();
                var watcher=record.Health.GetComponent<HollowElements.Game.Bosses.WatcherKnightsBoss>();
                if (!record.Health.gameObject.activeInHierarchy || record.Health.isDead || record.Health.hp<=0 || record.Health != record.Health.GetComponent<HealthManager>() || soul && !soul.BarVisible || watcher && !watcher.BarVisible)
                {
                    Bar duplicate; if(bars.TryGetValue(record.Health,out duplicate)) duplicate.Root.gameObject.SetActive(false);
                    continue;
                }
                Bar bar;
                if (!bars.TryGetValue(record.Health, out bar)) { bar = CreateBar(); bars.Add(record.Health, bar); }
                var collider = record.Health.GetComponent<Collider2D>();
                Vector3 top = collider ? new Vector3(collider.bounds.center.x,collider.bounds.max.y+.5f,record.Health.transform.position.z) : record.Health.transform.position + Vector3.up*1.9f;
                bool visible; Vector2 p = Screen(top, out visible);
                if (record.Boss) { p = new Vector2(0, -CanvasRoot.rect.height/2 + 62 + bosses++ * 65); visible=true; }
                bool showBars=Runtime.Mod.OnSaveGlobal().ShowHealthBars;
                Vector2 external=Vector2.zero;bar.External=!showBars && ExternalHealthBar.TryAnchor(record.Health,CanvasRoot,out external);
                if(bar.External) {p=external;visible=true;}
                bar.Root.anchoredPosition = p; bar.Root.gameObject.SetActive(visible);
                bool numbers=showBars && Runtime.Mod.OnSaveGlobal().ShowHealthNumbers;
                bar.Fill.gameObject.SetActive(showBars);bar.Background.gameObject.SetActive(showBars);
                float width=record.Boss ? Mathf.Min(680,CanvasRoot.rect.width-80) : numbers ? 185 : 110;
                bar.Root.sizeDelta = new Vector2(width,numbers ? 26 : 9);
                bar.Fill.anchorMax = new Vector2(Mathf.Clamp01((float)record.Health.hp / record.Maximum), 1);
                bar.Numbers.gameObject.SetActive(numbers); bar.NumberOutline.gameObject.SetActive(numbers);
                if(numbers)
                {
                    bar.Numbers.text = Math.Max(0,record.Health.hp) + " / " + record.Maximum;
                    bar.Numbers.fontSize=record.Boss ? 20 : 18;
                    bar.Numbers.rectTransform.sizeDelta=new Vector2(width,26);
                    bar.NumberOutline.text=bar.Numbers.text; bar.NumberOutline.fontSize=bar.Numbers.fontSize;
                    bar.NumberOutline.rectTransform.sizeDelta=bar.Numbers.rectTransform.sizeDelta;
                }
                bar.Icons.anchoredPosition=new Vector2(0,numbers ? 48 : 32);
                UpdateIcons(bar.Icons, record.State, null,record.Health);
                var uumuu=record.Health.GetComponent<HollowElements.Game.Bosses.UumuuBoss>();
                if(uumuu && bar.Nodes==null)
                {
                    bar.Nodes=Rect("破盾节点",bar.Root,new Vector2(0,70),new Vector2(64,24));
                    for(int i=0;i<2;i++) {var node=Image("节点",bar.Nodes,new Vector2(i==0?-15:15,0),new Vector2(22,22),Color.white);node.sprite=ring;}
                }
                if(bar.Nodes) {bar.Nodes.gameObject.SetActive(uumuu && uumuu.Shielded);if(uumuu) for(int i=0;i<2;i++) bar.Nodes.GetChild(i).GetComponent<UnityEngine.UI.Image>().color=i<uumuu.Nodes?new Color(.5f,.9f,1):new Color(.2f,.2f,.2f);}
                string prefix = record.Health.GetInstanceID().ToString();
                if (record.State.FrozenUntil > Runtime.Now) StateFx(prefix+"F", record.State.HardFrozen ? "frost-state" : "soft-frost-state", record.Health.transform, false, activeStates);
                if (record.State.SuperconductUntil > Runtime.Now) StateFx(prefix+"S", "superconduct-state", record.Health.transform, false, activeStates);
            }
            UpdateCharmMasks();
            var hero = HeroController.SilentInstance;
            playerIcons.gameObject.SetActive(hero);
            if (hero)
            {
                // Position within the actual viewport/safe area rather than a
                // fixed 16:9 offset. Reserve room for the native mask HUD.
                Rect safe=UnityEngine.Screen.safeArea;
                if(Camera.main) {Rect viewport=Camera.main.pixelRect;safe=UnityEngine.Rect.MinMaxRect(Mathf.Max(safe.xMin,viewport.xMin),Mathf.Max(safe.yMin,viewport.yMin),Mathf.Min(safe.xMax,viewport.xMax),Mathf.Min(safe.yMax,viewport.yMax));}
                float left=safe.xMin/UnityEngine.Screen.width*CanvasRoot.rect.width;
                float top=(UnityEngine.Screen.height-safe.yMax)/UnityEngine.Screen.height*CanvasRoot.rect.height;
                float safeWidth=safe.width/UnityEngine.Screen.width*CanvasRoot.rect.width;
                playerIcons.anchoredPosition=new Vector2(left+Mathf.Min(340,(safeWidth-64)*.5f),-top-180);
                playerIcons.sizeDelta=new Vector2(Mathf.Min(450,CanvasRoot.rect.width-64),48);
                UpdateIcons(playerIcons, null, Runtime.Player);
                if (Runtime.Player.FrozenUntil > Runtime.Now && !Runtime.Charms.Suppressed(Runtime.Now)) StateFx("PlayerF", "soft-frost-state", hero.transform, true, activeStates);
                if (Runtime.Player.SuperconductUntil > Runtime.Now && !Runtime.Charms.Suppressed(Runtime.Now)) StateFx("PlayerS", "superconduct-state", hero.transform, true, activeStates);
            }
            var expired = new List<string>();
            foreach (var state in states) if (!activeStates.Contains(state.Key)) { state.Value.Renderer.enabled = false; expired.Add(state.Key); }
            foreach (var key in expired) states.Remove(key);
        }
        private Bar CreateBar()
        {
            var root = Rect("血条", CanvasRoot, Vector2.zero, new Vector2(110, 9));
            var background = Image("底色", root, Vector2.zero, Vector2.zero, new Color(.02f,.03f,.05f,.85f)); Stretch(background.rectTransform);
            var fill = Image("生命", root, Vector2.zero, Vector2.zero, new Color(.85f,.88f,.94f,.95f)); Stretch(fill.rectTransform);
            var icons = Rect("元素附着", root, new Vector2(0, 32), new Vector2(240, 45));
            var numberOutline=Text("",root,Vector2.zero,new Vector2(110,26),18); numberOutline.font=Assets.OutlineFont;numberOutline.fontSharedMaterial=Assets.OutlineMaterial; numberOutline.name="血量白描边";
            var numbers=Text("",root,Vector2.zero,new Vector2(110,26),18);
            numbers.color=new Color(.04f,.055f,.075f,1);
            numberOutline.gameObject.SetActive(false); numbers.gameObject.SetActive(false);
            return new Bar { Root = root, Fill = fill.rectTransform, Background=background.rectTransform, Icons = icons,Numbers=numbers,NumberOutline=numberOutline };
        }
        internal object Diagnostics()
        {
            var corners=new Vector3[4];playerIcons.GetWorldCorners(corners);
            for(int i=0;i<corners.Length;i++) corners[i]=RectTransformUtility.WorldToScreenPoint(hudCanvas.worldCamera,corners[i]);
            return new {screen=new {width=UnityEngine.Screen.width,height=UnityEngine.Screen.height},playerCorners=corners.Select(p=>new {p.x,p.y}).ToArray(),
                popups=popups.Where(p=>p.Text && p.Text.gameObject.activeSelf).Select(p=>new{p.Reaction,text=p.Text.text,size=p.Text.fontSize,outlineSize=p.Outline.fontSize,style=p.Text.fontStyle.ToString(),width=p.Text.rectTransform.rect.width}).ToArray(),
                bars=bars.Where(p=>p.Key).Select(p=>new {id=p.Key.GetInstanceID(),name=p.Key.name,visible=p.Value.Root.gameObject.activeInHierarchy,barVisible=p.Value.Fill.gameObject.activeInHierarchy,external=p.Value.External,x=p.Value.Root.anchoredPosition.x,y=p.Value.Root.anchoredPosition.y,boss=Runtime.Enemies.ContainsKey(p.Key)&&Runtime.Enemies[p.Key].Boss,width=p.Value.Root.rect.width,
                    icons=p.Value.Icons.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text).ToArray(),
                    statuses=p.Value.Icons.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>new {label=t.GetChild(1).GetComponent<TextMeshProUGUI>().text,stacks=t.GetChild(3).GetComponent<TextMeshProUGUI>().text}).ToArray()}).ToArray()};
        }
        internal static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private void StateFx(string key, string effect, Transform follow, bool player, HashSet<string> active)
        {
            string onset=effect=="frost-state"?"freeze":effect=="soft-frost-state"?"frost":"superconduct";
            foreach(var fx in effects) if(fx.Renderer.enabled && fx.Follow==follow && fx.Name==onset) return;
            active.Add(key);
            FxInstance existing;
            if (states.TryGetValue(key,out existing))
            {
                if (existing.Name==effect) return;
                existing.Renderer.enabled=false;states.Remove(key);
            }
            var instance = Play(effect, follow.position, player, follow); if (instance != null) states.Add(key, instance);
        }
        private void UpdateIcons(RectTransform container, TargetState enemy, PlayerState player,HealthManager health=null)
        {
            var entries = new List<IconEntry>(); double now = Runtime.Now; var config = Runtime.Config.Reactions;
            Element aura = enemy != null ? enemy.Aura : player.Aura;
            double until = enemy != null ? enemy.AuraUntil : player.AuraUntil;
            double auraDuration = enemy != null ? enemy.AuraDuration : player.AuraDuration;
            if (aura != Element.None && until > now) entries.Add(new IconEntry { Element = aura, Label = "", Fraction = 1, Blink = until-now < Math.Min(2, auraDuration*.25) });
            var ec = enemy != null ? enemy.ElectroCharged : player.ElectroCharged;
            if (ec != null) entries.Add(new IconEntry { Element = Element.Electro, Label = "感电", Fraction = (float)Math.Min(1, ((ec.RemainingTicks-1)*ec.Interval + Math.Max(0,ec.NextTick-now))/(ec.TotalTicks*ec.Interval)) });
            double freeze = enemy != null ? enemy.FrozenUntil : player.FrozenUntil;
            if (freeze > now) entries.Add(new IconEntry { Element = Element.Cryo, Label = enemy != null && !enemy.HardFrozen ? "结霜" : "冻结", Fraction = (float)((freeze-now)/Math.Max(.001,enemy != null ? enemy.FrozenDuration : player.FrozenDuration)) });
            double sc = enemy != null ? enemy.SuperconductUntil : player.SuperconductUntil;
            if (sc > now) entries.Add(new IconEntry { Element = Element.Electro, Label = "超导", Stacks=enemy?.SuperconductStacks ?? 0,Revision=sc,Emphasize=true, Fraction = (float)((sc-now)/Math.Max(.001,enemy != null ? enemy.SuperconductDuration : player.SuperconductDuration)) });
            if (player != null && player.OverloadUntil > now) entries.Add(new IconEntry { Element = Element.Pyro, Label = "超载", Fraction = (float)((player.OverloadUntil-now)/Math.Max(.001,player.OverloadDuration)) });
            if(player!=null && Runtime.ShadowDashDisabledUntil>now) entries.Add(new IconEntry {Element=Element.Cryo,Label="冲刺",Fraction=(float)((Runtime.ShadowDashDisabledUntil-now)/3)});
            if(health)
            {
                var hornet=health.GetComponent<Bosses.HornetProtectorBoss>();
                if(hornet && hornet.Reactions>0)
                {
                    entries.Add(new IconEntry {Element=Element.Pyro,Label="易伤",Stacks=hornet.Reactions,Fraction=1});
                    entries.Add(new IconEntry {Element=Element.Electro,Label="加速",Stacks=hornet.SpeedStacks,Fraction=1});
                }
                var dung=health.GetComponent<Bosses.DungDefenderBoss>();
                if(dung && dung.SplitStacks>0) entries.Add(new IconEntry {Element=Element.Pyro,Label="分裂",Stacks=dung.SplitStacks,Fraction=1});
                if(dung && dung.OverloadProgress>0) entries.Add(new IconEntry {Element=Element.Electro,Label="超载",Stacks=dung.OverloadProgress,Fraction=.5f});
                var watcher=health.GetComponent<Bosses.WatcherKnightsBoss>();
                if(watcher && watcher.VulnerableUntil>now) entries.Add(new IconEntry {Element=Element.Pyro,Label="易伤",Fraction=(float)((watcher.VulnerableUntil-now)/Runtime.Config.Reactions.WatcherSuperconductSeconds)});
                var vessel=health.GetComponent<Bosses.BrokenVesselBoss>();
                if(vessel && vessel.Empowered) entries.Add(new IconEntry {Element=Element.Pyro,Label="强化",Fraction=1});
            }
            if(enemy!=null && enemy.Charms.FireUnits>0 && enemy.Charms.FireUntil>now) entries.Add(new IconEntry {Element=Element.Pyro,Label="",Icon=CharmIcons.Get(CharmId.C12),Fraction=(float)((enemy.Charms.FireUntil-now)/Math.Max(.001,enemy.Charms.FireDuration))});
            if(enemy!=null && enemy.Charms.FragileUntil>now) entries.Add(new IconEntry {Element=Element.Cryo,Label="脆弱",Icon=CharmIcons.Get(CharmId.C27),Fraction=(float)((enemy.Charms.FragileUntil-now)/5)});
            if(enemy!=null && enemy.Charms.FrostCharges>0) entries.Add(new IconEntry {Element=Element.Cryo,Label="",Icon=CharmIcons.Get(CharmId.C28),Stacks=enemy.Charms.FrostCharges,Fraction=1});
            if(player!=null)
            {
                bool suppressed=Runtime.Charms.Suppressed(now);
                foreach(var item in entries) if(item.Label!="") item.Suppressed=suppressed;
                AddCharmIcons(entries,now);
            }
            while (container.childCount < entries.Count) CreateIcon(container);
            for (int i = 0; i < container.childCount; i++)
            {
                var child = (RectTransform)container.GetChild(i); child.gameObject.SetActive(i < entries.Count); if (i >= entries.Count) continue;
                var entry = entries[i]; child.anchoredPosition = new Vector2((i-(entries.Count-1)*.5f)*43, 0);
                var image = child.GetComponent<UnityEngine.UI.Image>(); image.sprite = entry.Icon ? entry.Icon : Assets.Icons[entry.Element.ToString().ToLowerInvariant()];
                image.color = new Color(1,1,1,entry.Suppressed ? .3f : entry.Blink ? .35f + .65f*Mathf.Abs(Mathf.Sin(Time.time*8)) : 1);
                var progress = child.GetChild(0).GetComponent<UnityEngine.UI.Image>(); progress.fillAmount = Mathf.Clamp01(entry.Fraction); progress.color = Chinese.Colors[(int)entry.Element];
                child.GetChild(1).GetComponent<TextMeshProUGUI>().text = entry.Label;
                var count=child.GetChild(3).GetComponent<TextMeshProUGUI>();var countOutline=child.GetChild(2).GetComponent<TextMeshProUGUI>();
                count.gameObject.SetActive(entry.Stacks>0);countOutline.gameObject.SetActive(entry.Stacks>0);
                count.text=countOutline.text=entry.Stacks.ToString();
                var pulse=child.GetComponent<StatusIconPulse>();string key=entry.Label+":"+entry.Stacks+":"+entry.Revision;
                if(pulse.Key!=key) {pulse.Key=key;pulse.ChangedAt=Time.unscaledTime;}
                float age=Time.unscaledTime-pulse.ChangedAt;
                child.localScale=Vector3.one*(entry.Emphasize && age<.3f ? 1+.2f*Mathf.Sin(age/.3f*Mathf.PI) : 1);
            }
        }
        private void CreateIcon(Transform parent)
        {
            var icon = Image("状态", parent, Vector2.zero, new Vector2(29,29), Color.white); icon.preserveAspect = true;
            var progress = Image("进度", icon.transform, Vector2.zero, new Vector2(37,37), Color.white); progress.sprite = ring;
            progress.type = UnityEngine.UI.Image.Type.Filled; progress.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; progress.fillOrigin = 2; progress.fillClockwise = false;
            Text("", icon.transform, new Vector2(0,-26), new Vector2(48,20), 15);
            var outline=Text("",icon.transform,new Vector2(9,-5),new Vector2(24,24),20);outline.font=Assets.OutlineFont;outline.fontSharedMaterial=Assets.OutlineMaterial;outline.name="层数白描边";
            var count=Text("",icon.transform,new Vector2(9,-5),new Vector2(24,24),20);count.color=new Color(.03f,.05f,.07f);count.name="破绽层数";
            icon.gameObject.AddComponent<StatusIconPulse>();
        }
        private Sprite Ring()
        {
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false);
            var colors = new Color[4096];
            for (int y=0;y<64;y++) for(int x=0;x<64;x++) { float r = new Vector2(x-31.5f,y-31.5f).magnitude; colors[y*64+x] = r>27 && r<30 ? Color.white : Color.clear; }
            texture.SetPixels(colors); texture.Apply(); return Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f));
        }
        private void OnDestroy()
        {
            if (Runtime != null) { Runtime.Feedback -= Feedback; Runtime.Pulse -= Pulse; Runtime.Cleared -= Clear; }
            if (ring) { Destroy(ring.texture); Destroy(ring); } CharmIcons.Dispose();Assets?.Dispose();
        }
        private void Clear()
        {
            foreach (var effect in effects) if (effect.Renderer) effect.Renderer.enabled = false;
            foreach (var popup in popups) if (popup.Text) { popup.Text.gameObject.SetActive(false); popup.Outline.gameObject.SetActive(false); }
            foreach (var bar in bars.Values) if (bar.Root) Destroy(bar.Root.gameObject);
            bars.Clear(); states.Clear();
            foreach(var spark in GetComponentsInChildren<CharmSparkMotion>()) Destroy(spark.gameObject);
        }
        private sealed class FxInstance { internal SpriteRenderer Renderer; internal FxAsset Asset; internal string Name; internal float Start,BodyScale=1,ExtraScale=1; internal Transform Follow; internal bool Player; }
        private sealed class Popup { internal TextMeshProUGUI Text,Outline; internal Vector3 World; internal float Start,Offset; internal bool Reaction; }
        private sealed class Bar { internal RectTransform Root, Fill, Background, Icons, Nodes; internal TextMeshProUGUI Numbers,NumberOutline; internal bool External; }
        private sealed class IconEntry { internal Element Element; internal string Label; internal Sprite Icon; internal float Fraction; internal bool Blink,Emphasize,Suppressed;internal int Stacks;internal double Revision; }
    }
    internal sealed class StatusIconPulse : MonoBehaviour {internal string Key;internal float ChangedAt;}
}
