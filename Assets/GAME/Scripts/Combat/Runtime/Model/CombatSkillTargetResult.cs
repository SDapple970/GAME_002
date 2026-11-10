namespace Game.Combat.Model
{
    public sealed class CombatSkillTargetResult
    {
        public ICombatant Target { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public int DamageApplied { get; }
        /// <summary>Sum of direct damage receipts, or null if any attempted hit lacked a receipt.</summary>
        public long? DirectDamageApplied { get; }
        /// <summary>Direct recovery from this action, or null for a result without execution details.</summary>
        public int? DirectRecoveryApplied { get; }
        public bool HasDetailedHpAmounts => DirectDamageApplied.HasValue && DirectRecoveryApplied.HasValue;
        public int? DamageAttempts { get; }
        public bool? RecoveryAttempted { get; }

        internal CombatSkillTargetResult(ICombatant target, int hpBefore, int hpAfter)
            : this(target, hpBefore, hpAfter, null, null, null, null)
        {
        }

        internal CombatSkillTargetResult(ICombatant target, int hpBefore, int hpAfter,
            long? directDamageApplied, int? directRecoveryApplied, int? damageAttempts, bool? recoveryAttempted)
        {
            Target = target;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
            DamageApplied = hpBefore > hpAfter ? hpBefore - hpAfter : 0;
            DirectDamageApplied = directDamageApplied;
            DirectRecoveryApplied = directRecoveryApplied;
            DamageAttempts = damageAttempts;
            RecoveryAttempted = recoveryAttempted;
        }
    }
}
