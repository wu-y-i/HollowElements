using HollowElements.Rules;
using HutongGames.PlayMaker;

namespace HollowElements.Game.Bosses
{
    internal sealed class TraitorLordBoss : BossBehaviour
    {
        internal bool WavePending { get; private set; }
        internal int Replacements { get; private set; }
        private readonly ReactionAttackGate gate=new ReactionAttackGate();
        private static bool Antic(string name) => name=="Attack Antic" || name=="Jump Antic" || name=="Sickle Antic" || name=="Slam Antic";
        internal override void Entering(FsmState state) { if(state.Fsm.Name=="Mantis") gate.Enter(state.Name,Antic(state.Name)); }
        internal override void Reacted(Reaction reaction) { if(gate.Ready) WavePending=true; }
        internal override string Redirect(FsmState state)
        {
            if(state.Fsm.Name!="Mantis" || !WavePending) return null;
            if(state.Name!="Attack Antic" && state.Name!="Jump Antic" && state.Name!="Sickle Antic" && state.Name!="Slam Antic") return null;
            WavePending=false;gate.Forced();Replacements++;Trace("下一招替换为原版蓄力全屏波");
            return state.Name=="Slam Antic" ? null : "Slam Antic";
        }
    }
}
