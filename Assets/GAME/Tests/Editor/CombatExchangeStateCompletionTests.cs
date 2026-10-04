#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    public sealed class CombatExchangeStateCompletionTests
    {
        [Test]
        public void StateGraph_EnterCombatTransitionsToCleanStandoff()
        {
            Fixture fixture = CreateFixture();

            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Exchange.IsChainActive, Is.False);
            Assert.That(fixture.Exchange.HandoffCount, Is.Zero);
        }

        [Test]
        public void StateGraph_StandoffDeclarationRequiresAuthorityAndAffordableSkill()
        {
            Fixture fixture = CreateStartedFixture(initialMp: 2, attackMpCost: 2);
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Enemy, fixture.Ally, fixture.EnemySkill),
                version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));

            fixture.Session.GetCombatState(fixture.Ally).SetMp(1);
            Assert.That(fixture.StateMachine.TryDeclareAttack(Attack(fixture), version), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));

            fixture.Session.GetCombatState(fixture.Ally).SetMp(2);
            Assert.That(fixture.StateMachine.TryDeclareAttack(Attack(fixture), version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(fixture.Ally));
        }

        [Test]
        public void StateGraph_ResponsePathTransitionsThroughApproachClashAndApplyOutcome()
        {
            Fixture fixture = CreateStartedFixture();
            DeclareInitialAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(
                Response(fixture),
                fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(fixture.StateMachine.CompleteApproach(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));

            Assert.That(ResolveClash(fixture, attackerWins: true), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void StateGraph_NoResponseSkipsClashAndEntersApplyOutcome()
        {
            Fixture fixture = CreateStartedFixture();
            PrepareNoResponseToApplyOutcome(fixture, fixture.Ally, fixture.Enemy, fixture.AllySkill);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.Exchange.CurrentClashResult.Outcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(fixture.Exchange.CurrentClashResult.Winner, Is.SameAs(fixture.Ally));
        }

        [Test]
        public void Mental_ActualClashAndDamageAreAggregatedAndResolvedExactlyOnce()
        {
            Fixture fixture = CreateStartedFixture(attackDamage: 1);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareOutcomeExecution(fixture);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FinalCombatPostureRule()), Is.True);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            int version = fixture.Exchange.Version;
            Assert.That(fixture.StateMachine.TryResolveMental(version), Is.True);

            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMental, Is.EqualTo(85));
            Assert.That(fixture.Exchange.CurrentMentalResults, Has.Count.EqualTo(1));
            Assert.That(fixture.Exchange.CurrentMentalResults[0].RequestedDelta, Is.EqualTo(-15));
            Assert.That(fixture.StateMachine.TryResolveMental(fixture.Exchange.Version), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMental, Is.EqualTo(85));
        }

        [Test]
        public void Mental_UnopposedDamageDoesNotApplyClashLoss()
        {
            Fixture fixture = CreateStartedFixture(attackDamage: 1);
            PrepareNoResponseToApplyOutcome(fixture, fixture.Ally, fixture.Enemy, fixture.AllySkill);
            PrepareOutcomeExecution(fixture);
            Assert.That(fixture.StateMachine.TryResolvePosture(null), Is.True);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            Assert.That(fixture.StateMachine.TryResolveMental(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMental, Is.EqualTo(95));
            Assert.That(fixture.Exchange.CurrentMentalResults, Has.Count.EqualTo(1));
            Assert.That(fixture.Exchange.CurrentMentalResults[0].RequestedDelta, Is.EqualTo(-5));
        }

        [Test]
        public void Commit_InsufficientResponseMpDowngradesToNoResponse()
        {
            Fixture fixture = CreateStartedFixture(initialMp: 3, attackMpCost: 2, responseMpCost: 4);
            DeclareInitialAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(Response(fixture), fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.True);

            Assert.That(fixture.Exchange.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(fixture.Exchange.CurrentResponse, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void Commit_IsExactlyOnceForAttackAndResponse()
        {
            Fixture fixture = CreateStartedFixture(initialMp: 5, attackMpCost: 2, responseMpCost: 3);
            DeclareInitialAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(Response(fixture), fixture.Exchange.Version), Is.True);
            int commitVersion = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryCommitExchange(commitVersion), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(commitVersion), Is.False);
            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(3));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(2));
        }

        [Test]
        public void Commit_InvalidResponseDoesNotMutateResourcesOrExchange()
        {
            Fixture fixture = CreateStartedFixture(initialMp: 5, attackMpCost: 2, responseMpCost: 2);
            DeclareInitialAttack(fixture);
            TestSkill invalidSkill = AddSkill(fixture.Enemy, 19, TargetingRule.SingleAlly, 2, 0);
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryDeclareResponse(
                new CombatResponseDeclaration(fixture.Enemy, invalidSkill),
                version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.Exchange.ResponseState, Is.EqualTo(CombatResponseState.Pending));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(5));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(5));
        }

        [Test]
        public void Approach_StaleAndDuplicateCompletionAreRejected()
        {
            Fixture fixture = CreateStartedFixture(initialMp: 5, attackMpCost: 2);
            DeclareInitialAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.True);
            int beforeApproach = fixture.Exchange.Version;
            Assert.That(fixture.StateMachine.TryBeginApproach(beforeApproach), Is.True);
            int approachVersion = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.CompleteApproach(beforeApproach), Is.False);
            Assert.That(fixture.StateMachine.CompleteApproach(approachVersion), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(approachVersion), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void Clash_DuplicateResolutionDoesNotRerollOrMutateState()
        {
            Fixture fixture = CreateStartedFixture();
            PrepareCounterToClash(fixture);
            FakeRandomSource random = new FakeRandomSource(0, 0);
            FinalCombatClashRule rule = ClashRule(fixture, 3, 1, random);
            int clashVersion = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryResolveClash(rule, clashVersion), Is.True);
            CombatClashResult stored = fixture.Exchange.CurrentClashResult;
            Assert.That(fixture.StateMachine.TryResolveClash(rule, clashVersion), Is.False);
            Assert.That(fixture.StateMachine.TryResolveClash(rule, fixture.Exchange.Version), Is.False);
            Assert.That(fixture.Exchange.CurrentClashResult, Is.SameAs(stored));
            Assert.That(random.CallCount, Is.EqualTo(2));
        }

        [Test]
        public void ApplyOutcome_NonTerminalEntersChainDecisionAndRejectsDuplicateFinalize()
        {
            Fixture fixture = CreateStartedFixture();
            PrepareNoResponseToApplyOutcome(fixture, fixture.Ally, fixture.Enemy, fixture.AllySkill);
            PrepareAftermathDecision(fixture);
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), version), Is.True);
            int finalizedVersion = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Exchange.IsApplyOutcomeFinalized, Is.True);
            Assert.That(fixture.Exchange.IsChainActive, Is.True);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(finalizedVersion));
        }

        [Test]
        public void Authority_AttackerWinAndNoResponseKeepAttackAuthority()
        {
            Fixture clashFixture = CreateStartedFixture();
            PrepareCounterToApplyOutcome(clashFixture, attackerWins: true);
            PrepareAftermathDecision(clashFixture);
            Assert.That(clashFixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), clashFixture.Exchange.Version), Is.True);

            Fixture noResponseFixture = CreateStartedFixture();
            PrepareNoResponseToApplyOutcome(
                noResponseFixture,
                noResponseFixture.Ally,
                noResponseFixture.Enemy,
                noResponseFixture.AllySkill);
            PrepareAftermathDecision(noResponseFixture);
            Assert.That(noResponseFixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), noResponseFixture.Exchange.Version), Is.True);

            Assert.That(clashFixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(clashFixture.Exchange.CurrentAttackActor, Is.SameAs(clashFixture.Ally));
            Assert.That(noResponseFixture.Exchange.CurrentAttackActor, Is.SameAs(noResponseFixture.Ally));
        }

        [Test]
        public void Authority_DefenderWinTransfersOwnerAndRejectsLosingSideNextDeclaration()
        {
            Fixture fixture = CreateStartedFixture();
            PrepareCounterToApplyOutcome(fixture, attackerWins: false);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);

            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Exchange.ChainOwner, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.StateMachine.TryContinueChain(
                fixture.EnemySkill,
                fixture.Exchange.Version), Is.True);

            int version = fixture.Exchange.Version;
            Assert.That(fixture.StateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllySkill),
                version), Is.False);
            Assert.That(fixture.StateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Enemy, fixture.Ally, fixture.EnemySkill),
                version), Is.True);
        }

        [Test]
        public void Chain_ContinueClearsExchangeAndPreservesSequenceAndHandoffCount()
        {
            Fixture fixture = CreateChainDecisionFixture();
            int chainSequence = fixture.Exchange.ChainSequenceId;
            CombatClashResult previous = fixture.Exchange.CurrentClashResult;
            Assert.That(previous, Is.Not.Null);

            Assert.That(fixture.StateMachine.TryContinueChain(
                fixture.AllySkill,
                fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Exchange.CurrentResponse, Is.Null);
            Assert.That(fixture.Exchange.CurrentClashResult, Is.Null);
            Assert.That(fixture.Exchange.IsChainActive, Is.True);
            Assert.That(fixture.Exchange.ChainSequenceId, Is.EqualTo(chainSequence));
            Assert.That(fixture.Exchange.HandoffCount, Is.Zero);
        }

        [Test]
        public void Chain_InsufficientMpRejectsContinueButVoluntaryEndStillSucceeds()
        {
            Fixture fixture = CreateChainDecisionFixture(initialMp: 2, attackMpCost: 2);
            fixture.Session.GetCombatState(fixture.Ally).SetMp(0);
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryContinueChain(fixture.AllySkill, version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.StateMachine.TryEndChain(version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
        }

        [Test]
        public void Chain_EndReturnsToStandoffAndResetsChainMetadataWithoutRecoveringMp()
        {
            Fixture fixture = CreateChainDecisionFixture();
            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                fixture.Exchange.Version), Is.True);
            PrepareNoResponseToApplyOutcome(
                fixture,
                fixture.AllyTwo,
                fixture.Enemy,
                fixture.AllyTwoSkill);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.Exchange.HandoffCount, Is.EqualTo(1));
            fixture.Session.GetCombatState(fixture.Ally).SetMp(1);

            Assert.That(fixture.StateMachine.TryEndChain(fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Exchange.IsChainActive, Is.False);
            Assert.That(fixture.Exchange.ChainOwner, Is.Null);
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.Null);
            Assert.That(fixture.Exchange.HandoffCount, Is.Zero);
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(1));

            Assert.That(fixture.StateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllySkill),
                fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
        }

        [Test]
        public void Chain_DuplicateContinueCallbackIsRejectedWithoutMutation()
        {
            Fixture fixture = CreateChainDecisionFixture();
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryContinueChain(fixture.AllySkill, version), Is.True);
            int acceptedVersion = fixture.Exchange.Version;
            Assert.That(fixture.StateMachine.TryContinueChain(fixture.AllySkill, version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(acceptedVersion));
        }

        [Test]
        public void Handoff_EligibleTeammateChangesOwnerWithoutSpendingReceiverMp()
        {
            Fixture fixture = CreateChainDecisionFixture();
            int receiverMp = fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp;

            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(fixture.AllyTwo));
            Assert.That(fixture.Exchange.ChainOwner, Is.SameAs(fixture.AllyTwo));
            Assert.That(fixture.Exchange.HandoffCount, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp, Is.EqualTo(receiverMp));

            Assert.That(fixture.StateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.AllyTwo, fixture.Enemy, fixture.AllyTwoSkill),
                fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp, Is.EqualTo(receiverMp - 1));
        }

        [Test]
        public void Handoff_DeadStunnedOrIneligibleReceiverLeavesStateUnchanged()
        {
            Fixture dead = CreateChainDecisionFixture();
            dead.AllyTwo.ApplyDamage(int.MaxValue);
            AssertRejectedHandoffIsStable(dead, true);

            Fixture stunned = CreateChainDecisionFixture();
            stunned.AllyTwo.SetStunned(true);
            AssertRejectedHandoffIsStable(stunned, true);

            Fixture ineligible = CreateChainDecisionFixture();
            AssertRejectedHandoffIsStable(ineligible, false);
        }

        [Test]
        public void Handoff_SecondHandoffInSameChainIsRejected()
        {
            Fixture fixture = CreateChainDecisionFixture();
            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                fixture.Exchange.Version), Is.True);

            PrepareNoResponseToApplyOutcome(
                fixture,
                fixture.AllyTwo,
                fixture.Enemy,
                fixture.AllyTwoSkill);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.Ally,
                fixture.AllySkill,
                true,
                version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.Exchange.HandoffCount, Is.EqualTo(1));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(fixture.AllyTwo));
        }

        [Test]
        public void Handoff_DuplicateCallbackIsRejected()
        {
            Fixture fixture = CreateChainDecisionFixture();
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                version), Is.True);
            int acceptedVersion = fixture.Exchange.Version;
            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(acceptedVersion));
        }

        [Test]
        public void Terminal_VictoryAfterOutcomeSkipsChainDecision()
        {
            Fixture fixture = CreateStartedFixture(attackDamage: 10);
            PrepareNoResponseToApplyOutcome(fixture, fixture.Ally, fixture.Enemy, fixture.AllySkill);
            PrepareAftermathDecision(fixture);

            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(fixture.Exchange.IsChainActive, Is.False);
        }

        [Test]
        public void Terminal_DefeatAfterDefenderOutcomeSkipsChainDecision()
        {
            Fixture fixture = CreateStartedFixture(responseDamage: 10);
            PrepareCounterToApplyOutcome(fixture, attackerWins: false);
            fixture.AllyTwo.ApplyDamage(int.MaxValue);
            PrepareAftermathDecision(fixture);

            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Defeat));
        }

        [Test]
        public void Terminal_BothWipedResolvesToDefeat()
        {
            Fixture fixture = CreateStartedFixture();
            PrepareNoResponseToApplyOutcome(fixture, fixture.Ally, fixture.Enemy, fixture.AllySkill);
            PrepareOutcomeExecution(fixture);
            fixture.Ally.ApplyDamage(int.MaxValue);
            fixture.AllyTwo.ApplyDamage(int.MaxValue);
            fixture.Enemy.ApplyDamage(int.MaxValue);
            ResolvePostureAndPrepareAftermath(fixture);

            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Defeat));
        }

        [Test]
        public void Terminal_AllOutCandidateDoesNotExitCombat()
        {
            Fixture fixture = CreateStartedFixture(maxPosture: 1);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareAftermathDecision(fixture);

            Assert.That(fixture.Exchange.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.AllOutCandidate));
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.Exchange.IsAllOutCandidate, Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void AllOut_UsesAuthoredAreaSkillThenRoutesVictoryThroughTerminalPolicy()
        {
            Fixture fixture = CreateStartedFixture(maxPosture: 1);
            TestSkill allOutSkill = AddSkill(
                fixture.Ally,
                99,
                TargetingRule.AllEnemies,
                0,
                10);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);

            int version = fixture.Exchange.Version;
            int mentalBeforeAllOut = fixture.Session.GetCombatState(fixture.Enemy).CurrentMental;
            Assert.That(fixture.StateMachine.TryExecuteAllOut(version, out CombatSkillExecutionResult result), Is.True);

            Assert.That(result.Actor, Is.SameAs(fixture.Ally));
            Assert.That(result.Skill, Is.SameAs(allOutSkill));
            Assert.That(result.TargetResults, Has.Count.EqualTo(1));
            Assert.That(result.TargetResults[0].Target, Is.SameAs(fixture.Enemy));
            Assert.That(result.TargetResults[0].DamageApplied, Is.EqualTo(10));
            Assert.That(fixture.Enemy.HP, Is.Zero);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMental,
                Is.EqualTo(mentalBeforeAllOut - 5));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(fixture.StateMachine.TryExecuteAllOut(version, out _), Is.False);
        }

        [Test]
        public void AllOut_StaleOrUnavailableRequestDoesNotMutateCombatState()
        {
            Fixture fixture = CreateStartedFixture(maxPosture: 1);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);

            int version = fixture.Exchange.Version;
            int enemyHp = fixture.Enemy.HP;
            Assert.That(fixture.StateMachine.TryExecuteAllOut(version - 1, out _), Is.False);
            Assert.That(fixture.StateMachine.TryExecuteAllOut(version, out _), Is.False);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        [Test]
        public void AllOut_WhenEnemiesSurvive_ReturnsToNormalStandoff()
        {
            Fixture fixture = CreateStartedFixture(maxPosture: 1);
            AddSkill(fixture.Ally, 99, TargetingRule.AllEnemies, 0, 1);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.TryExecuteAllOut(
                fixture.Exchange.Version,
                out CombatSkillExecutionResult result), Is.True);

            Assert.That(result.TargetResults[0].DamageApplied, Is.EqualTo(1));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(9));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Exchange.IsAllOutCandidate, Is.False);
        }

        [Test]
        public void AllOut_TargetsEveryLivingStunnedEnemy()
        {
            Fixture fixture = CreateStartedFixture(maxPosture: 1, includeSecondEnemy: true);
            AddSkill(fixture.Ally, 99, TargetingRule.AllEnemies, 0, 10);
            fixture.EnemyTwo.SetStunned(true);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.TryExecuteAllOut(
                fixture.Exchange.Version,
                out CombatSkillExecutionResult result), Is.True);

            Assert.That(result.TargetResults, Has.Count.EqualTo(2));
            Assert.That(fixture.Enemy.HP, Is.Zero);
            Assert.That(fixture.EnemyTwo.HP, Is.Zero);
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Victory));
        }

        [Test]
        public void AllOutEligibility_IgnoresDefeatedEnemies()
        {
            Fixture fixture = CreateStartedFixture(includeSecondEnemy: true);
            fixture.Enemy.ApplyDamage(int.MaxValue);
            fixture.EnemyTwo.SetStunned(true);

            Assert.That(CombatAllOutPolicy.IsCandidate(fixture.Session), Is.True);
            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session),
                Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void Terminal_StunAloneWithAnotherLivingEnemyIsNotTerminal()
        {
            Fixture fixture = CreateStartedFixture(maxPosture: 1, includeSecondEnemy: true);
            PrepareCounterToApplyOutcome(fixture, attackerWins: true);
            PrepareAftermathDecision(fixture);

            Assert.That(fixture.Exchange.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.ChainDecisionRequired));
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Exchange.IsAllOutCandidate, Is.False);
        }

        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        [TestCase(CombatEndReason.Scripted)]
        public void Terminal_ExplicitReasonExitsExactlyOnce(CombatEndReason reason)
        {
            Fixture fixture = CreateStartedFixture();
            int version = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryTerminateExplicit(reason, version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(reason));
            Assert.That(fixture.StateMachine.TryTerminateExplicit(reason, version), Is.False);
        }

        [Test]
        public void InvalidTransitionsDoNotMutateMpPostureOrExchangeVersion()
        {
            Fixture fixture = CreateStartedFixture();
            int version = fixture.Exchange.Version;
            int mp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int posture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;

            Assert.That(fixture.StateMachine.TryCommitExchange(version), Is.False);
            Assert.That(fixture.StateMachine.CompleteApproach(version), Is.False);
            Assert.That(fixture.StateMachine.TryContinueChain(fixture.AllySkill, version), Is.False);
            Assert.That(fixture.StateMachine.TryEndChain(version), Is.False);

            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(posture));
        }

        [Test]
        public void Sequence_StaleVersionCannotOperateOnNewExchange()
        {
            Fixture fixture = CreateChainDecisionFixture();
            int oldVersion = fixture.Exchange.Version;
            Assert.That(fixture.StateMachine.TryContinueChain(fixture.AllySkill, oldVersion), Is.True);
            int nextVersion = fixture.Exchange.Version;

            Assert.That(fixture.StateMachine.TryDeclareAttack(Attack(fixture), oldVersion), Is.False);
            Assert.That(fixture.StateMachine.TryDeclareAttack(Attack(fixture), nextVersion), Is.True);
            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.Ally));
        }

        private static Fixture CreateChainDecisionFixture(
            int initialMp = 5,
            int attackMpCost = 1)
        {
            Fixture fixture = CreateStartedFixture(initialMp: initialMp, attackMpCost: attackMpCost);
            PrepareNoResponseToApplyOutcome(fixture, fixture.Ally, fixture.Enemy, fixture.AllySkill);
            PrepareAftermathDecision(fixture);
            Assert.That(fixture.StateMachine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            return fixture;
        }

        private static void DeclareInitialAttack(Fixture fixture)
        {
            Assert.That(fixture.StateMachine.TryDeclareAttack(
                Attack(fixture),
                fixture.Exchange.Version), Is.True);
        }

        private static void PrepareCounterToClash(Fixture fixture)
        {
            DeclareInitialAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(
                Response(fixture),
                fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
        }

        private static void PrepareCounterToApplyOutcome(Fixture fixture, bool attackerWins)
        {
            PrepareCounterToClash(fixture);
            Assert.That(ResolveClash(fixture, attackerWins), Is.True);
        }

        private static bool ResolveClash(Fixture fixture, bool attackerWins)
        {
            FinalCombatClashRule rule = ClashRule(
                fixture,
                attackerWins ? 3 : 1,
                attackerWins ? 1 : 3,
                new FakeRandomSource(0, 0));
            return fixture.StateMachine.TryResolveClash(rule, fixture.Exchange.Version);
        }

        private static FinalCombatClashRule ClashRule(
            Fixture fixture,
            int attackerPower,
            int responderPower,
            ICombatRuleRandomSource random)
        {
            CombatAttackDeclaration attack = fixture.Exchange.CurrentDeclaration;
            CombatResponseDeclaration response = fixture.Exchange.CurrentResponse;
            return new FinalCombatClashRule(
                new CombatClashSideInput(
                    attack.Attacker.Id,
                    attack.Skill.Id,
                    attackerPower,
                    attack.Skill.Speed,
                    true),
                new CombatClashSideInput(
                    response.Responder.Id,
                    response.Skill.Id,
                    responderPower,
                    response.Skill.Speed,
                    false),
                random);
        }

        private static void PrepareNoResponseToApplyOutcome(
            Fixture fixture,
            TestCombatant actor,
            TestCombatant target,
            TestSkill skill)
        {
            if (fixture.Exchange.CurrentDeclaration == null)
            {
                Assert.That(fixture.StateMachine.TryDeclareAttack(
                    new CombatAttackDeclaration(actor, target, skill),
                    fixture.Exchange.Version), Is.True);
            }

            Assert.That(fixture.StateMachine.ConfirmNoResponse(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        private static void PrepareAftermathDecision(Fixture fixture)
        {
            PrepareOutcomeExecution(fixture);
            ResolvePostureAndPrepareAftermath(fixture);
        }

        private static void PrepareOutcomeExecution(Fixture fixture)
        {
            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.True);
            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);
        }

        private static void ResolvePostureAndPrepareAftermath(Fixture fixture)
        {
            ICombatPostureRule postureRule = fixture.Exchange.CurrentClashResult.Outcome == CombatClashOutcome.Unopposed
                ? null
                : new FinalCombatPostureRule();
            Assert.That(fixture.StateMachine.TryResolvePosture(postureRule), Is.True);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);
        }

        private static void AssertRejectedHandoffIsStable(Fixture fixture, bool eligible)
        {
            int version = fixture.Exchange.Version;
            int handoffCount = fixture.Exchange.HandoffCount;
            ICombatant owner = fixture.Exchange.CurrentAttackActor;
            int mp = fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp;

            Assert.That(fixture.StateMachine.TryHandoffChain(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                eligible,
                version), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.Exchange.HandoffCount, Is.EqualTo(handoffCount));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(owner));
            Assert.That(fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp, Is.EqualTo(mp));
        }

        private static CombatAttackDeclaration Attack(Fixture fixture)
        {
            return new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllySkill);
        }

        private static CombatResponseDeclaration Response(Fixture fixture)
        {
            return new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill);
        }

        private static Fixture CreateStartedFixture(
            int initialMp = 5,
            int maxPosture = 3,
            int attackMpCost = 1,
            int responseMpCost = 1,
            int attackDamage = 0,
            int responseDamage = 0,
            bool includeSecondEnemy = false)
        {
            Fixture fixture = CreateFixture(
                initialMp,
                maxPosture,
                attackMpCost,
                responseMpCost,
                attackDamage,
                responseDamage,
                includeSecondEnemy);
            fixture.StateMachine.Tick();
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            return fixture;
        }

        private static Fixture CreateFixture(
            int initialMp = 5,
            int maxPosture = 3,
            int attackMpCost = 1,
            int responseMpCost = 1,
            int attackDamage = 0,
            int responseDamage = 0,
            bool includeSecondEnemy = false)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant allyTwo = new TestCombatant(2, Side.Allies);
            TestCombatant enemy = new TestCombatant(11, Side.Enemies);
            TestCombatant enemyTwo = includeSecondEnemy
                ? new TestCombatant(12, Side.Enemies)
                : null;
            TestSkill allySkill = AddSkill(
                ally,
                1,
                TargetingRule.SingleEnemy,
                attackMpCost,
                attackDamage);
            TestSkill allyTwoSkill = AddSkill(
                allyTwo,
                2,
                TargetingRule.SingleEnemy,
                1,
                0);
            TestSkill enemySkill = AddSkill(
                enemy,
                11,
                TargetingRule.SingleEnemy,
                responseMpCost,
                responseDamage);
            CombatRuntimeConfig config = new CombatRuntimeConfig(10, initialMp, maxPosture, 0);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                config);
            session.Allies.Add(ally);
            session.Allies.Add(allyTwo);
            session.Enemies.Add(enemy);
            if (enemyTwo != null)
                session.Enemies.Add(enemyTwo);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            return new Fixture(
                session,
                stateMachine,
                ally,
                allyTwo,
                enemy,
                enemyTwo,
                allySkill,
                allyTwoSkill,
                enemySkill);
        }

        private static TestSkill AddSkill(
            TestCombatant combatant,
            int id,
            TargetingRule targeting,
            int mpCost,
            int damage)
        {
            TestSkill skill = new TestSkill(id, targeting, mpCost, damage);
            combatant.AddSkill(skill);
            return skill;
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public CombatExchangeState Exchange => Session.ExchangeState;
            public TestCombatant Ally { get; }
            public TestCombatant AllyTwo { get; }
            public TestCombatant Enemy { get; }
            public TestCombatant EnemyTwo { get; }
            public TestSkill AllySkill { get; }
            public TestSkill AllyTwoSkill { get; }
            public TestSkill EnemySkill { get; }

            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                TestCombatant ally,
                TestCombatant allyTwo,
                TestCombatant enemy,
                TestCombatant enemyTwo,
                TestSkill allySkill,
                TestSkill allyTwoSkill,
                TestSkill enemySkill)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                AllyTwo = allyTwo;
                Enemy = enemy;
                EnemyTwo = enemyTwo;
                AllySkill = allySkill;
                AllyTwoSkill = allyTwoSkill;
                EnemySkill = enemySkill;
            }
        }

        private sealed class FakeRandomSource : ICombatRuleRandomSource
        {
            private readonly Queue<int> _values;

            public int CallCount { get; private set; }

            public FakeRandomSource(params int[] values)
            {
                _values = new Queue<int>(values);
            }

            public int NextInclusive(int minInclusive, int maxInclusive)
            {
                CallCount++;
                if (_values.Count == 0)
                    throw new InvalidOperationException("Random sequence exhausted.");

                int value = _values.Dequeue();
                if (value < minInclusive || value > maxInclusive)
                    throw new InvalidOperationException("Random value outside requested range.");

                return value;
            }
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider
        {
            public SkillId Id { get; }
            public string Name => $"Skill-{Id.Value}";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting { get; }
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage { get; }
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost { get; }

            public TestSkill(int id, TargetingRule targeting, int mpCost, int damage)
            {
                Id = new SkillId(id);
                Targeting = targeting;
                MpCost = mpCost;
                BaseDamage = damage;
            }
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;
            private int _hp = 10;

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? _hp;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;

            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public void AddSkill(ISkill skill)
            {
                _skills.Add(skill);
            }

            public void BindCombatState(CombatantCombatState state)
            {
                _state = state;
            }

            public void ApplyDamage(int amount)
            {
                if (_state != null)
                    _state.ApplyDamage(amount);
                else if (amount > 0)
                    _hp = Math.Max(0, _hp - amount);
            }

            public void AddStagger(int amount)
            {
            }

            public void SetStunned(bool value)
            {
                IsStunned = value;
            }

            public void ResetStaggerIfNeededOnStunEnd()
            {
            }
        }
    }
}
#endif
