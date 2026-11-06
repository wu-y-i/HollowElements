using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HollowElements.ElementGuide;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HollowElements.Game
{
    internal sealed partial class ElementGuideWindow : MonoBehaviour
    {
        private static readonly Color ReadingTextColor = new Color32(191, 195, 199, 255);
        internal static bool CapturesInput;
        internal ElementGuideInventoryPage Native;
        internal string Title => book?.Index?.Title ?? "元素系统";
        private CombatPresentation view;
        private ElementGuideBook book;
        private RectTransform panel, navContent, bodyContent;
        private UnityEngine.UI.ScrollRect nav, body;
        private TextMeshProUGUI footer;
        private UnityEngine.UI.Button configurationButton;
        private UnityEngine.UI.Button charmsButton;
        private readonly List<UnityEngine.UI.Button> configurationRows=new List<UnityEngine.UI.Button>();
        private readonly List<Rules.AttackSlot> configurationSlots=new List<Rules.AttackSlot>();
        private int configurationRow;
        private WorkshopMenus Workshop => view.GetComponent<WorkshopMenus>();
        private ElementGuideFonts fonts;
        private readonly List<UnityEngine.UI.Button> entries = new List<UnityEngine.UI.Button>();
        private readonly List<Texture2D> images = new List<Texture2D>();
        private readonly ElementGuideImageCache imageCache = new ElementGuideImageCache();
        private int renderedSelection = int.MinValue, builds, renders;
        private string renderedSource;
        private double openMs, renderMs;
        private bool invalidPictures;
        private int selected, screenWidth, screenHeight;
        private bool shown, focusedBody, pauseOwned, cursorVisible;
        private CursorLockMode cursorLock;
        private float previousTimeScale, nextAttach, nextRepeat;
        private string currentScene, lastError = "";
        private GameObject pointerEvents;
        private EventSystem borrowedEvents;
        private ElementGuidePointerModule pointerModule;
        private bool oldNavigation;
        private GameObject oldSelection;
        private readonly List<KeyValuePair<BaseInputModule,bool>> pausedModules=new List<KeyValuePair<BaseInputModule,bool>>();
        internal void Initialize(CombatPresentation presentation)
        {
            view = presentation;
            book = new ElementGuideBook(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ElementGuide"));
            try { book.Load(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException || e is ArgumentException) { lastError="图鉴读取失败："+e.Message; }
            fonts = view.Assets.SdfFonts;
            Native = new ElementGuideInventoryPage(this);
            StartCoroutine(WarmDirectory());
        }
        private System.Collections.IEnumerator WarmDirectory()
        {
            yield return null;
            string text = Title + "重新加载元素配置上下选择确认阅读返回关闭肩键翻页手柄梦钉左右调整自动保存请在长椅允许非长椅配置";
            if (book.Entries != null) text += string.Join("", book.Entries.Select(e => e.Title + e.Category)) + book.Entries[0].Source;
            text+="护符配置通用增幅剧变生存已装备空位长椅调整先卸下一枚"+string.Join("",Rules.CharmCatalog.All.Select(c=>c.Name+c.Description+Rules.CharmExplanation.For(c.Id)));
            var warm = fonts.Warm(text);
            while (true)
            {
                bool next;
                try { next = warm.MoveNext(); }
                catch (Exception e) { view.Runtime.Mod.LogError("图鉴字体预加载失败：" + e.Message); break; }
                if (!next) break;
                yield return warm.Current;
            }
            foreach(var charm in Rules.CharmCatalog.All) {view.CharmIcons.Get(charm.Id);yield return null;}
            // Build the fixed directory and first article during initial loading,
            // in separate frames. Hide them before either frame is presented.
            // Opening early remains safe: Show completes any unfinished work.
            if (!shown && !panel && book.Entries != null)
            {
                screenWidth = Screen.width; screenHeight = Screen.height;
                Build(); panel.gameObject.SetActive(false);
                yield return null;
                if (!shown && panel && renders == 0)
                {
                    panel.gameObject.SetActive(true); Render(); panel.gameObject.SetActive(false);
                }
            }
        }
        private void Update()
        {
            if (view == null || Native == null) return;
            if (!Native.Inventory && Time.unscaledTime >= nextAttach)
            {
                nextAttach = Time.unscaledTime + 1;
                var inventory = Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Inventory Control" && f.gameObject.scene.IsValid() && f.Fsm.Initialized);
                if (inventory)
                {
                    try { Native.Dispose(); Native.Attach(inventory); }
                    catch (Exception e) { Native.Dispose(); view.Runtime.Mod.LogError("图鉴背包页接入失败：" + e); nextAttach += 10; }
                }
            }
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if(shown && scene!=currentScene) {Native.Close();Hide();currentScene=scene;return;}
            bool visible = Native.Visible && scene == currentScene;
            if (Native.Visible && !shown) { currentScene = scene; Show(); visible = true; }
            if (shown && !visible) Hide();
            KeyCode key;
            if (!Enum.TryParse(view.Runtime.Mod.OnSaveGlobal().ElementGuideKey, true, out key)) key = KeyCode.F5;
            if (Input.GetKeyDown(key)) { if (shown) Native.Close(); else Native.Open(); }
            if (!shown) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height) { screenWidth = Screen.width; screenHeight = Screen.height; Resize(); Render(); }
            var actions = InputHandler.Instance.inputActions;
            if (actions.paneLeft.WasPressed || actions.paneRight.WasPressed || actions.openInventory.WasPressed) ReleasePause();
            if (Input.GetKeyDown(KeyCode.F4) && view.Runtime.Mod.OnSaveGlobal().DebugEnabled) Reload();
            bool configurationShortcut=Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)
                || actions.dreamNail.WasPressed && actions.LastInputType==InControl.BindingSourceType.DeviceBindingSource;
            if(configurationShortcut) {selected=-1;focusedBody=true;Render();}
            int direction = actions.up.WasPressed ? -1 : actions.down.WasPressed ? 1 : 0;
            if (!configurationShortcut && focusedBody)
            {
                if(selected==-2)
                {
                    NavigateCharms(direction,actions.left.WasPressed?-1:actions.right.WasPressed?1:0,actions.menuSubmit.WasPressed);
                }
                else if(selected==-1)
                {
                    if(direction!=0 && configurationRows.Count>0) {configurationRow=(configurationRow+direction+configurationRows.Count)%configurationRows.Count;HighlightConfiguration();}
                    if(actions.left.WasPressed) ChangeConfiguration(-1);
                    if(actions.right.WasPressed || actions.menuSubmit.WasPressed) ChangeConfiguration(1);
                }
                else {float axis = actions.down.IsPressed ? 1 : actions.up.IsPressed ? -1 : -actions.rightStick.Y;Scroll(axis * Time.unscaledDeltaTime * 480);}
            }
            else if (!configurationShortcut)
            {
                if (direction != 0) { Navigate(direction, false); nextRepeat = Time.unscaledTime + .35f; }
                else if (Time.unscaledTime > nextRepeat && (actions.up.IsPressed || actions.down.IsPressed)) { Navigate(actions.up.IsPressed ? -1 : 1, false); nextRepeat = Time.unscaledTime + .1f; }
                if (actions.menuSubmit.WasPressed || actions.right.WasPressed) Navigate(0, true);
            }
            if (!configurationShortcut && actions.left.WasPressed && selected>=0) ReturnToIndex();
            // Native tween/close states still run on scaled time. Release our pause
            // as soon as the native controller leaves its stable Opened state.
            if (Native.Visible && !pauseOwned) { previousTimeScale = Time.timeScale; Time.timeScale = 0; pauseOwned = true; }
        }
        private void Show()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            shown = true; CapturesInput = true; focusedBody = false;
            if(!EventSystem.current)
            {
                pointerEvents=new GameObject("HollowElements.ElementGuidePointer",typeof(EventSystem),typeof(ElementGuidePointerModule));
                pointerEvents.transform.SetParent(transform,false);pointerEvents.GetComponent<EventSystem>().sendNavigationEvents=false;
            }
            else
            {
                borrowedEvents=EventSystem.current;oldNavigation=borrowedEvents.sendNavigationEvents;oldSelection=borrowedEvents.currentSelectedGameObject;
                foreach(var module in borrowedEvents.GetComponents<BaseInputModule>()) {pausedModules.Add(new KeyValuePair<BaseInputModule,bool>(module,module.enabled));module.enabled=false;}
                borrowedEvents.sendNavigationEvents=false;borrowedEvents.SetSelectedGameObject(null);pointerModule=borrowedEvents.gameObject.AddComponent<ElementGuidePointerModule>();
            }
            cursorVisible = Cursor.visible; cursorLock = Cursor.lockState;
            Modding.ModHooks.CursorHook += ShowCursor;
            ShowCursor();
            bool resized = screenWidth != Screen.width || screenHeight != Screen.height;
            screenWidth = Screen.width; screenHeight = Screen.height;
            bool changed = book.HasChanges() && ReadBook();
            int boss=book.EntryForScene(currentScene);
            if(boss>=0) selected=boss;
            bool rebuild = !panel || changed;
            if (rebuild) Build(); else { panel.gameObject.SetActive(true); if (resized) Resize(); }
            panel.SetAsLastSibling();
            if (rebuild || resized || book.Entries == null || selected < 0 || selected != renderedSelection || renderedSource != Bind(book.Entries[selected].Source) || invalidPictures || imageCache.Changed(images)) Render();
            else { body.verticalNormalizedPosition = 1; body.velocity = Vector2.zero; SetFooter(); }
            Native.RefreshTitle();
            openMs = watch.Elapsed.TotalMilliseconds;
        }
        private bool ReadBook()
        {
            string id = book.Entries != null && selected>=0 && selected < book.Entries.Count ? book.Entries[selected].Id : null;
            try { book.Load(); lastError = ""; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException || e is ArgumentException)
            { lastError = "图鉴读取失败：" + e.Message; view.Runtime.Mod.Log(lastError); if (panel) { SetFooter(); return false; } }
            if (book.Entries != null && id != null) selected = Math.Max(0, book.Entries.FindIndex(e => e.Id == id));
            return lastError.Length == 0;
        }
        internal bool Reload()
        {
            bool success = ReadBook();
            if (!success) { SetFooter(); return false; }
            // Explicit author reload also invalidates same-name picture replacements.
            imageCache.Dispose(); images.Clear();
            if (shown) { Build(); Render(); Native.RefreshTitle(); }
            else if (panel) { Destroy(panel.gameObject); panel = null; }
            return true;
        }
        private void Build()
        {
            builds++;
            if (panel) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            entries.Clear();
            panel = view.Rect("HollowElements.元素系统", view.MenuCanvasRoot, Vector2.zero, Vector2.zero);
            panel.anchorMin = new Vector2(.06f, .13f); panel.anchorMax = new Vector2(.94f, .84f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .033f, .047f, .985f);
            var edge = panel.gameObject.AddComponent<UnityEngine.UI.Outline>(); edge.effectColor = new Color(.55f, .58f, .6f, .8f); edge.effectDistance = new Vector2(1, -1);
            var title = Label(Title, panel, 28, new Color(.88f, .83f, .68f)); Pin(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -52), new Vector2(-24, -8));
            footer = Label("", panel, 18, new Color(.7f, .73f, .76f)); Pin(footer.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(22, 6), new Vector2(-170, 40));
            var reload = MakeButton("重新加载", panel, ReloadAction); Pin((RectTransform)reload.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-154, 7), new Vector2(-14, 42));
            nav = Scroller("目录", out navContent); body = Scroller("正文", out bodyContent);
            Resize();
            configurationButton=MakeButton("元素配置",navContent,()=>{selected=-1;focusedBody=true;Render();});
            configurationButton.gameObject.AddComponent<LayoutElement>().preferredHeight=48;
            charmsButton=MakeButton("护符配置",navContent,()=>{selected=-2;focusedBody=true;Render();});
            charmsButton.gameObject.AddComponent<LayoutElement>().preferredHeight=48;
            if (book.Entries == null) return;
            string category = null;
            for (int i = 0; i < book.Entries.Count; i++)
            {
                var entry = book.Entries[i]; int index = i;
                if (entry.Category != category) { category = entry.Category; var text = Label(category, navContent, 19, new Color(.75f, .7f, .52f)); text.gameObject.AddComponent<LayoutElement>().preferredHeight = 35; }
                var button = MakeButton(entry.Title, navContent, () => { selected = index; focusedBody = true; Render(); });
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
                entries.Add(button);
            }
            MeasureSidebar();
            SetFooter();
        }
        private void ReloadAction() { Reload(); }
        private void Resize()
        {
            if (panel)
            {
                Rect safe=Screen.safeArea;
                if(Camera.main) {var viewport=Camera.main.pixelRect;safe=Rect.MinMaxRect(Mathf.Max(safe.xMin,viewport.xMin),Mathf.Max(safe.yMin,viewport.yMin),Mathf.Min(safe.xMax,viewport.xMax),Mathf.Min(safe.yMax,viewport.yMax));}
                // The native inventory frame is authored for 16:9 even when the
                // camera reports a full-screen pixelRect on a 4:3 monitor.
                const float frameAspect=16f/9;
                if(safe.width/safe.height<frameAspect) {float height=safe.width/frameAspect;safe.y+=(safe.height-height)/2;safe.height=height;}
                else {float width=safe.height*frameAspect;safe.x+=(safe.width-width)/2;safe.width=width;}
                panel.anchorMin=new Vector2((safe.xMin+safe.width*.06f)/Screen.width,(safe.yMin+safe.height*.13f)/Screen.height);
                panel.anchorMax=new Vector2((safe.xMin+safe.width*.94f)/Screen.width,(safe.yMin+safe.height*.84f)/Screen.height);
                panel.offsetMin=panel.offsetMax=Vector2.zero;
            }
            Canvas.ForceUpdateCanvases();
            if (!panel || !nav || !body) return;
            float sidebar = Mathf.Min(book.Index?.Layout.SidebarWidth ?? 290, panel.rect.width * .32f);
            Pin((RectTransform)nav.transform, Vector2.zero, new Vector2(0, 1), new Vector2(16, 50), new Vector2(sidebar + 16, -62));
            Pin((RectTransform)body.transform, Vector2.zero, Vector2.one, new Vector2(sidebar + 38, 50), new Vector2(-20, -62));
            Canvas.ForceUpdateCanvases();
            MeasureSidebar();
        }
        private void MeasureSidebar()
        {
            if(!nav) return;
            foreach(var button in entries)
            {
                var label=button.GetComponentInChildren<TextMeshProUGUI>();
                float width=Mathf.Max(60,nav.viewport.rect.width-44);
                button.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(46,label.GetPreferredValues(label.text,width,0).y+12);
            }
        }
        private UnityEngine.UI.ScrollRect Scroller(string name, out RectTransform content)
        {
            var root = view.Rect(name, panel, Vector2.zero, Vector2.zero); var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.inertia = false; scroll.scrollSensitivity = 32;
            var viewport = view.Rect("Viewport", root, Vector2.zero, Vector2.zero); Pin(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var maskImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); maskImage.color = new Color(1, 1, 1, .01f); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = view.Rect("Content", viewport, Vector2.zero, Vector2.zero); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 8; layout.padding = new RectOffset(8, 12, 6, 20);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return scroll;
        }
        private void Render()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                RenderBody();
                renderedSelection = selected;
                renderedSource = selected >= 0 && book.Entries != null ? Bind(book.Entries[selected].Source) : null;
            }
            finally { renderMs = watch.Elapsed.TotalMilliseconds; renders++; imageCache.Trim(images); }
        }
        private void RenderBody()
        {
            if (!bodyContent) return;
            invalidPictures = false;
            foreach (Transform child in bodyContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            images.Clear();
            configurationRows.Clear();configurationSlots.Clear();
            if (book.Entries == null) { Label(lastError, bodyContent, 24, new Color(1, .65f, .55f)); SetFooter(); return; }
            if(selected==-2)
            {
                RenderCharms();
                foreach(var button in entries) button.targetGraphic.color=new Color(.065f,.082f,.1f);
                configurationButton.targetGraphic.color=new Color(.065f,.082f,.1f);charmsButton.targetGraphic.color=new Color(.2f,.26f,.32f);
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);body.verticalNormalizedPosition=1;nav.verticalNormalizedPosition=1;SetFooter();return;
            }
            charmsButton.targetGraphic.color=new Color(.065f,.082f,.1f);
            if(selected==-1)
            {
                RenderConfiguration();
                for(int i=0;i<entries.Count;i++) entries[i].targetGraphic.color=new Color(.065f,.082f,.1f);
                configurationButton.targetGraphic.color=new Color(.2f,.26f,.32f);
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
                HighlightConfiguration();nav.verticalNormalizedPosition=1;SetFooter();return;
            }
            configurationButton.targetGraphic.color=new Color(.065f,.082f,.1f);
            selected = Mathf.Clamp(selected, 0, book.Entries.Count - 1);
            var entry = book.Entries[selected]; var layout = ElementGuideBook.LayoutFor(book.Index.Layout, entry.Layout);
            bodyContent.GetComponent<VerticalLayoutGroup>().spacing = layout.Spacing;
            foreach (var block in ElementGuideBook.Parse(Bind(entry.Source)))
            {
                if (block.Kind == "image") { Picture(entry, block, layout); continue; }
                if (block.Kind == "rule") { var line = view.Rect("分隔线", bodyContent, Vector2.zero, new Vector2(1, 1)); line.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.4f, .43f, .48f); line.gameObject.AddComponent<LayoutElement>().preferredHeight = 1; continue; }
                if (block.Kind == "table") { Table(block, layout.FontSize); continue; }
                int size = block.Kind == "heading" ? layout.FontSize + (block.Level == 1 ? 10 : block.Level == 2 ? 4 : 0) : layout.FontSize;
                var color = block.Kind == "heading" ? new Color(.9f, .85f, .71f) : block.Kind == "note" ? new Color(.64f, .79f, .88f) : ReadingTextColor;
                Label(ElementGuideBook.Inline(block.Text), bodyContent, size, color);
            }
            for (int i = 0; i < entries.Count; i++) entries[i].targetGraphic.color = i == selected ? new Color(.2f, .26f, .32f) : new Color(.065f, .082f, .1f);
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent); LayoutRebuilder.ForceRebuildLayoutImmediate(navContent);
            body.verticalNormalizedPosition = 1; body.velocity = Vector2.zero;
            if (selected < entries.Count)
            {
                float y = -((RectTransform)entries[selected].transform).anchoredPosition.y;
                float max = Mathf.Max(0, navContent.rect.height - nav.viewport.rect.height);
                navContent.anchoredPosition = new Vector2(0, Mathf.Clamp(y - nav.viewport.rect.height * .35f, 0, max));
            }
            SetFooter();
        }
        private void Picture(ElementGuideEntry entry, ElementGuideBlock block, ElementGuideLayout layout)
        {
            try
            {
                string relative = Path.Combine(Path.GetDirectoryName(entry.File) ?? "", block.Path); string path = book.Resolve(relative);
                var texture = imageCache.Get(path); images.Add(texture);
                var rect = view.Rect("插图", bodyContent, Vector2.zero, Vector2.zero);
                float height = Mathf.Min(layout.ImageMaxHeight, Mathf.Max(40, body.viewport.rect.width - 28) * texture.height / texture.width);
                // Keep the whole texture visible inside the allocated image block.
                rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
                var child = view.Rect("等比图片", rect, Vector2.zero, Vector2.zero); var image = child.gameObject.AddComponent<UnityEngine.UI.RawImage>(); image.texture = texture; image.raycastTarget = false;
                Pin(child, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-height * texture.width / texture.height / 2, -height / 2), new Vector2(height * texture.width / texture.height / 2, height / 2));
                if (!string.IsNullOrWhiteSpace(block.Text)) Label(ElementGuideBook.Inline(block.Text), bodyContent, 18, new Color(.6f, .67f, .72f));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { invalidPictures = true; Label("[图片未加载] " + block.Path + "：" + e.Message, bodyContent, 20, new Color(1, .65f, .55f)); }
        }
        private void Table(ElementGuideBlock block, int size)
        {
            int count = block.Rows.Max(r => r.Length); float width = Mathf.Max(100, body.viewport.rect.width - 44) / count;
            for (int row = 0; row < block.Rows.Count; row++)
            {
                var line = view.Rect("表格行", bodyContent, Vector2.zero, Vector2.zero); var group = line.gameObject.AddComponent<HorizontalLayoutGroup>(); group.childControlWidth = group.childControlHeight = true; group.childForceExpandHeight = false; group.spacing = 10;
                float height = 0;
                for (int col = 0; col < count; col++)
                {
                    string cell = col < block.Rows[row].Length ? block.Rows[row][col] : "";
                    var label = Label(ElementGuideBook.Inline(cell), line, size - 2, row == 0 ? new Color(.9f, .85f, .71f) : ReadingTextColor);
                    label.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
                    height = Mathf.Max(height, label.GetPreferredValues(label.text,width,0).y);
                }
                line.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(30, height + 6);
            }
        }
        private TextMeshProUGUI Label(string value, Transform parent, int size, Color color)
        {
            fonts.Ensure(value);
            var rect = view.Rect("图鉴文字", parent, Vector2.zero, new Vector2(300, 30)); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fonts.Primary; text.fontSize = size; text.text = value; text.color = color; text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing=false; text.enableWordWrapping=true; text.richText=true; text.raycastTarget = false;
            return text;
        }
        private void RenderConfiguration()
        {
            Label("元素配置",bodyContent,30,new Color(.9f,.85f,.71f));
            Label(Workshop.CanConfigureElements ? "左右调整 / 确认切换；关闭图鉴时自动保存。" : "请在长椅配置；可在 Mod 菜单开启“允许非长椅配置元素”。",bodyContent,22,ReadingTextColor);
            var icons=view.Assets.LoadoutIcons();var swords=view.Assets.SwordArtIcons();
            for(int i=0;i<7;i++)
            {
                var slot=(Rules.AttackSlot)i;if(!PresentationAssets.IsUnlocked(slot)) continue;
                int row=configurationRows.Count;
                var element=view.Runtime.Mod.OnSaveLocal().Get(slot);
                var button=MakeButton("‹ "+Chinese.Elements[(int)element]+" ›",bodyContent,()=>{configurationRow=row;focusedBody=true;ChangeConfiguration(1);});
                button.gameObject.AddComponent<LayoutElement>().preferredHeight=64;
                var label=button.GetComponentInChildren<TextMeshProUGUI>();label.color=Workshop.CanConfigureElements?Chinese.Colors[(int)element]:ReadingTextColor;
                Pin(label.rectTransform,new Vector2(.5f,0),Vector2.one,new Vector2(8,0),new Vector2(-8,0));
                if(i>=4 && swords[i-4]!=null)
                {
                    var rect=view.Rect("剑技图标",button.transform,Vector2.zero,Vector2.zero);Pin(rect,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(30,-23),new Vector2(164,23));
                    var graphic=rect.gameObject.AddComponent<NativeSlashGraphic>();graphic.Definition=swords[i-4];graphic.raycastTarget=false;graphic.SetAllDirty();
                }
                else
                {
                    bool any=false;for(int n=0;n<icons[i].Length;n++) if(icons[i][n])
                    {
                        var image=view.Image("招式图标",button.transform,Vector2.zero,new Vector2(78,48),Color.white);image.sprite=icons[i][n];image.preserveAspect=true;image.raycastTarget=false;
                        image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(0,.5f);image.rectTransform.anchoredPosition=new Vector2(60+n*78,0);
                        if(i==0) {image.rectTransform.sizeDelta=new Vector2(90,90);image.rectTransform.localEulerAngles=new Vector3(0,0,-62);}any=true;
                    }
                    if(!any) {var name=Label(Chinese.Slots[i],button.transform,21,ReadingTextColor);Pin(name.rectTransform,Vector2.zero,new Vector2(.5f,1),new Vector2(16,0),new Vector2(-8,0));}
                }
                configurationRows.Add(button);configurationSlots.Add(slot);
            }
            configurationRow=Mathf.Clamp(configurationRow,0,Mathf.Max(0,configurationRows.Count-1));HighlightConfiguration();
        }
        private void HighlightConfiguration()
        {
            for(int i=0;i<configurationRows.Count;i++) configurationRows[i].targetGraphic.color=i==configurationRow?new Color(.2f,.26f,.32f):new Color(.065f,.082f,.1f);
            if(configurationRow<configurationRows.Count)
            {
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
                float y=-((RectTransform)configurationRows[configurationRow].transform).anchoredPosition.y;
                float max=Mathf.Max(0,bodyContent.rect.height-body.viewport.rect.height);
                bodyContent.anchoredPosition=new Vector2(0,Mathf.Clamp(y-body.viewport.rect.height*.5f,0,max));
            }
        }
        internal void ChangeConfiguration(int step)
        {
            if(configurationRow>=configurationSlots.Count) return;
            if(Workshop.ChangeFromElementGuide(configurationSlots[configurationRow],step)) Render();
            SetFooter();
        }
        private UnityEngine.UI.Button MakeButton(string title, Transform parent, Action action)
        {
            var rect = view.Rect(title, parent, Vector2.zero, Vector2.zero); var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.065f, .082f, .1f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = background; button.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = Label(title, rect, 21, ReadingTextColor); text.alignment = TextAlignmentOptions.Left; Pin(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-8, 0));
            button.onClick.AddListener(() => action()); return button;
        }
        private static void Pin(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
        private string Bind(string source)
        {
            return Regex.Replace(source, @"\{\{([A-Za-z0-9_.]+)\}\}", match => {
                string key = match.Groups[1].Value; var config = view.Runtime.Config;
                if (key == "DirectDamageReduction") return ((1 - config.DirectDamageMultiplier) * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                if (key == "NailDamage") return (PlayerData.instance?.nailDamage ?? 5).ToString(CultureInfo.InvariantCulture);
                if (key == "PlayerElectroSeconds") return (config.Reactions.PlayerElectroInterval * config.Reactions.PlayerElectroTicks).ToString("0.##", CultureInfo.InvariantCulture);
                object target = config; string fieldName = key;
                if (key.StartsWith("Reactions.", StringComparison.Ordinal)) { target = config.Reactions; fieldName = key.Substring(10); }
                if (key.StartsWith("Attacks.", StringComparison.Ordinal))
                {
                    string[] parts=key.Split('.'); Rules.AttackSource attack;
                    if(parts.Length!=3 || !Enum.TryParse(parts[1],out attack) || !config.Attacks.ContainsKey(attack)) return "[未知参数："+key+"]";
                    target=config.Attacks[attack];fieldName=parts[2];
                }
                var field = target.GetType().GetField(fieldName);
                var property=field==null?target.GetType().GetProperty(fieldName):null;
                var type=field?.FieldType ?? property?.PropertyType;
                if(type!=typeof(double) && type!=typeof(int) || field==null && (property==null || !property.CanRead || property.GetIndexParameters().Length!=0)) return "[未知参数：" + key + "]";
                return Convert.ToDouble(field!=null?field.GetValue(target):property.GetValue(target,null), CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture);
            });
        }
        internal void Navigate(int direction, bool submit)
        {
            if (!shown || book.Entries == null) return;
            if (direction != 0) { focusedBody = false; int count=book.Entries.Count+2;int position=selected==-1?0:selected==-2?1:selected+2;position=((position+direction)%count+count)%count;selected=position==0?-1:position==1?-2:position-2;Render(); }
            if (submit) { focusedBody = true; SetFooter(); }
        }
        internal bool ReturnToIndex() { if (!shown || !focusedBody) return false; focusedBody = false; SetFooter(); return true; }
        internal void Scroll(float pixels)
        { if (body) body.content.anchoredPosition = new Vector2(0, Mathf.Clamp(body.content.anchoredPosition.y + pixels, 0, Mathf.Max(0, body.content.rect.height - body.viewport.rect.height))); }
        private void SetFooter()
        {
            if (!footer) return;
            if(selected==-2) {footer.text=lastError.Length>0?lastError:!view.Runtime.CanConfigureCharms?"请在长椅配置护符 · 可自由查看":focusedBody?"方向键选择 · 确认装备 / 卸下 · 返回目录 · 关闭自动保存":"确认查看护符 · 返回关闭 · 肩键翻页";return;}
            footer.text = lastError.Length > 0 ? lastError : selected<0 && focusedBody ? (Workshop.CanConfigureElements ? "上下选择 · 左右调整 · 返回目录 · 关闭自动保存" : "请在长椅配置 · 返回目录") : focusedBody ? "上下 / 右摇杆滚动 · 返回目录 · 肩键翻页" : "上下选择 · 确认阅读 · 返回关闭 · 肩键翻页";
            if(lastError.Length==0 && selected>=0) footer.text+=" · 1 / 手柄梦钉：元素配置";
        }
        internal void ReleasePause() { if (pauseOwned) { Time.timeScale = previousTimeScale; pauseOwned = false; } }
        private void Hide()
        {
            ReleasePause(); if (!shown) return; shown = false; CapturesInput = false;
            Workshop?.SaveEmbedded();
            view.Runtime.SaveCharms();
            if (panel) panel.gameObject.SetActive(false);
            if(pointerEvents) Destroy(pointerEvents);pointerEvents=null;
            if(pointerModule) {pointerModule.enabled=false;Destroy(pointerModule);}pointerModule=null;
            foreach(var pair in pausedModules) if(pair.Key) pair.Key.enabled=pair.Value;pausedModules.Clear();
            if(borrowedEvents) {borrowedEvents.sendNavigationEvents=oldNavigation;if(oldSelection && oldSelection.activeInHierarchy) borrowedEvents.SetSelectedGameObject(oldSelection);}
            borrowedEvents=null;oldSelection=null;
            Modding.ModHooks.CursorHook -= ShowCursor;
            Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
        }
        internal object Diagnostics() => new { attached = Native?.Inventory != null, page = Native?.PageNumber, visible = shown, focusedBody, pauseOwned, timeScale = Time.timeScale,
            canOpen = HeroController.SilentInstance && HeroController.SilentInstance.CanOpenInventory(), onGround = HeroController.SilentInstance && HeroController.SilentInstance.cState.onGround,
            eventSystem=EventSystem.current?EventSystem.current.name:null,mouse=Input.mousePosition.ToString(),cursor=Cursor.lockState.ToString(),
            cursorVisible=Cursor.visible,
            disablePause = PlayerData.instance != null && PlayerData.instance.disablePause, capturesInput=CapturesInput,
            title = Title, entries = book?.Entries?.Count ?? 0, selected = selected==-2 ? "charms" : selected==-1 ? "loadout" : book?.Entries != null ? book.Entries[selected].Id : null, error = lastError,
            performance = new { openMs, renderMs, builds, renders, fontPages = fonts?.LoadedPages, cachedImages = imageCache.Count, cachedImageBytes = imageCache.Bytes, imageDecodes = imageCache.Decodes },
            bodyHeight = bodyContent ? bodyContent.rect.height : 0, viewportHeight = body ? body.viewport.rect.height : 0, scroll = bodyContent ? bodyContent.anchoredPosition.y : 0,
            panelWidth = panel ? panel.rect.width : 0, font = fonts?.Primary?.name, renderer="TextMeshProUGUI", text = bodyContent ? bodyContent.GetComponentsInChildren<TextMeshProUGUI>().Select(t => t.text).ToArray() : null,
            missingCharacters=bodyContent ? bodyContent.GetComponentsInChildren<TextMeshProUGUI>().SelectMany(t=>t.text).Distinct().Where(c=>!char.IsWhiteSpace(c) && !fonts.Supports(c)).Select(c=>c.ToString()).ToArray() : null,
            images = images.Count, nativeState = Native?.Inventory ? Native.Inventory.ActiveStateName : null };
        private void OnDestroy() { StopAllCoroutines(); Hide(); if(panel) Destroy(panel.gameObject); Native?.Dispose(); imageCache.Dispose(); CapturesInput = false; }
        private void LateUpdate()
        {
            if(shown && panel && panel.GetSiblingIndex()!=panel.parent.childCount-1) panel.SetAsLastSibling();
            if(shown && EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(panel))
                EventSystem.current.SetSelectedGameObject(null);
        }
        private void ShowCursor()
        { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
    }
    // Mouse events only: native InControl continues to own keyboard/controller
    // navigation, without requiring legacy Unity Input Manager axes.
    internal sealed class ElementGuidePointerModule : StandaloneInputModule
    {
        public override bool ShouldActivateModule() => IsActive();
    }
}
