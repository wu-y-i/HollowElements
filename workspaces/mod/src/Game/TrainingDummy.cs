using UnityEngine;
using System.Reflection;

namespace HollowElements.Game
{
    // Uses the real hit receiver and its blocking/evasion gates. Only the visual/death implementation is ours.
    internal sealed class TrainingDummy : MonoBehaviour
    {
        private static readonly FieldInfo Evasion=typeof(HealthManager).GetField("evasionByHitRemaining",BindingFlags.Instance|BindingFlags.NonPublic);
        internal HealthManager Health;
        internal bool Recoilable;
        internal void Accept(HitInstance hit)
        {
            Health.hp = Mathf.Max(1, Health.hp - Mathf.Max(0,Mathf.RoundToInt(hit.DamageDealt*hit.Multiplier)));
            if(!hit.IgnoreInvulnerable) Evasion.SetValue(Health,.2f);
            if (hit.AttackType == AttackTypes.Nail && HeroController.SilentInstance) HeroController.SilentInstance.SoulGain();
            if (Recoilable)
            {
                var body = GetComponent<Rigidbody2D>();
                if (body) body.velocity = new Vector2(Mathf.Cos(hit.Direction * Mathf.Deg2Rad) * 2, 0);
            }
            FSMUtility.SendEventToGameObject(hit.Source, "HIT LANDED");
        }
        internal void AcceptExtra(int amount) { Health.hp = Mathf.Max(1, Health.hp - amount); }
        internal static TrainingDummy Spawn(Vector3 position, bool recoilable)
        {
            var obj = new GameObject(recoilable ? "反应木桩·可击退" : "反应木桩·固定");
            obj.layer = 11;
            obj.transform.position = position;
            var collider = obj.AddComponent<BoxCollider2D>(); collider.size = new Vector2(1.3f, 2.5f);
            var body = obj.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
            if (!recoilable) body.constraints |= RigidbodyConstraints2D.FreezePositionX;
            body.drag = 8;
            var dummy = obj.AddComponent<TrainingDummy>(); dummy.Recoilable = recoilable;
            if (recoilable)
            {
                var recoil = obj.AddComponent<Recoil>();
                typeof(Recoil).GetField("recoilSpeedBase",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(recoil,15f);
                typeof(Recoil).GetField("recoilDuration",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(recoil,.5f);
            }
            dummy.Health = obj.AddComponent<HealthManager>(); dummy.Health.hp = 1000000;
            var sprite = obj.AddComponent<SpriteRenderer>();
            var texture = new Texture2D(128, 256, TextureFormat.RGBA32, false);
            var pixels = new Color[128*256];
            for(int y=0;y<256;y++) for(int x=0;x<128;x++)
            {
                float radius=new Vector2(x-63.5f,y-183).magnitude;
                Color color=Color.clear;
                if(y>8 && y<175 && Mathf.Abs(x-63.5f)<(y<30?43:19)) color=new Color(.12f,.15f,.18f,1);
                if(radius<52) color=radius>47?new Color(.53f,.57f,.59f,1):new Color(.2f,.24f,.27f,1);
                if(radius>38 && radius<40) color=new Color(.35f,.39f,.41f,1);
                if(new Vector2(x-46,y-188).magnitude<8 || new Vector2(x-81,y-188).magnitude<8) color=new Color(.03f,.04f,.05f,1);
                if(y>34 && y<139 && (Mathf.Abs(x-53)<1 || Mathf.Abs(x-73)<1)) color=new Color(.23f,.27f,.29f,1);
                pixels[y*128+x]=color;
            }
            texture.SetPixels(pixels); texture.Apply();
            sprite.sprite = Sprite.Create(texture, new Rect(0, 0, 128, 256), new Vector2(.5f, .5f), 102.4f);
            sprite.sortingOrder = 10;
            return dummy;
        }
        private void OnDestroy()
        {
            var renderer=GetComponent<SpriteRenderer>();
            if(renderer && renderer.sprite) { Destroy(renderer.sprite.texture); Destroy(renderer.sprite); }
        }
    }
}
