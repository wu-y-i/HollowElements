using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HollowElements.Settings;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySceneManager=UnityEngine.SceneManagement.SceneManager;
namespace HollowElements.Game
{
    internal sealed class BossMechanicBanner : MonoBehaviour
    {
        private CombatPresentation view;
        private BossHintEntry entry;
        private RectTransform root;
        private CanvasGroup group;
        private TextMeshProUGUI nameLabel;
        private Material textMaterial;
        private int sceneHandle=int.MinValue;
        private bool shown;
        private float age,readyAge;
        internal void Initialize(CombatPresentation presentation) {view=presentation;}
        private void ReadScene()
        {
            if(root) Destroy(root.gameObject);root=null;group=null;if(textMaterial) Destroy(textMaterial);textMaterial=null;shown=false;age=readyAge=0;entry=null;
            var scene=UnitySceneManager.GetActiveScene();sceneHandle=scene.handle;
            string path=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"BossHints.json");
            try {if(!File.Exists(path))return;var config=JsonConvert.DeserializeObject<BossHintSettings>(File.ReadAllText(path));config.Validate();entry=config.ForScene(scene.name);}
            catch(Exception e) {view.Runtime.Mod.LogError("Boss 机制提示读取失败："+e.Message);}
        }
        private bool BossVisible()
        {
            var camera=Camera.main;
            return view.Runtime.Enemies.Values.Any(r=>r.Boss && r.Health && r.Health.isActiveAndEnabled && !r.Health.isDead && r.Health.hp>0 && (!camera || InView(camera,r.Health.transform.position)));
        }
        private static bool InView(Camera camera,Vector3 position) {var p=camera.WorldToViewportPoint(position);return p.z>0 && p.x>=-.1f && p.x<=1.1f && p.y>=-.1f && p.y<=1.1f;}
        private void Create()
        {
            shown=true;age=0;
            root=view.Rect("HollowElements.BossMechanicBanner",view.CanvasRoot,Vector2.zero,new Vector2(1740,210));
            group=root.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            nameLabel=view.Text(entry.Name,root,new Vector2(0,80),new Vector2(1500,32),24);nameLabel.color=new Color(.76f,.72f,.59f);
            var label=view.Text(entry.Text,root,new Vector2(0,8),new Vector2(1660,128),60);
            label.richText=false;label.enableWordWrapping=true;label.enableAutoSizing=true;label.fontSizeMin=30;label.fontSizeMax=60;label.color=new Color(.94f,.92f,.85f);
            textMaterial=new Material(view.Assets.Font.material) {name="HollowElements.ElementGuideBanner.Material"};
            textMaterial.SetFloat("_OutlineWidth",.13f);textMaterial.SetColor("_OutlineColor",new Color(.02f,.025f,.04f,.95f));label.fontSharedMaterial=textMaterial;
            for(int i=0;i<2;i++) {var line=view.Image("饰线",root,new Vector2((i==0?-1:1)*170,-78),new Vector2(280,1.2f),new Color(.75f,.71f,.58f,.7f));line.raycastTarget=false;}
            var crest=view.Image("纹章",root,new Vector2(0,-78),new Vector2(18,18),new Color(.87f,.83f,.7f,.9f));crest.sprite=view.CharmSlotPattern;crest.preserveAspect=true;
            // Borrow an existing native divider when one is available; never own its texture.
            var native=Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s=>s.name.IndexOf("divider",StringComparison.OrdinalIgnoreCase)>=0 && s.rect.width>s.rect.height*3);
            if(native) {var ornament=view.Image("原版花纹",root,new Vector2(0,-79),new Vector2(650,32),new Color(.87f,.83f,.7f,.75f));ornament.sprite=native;ornament.preserveAspect=true;}
        }
        private void Update()
        {
            if(!view || !view.Runtime)return;
            if(UnitySceneManager.GetActiveScene().handle!=sceneHandle) ReadScene();
            bool enabled=view.Runtime.Mod.OnSaveGlobal().BossMechanicHints;
            if(!enabled || entry==null) {if(root)root.gameObject.SetActive(false);return;}
            bool sceneReady=GameManager.instance.gameState==GlobalEnums.GameState.PLAYING && !GameManager.instance.IsInSceneTransition && (!BossSceneController.Instance || BossSceneController.Instance.HasTransitionedIn);
            if(!shown)
            {
                readyAge=sceneReady?readyAge+Time.deltaTime:0;
                if(readyAge>=.5f && HeroController.SilentInstance && !HeroController.SilentInstance.cState.dead && BossVisible()) Create();
            }
            if(!root)return;
            bool hidden=ElementGuideWindow.CapturesInput || WorkshopMenus.CapturesInput || GameManager.instance.isPaused || !HeroController.SilentInstance || HeroController.SilentInstance.cState.dead;
            root.gameObject.SetActive(!hidden);if(hidden)return;
            age+=Time.deltaTime;
            float t=Mathf.Clamp01((age-1.5f)/.75f);float eased=t*t*t*(t*(t*6-15)+10);
            float scale=Mathf.Lerp(1,.52f,eased);root.localScale=new Vector3(scale,scale,1);
            root.anchoredPosition=new Vector2(0,Mathf.Lerp(0,view.CanvasRoot.rect.height*.5f-62,eased));
            float width=view.CanvasRoot.rect.width;root.localScale*=Mathf.Min(1,(width-64)/1740);
            group.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.22f));nameLabel.alpha=1-eased;
        }
        internal object Diagnostics() => new {shown,age,sceneHandle,id=entry?.Id,text=entry?.Text,visible=root && root.gameObject.activeInHierarchy,x=root?root.anchoredPosition.x:0,y=root?root.anchoredPosition.y:0,scale=root?root.localScale.x:0};
        private void OnDestroy() {if(root)Destroy(root.gameObject);if(textMaterial)Destroy(textMaterial);}
    }
}
