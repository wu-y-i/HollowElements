using System.Linq;
using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class DungDefenderBoss : BossBehaviour
    {
        internal const float BallSpeed = 1.3f;
        internal bool Ball { get; private set; }
        internal int ForcedKnockdowns { get; private set; }
        internal int SplitStacks { get; private set; }
        internal int OverloadProgress { get; private set; }
        private bool knockdown;
        internal double KnockdownReadyAt { get; private set; }
        private PlayMakerFSM control;
        private float surfaceY=float.NaN;
        internal float GroundY => surfaceY;
        private void OnEnable() {SplitStacks=0;ForcedKnockdowns=0;OverloadProgress=0;knockdown=false;Ball=false;surfaceY=float.NaN;KnockdownReadyAt=0;}
        private void Awake() { control = GetComponents<PlayMakerFSM>().First(f => f.FsmName == "Dung Defender"); gameObject.AddComponent<BossMotion>(); }
        internal override float Tempo => Ball ? BallSpeed : 1;
        internal override float ActionTempo(FsmStateAction action) => action.Fsm.Name == "Dung Defender" ? Tempo : 1;
        internal override void Entering(FsmState state)
        {
            if (state.Fsm.Name != "Dung Defender") return;
            // Only native grounded idle is a floor sample. Stun Set can begin in midair;
            // sampling it would classify the next normal grounded hit as underground.
            if(state.Name=="Idle") surfaceY=transform.position.y;
            bool ball = state.Name == "RJ Launch" || state.Name == "RJ In Air" || state.Name == "RJ Bounce" || state.Name == "RJ Wall" || state.Name.StartsWith("Ball Hit ");
            bool began = ball && !Ball; Ball = ball;
            if (began) Runtime.ApplyAttachment(Health, Element.Electro);
        }
        internal override void SecondaryAccepted(SecondaryDamage damage)
        {
            if(damage.Origin!=DamageOrigin.Overload || knockdown || Runtime.Now<KnockdownReadyAt) return;
            if(++OverloadProgress >= (gameObject.scene.name=="GG_White_Defender" ? 2 : 1)) {OverloadProgress=0;knockdown=true;}
        }
        private void LateUpdate()
        {
            if (!knockdown) return;
            knockdown = false;
            if (!Health || Health.isDead || Health.hp<=0 || !control) return;
            string native=control.ActiveStateName;
            bool underground=Health.IsInvincible || native.Contains("Tunn") || native.Contains("Erupt") || native.Contains("Burrow") || native=="Dig" || native=="Under" || !float.IsNaN(surfaceY) && transform.position.y<surfaceY-.5f;
            var clock=GetComponent<EnemyClock>();if(clock) clock.Release();
            if(underground)
            {
                // Native erupt entry restores ground collision, invincibility and burrow emitters.
                // Do not select stun from IsInvincible AFTER entry, since entry changes that flag.
                if(gameObject.scene.name=="GG_White_Defender") control.SetState("Erupt Out");
                else if(control.FsmStates.Any(s=>s.Name=="Quaked Out")) control.SetState("Quaked Out");
                if(!float.IsNaN(surfaceY) && transform.position.y<surfaceY)
                {var p=transform.position;p.y=surfaceY;transform.position=p;}
            }
            control.SetState("Stun Set"); KnockdownReadyAt=Runtime.Now+8;ForcedKnockdowns++;SplitStacks=Mathf.Min(3,SplitStacks+1);
            Trace("超载强制倒地");
        }
        internal override void Projectile(GameObject projectile, FsmStateAction action)
        {
            if(!projectile.name.StartsWith("Dung Ball Large")) return;
            ElementalAttack.Set(projectile, ElementalAttack.RandomElement(), Health.GetInstanceID());
            var spawn=action as HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPool;
            if(spawn!=null) (projectile.GetComponent<DungBallSplit>() ?? projectile.AddComponent<DungBallSplit>()).Configure(this,spawn.gameObject.Value,SplitStacks,false);
        }
    }
}
