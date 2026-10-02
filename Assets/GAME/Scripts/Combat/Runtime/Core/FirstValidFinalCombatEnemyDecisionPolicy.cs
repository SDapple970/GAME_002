using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Deterministic FinalExchange V1 policy. It preserves authored roster and skill ordering
    /// and delegates legality to the shared declaration and target policies.
    /// </summary>
    public sealed class FirstValidFinalCombatEnemyDecisionPolicy : IFinalCombatEnemyDecisionPolicy
    {
        public bool TryCreateAttack(
            CombatSession session,
            ICombatant requestedActor,
            out CombatAttackDeclaration declaration)
        {
            declaration = null;
            ICombatant actor = requestedActor ?? FindFirstActiveEnemy(session);
            if (actor?.Side != Side.Enemies || actor.Skills == null)
                return false;

            for (int skillIndex = 0; skillIndex < actor.Skills.Count; skillIndex++)
            {
                ISkill skill = actor.Skills[skillIndex];
                if (!CanAfford(session, actor, skill))
                    continue;

                IReadOnlyList<ICombatant> targets = session.GetSide(Opposite(actor.Side));
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    CombatAttackDeclaration candidate = new CombatAttackDeclaration(actor, targets[targetIndex], skill);
                    if (!CombatDeclarationPolicy.CanDeclareAttack(session, candidate))
                        continue;

                    declaration = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryCreateResponse(
            CombatSession session,
            ICombatant responder,
            out CombatResponseDeclaration response)
        {
            response = null;
            CombatAttackDeclaration attack = session?.ExchangeState?.CurrentDeclaration;
            if (attack == null || responder?.Side != Side.Enemies || responder.Skills == null)
                return false;

            for (int skillIndex = 0; skillIndex < responder.Skills.Count; skillIndex++)
            {
                ISkill skill = responder.Skills[skillIndex];
                if (!CanAfford(session, responder, skill))
                    continue;

                CombatResponseDeclaration candidate = new CombatResponseDeclaration(responder, skill);
                if (!CombatDeclarationPolicy.CanDeclareResponse(session, attack, candidate))
                    continue;

                response = candidate;
                return true;
            }

            return false;
        }

        public bool CanContinue(CombatSession session, ICombatant actor)
        {
            return actor?.Side == Side.Enemies &&
                   CombatChainPolicy.CanContinueWithAnySkill(session, actor);
        }

        private static ICombatant FindFirstActiveEnemy(CombatSession session)
        {
            if (session == null)
                return null;

            IReadOnlyList<ICombatant> enemies = session.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                ICombatant enemy = enemies[i];
                if (CombatDeclarationPolicy.IsActiveMember(session, enemy))
                    return enemy;
            }

            return null;
        }

        private static bool CanAfford(CombatSession session, ICombatant actor, ISkill skill)
        {
            return session != null && actor != null && skill != null &&
                   session.TryGetCombatState(actor, out CombatantCombatState state) &&
                   state.CanSpendMp(CombatMpCostResolver.Resolve(skill));
        }

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }
    }
}
