using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using HollowElements.Rules;
using HollowElements.Settings;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace HollowElements.Game
{
    internal sealed class WorkshopMenus : MonoBehaviour
    {
        internal static bool CapturesInput;
        private CombatPresentation view;
        private CombatRuntime runtime;
        private HollowElementsMod mod;
        private RectTransform panel;
        private TextMeshProUGUI status;
        private TMP_InputField json;
        private bool open, capturedControl, oldCursorVisible;
        private CursorLockMode oldCursorLock;
        private string scene;
        private readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();
        private int selected;
        private Loadout embeddedLoadout;
        private PlayerData embeddedPlayer;
        private bool embeddedChanged;
        internal bool CanConfigureElements => HeroController.SilentInstance && !HeroController.SilentInstance.cState.dead &&
            (PlayerData.instance.atBench || mod.OnSaveGlobal().AllowAwayFromBench);
        private readonly List<KeyValuePair<PlayMakerFSM,bool>> pausedBenches = new List<KeyValuePair<PlayMakerFSM,bool>>();
        internal void Initialize(CombatPresentation presentation)
        {
            view = presentation; runtime = view.Runtime; mod=runtime.Mod;
            scene = UnitySceneManager.GetActiveScene().name;
            On.GameManager.PauseGameToggle += PauseToggle;
            On.GameManager.PauseGameToggleByMenu += PauseByMenu;
        }
        private void Update()
        {
            if (!runtime || !view) return;
            if (ElementGuideWindow.CapturesInput) return;
            var hero = HeroController.SilentInstance;
            if (scene != UnitySceneManager.GetActiveScene().name || open && (!hero || hero.cState.dead))
            { Close(); scene = UnitySceneManager.GetActiveScene().name; }
            KeyCode key;
            if (!Enum.TryParse(runtime.Mod.OnSaveGlobal().DebugKey, true, out key)) key = KeyCode.F8;
            if (runtime.Mod.OnSaveGlobal().DebugEnabled && Input.GetKeyDown(key) && hero) { if (open) Close(); else Open(true); }
            var actions = InputHandler.Instance ? InputHandler.Instance.inputActions : null;
            if (!open) return;
            if (Input.GetKeyDown(KeyCode.Escape) || actions != null && actions.menuCancel.WasPressed) { Close(); return; }
            if(actions!=null) Navigate(actions.down.WasPressed?1:actions.up.WasPressed?-1:0,actions.left.WasPressed?-1:actions.right.WasPressed?1:0,actions.menuSubmit.WasPressed,false);
        }
        internal void Navigate(int vertical,int horizontal,bool submit,bool cancel)
        {
            if(!open) return;
            if(cancel) { Close(); return; }
            if(json && json.isFocused || buttons.Count==0) return;
            if(vertical!=0) Select(selected+vertical);
            if(submit) buttons[selected].onClick.Invoke();
        }
        private void Open(bool isDebug)
        {
            if (!isDebug) return;
            if (open) return;
            open = true; buttons.Clear(); selected = 0;
            CapturesInput = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            oldCursorVisible = Cursor.visible; oldCursorLock = Cursor.lockState; Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            var hero = HeroController.SilentInstance;
            capturedControl = hero && !PlayerData.instance.atBench;
            if (capturedControl) hero.RelinquishControl();
            if (hero && PlayerData.instance.atBench)
                foreach(var fsm in FindObjectsOfType<PlayMakerFSM>())
                    if(Vector2.Distance(fsm.transform.position,hero.transform.position)<12 && (fsm.name.IndexOf("Bench",StringComparison.OrdinalIgnoreCase)>=0 || fsm.FsmName.IndexOf("Bench",StringComparison.OrdinalIgnoreCase)>=0))
                    { pausedBenches.Add(new KeyValuePair<PlayMakerFSM,bool>(fsm,fsm.Fsm.ManualUpdate)); fsm.Fsm.ManualUpdate=true; }
            const float height=850;
            panel = view.Rect("反应调试", view.MenuCanvasRoot, Vector2.zero, new Vector2(1540, height));
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.018f,.027f,.047f,.97f);
            view.Text("四元素 · 调试工坊", panel, new Vector2(0,height/2-60), new Vector2(1300,60), 38);
            status = view.Text("", panel, new Vector2(0,-height/2+60), new Vector2(1440,80), 20); status.enableWordWrapping = true;
            DebugPanel();
            Select(0);
        }
        internal bool ChangeFromElementGuide(AttackSlot slot,int step)
        {
            if(!CanConfigureElements || !PresentationAssets.IsUnlocked(slot)) return false;
            embeddedLoadout=mod.OnSaveLocal();embeddedPlayer=PlayerData.instance;
            embeddedLoadout.Set(slot,(Element)(((int)embeddedLoadout.Get(slot)+step+5)%5));embeddedChanged=true;return true;
        }
        internal void SaveEmbedded()
        {
            bool save=embeddedChanged && ReferenceEquals(embeddedPlayer,PlayerData.instance) && ReferenceEquals(embeddedLoadout,mod.OnSaveLocal());
            embeddedChanged=false;embeddedPlayer=null;embeddedLoadout=null;
            if(save) SaveLoadout();
        }
        private void SaveLoadout()
        {
            var hero=HeroController.SilentInstance;
            if(!GameManager.instance || !hero || hero.cState.dead) return;
            try {GameManager.instance.SaveGame(ok=>{if(!ok) mod.Log("元素配置已应用；将随下一次游戏存档保存。");});}
            catch(Exception e) {mod.LogError("元素配置自动保存失败："+e);}
        }
        private void DebugPanel()
        {
            view.Text("木桩仅在神居可用；受到真实伤害后才结算反应。", panel, new Vector2(-345,305), new Vector2(800,40), 20);
            Button("生成可击退木桩", new Vector2(-565,240), new Vector2(300,48), () => Spawn(true));
            Button("生成固定木桩", new Vector2(-235,240), new Vector2(300,48), () => Spawn(false));
            Button("重置全部木桩", new Vector2(-565,180), new Vector2(300,48), () => {
                foreach(var dummy in FindObjectsOfType<TrainingDummy>()) { dummy.Health.hp = 1000000; runtime.Track(dummy.Health).State = new TargetState(); } status.text = "木桩生命、附着和预算已重置。";
            });
            Button("移除全部木桩", new Vector2(-235,180), new Vector2(300,48), () => { foreach(var dummy in FindObjectsOfType<TrainingDummy>()) Destroy(dummy.gameObject); });
            view.Text("自身附着（不扣血）", panel, new Vector2(-400,112), new Vector2(650,40), 22);
            for(int i=1;i<=4;i++)
            {
                Element element = (Element)i;
                Button(Chinese.Elements[i],new Vector2(-650+(i-1)*165,60),new Vector2(145,46),() => {
                    runtime.Player.Aura = element; runtime.Player.AuraUnits = 1; runtime.Player.AuraUntil = runtime.Now+runtime.Config.Reactions.AuraSeconds;
                    runtime.Player.AuraDuration = runtime.Config.Reactions.AuraSeconds;
                    status.text = "已附着" + Chinese.Elements[(int)element] + "元素。";
                });
            }
            view.Text("模拟元素攻击（遵循无敌与护符减伤）", panel, new Vector2(-400,0), new Vector2(700,40), 22);
            for(int i=1;i<=4;i++)
            {
                Element element=(Element)i;
                Button(Chinese.Elements[i]+" · 1伤",new Vector2(-650+(i-1)*165,-55),new Vector2(145,46),() => Hurt(element,1));
                Button(Chinese.Elements[i]+" · 2伤",new Vector2(-650+(i-1)*165,-112),new Vector2(145,46),() => Hurt(element,2));
            }
            Button("无元素受伤",new Vector2(-565,-180),new Vector2(300,46),() => Hurt(Element.None,1));
            Button("清空自身附着 / 状态",new Vector2(-235,-180),new Vector2(300,46),() => { runtime.Player = new PlayerState(); status.text="自身状态已清空。"; });
            Button("回满生命与灵魂",new Vector2(-565,-242),new Vector2(300,46),() => { HeroController.SilentInstance.MaxHealth(); HeroController.SilentInstance.AddMPCharge(99); });
            Button("导出状态详情",new Vector2(-235,-242),new Vector2(300,46),Export);
            CreateJsonInput();
            Button("应用",new Vector2(120,-242),new Vector2(140,46),Apply);
            Button("保存",new Vector2(280,-242),new Vector2(140,46),() => { Apply(); runtime.Settings.Save(Path.Combine(runtime.Directory,"balance.json")); });
            Button("重置",new Vector2(440,-242),new Vector2(140,46),() => { json.text=SettingsStore.Serialize(RuleConfig.Defaults()); status.text="已填入默认值；应用后生效。"; });
            Button("重载",new Vector2(600,-242),new Vector2(140,46),() => { string message; if(runtime.Settings.TryReload(Path.Combine(runtime.Directory,"balance.json"),out message)) { runtime.RefreshRules(); json.text=SettingsStore.Serialize(runtime.Config); } status.text=message; });
            Button("关闭调试面板",new Vector2(0,-310),new Vector2(520,42),Close);
            status.text = "参数在开发阶段分招式配置；已有感电保留其创建时的参数。";
        }
        private void CreateJsonInput()
        {
            var root=view.Rect("参数JSON",panel,new Vector2(390,45),new Vector2(670,520));
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.045f,.06f,.085f,1);
            var viewport=view.Rect("裁剪区域",root,Vector2.zero,new Vector2(640,490)); viewport.gameObject.AddComponent<RectMask2D>();
            var text=view.Text("",viewport,Vector2.zero,new Vector2(640,490),17); text.alignment=TextAlignmentOptions.TopLeft; text.enableWordWrapping=true;
            json=root.gameObject.AddComponent<TMP_InputField>(); json.textViewport=viewport; json.textComponent=text;
            json.lineType=TMP_InputField.LineType.MultiLineNewline; json.characterLimit=1024*1024; json.richText=false;
            json.text=SettingsStore.Serialize(runtime.Config);
        }
        private void Apply()
        { string message; if(runtime.Settings.TryApply(json.text,out message)) runtime.RefreshRules(); status.text=message; }
        private void Spawn(bool recoilable)
        {
            if(!UnitySceneManager.GetActiveScene().name.StartsWith("GG_")) { status.text="木桩仅可在神居生成。"; return; }
            var dummy=TrainingDummy.Spawn(HeroController.SilentInstance.transform.position+new Vector3(4,0),recoilable); runtime.Track(dummy.Health); status.text="木桩已生成。";
        }
        private void Hurt(Element element,int amount)
        { status.text=runtime.HurtPlayer(element,amount) ? "已造成真实伤害。" : "本次被无敌或护盾阻止，状态未改变。"; }
        private void Export()
        {
            var targets=new List<object>();
            foreach(var record in runtime.Enemies.Values) if(record.Health) targets.Add(new { name=record.Health.name,hp=record.Health.hp,state=record.State });
            File.WriteAllText(Path.Combine(runtime.Directory,"debug-state.json"),JsonConvert.SerializeObject(new { time=runtime.Now,player=runtime.Player,targets,trace=runtime.Trace.ToArray() },Formatting.Indented));
            status.text="已写出状态、ICD 与施放预算：HollowElements/debug-state.json";
        }
        private UnityEngine.UI.Button Button(string title,Vector2 position,Vector2 size,Action click)
        {
            var rect=view.Rect(title,panel,position,size); var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=new Color(.08f,.11f,.16f,1);
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=image; button.navigation=new Navigation { mode=Navigation.Mode.None };
            view.Text(title,rect,Vector2.zero,size,23);
            button.onClick.AddListener(() => { try { click(); } catch(Exception e) { if(status) status.text="操作失败："+e.Message; runtime.Mod.LogError(e); } });
            buttons.Add(button); return button;
        }
        private void Select(int index)
        {
            if(buttons.Count==0) return; selected=(index+buttons.Count)%buttons.Count;
            for(int i=0;i<buttons.Count;i++) buttons[i].GetComponent<UnityEngine.UI.Image>().color=i==selected ? new Color(.19f,.29f,.38f,1) : new Color(.08f,.11f,.16f,1);
        }
        private void Close()
        {
            if(!open) return;
            if(panel) Destroy(panel.gameObject); panel=null; json=null; open=false; buttons.Clear();
            CapturesInput = false;
            foreach(var entry in pausedBenches) if(entry.Key) entry.Key.Fsm.ManualUpdate=entry.Value;
            pausedBenches.Clear();
            if(EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Cursor.visible=oldCursorVisible; Cursor.lockState=oldCursorLock;
            if(capturedControl && HeroController.SilentInstance && !HeroController.SilentInstance.cState.dead) HeroController.SilentInstance.RegainControl();
            capturedControl=false;
        }
        private void OnDestroy()
        {
            Close(); SaveEmbedded(); On.GameManager.PauseGameToggle -= PauseToggle; On.GameManager.PauseGameToggleByMenu -= PauseByMenu;
        }
        private IEnumerator PauseToggle(On.GameManager.orig_PauseGameToggle orig,GameManager manager)
        { if(open) { Close(); return Empty(); } return orig(manager); }
        private IEnumerator PauseByMenu(On.GameManager.orig_PauseGameToggleByMenu orig,GameManager manager)
        { if(open) { Close(); return Empty(); } return orig(manager); }
        private static IEnumerator Empty() { yield break; }
        private void LateUpdate()
        {
            if(!open || json && json.isFocused || !EventSystem.current) return;
            var selectedObject = EventSystem.current.currentSelectedGameObject;
            if(selectedObject && selectedObject.transform.IsChildOf(panel)) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
