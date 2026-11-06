using System.Linq;
using Modding;
using Modding.Menu;
using Modding.Menu.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HollowElements.Game
{
    internal static class SettingsMenu
    {
        internal static MenuScreen Build(HollowElementsMod mod,MenuScreen parent,ModToggleDelegates? toggle)
        {
            IMenuMod.MenuEntry? entry=toggle.HasValue ? new IMenuMod.MenuEntry {
                Name="启用 HollowElements",Values=new[]{"关闭","开启"},
                Saver=i=>toggle.Value.SetModEnabled(i==1),Loader=()=>toggle.Value.GetModEnabled()?1:0
            } : (IMenuMod.MenuEntry?)null;
            var screen=MenuUtils.CreateMenuScreen("HollowElements",mod.GetMenuData(entry),parent);
            // API 77 maps scrollbar value directly to content Y, opposite to
            // ScrollRect's normalized position. Use one native binding only.
            foreach(var scroll in screen.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
            {
                var scrollbar=scroll.verticalScrollbar;
                scroll.verticalScrollbar=null;
                scrollbar.onValueChanged=new Scrollbar.ScrollEvent();
                scroll.verticalScrollbar=scrollbar;
                scroll.verticalNormalizedPosition=1;
            }
            var options=screen.GetComponentsInChildren<MenuOptionHorizontal>(true);
            var back=screen.GetComponentsInChildren<MenuButton>(true).First(b=>b.name=="BackButton");
            for(int i=0;i<options.Length;i++)
            {
                var option=options[i];var navigation=option.navigation;
                navigation.mode=Navigation.Mode.Explicit;
                navigation.selectOnUp=i==0 ? (Selectable)back : options[i-1];
                navigation.selectOnDown=i==options.Length-1 ? (Selectable)back : options[i+1];
                navigation.selectOnLeft=navigation.selectOnRight=null;option.navigation=navigation;
                foreach(var text in option.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    bool description=text.name=="Description";
                    text.fontSize=text.resizeTextMaxSize=description ? 22 : 34;
                    text.resizeTextForBestFit=false;
                    if(!description) continue;
                    text.alignment=TextAnchor.UpperLeft;text.horizontalOverflow=HorizontalWrapMode.Wrap;
                    text.verticalOverflow=VerticalWrapMode.Truncate;
                    var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                    rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-32);rect.sizeDelta=new Vector2(1000,32);
                }
                var old=option.GetComponent<ScrollPaneSelector>();
                if(old)
                {
                    old.enabled=false;
                    var follow=option.gameObject.AddComponent<SettingsScrollSelection>();
                    follow.Pane=old.PaneRect;follow.Mask=old.MaskRect;follow.Scrollbar=old.Scrollbar;
                    follow.View=old.MaskRect.GetComponent<UnityEngine.UI.ScrollRect>();
                }
            }
            var backNav=back.navigation;backNav.mode=Navigation.Mode.Explicit;
            backNav.selectOnUp=options.Last();backNav.selectOnDown=options.First();
            backNav.selectOnLeft=backNav.selectOnRight=null;back.navigation=backNav;
            return screen;
        }
    }

    internal sealed class SettingsScrollSelection : MonoBehaviour,ISelectHandler
    {
        internal RectTransform Pane,Mask;
        internal Scrollbar Scrollbar;
        internal UnityEngine.UI.ScrollRect View;
        public void OnSelect(BaseEventData eventData)
        {
            // Hover also selects native menu options. Only directional
            // navigation may reveal a row; pointer selection must stay still.
            if(!(eventData is AxisEventData)) return;
            if(!Pane || !Mask || !Scrollbar) return;
            var rect=(RectTransform)transform;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(Mask,rect);
            float shift=0;
            if(bounds.max.y>Mask.rect.yMax-12) shift=Mask.rect.yMax-12-bounds.max.y;
            else if(bounds.min.y<Mask.rect.yMin+48) shift=Mask.rect.yMin+48-bounds.min.y;
            float hidden=Pane.rect.height-Mask.rect.height;
            if(hidden>0 && View) {View.StopMovement();View.verticalNormalizedPosition=1-Mathf.Clamp01((Pane.anchoredPosition.y+shift)/hidden);}
        }
    }
}
