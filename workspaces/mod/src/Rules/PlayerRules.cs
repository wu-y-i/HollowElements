using System;

namespace HollowElements.Rules
{
    public sealed class PlayerState
    {
        public Element Aura;
        public double AuraUnits;
        public double AuraUntil;
        public double AuraDuration;
        public double OverloadUntil;
        public double OverloadDuration;
        public double SuperconductUntil;
        public double SuperconductDuration;
        public double FrozenUntil;
        public double FrozenDuration;
        public double MoveScale = 1;
        public double AttackSpeedScale = 1;
        public PeriodicState ElectroCharged;

        public PlayerState Copy()
        {
            var copy = (PlayerState)MemberwiseClone();
            copy.ElectroCharged = ElectroCharged == null ? null : ElectroCharged.Copy();
            return copy;
        }
        public void Expire(double now)
        {
            if (now >= AuraUntil) { Aura = Element.None; AuraUnits = 0; AuraUntil = 0; }
            if (now >= OverloadUntil) OverloadUntil = 0;
            if (now >= SuperconductUntil) SuperconductUntil = 0;
            if (now >= FrozenUntil) { FrozenUntil = 0; MoveScale = 1; AttackSpeedScale = 1; }
        }
    }

    public sealed class PlayerHitResult
    {
        public PlayerState NextState;
        public int Damage;
        public Reaction Reaction;
        public CharmState NextCharms;
    }

    public sealed class PlayerRules
    {
        private readonly ReactionProfile config;
        public PlayerRules(RuleConfig snapshot) { snapshot.Validate(); config = snapshot.Reactions.Copy(); }

        // Input damage is AFTER vanilla difficulty/charm adjustments; a blocked hit must not commit this result.
        public PlayerHitResult Prepare(PlayerState current, int effectiveDamage, Element incoming, double units, double now,CharmState charms=null)
        {
            if (effectiveDamage < 1 || units <= 0) throw new ArgumentException("玩家命中数据无效。");
            return Resolve(current,effectiveDamage,incoming,units,now,true,charms);
        }
        // Environmental attachment has no synthetic mask loss and does not clear an existing EC instance.
        public PlayerHitResult Attach(PlayerState current,Element incoming,double units,double now,CharmState charms=null)
        {
            if(incoming==Element.None || units<=0 || double.IsNaN(units) || double.IsInfinity(units)) throw new ArgumentException("玩家附着数据无效。");
            return Resolve(current,0,incoming,units,now,false,charms);
        }
        private PlayerHitResult Resolve(PlayerState current,int effectiveDamage,Element incoming,double units,double now,bool externalHurt,CharmState charms)
        {
            charms=charms?.Copy();
            var state = current.Copy(); state.Expire(now);
            if(externalHurt) state.ElectroCharged = null; // real external hurt clears the old instance before installing the new one
            Reaction reaction = Reactions.Identify(state.Aura, incoming);
            int amplified = effectiveDamage;
            if(charms!=null && charms.Has(CharmId.C36) && Reactions.Amplifying(reaction)) amplified=checked(effectiveDamage+(effectiveDamage>0?1:0));
            else if (Reactions.Forward(reaction)) amplified = checked(effectiveDamage * 2);
            else if (effectiveDamage>0 && Reactions.Amplifying(reaction)) amplified = checked(effectiveDamage + 1);
            bool overload=state.OverloadUntil>now && !(charms?.Suppressed(now)??false) && !(charms?.Has(CharmId.C35)??false);
            int overloaded = overload ? checked(effectiveDamage * 2) : effectiveDamage;
            if(charms!=null && charms.Has(CharmId.C36) && externalHurt)
            {
                if(!overload) charms.SkipOverload=false;
                else {if(charms.SkipOverload) overloaded=effectiveDamage;charms.SkipOverload=!charms.SkipOverload;}
            }
            var result = new PlayerHitResult { NextState = state, Damage = Math.Max(amplified, overloaded), Reaction = reaction,NextCharms=charms };
            if(externalHurt && reaction==Reaction.Overload && (charms?.Has(CharmId.C35)??false) && !charms.Suppressed(now)) result.Damage++;
            if (incoming == Element.None) return result;
            if (reaction == Reaction.None)
            {
                state.Aura = incoming; state.AuraUnits = Math.Max(state.AuraUnits, units); state.AuraUntil = now + config.AuraSeconds; state.AuraDuration = config.AuraSeconds;
                return result;
            }
            double consumption = Reactions.Forward(reaction) ? config.ForwardConsumption : Reactions.Amplifying(reaction) ? config.ReverseConsumption : 1;
            state.AuraUnits = Math.Max(0, state.AuraUnits - units * consumption);
            if (state.AuraUnits <= 1e-9) { state.Aura = Element.None; state.AuraUntil = 0; }
            switch (reaction)
            {
                case Reaction.Overload: state.OverloadUntil = now + config.PlayerOverloadSeconds; state.OverloadDuration = config.PlayerOverloadSeconds; break;
                case Reaction.Superconduct: state.SuperconductUntil = now + config.PlayerSuperconductSeconds; state.SuperconductDuration = config.PlayerSuperconductSeconds; break;
                case Reaction.Freeze: state.FrozenUntil = now + config.PlayerFreezeSeconds; state.FrozenDuration = config.PlayerFreezeSeconds; state.MoveScale = config.PlayerMoveScale; state.AttackSpeedScale = config.PlayerAttackSpeedScale; break;
                case Reaction.ElectroCharged:
                    state.ElectroCharged = new PeriodicState { NextTick = now + config.PlayerElectroInterval,
                        Interval = config.PlayerElectroInterval, RemainingTicks = (charms?.Has(CharmId.C35)??false)?1:config.PlayerElectroTicks, TotalTicks = (charms?.Has(CharmId.C35)??false)?1:config.PlayerElectroTicks, Damage = 1 }; break;
            }
            return result;
        }

        // Caller attempts a normal hurt and reports success. Invulnerability queues a due pulse, never erases it.
        public bool Tick(PlayerState state, double now, Func<bool> tryTakeOneMask)
        {
            state.Expire(now);
            var ec = state.ElectroCharged;
            if (ec == null || now + 1e-9 < ec.NextTick || !tryTakeOneMask()) return false;
            ec.RemainingTicks--;
            ec.NextTick = Math.Max(ec.NextTick + ec.Interval, now + ec.Interval);
            if (ec.RemainingTicks <= 0) state.ElectroCharged = null;
            return true;
        }
    }
}
