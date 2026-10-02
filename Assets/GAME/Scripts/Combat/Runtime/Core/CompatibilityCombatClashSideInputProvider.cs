using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class CompatibilityCombatClashSideInputProvider : ICombatClashSideInputProvider
    {
        public bool TryCreate(
            ICombatant actor,
            ISkill skill,
            bool isCurrentAttackOwner,
            out CombatClashSideInput input)
        {
            input = default;
            if (actor == null || skill == null)
                return false;

            // ClashPower remains unauthored until COMBAT-06. Do not infer it from damage.
            input = new CombatClashSideInput(
                actor.Id,
                skill.Id,
                0,
                0,
                skill.Speed,
                isCurrentAttackOwner);
            return true;
        }
    }
}
