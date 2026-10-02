using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Converts selected FinalExchange skills into authored clash inputs.
    /// Damage deliberately remains outside the clash calculation.
    /// </summary>
    public sealed class AuthoredCombatClashSideInputProvider : ICombatClashSideInputProvider
    {
        public bool TryCreate(
            ICombatant actor,
            ISkill skill,
            bool isCurrentAttackOwner,
            out CombatClashSideInput input)
        {
            input = default;
            if (actor == null || !(skill is IFinalCombatSkillStats finalStats))
                return false;

            input = new CombatClashSideInput(
                actor.Id,
                skill.Id,
                finalStats.ClashPower < 0 ? 0 : finalStats.ClashPower,
                0,
                skill.Speed,
                isCurrentAttackOwner);
            return true;
        }
    }
}
