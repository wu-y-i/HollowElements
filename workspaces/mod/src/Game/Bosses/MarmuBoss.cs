using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class MarmuBoss : BossBehaviour
    {
        internal const double RestSeconds=3;
        internal double RestUntil { get; private set; }
        private bool requested,resting,protectedBounce,bounceStarted,accelerated,bounceCompleted;
        private void OnEnable() {requested=resting=protectedBounce=bounceStarted=accelerated=bounceCompleted=false;RestUntil=0;}
        internal override float Tempo => accelerated ? 1.5f : 1;
        internal override float ControlScale => resting && Runtime.Now<RestUntil ? 0 : 1;
        internal override void Reacted(Reaction reaction) { if(reaction==Reaction.Freeze && !resting && !protectedBounce) requested=true; }
        internal override void SecondaryAccepted(SecondaryDamage damage) { if(damage.Origin==DamageOrigin.Overload && !resting && !protectedBounce) requested=true; }
        internal override void Entering(FsmState state)
        {
            if(state.Fsm.Name=="Control" && state.Name=="Chase")
                foreach(var a in state.Actions) if(a is HutongGames.PlayMaker.Actions.ChaseObjectV2)
                {var chase=(HutongGames.PlayMaker.Actions.ChaseObjectV2)a;chase.speedMax.Value=accelerated?60:40;chase.accelerationForce.Value=accelerated?127.5f:85;}
            if(state.Fsm.Name!="Control" || !protectedBounce) return;
            if(state.Name=="Chase" || state.Name=="Roll" || state.Name=="In Air") bounceStarted=true;
            if(bounceStarted && (state.Name.StartsWith("Hit ") || state.Name=="Roll Warp?")) bounceCompleted=true;
        }
        private void OnDestroy()
        {foreach(var f in GetComponents<PlayMakerFSM>()) if(f.FsmName=="Control") foreach(var s in f.FsmStates) foreach(var a in s.Actions) if(a is HutongGames.PlayMaker.Actions.ChaseObjectV2) {var c=(HutongGames.PlayMaker.Actions.ChaseObjectV2)a;c.speedMax.Value=40;c.accelerationForce.Value=85;} }
        private void LateUpdate()
        {
            // Release protection after the hit's native FSM dispatch, so the
            // reaction on the very hit ending this bounce cannot restun it.
            if(bounceCompleted) {bounceCompleted=false;protectedBounce=false;bounceStarted=false;}
            if(!Health || Health.isDead || Health.hp<=0) return;
            var control=GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Control");
            if(requested)
            {
                requested=false; resting=true; RestUntil=Runtime.Now+RestSeconds;
                control.SetState("Unroll");
                var body=GetComponent<Rigidbody2D>(); body.velocity=Vector2.zero; body.angularVelocity=0;
                var roll=transform.Find("Hit Roll"); if(roll) roll.gameObject.SetActive(false);
                var idle=transform.Find("Hit Idle"); if(idle) idle.gameObject.SetActive(true);
                var animator=GetComponent<tk2dSpriteAnimator>();
                // Roll End's final frame is the native unrolled waiting pose.
                var clip=animator.GetClipByName("Roll End"); if(clip!=null) animator.PlayFromFrame(clip,clip.frames.Length-1);
                Trace("停止弹跳3秒");
            }
            else if(resting && Runtime.Now>=RestUntil)
            { resting=false;protectedBounce=true;bounceStarted=false;accelerated=true;control.SetState("Antic"); Trace("恢复1.5倍弹跳，完成一轮前保护"); }
        }
    }
}
