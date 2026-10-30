using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Applies an optional, snapshot-backed attack preference and otherwise preserves
    /// the deterministic FirstValid FinalExchange behavior.
    /// </summary>
    public sealed class ConditionalFinalCombatEnemyDecisionPolicy : IFinalCombatEnemyDecisionPolicy
    {
        private readonly FirstValidFinalCombatEnemyDecisionPolicy _fallback = new();

        public bool TryCreateAttack(
            CombatSession session,
            ICombatant requestedActor,
            out CombatAttackDeclaration declaration)
        {
            declaration = null;
            ICombatant actor = requestedActor ?? FindFirstActiveEnemy(session);
            if (session == null || actor?.Side != Side.Enemies ||
                !session.TryGetEnemyBehavior(actor, out EnemyCombatBehaviorSnapshot profile) ||
                !session.TryGetCombatState(actor, out CombatantCombatState state))
            {
                return _fallback.TryCreateAttack(session, requestedActor, out declaration);
            }

            IReadOnlyList<EnemyCombatAttackPrioritySnapshot> priorities = profile.AttackPriorities;
            for (int priorityIndex = 0; priorityIndex < priorities.Count; priorityIndex++)
            {
                EnemyCombatAttackPrioritySnapshot priority = priorities[priorityIndex];
                if (!priority.Matches(state) ||
                    !TryCreateAttackWithSkill(session, actor, priority.SkillId, out declaration))
                {
                    continue;
                }

                return true;
            }

            return _fallback.TryCreateAttack(session, requestedActor, out declaration);
        }

        public bool TryCreateResponse(
            CombatSession session,
            ICombatant responder,
            out CombatResponseDeclaration response)
        {
            return _fallback.TryCreateResponse(session, responder, out response);
        }

        public bool CanContinue(CombatSession session, ICombatant actor)
        {
            return _fallback.CanContinue(session, actor);
        }

        private static bool TryCreateAttackWithSkill(
            CombatSession session,
            ICombatant actor,
            int skillId,
            out CombatAttackDeclaration declaration)
        {
            declaration = null;
            if (session == null || actor?.Skills == null ||
                !session.TryGetCombatState(actor, out CombatantCombatState state))
            {
                return false;
            }

            for (int skillIndex = 0; skillIndex < actor.Skills.Count; skillIndex++)
            {
                ISkill skill = actor.Skills[skillIndex];
                if (skill == null || skill.Id.Value != skillId ||
                    (session.TryGetHand(actor, out CombatHandRuntime hand) &&
                     !hand.TryFindInHand(skill, CombatCardKind.Normal, null, out _)) ||
                    !state.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
                {
                    continue;
                }

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

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }
    }
}
