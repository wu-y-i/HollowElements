namespace HollowElements.Game.Bosses
{
    // 0 ready, 1 forced attack, 2 awaiting native selection, 3 native attack.
    // Ignore reactions until both the forced and a subsequent native attack end.
    internal sealed class ReactionAttackGate
    {
        private int phase;
        internal bool Ready => phase==0;
        internal void Forced() { phase=1; }
        internal void Enter(string name,bool anticipation)
        {
            bool idle=name=="Idle" || name=="Idle CD" || name=="Idle Pause";
            if(idle && phase==1) phase=2;
            else if(phase==2 && anticipation) phase=3;
            else if(idle && phase==3) phase=0;
        }
    }
}
