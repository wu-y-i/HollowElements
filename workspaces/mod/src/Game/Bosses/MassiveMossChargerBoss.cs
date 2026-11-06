using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class MassiveMossChargerBoss : BossBehaviour
    {
        internal const float FrostRecoilMultiplier = .5f;
        internal bool Frosted => Runtime && Record.State.FrozenUntil > Runtime.Now;
        internal override void PrepareHit(AttackContext hit) { hit.TargetCanRecoil = false; }
        internal override Element ContactElement() => Random.value < .5f ? Element.Hydro : Element.Cryo;
        internal override int ContactDamage(int original) => Runtime && Runtime.Player.FrozenUntil>Runtime.Now ? original * 2 : original;
        internal override void Entered(FsmState state)
        {
            if (state.Fsm.Name != "Mossy Control" || (state.Name != "Hit Left" && state.Name != "Hit Right")) return;
            // This boss's native recoil is an FSM SetVelocity2d action (+/-10), not Recoil.RecoilByDirection.
            var body = GetComponent<Rigidbody2D>();
            if (body) body.velocity = new Vector2(body.velocity.x * FrostRecoilMultiplier, body.velocity.y);
        }
    }
}
