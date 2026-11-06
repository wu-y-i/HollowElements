using System;
using System.Collections.Generic;

namespace HollowElements.Rules
{
    public sealed class AttackProfile
    {
        public AttackSlot Slot;
        public string AttachmentGroup;
        public string IcdGroup; // independent timer/counter; AttachmentGroup only shares the per-cast budget
        public double AuraUnits = 1;
        public double IcdSeconds;
        public int HitStride = 1;
        public int MaxApplicationsPerCast = 1;
        public int AttachmentHitStride = 1;
        public string SuperconductGroup;
        public double SuperconductCoefficient = 1;
        public int SuperconductHitsPerCast = 1;
        public double SuperconductDamageCap; // zero = no additional cap
        public double SuperconductMinInterval = .2;
        public AttackProfile Copy() { return (AttackProfile)MemberwiseClone(); }
    }

    public sealed class ReactionProfile
    {
        public double AuraSeconds = 8;
        public double ForwardMultiplier = 4;
        public double ReverseMultiplier = 2.5;
        public double ForwardConsumption = 2;
        public double ReverseConsumption = 0.5;
        public double OverloadNailScale = 1;
        public double OverloadRadius = 2;
        public double HeavyRecoilMultiplier = 1.75;
        public double EnemyElectroSeconds = 6;
        public double EnemyElectroInterval = 1;
        public double EnemyElectroNailScale = 0.45;
        public double FreezeSeconds = 1;
        public double FrostSeconds = 3;
        public double FrostSpeed = 0.8;
        public double FreezeDecayAcceleration = .25;
        public double FreezeDecayRecovery = .5;
        public double FreezeDecayFrostRecovery = .05;
        public double ShatterNailScale = 1.5;
        public double SuperconductSeconds = 6;
        public int SuperconductLayers = 4;
        public double SuperconductNailScale = .5;
        public double SuperconductNeutralMultiplier = 1.5;
        public double WatcherSuperconductSeconds = 8;
        public double PlayerOverloadSeconds = 6;
        // Legacy JSON key remains readable; both sides now use the same duration.
        public double PlayerSuperconductSeconds { get => SuperconductSeconds; set { } }
        public double PlayerFreezeSeconds = 3;
        public double PlayerMoveScale = 0.85;
        public double PlayerAttackSpeedScale = 0.75;
        public double PlayerElectroInterval = 4;
        public int PlayerElectroTicks = 3;
        public ReactionProfile Copy() { return (ReactionProfile)MemberwiseClone(); }
    }

    public sealed class RuleConfig
    {
        public int SchemaVersion = 2;
        public int BossRevision = 1;
        public int AttackRevision = 2;
        public int CharmRevision = 2;
        public double DirectDamageMultiplier = .8;
        [Newtonsoft.Json.JsonIgnore]
        public double ReactionDamageMultiplier => Math.Abs(DirectDamageMultiplier-.35)<1e-8 ? .65 : Math.Abs(DirectDamageMultiplier-.45)<1e-8 ? .85 : 1;
        public ReactionProfile Reactions = new ReactionProfile();
        public Dictionary<AttackSource, AttackProfile> Attacks = new Dictionary<AttackSource, AttackProfile>();

        public static RuleConfig Defaults()
        {
            var config = new RuleConfig();
            foreach (AttackSource source in Enum.GetValues(typeof(AttackSource)))
            {
                AttackSlot slot = SlotOf(source);
                var p = new AttackProfile { Slot = slot, AttachmentGroup = slot.ToString(), IcdGroup = source.ToString(), SuperconductGroup = slot.ToString() };
                if (source!=AttackSource.NativeAuxiliary && slot == AttackSlot.Nail) { p.IcdSeconds = 2.5; p.HitStride = 3; }
                if (slot == AttackSlot.Cyclone) { p.AuraUnits=.5;p.AttachmentHitStride=2;p.MaxApplicationsPerCast = int.MaxValue; p.SuperconductHitsPerCast = 2; }
                if(source==AttackSource.BlackShriek) {p.MaxApplicationsPerCast=2;p.AttachmentHitStride=2;p.SuperconductHitsPerCast=2;}
                if(source==AttackSource.BlackDiveBody || source==AttackSource.BlackDiveShock) {p.AttachmentGroup=source.ToString();p.SuperconductHitsPerCast=2;}
                if (slot == AttackSlot.Dive || slot == AttackSlot.GreatSlash || slot == AttackSlot.DashSlash) p.AuraUnits = 2;
                if (slot == AttackSlot.Wave || slot == AttackSlot.Dive) p.SuperconductCoefficient = 0.75;
                if (slot == AttackSlot.Shriek || slot == AttackSlot.Cyclone) p.SuperconductCoefficient = 0.5;
                config.Attacks.Add(source, p); // separate objects even for white/black and dive sub-hits
            }
            return config;
        }

        public static AttackSlot SlotOf(AttackSource source)
        {
            switch (source)
            {
                case AttackSource.Nail: return AttackSlot.Nail;
                case AttackSource.WhiteWave: case AttackSource.BlackWave: return AttackSlot.Wave;
                case AttackSource.WhiteDiveBody: case AttackSource.WhiteDiveShock:
                case AttackSource.BlackDiveBody: case AttackSource.BlackDiveShock: return AttackSlot.Dive;
                case AttackSource.WhiteShriek: case AttackSource.BlackShriek: return AttackSlot.Shriek;
                case AttackSource.GreatSlash: return AttackSlot.GreatSlash;
                case AttackSource.DashSlash: return AttackSlot.DashSlash;
                case AttackSource.NativeAuxiliary: return AttackSlot.Nail; // internal profile; no new player loadout slot
                default: return AttackSlot.Cyclone;
            }
        }

        public RuleConfig Copy()
        {
            var copy = new RuleConfig { SchemaVersion = SchemaVersion, BossRevision=BossRevision, AttackRevision=AttackRevision, CharmRevision=CharmRevision, DirectDamageMultiplier=DirectDamageMultiplier, Reactions = Reactions.Copy() };
            foreach (var pair in Attacks) copy.Attacks.Add(pair.Key, pair.Value.Copy());
            return copy;
        }

        public void Validate()
        {
            if (SchemaVersion != 2 || Reactions == null || Attacks == null) throw new ArgumentException("配置结构版本或必填项目无效。");
            Positive(DirectDamageMultiplier,nameof(DirectDamageMultiplier));
            if(DirectDamageMultiplier>1 || Reactions.SuperconductLayers<1 || Reactions.SuperconductLayers>10) throw new ArgumentException("减伤或超导层数无效。");
            // Reject NaN/Infinity as well as out-of-range values before a snapshot can be activated.
            foreach (var field in typeof(ReactionProfile).GetFields())
            {
                if (field.FieldType == typeof(double)) Positive((double)field.GetValue(Reactions), field.Name);
            }
            if(Reactions.FreezeDecayFrostRecovery>Reactions.FreezeDecayRecovery) throw new ArgumentException("结霜期间的冻结恢复速度不得高于普通恢复速度。");
            if (Reactions.PlayerElectroTicks < 1 || Reactions.PlayerElectroTicks > 100) throw new ArgumentException("感电次数无效。");
            if (Reactions.PlayerMoveScale > 1 || Reactions.PlayerAttackSpeedScale > 1 || Reactions.FrostSpeed > 1) throw new ArgumentException("减速系数不得大于 1。");
            double enemyTicks = Reactions.EnemyElectroSeconds / Reactions.EnemyElectroInterval;
            if (enemyTicks < 1 || enemyTicks > 100 || Math.Abs(enemyTicks - Math.Round(enemyTicks)) > 1e-7) throw new ArgumentException("敌人感电时长必须为跳伤间隔的整数倍。");
            foreach (AttackSource source in Enum.GetValues(typeof(AttackSource)))
            {
                AttackProfile p;
                if (!Attacks.TryGetValue(source, out p) || p == null || p.Slot != SlotOf(source)) throw new ArgumentException("缺少或错误的攻击来源：" + source);
                if (string.IsNullOrWhiteSpace(p.AttachmentGroup) || string.IsNullOrWhiteSpace(p.SuperconductGroup)) throw new ArgumentException("预算组不能为空。");
                // Schema 1 files predating this field acquire one timer/counter per actual attack source.
                if (p.IcdGroup == null) p.IcdGroup = source.ToString();
                if (string.IsNullOrWhiteSpace(p.IcdGroup)) throw new ArgumentException("附着计时计数组不能为空。");
                Positive(p.AuraUnits, source + ".AuraUnits"); NonNegative(p.IcdSeconds, source + ".IcdSeconds");
                NonNegative(p.SuperconductCoefficient, source + ".SuperconductCoefficient"); NonNegative(p.SuperconductDamageCap, source + ".SuperconductDamageCap");
                NonNegative(p.SuperconductMinInterval,source+".SuperconductMinInterval");
                if (p.HitStride < 1 || p.AttachmentHitStride<1 || p.MaxApplicationsPerCast < 1 || p.SuperconductHitsPerCast < 0) throw new ArgumentException("次数预算无效：" + source);
            }
        }
        private static void Positive(double value, string name) { if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentException(name + " 必须为有限正数。"); }
        private static void NonNegative(double value, string name) { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentException(name + " 必须为有限非负数。"); }
    }

    // Runtime charms modify per-hit copies, never the shared baseline or already scheduled periodic damage.
    public interface IRuleModifier
    {
        void Modify(AttackContext hit, AttackProfile attack, ReactionProfile reaction);
    }
}
