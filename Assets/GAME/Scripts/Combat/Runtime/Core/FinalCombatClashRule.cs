using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class FinalCombatClashRule : ICombatClashRule
    {
        private readonly CombatClashSideInput _attacker;
        private readonly CombatClashSideInput _responder;
        private readonly ICombatRuleRandomSource _randomSource;

        public CombatClashResolution LastResolution { get; private set; }

        public FinalCombatClashRule(
            CombatClashSideInput attacker,
            CombatClashSideInput responder,
            ICombatRuleRandomSource randomSource)
        {
            if (attacker.IsCurrentAttackOwner == responder.IsCurrentAttackOwner)
            {
                throw new ArgumentException(
                    "Exactly one clash participant must own the current attack authority.");
            }

            _attacker = attacker;
            _responder = responder;
            _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public CombatClashOutcome Resolve(CombatClashRequest request)
        {
            LastResolution = null;
            ValidateRequest(request);

            int attackerFirstVariance = NextVariance();
            int responderFirstVariance = NextVariance();
            int attackerFirstScore = CalculateScore(_attacker, attackerFirstVariance);
            int responderFirstScore = CalculateScore(_responder, responderFirstVariance);

            if (attackerFirstScore != responderFirstScore)
            {
                return StoreResolution(
                    request,
                    attackerFirstScore > responderFirstScore,
                    attackerFirstVariance,
                    responderFirstVariance,
                    attackerFirstScore,
                    responderFirstScore,
                    false,
                    null,
                    null,
                    null,
                    null,
                    CombatClashDecisionStage.FirstScore,
                    2);
            }

            int attackerReClashVariance = NextVariance();
            int responderReClashVariance = NextVariance();
            int attackerReClashScore = CalculateScore(_attacker, attackerReClashVariance);
            int responderReClashScore = CalculateScore(_responder, responderReClashVariance);

            if (attackerReClashScore != responderReClashScore)
            {
                return StoreResolution(
                    request,
                    attackerReClashScore > responderReClashScore,
                    attackerFirstVariance,
                    responderFirstVariance,
                    attackerFirstScore,
                    responderFirstScore,
                    true,
                    attackerReClashVariance,
                    responderReClashVariance,
                    attackerReClashScore,
                    responderReClashScore,
                    CombatClashDecisionStage.ReClashScore,
                    4);
            }

            if (_attacker.Speed != _responder.Speed)
            {
                return StoreResolution(
                    request,
                    _attacker.Speed > _responder.Speed,
                    attackerFirstVariance,
                    responderFirstVariance,
                    attackerFirstScore,
                    responderFirstScore,
                    true,
                    attackerReClashVariance,
                    responderReClashVariance,
                    attackerReClashScore,
                    responderReClashScore,
                    CombatClashDecisionStage.Speed,
                    4);
            }

            return StoreResolution(
                request,
                _attacker.IsCurrentAttackOwner,
                attackerFirstVariance,
                responderFirstVariance,
                attackerFirstScore,
                responderFirstScore,
                true,
                attackerReClashVariance,
                responderReClashVariance,
                attackerReClashScore,
                responderReClashScore,
                CombatClashDecisionStage.CurrentAttackOwner,
                4);
        }

        private static int CalculateScore(CombatClashSideInput input, int variance)
        {
            long rawScore = (long)input.ClashPower + input.ClashModifier + variance;
            if (rawScore <= 0L)
                return 0;

            return rawScore >= int.MaxValue ? int.MaxValue : (int)rawScore;
        }

        private int NextVariance()
        {
            int variance = _randomSource.NextInclusive(-1, 1);
            if (variance < -1 || variance > 1)
                throw new InvalidOperationException("Combat variance must be -1, 0, or +1.");

            return variance;
        }

        private CombatClashOutcome StoreResolution(
            CombatClashRequest request,
            bool attackerWon,
            int attackerFirstVariance,
            int responderFirstVariance,
            int attackerFirstScore,
            int responderFirstScore,
            bool reClashOccurred,
            int? attackerReClashVariance,
            int? responderReClashVariance,
            int? attackerReClashScore,
            int? responderReClashScore,
            CombatClashDecisionStage decisionStage,
            int randomSampleCount)
        {
            CombatClashOutcome outcome = attackerWon
                ? CombatClashOutcome.AttackerWin
                : CombatClashOutcome.ResponderWin;
            Side winnerSide = attackerWon
                ? request.Attacker.Side
                : request.ResponseDeclaration.Responder.Side;

            LastResolution = new CombatClashResolution(
                outcome,
                attackerFirstVariance,
                responderFirstVariance,
                attackerFirstScore,
                responderFirstScore,
                reClashOccurred,
                attackerReClashVariance,
                responderReClashVariance,
                attackerReClashScore,
                responderReClashScore,
                decisionStage,
                winnerSide,
                randomSampleCount);
            return outcome;
        }

        private void ValidateRequest(CombatClashRequest request)
        {
            CombatAttackDeclaration attack = request?.AttackDeclaration;
            CombatResponseDeclaration response = request?.ResponseDeclaration;
            if (request.ResponseState != CombatResponseState.CounterDeclared ||
                attack?.Attacker == null || attack.Skill == null ||
                response?.Responder == null || response.Skill == null ||
                !ReferenceEquals(response.Responder, attack.Target) ||
                attack.Attacker.Id.Value != _attacker.ActorId.Value ||
                attack.Skill.Id.Value != _attacker.SkillId.Value ||
                response.Responder.Id.Value != _responder.ActorId.Value ||
                response.Skill.Id.Value != _responder.SkillId.Value)
            {
                throw new ArgumentException("Clash request does not match the configured pure rule input.");
            }
        }
    }
}
