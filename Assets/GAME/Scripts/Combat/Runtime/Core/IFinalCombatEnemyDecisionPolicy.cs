using Game.Combat.Model;

namespace Game.Combat.Core
{
    public interface IFinalCombatEnemyDecisionPolicy
    {
        bool TryCreateAttack(
            CombatSession session,
            ICombatant requestedActor,
            out CombatAttackDeclaration declaration);

        bool TryCreateResponse(
            CombatSession session,
            ICombatant responder,
            out CombatResponseDeclaration response);

        bool CanContinue(CombatSession session, ICombatant actor);
    }
}
