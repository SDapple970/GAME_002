using System;
using System.Collections.Generic;
using Game.Combat.Core;

namespace Game.Combat.Model
{
    /// <summary>
    /// Opt-in clash state attached to an already committed FinalExchange.
    /// It never applies HP, posture, mental, status effects, or rewards.
    /// </summary>
    public sealed class CombatClashSeriesState
    {
        private readonly CombatSession _session;
        private readonly CombatStateMachine _stateMachine;
        private readonly CombatExchangeState _exchange;
        private readonly int _attachedExchangeVersion;
        private readonly CombatAttackDeclaration _firstAttack;
        private readonly CombatResponseDeclaration _firstResponse;
        private readonly List<ISkill> _committedAttackSkills = new List<ISkill>();
        private readonly List<ISkill> _committedResponseSkills = new List<ISkill>();
        private readonly List<CombatAttackDeclaration> _committedAttackActions = new();
        private readonly List<CombatResponseDeclaration> _committedResponseActions = new();
        private readonly int _maxComparisons;
        private CombatAttackDeclaration _currentAttack;
        private CombatResponseDeclaration _currentResponse;
        private bool _roundCommitted;
        private bool _finalOutcomeClaimed;

        public long CommandToken { get; private set; }
        public int ComparisonCount { get; private set; }
        public int AttackerDurability { get; private set; }
        public int ResponderDurability { get; private set; }
        public int AttackMpSpent { get; private set; }
        public int ResponseMpSpent { get; private set; }
        public bool IsComplete { get; private set; }
        public CombatClashOutcome? FinalOutcome { get; private set; }
        public bool CanClaimFinalAttack => IsComplete &&
            FinalOutcome != CombatClashOutcome.Tie && !_finalOutcomeClaimed;
        public IReadOnlyList<ISkill> CommittedAttackSkills => _committedAttackSkills.AsReadOnly();
        public IReadOnlyList<ISkill> CommittedResponseSkills => _committedResponseSkills.AsReadOnly();
        public ISkill CurrentAttackSkill => _currentAttack?.Skill;
        public ISkill CurrentResponseSkill => _currentResponse?.Skill;
        public int CurrentAttackBpInvestment => _currentAttack?.BpInvestment ?? 0;
        public int CurrentResponseBpInvestment => _currentResponse?.BpInvestment ?? 0;
        public bool IsRoundCommitted => _roundCommitted;

        public CombatClashSeriesState(
            CombatSession session,
            CombatStateMachine stateMachine,
            CombatClashSeriesConfig config)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _exchange = session.ExchangeState;
            if (!stateMachine.OwnsSession(session) || stateMachine.Phase != Phase.Clash ||
                session.FlowMode != CombatFlowMode.StandoffClashChain ||
                config.AttackerDurability <= 0 || config.ResponderDurability <= 0 ||
                config.MaxComparisons <= 0 || !_exchange.IsCommitted ||
                _exchange.ResponseState != CombatResponseState.CounterDeclared ||
                _exchange.CurrentDeclaration?.Attacker == null ||
                _exchange.CurrentResponse?.Responder == null ||
                !ReferenceEquals(_exchange.CurrentDeclaration.Target, _exchange.CurrentResponse.Responder) ||
                _exchange.CurrentClashResult != null || _exchange.IsOutcomePrepared)
            {
                throw new InvalidOperationException("A clash series requires a committed, unresolved FinalExchange with a response.");
            }

            _firstAttack = _exchange.CurrentDeclaration;
            _firstResponse = _exchange.CurrentResponse;
            _currentAttack = _firstAttack;
            _currentResponse = _firstResponse;
            _roundCommitted = true;
            _committedAttackSkills.Add(_firstAttack.Skill);
            _committedResponseSkills.Add(_firstResponse.Skill);
            _committedAttackActions.Add(_firstAttack);
            _committedResponseActions.Add(_firstResponse);
            AttackerDurability = config.AttackerDurability;
            ResponderDurability = config.ResponderDurability;
            _maxComparisons = config.MaxComparisons;
            AttackMpSpent = _exchange.CommittedAttackMpCost;
            ResponseMpSpent = _exchange.CommittedResponseMpCost;
            _attachedExchangeVersion = _exchange.Version < int.MaxValue
                ? _exchange.Version + 1
                : int.MaxValue;
            if (!_exchange.TryAttachClashSeries(this))
                throw new InvalidOperationException("A clash series is already attached to this exchange.");
        }

        public bool TryCommitNextRound(
            long expectedToken,
            ISkill attackSkill,
            ISkill responseSkill,
            out CombatExchangeCommitResult commitResult)
        {
            return TryCommitNextRound(expectedToken, attackSkill, responseSkill,
                null, null, out commitResult);
        }

        public bool TryCommitNextRound(
            long expectedToken,
            ISkill attackSkill,
            ISkill responseSkill,
            long? attackCardRuntimeId,
            long? responseCardRuntimeId,
            out CombatExchangeCommitResult commitResult)
        {
            return TryCommitNextRound(expectedToken, attackSkill, responseSkill,
                attackCardRuntimeId, responseCardRuntimeId, 0, 0, out commitResult);
        }

        public bool TryCommitNextRound(
            long expectedToken,
            ISkill attackSkill,
            ISkill responseSkill,
            long? attackCardRuntimeId,
            long? responseCardRuntimeId,
            int attackBpInvestment,
            int responseBpInvestment,
            out CombatExchangeCommitResult commitResult)
        {
            commitResult = null;
            if (!IsCurrent(expectedToken) || IsComplete || _roundCommitted ||
                ComparisonCount == 0 || attackSkill == null ||
                (responseSkill == null && responseBpInvestment != 0))
            {
                return false;
            }

            CombatAttackDeclaration attack = new CombatAttackDeclaration(
                _firstAttack.Attacker, _firstAttack.Target, attackSkill, attackCardRuntimeId,
                attackBpInvestment);
            CombatResponseDeclaration response = responseSkill == null
                ? null
                : new CombatResponseDeclaration(_firstResponse.Responder, responseSkill,
                    responseCardRuntimeId, responseBpInvestment);
            if (!CombatExchangeCommitPolicy.TryCommit(_session, attack, response,
                    CombatCardKind.Clash, out commitResult))
                return false;

            _currentAttack = attack;
            _currentResponse = commitResult.CommittedResponse;
            _committedAttackSkills.Add(attackSkill);
            _committedAttackActions.Add(attack);
            if (_currentResponse != null)
                _committedResponseActions.Add(_currentResponse);
            AttackMpSpent = AddClamped(AttackMpSpent, commitResult.AttackMpSpent);
            ResponseMpSpent = AddClamped(ResponseMpSpent, commitResult.ResponseMpSpent);
            _roundCommitted = true;
            CommandToken++;

            if (_currentResponse == null)
            {
                IsComplete = true;
                FinalOutcome = CombatClashOutcome.Unopposed;
            }

            return true;
        }

        public bool TryResolveRound(
            long expectedToken,
            CombatClashSideInput attacker,
            CombatClashSideInput responder,
            out CombatClashRoundResult result)
        {
            result = null;
            if (!IsCurrent(expectedToken) || IsComplete || !_roundCommitted ||
                _currentResponse == null || !MatchesCommittedSkills(attacker, responder))
            {
                return false;
            }

            int attackerPower = FinalPower(attacker,
                CombatBpEnhancementPolicy.Magnitude(_session, _currentAttack.Skill,
                    _currentAttack.BpInvestment, CombatBpEnhancementKind.ClashPower));
            int responderPower = FinalPower(responder,
                CombatBpEnhancementPolicy.Magnitude(_session, _currentResponse.Skill,
                    _currentResponse.BpInvestment, CombatBpEnhancementKind.ClashPower));
            int attackerBefore = AttackerDurability;
            int responderBefore = ResponderDurability;

            if (attackerPower > responderPower)
                ResponderDurability = Reduce(ResponderDurability, (long)attackerPower - responderPower);
            else if (responderPower > attackerPower)
                AttackerDurability = Reduce(AttackerDurability, (long)responderPower - attackerPower);
            else
            {
                AttackerDurability--;
                ResponderDurability--;
            }

            ComparisonCount++;
            _roundCommitted = false;
            CommandToken++;

            if (AttackerDurability == 0 && ResponderDurability == 0)
            {
                FinalOutcome = attacker.Speed != responder.Speed
                    ? (attacker.Speed > responder.Speed
                        ? CombatClashOutcome.AttackerWin : CombatClashOutcome.ResponderWin)
                    : (attacker.IsCurrentAttackOwner
                        ? CombatClashOutcome.AttackerWin : CombatClashOutcome.ResponderWin);
            }
            else if (AttackerDurability == 0)
                FinalOutcome = CombatClashOutcome.ResponderWin;
            else if (ResponderDurability == 0)
                FinalOutcome = CombatClashOutcome.AttackerWin;
            else if (ComparisonCount >= _maxComparisons)
                FinalOutcome = CombatClashOutcome.Tie;

            IsComplete = FinalOutcome.HasValue;
            result = new CombatClashRoundResult(
                ComparisonCount,
                attackerPower,
                responderPower,
                attackerBefore,
                AttackerDurability,
                responderBefore,
                ResponderDurability,
                IsComplete,
                FinalOutcome);
            return true;
        }

        // The caller must choose a committed skill explicitly; first versus last remains a policy decision.
        // A successful claim is single use. Recovery after a failed downstream execution is unresolved.
        public bool TryClaimFinalOutcome(
            long expectedToken,
            ISkill selectedSkill,
            out CombatClashResult result)
        {
            result = null;
            if (!IsCurrent(expectedToken) || !CanClaimFinalAttack || selectedSkill == null)
                return false;

            CombatClashOutcome outcome = FinalOutcome.Value;
            bool attackerWon = outcome == CombatClashOutcome.AttackerWin ||
                               outcome == CombatClashOutcome.Unopposed;
            List<ISkill> allowedSkills = attackerWon
                ? _committedAttackSkills : _committedResponseSkills;
            if (!ContainsReference(allowedSkills, selectedSkill))
                return false;

            CombatAttackDeclaration attack = attackerWon
                ? LastCommittedAttack(selectedSkill)
                : _firstAttack;
            CombatResponseDeclaration response = attackerWon
                ? (outcome == CombatClashOutcome.Unopposed ? null : _firstResponse)
                : LastCommittedResponse(selectedSkill);
            result = new CombatClashResult(
                outcome,
                attack,
                response,
                attackerWon ? _firstAttack.Attacker : _firstResponse.Responder);
            _finalOutcomeClaimed = true;
            CommandToken++;
            return true;
        }

        private bool IsCurrent(long expectedToken)
        {
            return expectedToken == CommandToken &&
                   _stateMachine.Phase == Phase.Clash &&
                   CombatEndEvaluator.Evaluate(_session) == CombatEndReason.None &&
                   _exchange.Version == _attachedExchangeVersion &&
                   ReferenceEquals(_exchange.ClashSeries, this) &&
                   ReferenceEquals(_exchange.CurrentDeclaration, _firstAttack);
        }

        private bool MatchesCommittedSkills(CombatClashSideInput attacker, CombatClashSideInput responder)
        {
            return attacker.ActorId.Value == _firstAttack.Attacker.Id.Value &&
                   responder.ActorId.Value == _firstResponse.Responder.Id.Value &&
                   attacker.SkillId.Value == _currentAttack.Skill.Id.Value &&
                   responder.SkillId.Value == _currentResponse.Skill.Id.Value &&
                   attacker.IsCurrentAttackOwner != responder.IsCurrentAttackOwner &&
                   attacker.IsCurrentAttackOwner ==
                       (_exchange.CurrentAttackSide == _firstAttack.Attacker.Side);
        }

        private CombatAttackDeclaration LastCommittedAttack(ISkill skill)
        {
            for (int i = _committedAttackActions.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_committedAttackActions[i].Skill, skill))
                    return _committedAttackActions[i];
            return null;
        }

        private CombatResponseDeclaration LastCommittedResponse(ISkill skill)
        {
            for (int i = _committedResponseActions.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_committedResponseActions[i].Skill, skill))
                    return _committedResponseActions[i];
            return null;
        }

        private static int FinalPower(CombatClashSideInput input, int bpBonus)
        {
            long power = (long)input.ClashPower + input.ClashModifier + bpBonus;
            return power <= 0 ? 0 : power >= int.MaxValue ? int.MaxValue : (int)power;
        }

        private static int Reduce(int durability, long amount)
        {
            return amount >= durability ? 0 : durability - (int)amount;
        }

        private static int AddClamped(int current, int amount)
        {
            long total = (long)current + amount;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        private static bool ContainsReference(IReadOnlyList<ISkill> skills, ISkill selected)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (ReferenceEquals(skills[i], selected))
                    return true;
            }

            return false;
        }
    }
}
