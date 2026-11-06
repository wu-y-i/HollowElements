using System;
using System.Collections.Generic;
using System.IO;
using HollowElements.Settings;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace HollowElements.Game
{
    public sealed class HollowElementsMod : Mod, ITogglableMod, IGlobalSettings<GlobalSettings>, ILocalSettings<Loadout>, ICustomMenuMod
    {
        private GlobalSettings global = new GlobalSettings();
        private Loadout loadout = new Loadout();
        private GameObject root;
        private CombatRuntime runtime;
        private SettingsStore balanceSettings;
        private static readonly double[] DirectDamageMultipliers = { .35, .45, .5, .8, 1 };
        private static string BalancePath => Path.Combine(Application.persistentDataPath, "HollowElements", "balance.json");
        public HollowElementsMod() : base("HollowElements") { }
        public override string GetVersion() => "1.0";
        public void OnLoadGlobal(GlobalSettings value)
        {
            global=value ?? new GlobalSettings();
            if(!global.PopupScaleMigrated)
            {
                float legacy=0;
                foreach(var valueScale in new[]{global.DamagePopupScale,global.ReactionPopupScale})
                    if(valueScale.HasValue && !float.IsNaN(valueScale.Value) && !float.IsInfinity(valueScale.Value)) legacy=Mathf.Max(legacy,valueScale.Value);
                if(legacy>0) global.PopupScale=legacy;
                global.DamagePopupScale=global.ReactionPopupScale=null;global.PopupScaleMigrated=true;
            }
            if(float.IsNaN(global.PopupScale)||float.IsInfinity(global.PopupScale)) global.PopupScale=1.5f;
            global.PopupScale=Mathf.Clamp(global.PopupScale,1,2.5f);
            if(float.IsNaN(global.PlayerFxScale)||float.IsInfinity(global.PlayerFxScale)||global.PlayerFxScale<=0) global.PlayerFxScale=.65f;
            if(float.IsNaN(global.EnemyFxScale)||float.IsInfinity(global.EnemyFxScale)||global.EnemyFxScale<=0) global.EnemyFxScale=1;
            if(float.IsNaN(global.ReactionFxScale)||float.IsInfinity(global.ReactionFxScale)||global.ReactionFxScale<=0) global.ReactionFxScale=1.8f;
            global.ReactionFxScale=Mathf.Clamp(global.ReactionFxScale,.5f,4f);
            if(float.IsNaN(global.SoundVolume)||float.IsInfinity(global.SoundVolume)) global.SoundVolume=.55f;
            global.SoundVolume=Mathf.Clamp01(global.SoundVolume);
            if(float.IsNaN(global.AttackTintStrength)||float.IsInfinity(global.AttackTintStrength)) global.AttackTintStrength=.65f;
            global.AttackTintStrength=Mathf.Clamp01(global.AttackTintStrength);
        }
        public GlobalSettings OnSaveGlobal() => global;
        public void OnLoadLocal(Loadout value)
        {
            loadout=value ?? new Loadout();
            if(loadout.Charms==null) loadout.Charms=new Rules.CharmEquipment();loadout.Charms.Normalize();
            foreach(Rules.AttackSlot slot in Enum.GetValues(typeof(Rules.AttackSlot))) if(!Enum.IsDefined(typeof(Rules.Element),loadout.Get(slot))) loadout.Set(slot,Rules.Element.None);
            if(runtime) runtime.Clear();
        }
        public Loadout OnSaveLocal() => loadout;
        public Rules.IRuleModifier RuleModifier { get; set; }
        public bool ToggleButtonInsideMenu => true;
        public MenuScreen GetMenuScreen(MenuScreen parent,ModToggleDelegates? toggle) => SettingsMenu.Build(this,parent,toggle);
        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggle)
        {
            var list = new List<IMenuMod.MenuEntry>();
            if (toggle.HasValue) list.Add(toggle.Value);
            EnsureBalanceSettings();
            list.Add(new IMenuMod.MenuEntry { Name = "难度", Description = "默认普通；极限／困难额外降低反应附伤。", Values = new[] { "极限", "困难", "平衡", "普通", "简单" },
                Saver = SetDamageReduction, Loader = () => {
                    double multiplier = balanceSettings.Snapshot().DirectDamageMultiplier;
                    int nearest = 0;
                    for (int i = 1; i < DirectDamageMultipliers.Length; i++)
                        if (Math.Abs(DirectDamageMultipliers[i] - multiplier) < Math.Abs(DirectDamageMultipliers[nearest] - multiplier)) nearest = i;
                    return nearest;
                } });
            list.Add(new IMenuMod.MenuEntry { Name = "调试模式", Description = "测试木桩与参数工坊，默认 F8。", Values = new[] { "关闭", "开启" },
                Saver = i => global.DebugEnabled = i == 1, Loader = () => global.DebugEnabled ? 1 : 0 });
            list.Add(new IMenuMod.MenuEntry {Name="允许非长椅配置元素",Description="关闭时只能在长椅修改，图鉴仍可查看。",Values=new[]{"关闭","开启"},Saver=i=>global.AllowAwayFromBench=i==1,Loader=()=>global.AllowAwayFromBench?1:0});
            list.Add(new IMenuMod.MenuEntry {Name="Boss 机制提示",Description="出现时居中提示，随后缩小移至顶部。",Values=new[]{"关闭","开启"},Saver=i=>global.BossMechanicHints=i==1,Loader=()=>global.BossMechanicHints?1:0});
            var keys = new[] { "F8", "F6", "F7", "F9", "F10" };
            var element_guideKeys = new[] { "F5", "F2", "F3", "F11", "F12" };
            list.Add(new IMenuMod.MenuEntry { Name = "元素系统快捷键", Description = "默认 F5，也可从原版背包翻页进入。", Values = element_guideKeys, Saver = i => global.ElementGuideKey = element_guideKeys[i], Loader = () => Math.Max(0, Array.IndexOf(element_guideKeys, global.ElementGuideKey)) });
            list.Add(new IMenuMod.MenuEntry { Name = "调试面板按键", Values = keys, Saver = i => global.DebugKey = keys[i], Loader = () => Math.Max(0, Array.IndexOf(keys,global.DebugKey)) });
            list.Add(new IMenuMod.MenuEntry { Name="攻击元素染色",Description="按元素染色；关闭后恢复原色。",Values=new[]{"关闭","开启"},Saver=i=>global.TintAttacks=i==1,Loader=()=>global.TintAttacks?1:0 });
            list.Add(new IMenuMod.MenuEntry { Name="血条显示生命数值",Description="显示当前／最大生命；新安装默认开启。",Values=new[]{"关闭","开启"},Saver=i=>global.ShowHealthNumbers=i==1,Loader=()=>global.ShowHealthNumbers?1:0 });
            list.Add(new IMenuMod.MenuEntry { Name="显示本 Mod 血条",Description="关闭后保留状态图标，并适配 EnemyHPBar。",Values=new[]{"隐藏","显示"},Saver=i=>global.ShowHealthBars=i==1,Loader=()=>global.ShowHealthBars?1:0 });
            float[] scales={1f,1.5f,1.8f,2.2f,2.6f};
            float[] popupScales={1f,1.25f,1.5f,1.75f,2f,2.5f};
            string[] popupSizes={"100%","125%","150%（默认）","175%","200%","250%"};
            Func<float,int> popupIndex=value=>{int nearest=0;for(int i=1;i<popupScales.Length;i++)if(Mathf.Abs(popupScales[i]-value)<Mathf.Abs(popupScales[nearest]-value))nearest=i;return nearest;};
            list.Add(new IMenuMod.MenuEntry{Name="跳字大小",Description="伤害与反应跳字统一缩放，默认150%。",Values=popupSizes,Saver=i=>global.PopupScale=popupScales[i],Loader=()=>popupIndex(global.PopupScale)});
            list.Add(new IMenuMod.MenuEntry { Name="反应特效大小",Description="反应与持续特效统一缩放，默认180%。",Values=new[]{"100%","150%","180%（默认）","220%","260%"},Saver=i=>global.ReactionFxScale=scales[i],Loader=()=>{int nearest=0;for(int i=1;i<scales.Length;i++) if(Mathf.Abs(scales[i]-global.ReactionFxScale)<Mathf.Abs(scales[nearest]-global.ReactionFxScale)) nearest=i;return nearest;} });
            return list;
        }
        private void EnsureBalanceSettings()
        {
            if (balanceSettings != null) return;
            balanceSettings = new SettingsStore();
            ReloadBalanceSettings();
        }
        private void ReloadBalanceSettings()
        {
            if (File.Exists(BalancePath)) { string message; balanceSettings.TryReload(BalancePath, out message); Log(message); }
            else balanceSettings.Save(BalancePath);
        }
        private void SetDamageReduction(int index)
        {
            if (index < 0 || index >= DirectDamageMultipliers.Length) return;
            EnsureBalanceSettings();
            var previous = balanceSettings.Snapshot();
            var next = balanceSettings.Snapshot();
            next.DirectDamageMultiplier = DirectDamageMultipliers[index];
            string message;
            if (!balanceSettings.TryApply(SettingsStore.Serialize(next), out message)) { LogError(message); return; }
            try { balanceSettings.Save(BalancePath); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                balanceSettings.TryApply(SettingsStore.Serialize(previous), out message);
                LogError("减伤设置保存失败，保留原值：" + e.Message);
                return;
            }
            if (runtime) runtime.RefreshRules();
        }
        public override void Initialize()
        {
            if (root) return;
            if (balanceSettings == null) EnsureBalanceSettings(); else ReloadBalanceSettings();
            string directory = Path.Combine(Application.persistentDataPath, "HollowElements");
            System.IO.Directory.CreateDirectory(directory);
            root = new GameObject("HollowElements.Runtime"); UnityEngine.Object.DontDestroyOnLoad(root);
            runtime = root.AddComponent<CombatRuntime>(); runtime.Initialize(this, balanceSettings, directory);
            root.AddComponent<DevelopmentBridge>().Initialize(runtime);
            try { var presentation = root.AddComponent<CombatPresentation>(); presentation.Initialize(runtime); root.AddComponent<BossMechanicBanner>().Initialize(presentation); root.AddComponent<WorkshopMenus>().Initialize(presentation); root.AddComponent<ElementGuideWindow>().Initialize(presentation); }
            catch (Exception e) { LogError("资源加载失败：" + e); }
            UnitySceneManager.activeSceneChanged += SceneChanged;
            Log("四元素运行层已加载。");
        }
        private void SceneChanged(Scene previous, Scene next) { if (runtime) runtime.Clear(true); }
        public void Unload()
        {
            UnitySceneManager.activeSceneChanged -= SceneChanged;
            if (root) UnityEngine.Object.Destroy(root);
            root = null; runtime = null;
            Log("四元素已卸载。");
        }
    }
}
