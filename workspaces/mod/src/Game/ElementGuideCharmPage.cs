using System.Collections.Generic;
using System.Linq;
using HollowElements.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HollowElements.Game
{
    internal sealed partial class ElementGuideWindow
    {
        private readonly List<UnityEngine.UI.Button> charmButtons=new List<UnityEngine.UI.Button>();
        private TextMeshProUGUI charmName,charmDescription,charmHint,charmDetails;
        private int charmSelection;
        private string charmMessage="";
        internal bool OpenCharms()
        {
            if(!shown) return false;selected=-2;focusedBody=true;Render();return true;
        }
        internal bool SelectCharm(CharmId id)
        {
            int index=System.Array.FindIndex(CharmCatalog.All,c=>c.Id==id);if(index<0 || selected!=-2 || !shown) return false;
            charmSelection=index;HighlightCharms();return true;
        }
        internal object CharmDiagnostics() => new {selected=CharmCatalog.All[charmSelection].Id.ToString(),name=charmName?charmName.text:null,description=charmDescription?charmDescription.text:null,details=charmDetails?charmDetails.text:null,
            hint=charmHint?charmHint.text:null,buttons=charmButtons.Count,icons=charmButtons.Select(b=>b.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.sprite!=null)).Sum(),
            canEquip=view.Runtime.CanConfigureCharms,equipped=view.Runtime.Mod.OnSaveLocal().Charms,state=view.Runtime.Charms,pendingExplosions=view.Runtime.PendingCharmExplosions,
            maskSprites=view.CanvasRoot.GetComponentsInChildren<NativeSlashGraphic>().Where(g=>g.name=="临时面具").Select(g=>g.DefinitionName).ToArray()};
        private void RenderCharms()
        {
            charmButtons.Clear();
            Label("护符配置",bodyContent,30,new Color(.9f,.85f,.71f));
            charmHint=Label("",bodyContent,21,ReadingTextColor);
            var root=view.Rect("护符装配",bodyContent,Vector2.zero,Vector2.zero);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight=560;
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
            float left=Mathf.Max(300,(body.viewport.rect.width-300)*.62f),step=Mathf.Min(88,(left-16)/8),iconSize=Mathf.Min(80,step-6);
            for(int category=0;category<4;category++)
            {
                float y=-category*108;
                var title=Label(CharmCatalog.Categories[category],root,22,new Color(.75f,.7f,.52f));
                Pin(title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(8,y-27),new Vector2(left,y));
                var row=CharmCatalog.All.Where(c=>c.Category==category).ToArray();
                for(int col=0;col<row.Length;col++)
                {
                    var definition=row[col];int index=System.Array.IndexOf(CharmCatalog.All,definition);
                    var button=MakeButton("",root,()=>{charmSelection=index;ToggleSelectedCharm();});
                    var rect=(RectTransform)button.transform;
                    Pin(rect,new Vector2(0,1),new Vector2(0,1),new Vector2(8+col*step,y-104),new Vector2(8+col*step+iconSize,y-104+iconSize));
                    var image=view.Image(definition.Name,rect,Vector2.zero,new Vector2(iconSize,iconSize),Color.white);
                    image.sprite=view.CharmIcons.Get(definition.Id);image.preserveAspect=true;image.raycastTarget=false;
                    var outline=button.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectDistance=new Vector2(2,-2);
                    outline.effectColor=view.Runtime.Charms.Has(definition.Id)?new Color(.8f,.7f,.4f,.9f):Color.clear;
                    var events=button.gameObject.AddComponent<EventTrigger>();
                    var enter=new EventTrigger.Entry {eventID=EventTriggerType.PointerEnter};enter.callback.AddListener(_=>{charmSelection=index;HighlightCharms();});events.triggers.Add(enter);
                    charmButtons.Add(button);
                }
            }
            var slots=Label("当前装备",root,24,new Color(.9f,.85f,.71f));
            Pin(slots.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-258,-30),new Vector2(-8,0));
            var equipped=view.Runtime.Mod.OnSaveLocal().Charms;
            for(int category=0;category<4;category++)
            {
                float y=-40-category*103;
                int occupied=category==0?(equipped.General==CharmId.None?0:1):category==1?(equipped.Amplification==CharmId.None?0:1):category==2?(equipped.TransformA==CharmId.None?0:1)+(equipped.TransformB==CharmId.None?0:1):(equipped.Survival==CharmId.None?0:1);
                var title=Label((category==1?"共用槽 · 增幅 / 剧变":CharmCatalog.Categories[category])+"  "+occupied+" / "+CharmCatalog.Capacities[category],root,20,ReadingTextColor);
                Pin(title.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-258,y-25),new Vector2(-8,y));
                var ids=category==0?new[]{equipped.General}:category==1?new[]{equipped.Amplification}:category==2?new[]{equipped.TransformA,equipped.TransformB}:new[]{equipped.Survival};
                for(int slot=0;slot<ids.Length;slot++)
                {
                    var id=ids[slot];var button=MakeButton("",root,()=>{if(id!=CharmId.None){charmSelection=System.Array.FindIndex(CharmCatalog.All,c=>c.Id==id);ToggleSelectedCharm();}});
                    Pin((RectTransform)button.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-258+slot*88,y-101),new Vector2(-186+slot*88,y-29));
                    if(id!=CharmId.None) {var image=view.Image(id.ToString(),button.transform,Vector2.zero,new Vector2(70,70),Color.white);image.sprite=view.CharmIcons.Get(id);image.preserveAspect=true;image.raycastTarget=false;}
                    else {var pattern=view.Image("空护符槽",button.transform,Vector2.zero,new Vector2(48,48),new Color(.5f,.54f,.6f,.8f));pattern.sprite=view.CharmSlotPattern;pattern.preserveAspect=true;pattern.raycastTarget=false;}
                }
            }
            float infoX=left+20,infoWidth=Mathf.Max(180,body.viewport.rect.width-infoX-285);
            var info=view.Rect("护符说明",root,Vector2.zero,Vector2.zero);
            Pin(info,new Vector2(0,1),new Vector2(0,1),new Vector2(infoX,-548),new Vector2(infoX+infoWidth,-4));
            info.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.09f,.11f,.14f,.5f);
            charmName=Label("",info,25,new Color(.9f,.85f,.71f));
            Pin(charmName.rectTransform,new Vector2(0,1),new Vector2(1,1),new Vector2(12,-68),new Vector2(-10,-10));
            charmDescription=Label("",info,21,ReadingTextColor);
            Pin(charmDescription.rectTransform,new Vector2(0,1),new Vector2(1,1),new Vector2(12,-168),new Vector2(-10,-72));
            charmDetails=Label("",info,20,ReadingTextColor);
            Pin(charmDetails.rectTransform,new Vector2(0,1),new Vector2(1,1),new Vector2(12,-530),new Vector2(-10,-178));
            charmSelection=Mathf.Clamp(charmSelection,0,CharmCatalog.All.Length-1);HighlightCharms();
        }
        internal void NavigateCharms(int vertical,int horizontal,bool submit)
        {
            if(selected!=-2 || !shown) return;
            var definition=CharmCatalog.All[charmSelection];var row=CharmCatalog.All.Where(c=>c.Category==definition.Category).ToArray();
            int col=System.Array.IndexOf(row,definition);
            if(vertical!=0)
            {
                var next=CharmCatalog.All.Where(c=>c.Category==(definition.Category+vertical+4)%4).ToArray();
                charmSelection=System.Array.IndexOf(CharmCatalog.All,next[Mathf.Min(col,next.Length-1)]);
            }
            if(horizontal!=0) charmSelection=System.Array.IndexOf(CharmCatalog.All,row[(col+horizontal+row.Length)%row.Length]);
            if(submit) ToggleSelectedCharm();else HighlightCharms();
        }
        internal bool ToggleSelectedCharm()
        {
            if(selected!=-2 || !shown) return false;
            focusedBody=true;charmMessage="";
            if(!view.Runtime.CanConfigureCharms) {charmMessage="请在长椅配置护符。";HighlightCharms();return false;}
            if(!view.Runtime.ToggleCharm(CharmCatalog.All[charmSelection].Id)) {charmMessage="栏位已满或共用槽正被占用，请先卸下一枚。";HighlightCharms();return false;}
            Render();return true;
        }
        private void HighlightCharms()
        {
            if(!charmName || charmButtons.Count==0) return;
            var definition=CharmCatalog.All[charmSelection];
            for(int i=0;i<charmButtons.Count;i++) charmButtons[i].targetGraphic.color=i==charmSelection?new Color(.2f,.26f,.32f):new Color(.065f,.082f,.1f);
            fonts.Ensure(definition.Name+definition.Description+CharmExplanation.For(definition.Id));
            charmName.text=definition.Name+(view.Runtime.Charms.Has(definition.Id)?" · 已装备":"");
            charmDescription.text=definition.Description;
            charmDetails.text=CharmExplanation.For(definition.Id);
            charmHint.text=charmMessage.Length>0?charmMessage:view.Runtime.CanConfigureCharms?"确认装备，再次确认卸下；空出的增幅槽可多装一枚剧变护符。":"请在长椅配置护符；当前可查看介绍与装备。";
        }
    }
}
