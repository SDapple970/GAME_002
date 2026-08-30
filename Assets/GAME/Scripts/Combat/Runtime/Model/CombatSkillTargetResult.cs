namespace Game.Combat.Model
{
    public sealed class CombatSkillTargetResult
    {
        public ICombatant Target { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public int DamageApplied { get; }

        internal CombatSkillTargetResult(ICombatant target, int hpBefore, int hpAfter)
        {
            Target = target;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
            DamageApplied = hpBefore > hpAfter ? hpBefore - hpAfter : 0;
        }
    }
}
