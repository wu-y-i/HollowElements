using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HollowElements.Game
{
    // Optional integration: no assembly dependency and no edits to the other mod's UI.
    internal static class ExternalHealthBar
    {
        private static Type bossType,enemyType;
        private static FieldInfo bossImage,enemyImage;
        private static bool resolved;
        private static readonly Vector3[] corners=new Vector3[4];
        internal static bool TryAnchor(HealthManager health,RectTransform target,out Vector2 anchor)
        {
            anchor=Vector2.zero;
            if(!resolved)
            {
                resolved=true;
                bossType=Type.GetType("EnemyHPBar.BossHPBar, EnemyHPBar");enemyType=Type.GetType("EnemyHPBar.HPBar, EnemyHPBar");
                bossImage=bossType?.GetField("health_bar",BindingFlags.Public|BindingFlags.Instance);
                enemyImage=enemyType?.GetField("health_bar",BindingFlags.Public|BindingFlags.Instance);
            }
            var component=bossType!=null ? health.GetComponent(bossType) : null;
            var field=bossImage;
            if(!component && enemyType!=null) {component=health.GetComponent(enemyType);field=enemyImage;}
            var image=component && field!=null ? field.GetValue(component) as UnityEngine.UI.Image : null;
            if(!image || !image.gameObject.activeInHierarchy || image.canvasRenderer.GetAlpha()<=.01f) return false;
            var canvas=image.canvas;if(!canvas) return false;
            image.rectTransform.GetWorldCorners(corners);
            var point=(corners[1]+corners[2])*.5f;
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera ? canvas.worldCamera : Camera.main;
            var targetCanvas=target.GetComponentInParent<Canvas>();
            Camera targetCamera=targetCanvas && targetCanvas.renderMode!=RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(target,RectTransformUtility.WorldToScreenPoint(camera,point),targetCamera,out anchor);
        }
    }
}
