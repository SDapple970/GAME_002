using System;

namespace Game.Combat.Model
{
    public sealed class EnemyCombatDecisionRequest
    {
        public CombatSession Session { get; }
        public StandoffRuntimeState StandoffState => Session.StandoffState;

        public EnemyCombatDecisionRequest(CombatSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }
    }
}
