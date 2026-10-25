using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>Combat-owned skill permission and reason used by command and UI adapters.</summary>
    public static class CombatSkillAvailabilityPolicy
    {
        public static CombatSkillAvailability Evaluate(
            CombatSession session,
            CombatExchangeDecisionRequest request,
            ICombatant actor,
            ISkill skill,
            ISkill seriesAttackSkill = null)
        {
            return Evaluate(session, request, actor, skill, seriesAttackSkill, null);
        }

        public static CombatSkillAvailability Evaluate(
            CombatSession session,
            CombatExchangeDecisionRequest request,
            ICombatant actor,
            ISkill skill,
            ISkill seriesAttackSkill,
            long? cardRuntimeId)
        {
            if (session == null || request == null || actor == null || skill == null ||
                (!ReferenceEquals(actor, request.ActingActor) && request.ActingActor != null))
                return new CombatSkillAvailability(skill, CombatSkillUnavailableReason.InvalidPhase);

            if (!session.TryGetCombatState(actor, out CombatantCombatState state) ||
                actor.HP <= 0 || actor.IsStunned || state.IsPanicked)
                return new CombatSkillAvailability(skill, CombatSkillUnavailableReason.RestrictedByStatus);

            if (session.TryGetHand(actor, out CombatHandRuntime hand) &&
                request.Kind != CombatExchangeDecisionKind.Chain)
            {
                CombatCardKind required = request.Kind == CombatExchangeDecisionKind.Attack
                    ? CombatCardKind.Normal : CombatCardKind.Clash;
                if (!hand.TryFindInHand(skill, required, cardRuntimeId, out _))
                {
                    CombatCardKind other = required == CombatCardKind.Normal
                        ? CombatCardKind.Clash : CombatCardKind.Normal;
                    return new CombatSkillAvailability(skill,
                        hand.TryFindInHand(skill, other, cardRuntimeId, out _)
                            ? CombatSkillUnavailableReason.InvalidPhase
                            : CombatSkillUnavailableReason.NotInHand);
                }
            }

            if (!state.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
                return new CombatSkillAvailability(skill, CombatSkillUnavailableReason.InsufficientMp);

            CombatExchangeState exchange = session.ExchangeState;
            bool legal = false;
            switch (request.Kind)
            {
                case CombatExchangeDecisionKind.Attack:
                case CombatExchangeDecisionKind.ClashAttack:
                    if (request.Kind == CombatExchangeDecisionKind.ClashAttack)
                    {
                        CombatAttackDeclaration first = exchange.CurrentDeclaration;
                        legal = first != null && ReferenceEquals(first.Attacker, actor) &&
                            CombatDeclarationPolicy.CanDeclareAttack(session,
                                new CombatAttackDeclaration(actor, first.Target, skill));
                    }
                    else
                    {
                        var targets = session.GetSide(actor.Side == Side.Allies ? Side.Enemies : Side.Allies);
                        for (int i = 0; i < targets.Count; i++)
                        {
                            ICombatant target = targets[i];
                            if (target != null && CombatDeclarationPolicy.CanDeclareAttack(session,
                                    new CombatAttackDeclaration(actor, target, skill)))
                            {
                                legal = true;
                                break;
                            }
                        }
                    }
                    break;

                case CombatExchangeDecisionKind.Response:
                case CombatExchangeDecisionKind.ClashResponse:
                    CombatAttackDeclaration attack = exchange.CurrentDeclaration;
                    if (request.Kind == CombatExchangeDecisionKind.ClashResponse &&
                        attack != null && seriesAttackSkill != null)
                        attack = new CombatAttackDeclaration(attack.Attacker, attack.Target, seriesAttackSkill);
                    legal = attack != null && ReferenceEquals(attack.Target, actor) &&
                        CombatDeclarationPolicy.CanDeclareResponse(session, attack,
                            new CombatResponseDeclaration(actor, skill));
                    break;

                case CombatExchangeDecisionKind.Chain:
                    legal = CombatChainPolicy.CanContinue(session, actor, skill);
                    break;

                default:
                    return new CombatSkillAvailability(skill, CombatSkillUnavailableReason.InvalidPhase);
            }

            return new CombatSkillAvailability(skill,
                legal ? CombatSkillUnavailableReason.None : CombatSkillUnavailableReason.InvalidTarget);
        }
    }
}
