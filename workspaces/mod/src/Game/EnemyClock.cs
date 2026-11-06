using System.Collections;
using HutongGames.PlayMaker;
using HollowElements.Rules;
using HollowElements.Game.Bosses;
using UnityEngine;

namespace HollowElements.Game
{
    // Local step gating keeps native 2D physics and FSM delta-time on the same clock.
    // Never disables an FSM (OnDisable resets its state), changes shared clips, or changes Time.timeScale.
    [DefaultExecutionOrder(-30000)]
    internal sealed class EnemyClock : MonoBehaviour
    {
        internal EnemyRecord Record;
        internal HealthManager ControlOwner;
        internal bool KeepMovingWhenFrozen;
        private Rigidbody2D body;
        private HealthManager health;
        private bool held;
        private RigidbodyConstraints2D constraints;
        private Vector2 velocity;
        private float angularVelocity;
        private readonly LocalStepBudget fixedBudget = new LocalStepBudget();
        private readonly LocalStepBudget frameBudget = new LocalStepBudget();
        private int frame = -1;
        private bool frameAllowed = true;
        internal bool PhysicsAllowed = true;
        internal int AcceptedFrames, SkippedFrames, AcceptedPhysics, SkippedPhysics;
        internal double AnimationInput, AnimationOutput;
        internal float Scale
        {
            get
            {
                var runtime = CombatRuntime.Current;
                var owner=ControlOwner ? ControlOwner : health;
                if(owner && (owner.hp<=0 || owner.isDead)) return 1;
                // Debug clear and native re-enable replace EnemyRecord; keep the actor identity,
                // then resolve its current record rather than retaining an obsolete snapshot.
                var control=Record;
                if(runtime && ControlOwner && ControlOwner.isActiveAndEnabled && !ControlOwner.isDead)
                    runtime.Enemies.TryGetValue(ControlOwner,out control);
                float scale=1;
                if (runtime != null && control != null && control.State.FrozenUntil > runtime.Now)
                    scale=control.State.HardFrozen ? (KeepMovingWhenFrozen ? (float)runtime.Config.Reactions.FrostSpeed : 0) : (float)control.State.FrozenSpeed;
                return scale * (GetComponent<BossBehaviour>()?.ControlScale ?? 1) * (GetComponent<MarkothOrbit>()?.Scale ?? 1);
            }
        }
        private void Awake() { body = GetComponent<Rigidbody2D>(); health=GetComponent<HealthManager>(); gameObject.AddComponent<EnemyPhysicsHold>().Clock=this; }
        private void OnEnable() { StartCoroutine(AfterPhysics()); }
        private IEnumerator AfterPhysics()
        {
            var wait=new WaitForFixedUpdate();
            while(true) {yield return wait;Release();}
        }
        private void FixedUpdate()
        {
            Release();
            float scale = Scale;
            PhysicsAllowed = fixedBudget.Advance(scale);
            if (PhysicsAllowed) AcceptedPhysics++; else SkippedPhysics++;
        }
        internal void HoldForPhysics()
        {
            if (!PhysicsAllowed && body)
            {
                constraints = body.constraints; velocity = body.velocity; angularVelocity = body.angularVelocity;
                held = true; body.constraints = RigidbodyConstraints2D.FreezeAll; body.velocity = Vector2.zero; body.angularVelocity = 0;
            }
        }
        internal void Release()
        {
            if (!held) return;
            if (body) { body.constraints = constraints; body.velocity = velocity; body.angularVelocity = angularVelocity; }
            held = false;
        }
        private void OnDisable() { StopAllCoroutines(); Release(); PhysicsAllowed = true; frameBudget.Reset(); fixedBudget.Reset(); }
        private void OnDestroy() { Release(); }
        private bool FrameAllowed()
        {
            if (frame == Time.frameCount) return frameAllowed;
            frame = Time.frameCount;
            float scale = Scale;
            frameAllowed = frameBudget.Advance(scale);
            if (frameAllowed) AcceptedFrames++; else SkippedFrames++;
            return frameAllowed;
        }
        internal static EnemyClock Clock(GameObject obj)
        {
            if (!obj) return null;
            return obj.GetComponentInParent<EnemyClock>();
        }
        private static void FsmUpdate(On.HutongGames.PlayMaker.Fsm.orig_Update orig, Fsm fsm)
        { var clock = Clock(fsm.GameObject); if (!clock || clock.Scale>0) orig(fsm); }
        private static void FsmFixed(On.HutongGames.PlayMaker.Fsm.orig_FixedUpdate orig, Fsm fsm)
        { var clock = Clock(fsm.GameObject); if (!clock || clock.Scale >= 1 || clock.PhysicsAllowed) orig(fsm); }
        private static void FsmLate(On.HutongGames.PlayMaker.Fsm.orig_LateUpdate orig, Fsm fsm)
        { var clock = Clock(fsm.GameObject); if (!clock || clock.Scale>0) orig(fsm); }
        private static void RecoilPhysics(On.Recoil.orig_FixedUpdate orig,Recoil recoil)
        { var clock=Clock(recoil.gameObject); if(!clock || clock.Scale>=1 || clock.PhysicsAllowed) orig(recoil); }
        private static void Animation(On.tk2dSpriteAnimator.orig_UpdateAnimation orig, tk2dSpriteAnimator animator, float delta)
        {
            var clock = Clock(animator.gameObject);
            if (clock) { clock.AnimationInput += delta; clock.AnimationOutput += delta*clock.Scale; }
            float tempo = animator.GetComponentInParent<BossBehaviour>()?.Tempo ?? 1;
            orig(animator, (clock ? delta * clock.Scale : delta) * tempo);
        }
        private static void TweenUpdate(On.iTween.orig_Update orig,iTween tween)
        { var clock=Clock(tween.gameObject); if(!clock || clock.FrameAllowed()) orig(tween); }
        private static void TweenFixed(On.iTween.orig_FixedUpdate orig,iTween tween)
        { var clock=Clock(tween.gameObject); if(!clock || clock.Scale>=1 || clock.PhysicsAllowed) orig(tween); }
        internal static void Install()
        {
            On.HutongGames.PlayMaker.Fsm.Update += FsmUpdate;
            On.HutongGames.PlayMaker.Fsm.FixedUpdate += FsmFixed;
            On.HutongGames.PlayMaker.Fsm.LateUpdate += FsmLate;
            On.tk2dSpriteAnimator.UpdateAnimation += Animation;
            On.Recoil.FixedUpdate += RecoilPhysics;
            On.iTween.Update += TweenUpdate; On.iTween.FixedUpdate += TweenFixed;
        }
        internal static void Uninstall()
        {
            On.HutongGames.PlayMaker.Fsm.Update -= FsmUpdate;
            On.HutongGames.PlayMaker.Fsm.FixedUpdate -= FsmFixed;
            On.HutongGames.PlayMaker.Fsm.LateUpdate -= FsmLate;
            On.tk2dSpriteAnimator.UpdateAnimation -= Animation;
            On.Recoil.FixedUpdate -= RecoilPhysics;
            On.iTween.Update -= TweenUpdate; On.iTween.FixedUpdate -= TweenFixed;
        }
    }
    // Capture after native FSM FixedUpdate; restore after simulation before Update.
    [DefaultExecutionOrder(30000)]
    internal sealed class EnemyPhysicsHold : MonoBehaviour
    {
        internal EnemyClock Clock;
        private void FixedUpdate() {if(Clock) Clock.HoldForPhysics();}
        private void OnDestroy() {if(Clock) Clock.Release();}
    }
}
