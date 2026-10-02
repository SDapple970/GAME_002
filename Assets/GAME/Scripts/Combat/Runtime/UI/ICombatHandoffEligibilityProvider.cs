using Game.Combat.Model;

namespace Game.Combat.UI
{
    /// <summary>
    /// Supplies party-specific handoff eligibility without giving the combat command layer
    /// ownership of level, trait, or relationship rules.
    /// </summary>
    public interface ICombatHandoffEligibilityProvider
    {
        bool IsEligible(
            CombatSession session,
            ICombatant currentAttackOwner,
            ICombatant receivingActor);
    }
}
