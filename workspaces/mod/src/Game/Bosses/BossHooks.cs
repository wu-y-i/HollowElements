using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Hooks are global; each encounter explicitly opts into its own native actors and actions.
    internal static class BossHooks
    {
        private static readonly FieldInfo WaitTimer = typeof(Wait).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RandomTimer = typeof(WaitRandom).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static ElementalAttack spawningWave;
        private static BossBehaviour spawningBoss;
        private static FsmStateAction spawningAction;
        private static BossBehaviour Boss(FsmStateAction action) => action.Fsm.GameObject ? action.Fsm.GameObject.GetComponentInParent<BossBehaviour>() : null;
        private static float Tempo(FsmStateAction action) => Boss(action)?.ActionTempo(action) ?? 1;
        private static float WaitTempo(FsmStateAction action) => Tempo(action)*(EnemyClock.Clock(action.Fsm.GameObject)?.Scale ?? 1);
        private static FsmFloat Scaled(FsmFloat value, float scale) => value == null || value.IsNone ? value : new FsmFloat { Value = value.Value * scale };
        private static void WaitUpdate(On.HutongGames.PlayMaker.Actions.Wait.orig_OnUpdate orig, Wait action)
        {
            if (!action.realTime && WaitTimer != null) WaitTimer.SetValue(action, (float)WaitTimer.GetValue(action) + Time.deltaTime * (WaitTempo(action) - 1));
            orig(action);
        }
        private static void RandomUpdate(On.HutongGames.PlayMaker.Actions.WaitRandom.orig_OnUpdate orig, WaitRandom action)
        {
            if (!action.realTime && RandomTimer != null) RandomTimer.SetValue(action, (float)RandomTimer.GetValue(action) + Time.deltaTime * (WaitTempo(action) - 1));
            orig(action);
        }
        private static void Velocity(On.HutongGames.PlayMaker.Actions.SetVelocity2d.orig_DoSetVelocity orig, SetVelocity2d action)
        {
            float scale = Tempo(action); var x = action.x; var y = action.y; var vector = action.vector;
            if (scale == 1) { orig(action); return; }
            try
            {
                action.x = Scaled(x, scale); action.y = Scaled(y, scale);
                if (vector != null && !vector.IsNone) action.vector = new FsmVector2 { Value = vector.Value * scale };
                orig(action);
            }
            finally { action.x = x; action.y = y; action.vector = vector; }
        }
        private static void Angle(On.HutongGames.PlayMaker.Actions.SetVelocityAsAngle.orig_DoSetVelocity orig, SetVelocityAsAngle action)
        { var speed = action.speed; try { action.speed = Scaled(speed, Tempo(action)); orig(action); } finally { action.speed = speed; } }
        private static void Fire(On.HutongGames.PlayMaker.Actions.FireAtTarget.orig_DoSetVelocity orig, FireAtTarget action)
        { var speed = action.speed; try { action.speed = Scaled(speed, Tempo(action)); orig(action); } finally { action.speed = speed; } }
        private static void Spawn(On.HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPool.orig_OnEnter orig, SpawnObjectFromGlobalPool action)
        {
            var boss=Boss(action);
            if(!boss && action.Fsm.GameObject.name=="Orb Spinner")
                boss=Object.FindObjectsOfType<SoulMasterBoss>().FirstOrDefault(b=>b.gameObject.scene==action.Fsm.GameObject.scene && !b.name.Contains("Phase2"));
            var previousBoss=spawningBoss; var previousAction=spawningAction;
            var previousWave=spawningWave;
            try
            {
                spawningBoss=boss;spawningAction=action;
                var wave=action.Fsm.GameObject.GetComponent<ElementalAttack>();
                if(wave && wave.name.StartsWith("Shockwave Wave") && wave.Element!=Rules.Element.None) spawningWave=wave;
                orig(action);
            }
            finally { spawningBoss=previousBoss;spawningAction=previousAction;spawningWave=previousWave; }
        }
        private static void SetScale(On.HutongGames.PlayMaker.Actions.SetScale.orig_DoSetScale orig, SetScale action)
        {
            orig(action);
            var obj = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            if (obj) obj.GetComponent<ElementalAttack>()?.NativeScaleSet();
            if(obj && action.Fsm.GameObject!=obj) obj.GetComponent<ZoteWave>()?.ScaleChanged();
        }
        private static void Create(On.HutongGames.PlayMaker.Actions.CreateObject.orig_OnEnter orig, CreateObject action)
        {
            var store=action.storeObject;
            try
            {
                if(store==null || store.IsNone) action.storeObject=new FsmGameObject();
                orig(action);var obj=action.storeObject.Value;
                if(obj) { Boss(action)?.Projectile(obj,action);BossActors.Observe(obj); }
            }
            finally { action.storeObject=store; }
        }
        private static void WaveSpurts(On.HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPoolOverTimeV2.orig_OnUpdate orig, SpawnObjectFromGlobalPoolOverTimeV2 action)
        {
            var previous=spawningWave;
            try
            {
                // The travelling wave is only an emitter. Its detached spurts own the real DamageHero.
                spawningWave=action.Fsm.GameObject ? action.Fsm.GameObject.GetComponent<ElementalAttack>() : null;
                if(!spawningWave || spawningWave.Element==Rules.Element.None || !spawningWave.name.StartsWith("Shockwave Wave")) spawningWave=null;
                orig(action);
            }
            finally { spawningWave=previous; }
        }
        private static GameObject WaveSpawn(On.ObjectPoolExtensions.orig_Spawn_GameObject_Vector3_Quaternion orig,GameObject prefab,Vector3 position,Quaternion rotation)
        {
            var obj=orig(prefab,position,rotation);
            if(spawningWave && obj && obj.name.StartsWith("Shockwave Spurt"))
                ElementalAttack.Set(obj,spawningWave.Element,spawningWave.OwnerId,tint:true,outline:false);
            if(obj && spawningBoss) spawningBoss.Projectile(obj,spawningAction);
            if(obj) BossActors.Observe(obj);
            if(obj && obj.GetComponent<HealthManager>()) EncounterSummon.Discover(obj.GetComponent<HealthManager>());
            return obj;
        }
        private static void Died(On.HealthManager.orig_Die orig,HealthManager health,float? direction,AttackTypes type,bool ignore)
        {
            var summon=health.GetComponent<EncounterSummon>(); summon?.CaptureMaximum(); bool alive=!health.isDead;
            orig(health,direction,type,ignore);
            // Native death effects can destroy/recycle the component before Die returns.
            if(alive && (!health || health.isDead)) summon?.Died();
        }
        private static void BlockedHit(On.HealthManager.orig_Invincible orig,HealthManager health,HitInstance hit)
        {
            orig(health,hit);
            if(health.InvincibleFromDirection==4 && hit.AttackType==AttackTypes.Nail)
                (BossRegistry.Attach(health.gameObject) as CrystalGuardianBoss)?.HeadHit();
        }
        internal static void ActorState(FsmState state)
        {
            var obj=state.Fsm.GameObject; if(!obj) return;
            BossActors.Observe(obj);
            WaterExposure.Register(state.Fsm.FsmComponent);
            FlukemarmBoss.Released(state);
            string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if(scene=="GG_Ghost_Galien" && (obj.name=="Galien Hammer" || obj.name.StartsWith("Galien Mini Hammer")))
                foreach(var owner in Object.FindObjectsOfType<GalienBoss>()) { owner.Gear(obj); break; }
            if(scene=="GG_Ghost_Hu" && obj.transform.parent && obj.transform.parent.name=="Ring Holder" && state.Fsm.Name=="Control" && (state.Name=="Init" || state.Name=="Antic"))
                foreach(var owner in Object.FindObjectsOfType<ElderHuBoss>()) { owner.Hammer(obj); break; }
            if(scene=="GG_Hive_Knight" && obj.name.StartsWith("Bee Dropper") && state.Fsm.Name=="Control" && state.Name=="Swarm Start")
                foreach(var owner in Object.FindObjectsOfType<HiveKnightBoss>())
                { (obj.GetComponent<HiveBee>() ?? obj.AddComponent<HiveBee>()).Born(owner); break; }
            if(state.Fsm.Name=="xero_nail")
                foreach(var boss in Object.FindObjectsOfType<XeroBoss>()) if(boss.gameObject.scene==obj.scene) { boss.Sword(obj); break; }
            if(state.Fsm.Name=="Orb Control" && (obj.scene.name=="GG_Soul_Master" || obj.scene.name=="GG_Soul_Tyrant"))
            {
                var marker=obj.GetComponent<ElementalAttack>();
                if(marker && marker.Element!=Rules.Element.None) return;
                foreach(var boss in Object.FindObjectsOfType<SoulMasterBoss>()) if(boss.gameObject.scene==obj.scene) { boss.Orb(obj); break; }
            }
        }
        private static void Fling(On.HutongGames.PlayMaker.Actions.FlingObjectsFromGlobalPool.orig_OnEnter orig, FlingObjectsFromGlobalPool action)
        {
            var previousBoss=spawningBoss; var previousAction=spawningAction;
            try { spawningBoss=Boss(action); spawningAction=action; orig(action); }
            finally { spawningBoss=previousBoss; spawningAction=previousAction; }
        }
        internal static void Install()
        {
            On.HutongGames.PlayMaker.Actions.Wait.OnUpdate += WaitUpdate;
            On.HutongGames.PlayMaker.Actions.WaitRandom.OnUpdate += RandomUpdate;
            On.HutongGames.PlayMaker.Actions.SetVelocity2d.DoSetVelocity += Velocity;
            On.HutongGames.PlayMaker.Actions.SetVelocityAsAngle.DoSetVelocity += Angle;
            On.HutongGames.PlayMaker.Actions.FireAtTarget.DoSetVelocity += Fire;
            On.HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPool.OnEnter += Spawn;
            On.HutongGames.PlayMaker.Actions.FlingObjectsFromGlobalPool.OnEnter += Fling;
            On.HutongGames.PlayMaker.Actions.SetScale.DoSetScale += SetScale;
            On.HutongGames.PlayMaker.Actions.CreateObject.OnEnter += Create;
            On.HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPoolOverTimeV2.OnUpdate += WaveSpurts;
            On.ObjectPoolExtensions.Spawn_GameObject_Vector3_Quaternion += WaveSpawn;
            On.HealthManager.Invincible += BlockedHit;
            On.HealthManager.Die += Died;
        }
        internal static void Uninstall()
        {
            On.HutongGames.PlayMaker.Actions.Wait.OnUpdate -= WaitUpdate;
            On.HutongGames.PlayMaker.Actions.WaitRandom.OnUpdate -= RandomUpdate;
            On.HutongGames.PlayMaker.Actions.SetVelocity2d.DoSetVelocity -= Velocity;
            On.HutongGames.PlayMaker.Actions.SetVelocityAsAngle.DoSetVelocity -= Angle;
            On.HutongGames.PlayMaker.Actions.FireAtTarget.DoSetVelocity -= Fire;
            On.HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPool.OnEnter -= Spawn;
            On.HutongGames.PlayMaker.Actions.FlingObjectsFromGlobalPool.OnEnter -= Fling;
            On.HutongGames.PlayMaker.Actions.SetScale.DoSetScale -= SetScale;
            On.HutongGames.PlayMaker.Actions.CreateObject.OnEnter -= Create;
            On.HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPoolOverTimeV2.OnUpdate -= WaveSpurts;
            On.ObjectPoolExtensions.Spawn_GameObject_Vector3_Quaternion -= WaveSpawn;
            spawningWave=null;
            spawningBoss=null; spawningAction=null;
            On.HealthManager.Invincible -= BlockedHit;
            On.HealthManager.Die -= Died;
        }
    }

    // g * tempo^2 with launch velocity * tempo preserves jump arcs while shortening flight time.
    [DefaultExecutionOrder(20000)]
    internal sealed class BossMotion : MonoBehaviour
    {
        private BossBehaviour boss;
        private Rigidbody2D body;
        private float original, applied;
        private bool owned;
        private void Awake() { boss = GetComponent<BossBehaviour>(); body = GetComponent<Rigidbody2D>(); }
        private void LateUpdate()
        {
            if (!body || !boss) return;
            if (!owned || Mathf.Abs(body.gravityScale - applied) > .0001f) original = body.gravityScale;
            applied = original * boss.Tempo * boss.Tempo; body.gravityScale = applied; owned = true;
        }
        internal void Restore() { if (owned && body && Mathf.Abs(body.gravityScale - applied) < .0001f) body.gravityScale = original; owned = false; }
        internal float NativeGravity => owned ? original : body.gravityScale;
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
