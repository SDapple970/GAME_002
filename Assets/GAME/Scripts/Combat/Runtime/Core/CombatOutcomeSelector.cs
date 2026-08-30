using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatOutcomeSelector
    {
        public static bool TrySelect(CombatClashResult result, out CombatOutcomeAction action)
        {
            action = null;
            CombatAttackDeclaration attack = result?.AttackDeclaration;
            if (attack?.Attacker == null || attack.Target == null || attack.Skill == null)
                return false;

            switch (result.Outcome)
            {
                case CombatClashOutcome.AttackerWin:
                case CombatClashOutcome.Unopposed:
                    action = new CombatOutcomeAction(
                        attack.Attacker,
                        attack.Skill,
                        attack.Target,
                        result.Outcome);
                    return true;

                case CombatClashOutcome.ResponderWin:
                    CombatResponseDeclaration response = result.ResponseDeclaration;
                    if (response?.Responder == null || response.Skill == null ||
                        !ReferenceEquals(response.Responder, attack.Target))
                    {
                        return false;
                    }

                    action = new CombatOutcomeAction(
                        response.Responder,
                        response.Skill,
                        attack.Attacker,
                        result.Outcome);
                    return true;

                case CombatClashOutcome.Tie:
                    return result.ResponseDeclaration?.Responder != null &&
                           result.ResponseDeclaration.Skill != null &&
                           ReferenceEquals(result.ResponseDeclaration.Responder, attack.Target);

                default:
                    return false;
            }
        }
    }
}
