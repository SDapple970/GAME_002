using System;

namespace Game.Combat.Model
{
    /// <summary>
    /// The bounded input for a Legacy Planning enemy decision. The policy receives
    /// the session snapshot rather than any field or scene object.
    /// </summary>
    internal sealed class EnemyCombatPlanRequest
    {
        public CombatSession Session { get; }
        public ICombatant Enemy { get; }
        public CombatantCombatState RuntimeState { get; }

        public EnemyCombatPlanRequest(CombatSession session, ICombatant enemy)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));

            if (!session.TryGetCombatState(enemy, out CombatantCombatState runtimeState))
            {
                throw new ArgumentException(
                    "The enemy is not registered in the supplied combat session.",
                    nameof(enemy));
            }

            RuntimeState = runtimeState;
        }
    }
}
