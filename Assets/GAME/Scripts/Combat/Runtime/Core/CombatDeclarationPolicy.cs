using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatDeclarationPolicy
    {
        public static bool CanDeclareAttack(
            CombatSession session,
            CombatAttackDeclaration declaration)
        {
            if (session == null || declaration?.Attacker == null ||
                declaration.Target == null || declaration.Skill == null ||
                declaration.Attacker.Side == declaration.Target.Side ||
                FinalCombatTerminalPolicy.Evaluate(session) != CombatEndReason.None ||
                !IsActiveMember(session, declaration.Attacker) ||
                IsPanicked(session, declaration.Attacker) ||
                !IsLivingMember(session, declaration.Target) ||
                declaration.Attacker.IsStunned ||
                !OwnsSkill(declaration.Attacker, declaration.Skill))
            {
                return false;
            }

            return CombatOutcomeTargetResolver.CanResolve(
                declaration.Attacker,
                declaration.Skill,
                declaration.Target,
                session);
        }

        public static bool CanDeclareResponse(
            CombatSession session,
            CombatAttackDeclaration attack,
            CombatResponseDeclaration response)
        {
            if (!CanDeclareAttack(session, attack) ||
                response?.Responder == null || response.Skill == null ||
                !ReferenceEquals(response.Responder, attack.Target) ||
                response.Responder.Side == attack.Attacker.Side ||
                !IsActiveMember(session, response.Responder) ||
                IsPanicked(session, response.Responder) ||
                response.Responder.IsStunned ||
                !OwnsSkill(response.Responder, response.Skill))
            {
                return false;
            }

            return CombatOutcomeTargetResolver.CanResolve(
                response.Responder,
                response.Skill,
                attack.Attacker,
                session);
        }

        internal static bool OwnsSkill(ICombatant combatant, ISkill skill)
        {
            if (combatant?.Skills == null || skill == null)
                return false;

            for (int i = 0; i < combatant.Skills.Count; i++)
            {
                if (ReferenceEquals(combatant.Skills[i], skill))
                    return true;
            }

            return false;
        }

        internal static bool IsActiveMember(CombatSession session, ICombatant combatant)
        {
            return IsLivingMember(session, combatant) && !combatant.IsStunned;
        }

        internal static bool IsLivingMember(CombatSession session, ICombatant combatant)
        {
            return session != null && combatant != null &&
                   session.TryGetCombatState(combatant, out CombatantCombatState state) &&
                   state.IsAlive;
        }

        internal static bool IsPanicked(CombatSession session, ICombatant combatant)
        {
            return session != null && combatant != null &&
                   session.TryGetCombatState(combatant, out CombatantCombatState state) &&
                   state.IsPanicked;
        }
    }
}
