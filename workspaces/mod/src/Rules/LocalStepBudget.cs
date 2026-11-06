using System;

namespace HollowElements.Rules
{
    // Preserve fractional carry: truncating at 1 would turn 90% speed into alternating steps (50%).
    public sealed class LocalStepBudget
    {
        private double carry;
        public bool Advance(double speed)
        {
            if (speed >= 1) { carry = 0; return true; }
            if (speed <= 0) return false;
            carry += speed;
            if (carry + 1e-7 < 1) return false;
            carry = Math.Max(0, carry - 1);
            return true;
        }
        public void Reset() { carry = 0; }
    }
}
