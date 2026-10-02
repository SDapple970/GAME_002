using Game.Combat.Model;

namespace Game.Combat.Core
{
    public interface ICombatClashSideInputProvider
    {
        bool TryCreate(
            ICombatant actor,
            ISkill skill,
            bool isCurrentAttackOwner,
            out CombatClashSideInput input);
    }
}
