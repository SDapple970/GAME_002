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
    public sealed class CombatFinalRulesPolicyTests
    {
        [Test]
        public void Clash_HigherPowerWins()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult result = ResolveClash(fixture, 3, 1, 0, 0, 1, 1, true, 0, 0, out _);

            Assert.That(result.Outcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
        }

        [Test]
        public void Clash_ModifierAffectsScoreAndWinner()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult result = ResolveClash(fixture, 1, 2, 2, 0, 1, 1, true, 0, 0, out FinalCombatClashRule rule);

            Assert.That(result.Outcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(rule.LastResolution.AttackerFirstScore, Is.EqualTo(3));
        }

        [Test]
        public void Clash_AppliesIndependentMinusZeroPlusOneVariance()
        {
            Fixture fixture = CreateFixture();
            ResolveClash(fixture, 2, 2, 0, 0, 1, 1, true, -1, 1, out FinalCombatClashRule rule);

            Assert.That(rule.LastResolution.AttackerFirstVariance, Is.EqualTo(-1));
            Assert.That(rule.LastResolution.ResponderFirstVariance, Is.EqualTo(1));
            Assert.That(rule.LastResolution.AttackerFirstScore, Is.EqualTo(1));
            Assert.That(rule.LastResolution.ResponderFirstScore, Is.EqualTo(3));
        }

        [Test]
        public void Clash_ClampsScoreAtZero()
        {
            Fixture fixture = CreateFixture();
            ResolveClash(fixture, 0, 1, 0, 0, 1, 1, true, -1, 0, out FinalCombatClashRule rule);

            Assert.That(rule.LastResolution.AttackerFirstScore, Is.Zero);
        }

        [Test]
        public void Clash_FirstTieTriggersExactlyOneReClash()
        {
            Fixture fixture = CreateFixture();
            FakeRandomSource random = new FakeRandomSource(0, 0, 1, 0);
            ResolveClash(fixture, 2, 2, 0, 0, 1, 1, true, random, out FinalCombatClashRule rule);

            Assert.That(rule.LastResolution.ReClashOccurred, Is.True);
            Assert.That(rule.LastResolution.DecisionStage, Is.EqualTo(CombatClashDecisionStage.ReClashScore));
            Assert.That(random.CallCount, Is.EqualTo(4));
        }

        [Test]
        public void Clash_SecondTieUsesSpeed()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult result = ResolveClash(fixture, 2, 2, 0, 0, 1, 3, true, 0, 0, 0, 0, out FinalCombatClashRule rule);

            Assert.That(result.Outcome, Is.EqualTo(CombatClashOutcome.ResponderWin));
            Assert.That(rule.LastResolution.DecisionStage, Is.EqualTo(CombatClashDecisionStage.Speed));
        }

        [Test]
        public void Clash_SpeedTieUsesCurrentAttackOwner()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult result = ResolveClash(fixture, 2, 2, 0, 0, 3, 3, false, 0, 0, 0, 0, out FinalCombatClashRule rule);

            Assert.That(result.Outcome, Is.EqualTo(CombatClashOutcome.ResponderWin));
            Assert.That(rule.LastResolution.DecisionStage, Is.EqualTo(CombatClashDecisionStage.CurrentAttackOwner));
            Assert.That(rule.LastResolution.NextAttackSide, Is.EqualTo(Side.Enemies));
        }

        [Test]
        public void Clash_DoesNotRerollBeyondOneReClash()
        {
            Fixture fixture = CreateFixture();
            FakeRandomSource random = new FakeRandomSource(0, 0, 0, 0);
            ResolveClash(fixture, 2, 2, 0, 0, 2, 2, true, random, out FinalCombatClashRule rule);

            Assert.That(rule.LastResolution.Outcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(rule.LastResolution.RandomSampleCount, Is.EqualTo(4));
            Assert.That(random.CallCount, Is.EqualTo(4));
        }

        [Test]
        public void Clash_IdenticalFakeSequencesAreDeterministic()
        {
            Fixture first = CreateFixture();
            Fixture second = CreateFixture();
            ResolveClash(first, 3, 3, 0, 0, 2, 2, true, -1, -1, 1, 0, out FinalCombatClashRule firstRule);
            ResolveClash(second, 3, 3, 0, 0, 2, 2, true, -1, -1, 1, 0, out FinalCombatClashRule secondRule);

            Assert.That(secondRule.LastResolution.Outcome, Is.EqualTo(firstRule.LastResolution.Outcome));
            Assert.That(secondRule.LastResolution.AttackerReClashScore, Is.EqualTo(firstRule.LastResolution.AttackerReClashScore));
            Assert.That(secondRule.LastResolution.ResponderReClashScore, Is.EqualTo(firstRule.LastResolution.ResponderReClashScore));
        }

        [Test]
        public void Outcome_AttackerWinActivatesOnlyAttackerSkill()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult clash = ResolveClash(fixture, 3, 1, 0, 0, 1, 1, true, 0, 0, out _);

            Assert.That(CombatOutcomeSelector.TrySelect(clash, out CombatOutcomeAction action), Is.True);
            Assert.That(action.Actor, Is.SameAs(fixture.Ally));
            Assert.That(action.Skill, Is.SameAs(fixture.AllySkill));
        }

        [Test]
        public void Outcome_ResponderWinActivatesOnlyResponseSkill()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult clash = ResolveClash(fixture, 1, 3, 0, 0, 1, 1, true, 0, 0, out _);

            Assert.That(CombatOutcomeSelector.TrySelect(clash, out CombatOutcomeAction action), Is.True);
            Assert.That(action.Actor, Is.SameAs(fixture.Enemy));
            Assert.That(action.Skill, Is.SameAs(fixture.EnemySkill));
        }

        [Test]
        public void Response_NoResponseSkipsClashAndActivatesAttackerOutcome()
        {
            Fixture fixture = CreateFixture();
            CombatAttackDeclaration attack = Attack(fixture);
            CombatClashRequest request = new CombatClashRequest(
                attack,
                CombatResponseState.NoResponse,
                null);

            Assert.That(CombatClashResolver.TryResolve(request, null, out CombatClashResult clash), Is.True);
            Assert.That(clash.Outcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(CombatOutcomeSelector.TrySelect(clash, out CombatOutcomeAction action), Is.True);
            Assert.That(action.Actor, Is.SameAs(fixture.Ally));
            Assert.That(action.Skill, Is.SameAs(fixture.AllySkill));
            Assert.That(CombatAttackAuthorityPolicy.TryResolve(clash, out CombatAttackAuthorityPolicy.Result authority), Is.True);
            Assert.That(authority.NextAttackSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void Targeting_PlayerSingleEnemyTargetsEnemy()
        {
            Fixture fixture = CreateFixture();
            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Ally, fixture.AllySkill, fixture.Enemy, fixture.Session), Is.True);
        }

        [Test]
        public void Targeting_EnemySingleEnemyTargetsAlly()
        {
            Fixture fixture = CreateFixture();
            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Enemy, fixture.EnemySkill, fixture.Ally, fixture.Session), Is.True);
        }

        [Test]
        public void Targeting_PlayerSingleAllyTargetsOwnAlly()
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = AddSkill(fixture.Ally, 3, TargetingRule.SingleAlly, 0);

            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Ally, skill, fixture.AllyTwo, fixture.Session), Is.True);
            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Ally, skill, fixture.Enemy, fixture.Session), Is.False);
        }

        [Test]
        public void Targeting_EnemySingleAllyTargetsEnemySideAlly()
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = AddSkill(fixture.Enemy, 13, TargetingRule.SingleAlly, 0);

            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Enemy, skill, fixture.EnemyTwo, fixture.Session), Is.True);
            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Enemy, skill, fixture.Ally, fixture.Session), Is.False);
        }

        [TestCase(TargetingRule.AnySingle)]
        [TestCase(TargetingRule.Environment)]
        public void Targeting_UnsupportedRulesRejectExplicitly(TargetingRule targeting)
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = AddSkill(fixture.Ally, 4, targeting, 0);

            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Ally, skill, fixture.Enemy, fixture.Session), Is.False);
        }

        [Test]
        public void Targeting_DeadTargetIsInvalid()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.ApplyDamage(int.MaxValue);

            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Ally, fixture.AllySkill, fixture.Enemy, fixture.Session), Is.False);
        }

        [Test]
        public void Targeting_IsActorRelativeInsteadOfAbsoluteRosterNamed()
        {
            Fixture fixture = CreateFixture();
            TestSkill enemyAllySkill = AddSkill(fixture.Enemy, 14, TargetingRule.AllAllies, 0);
            TestSkill enemyHostileSkill = AddSkill(fixture.Enemy, 15, TargetingRule.AllEnemies, 0);

            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Enemy, enemyAllySkill, null, fixture.Session), Is.True);
            Assert.That(CombatOutcomeTargetResolver.CanResolve(fixture.Enemy, enemyHostileSkill, null, fixture.Session), Is.True);
        }

        [Test]
        public void Mp_SelectionValidationDoesNotSpend()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2);
            CombatAttackDeclaration attack = Attack(fixture);

            Assert.That(CombatDeclarationPolicy.CanDeclareAttack(fixture.Session, attack), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(5));
        }

        [Test]
        public void Mp_SuccessfulCommitSpendsExactAttackCost()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), null, out CombatExchangeCommitResult result), Is.True);
            Assert.That(result.AttackMpSpent, Is.EqualTo(2));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void Mp_InvalidCommitSpendsZero()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2);
            fixture.Ally.SetStunned(true);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), null, out CombatExchangeCommitResult result), Is.False);
            Assert.That(result.AttackMpSpent, Is.Zero);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(5));
        }

        [Test]
        public void Mp_InvalidResponseCommitSpendsZeroForBothSides()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2);
            TestSkill invalidResponseSkill = AddSkill(fixture.Enemy, 16, TargetingRule.SingleAlly, 2);
            CombatResponseDeclaration invalidResponse = new CombatResponseDeclaration(
                fixture.Enemy,
                invalidResponseSkill);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(
                fixture.Session,
                Attack(fixture),
                invalidResponse,
                out CombatExchangeCommitResult result), Is.False);
            Assert.That(result.Failure, Is.EqualTo(CombatExchangeCommitFailure.InvalidResponse));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(5));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(5));
        }

        [Test]
        public void Mp_LosingClashDoesNotRefundCommittedCost()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2, responseMpCost: 1);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), Response(fixture), out _), Is.True);
            ResolveClash(fixture, 1, 3, 0, 0, 1, 1, true, 0, 0, out _);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void Mp_NoResponseStillSpendsAttackerCost()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 3);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), null, out CombatExchangeCommitResult result), Is.True);
            Assert.That(result.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
        }

        [Test]
        public void Mp_ValidResponseSpendsDefenderCost()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2, responseMpCost: 3);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), Response(fixture), out CombatExchangeCommitResult result), Is.True);
            Assert.That(result.ResponseMpSpent, Is.EqualTo(3));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(2));
        }

        [Test]
        public void Mp_InsufficientDefenderMpBecomesNoResponse()
        {
            Fixture fixture = CreateFixture(initialMp: 5, attackMpCost: 2, responseMpCost: 4);
            fixture.Session.GetCombatState(fixture.Enemy).SetMp(3);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), Response(fixture), out CombatExchangeCommitResult result), Is.True);
            Assert.That(result.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(result.ResponseDowngradedForInsufficientMp, Is.True);
            Assert.That(result.CommittedResponse, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void Mp_NeverDropsBelowZero()
        {
            Fixture fixture = CreateFixture(initialMp: 1, attackMpCost: 2);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(fixture.Session, Attack(fixture), null, out _), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(1));
        }

        [Test]
        public void Posture_ClashLoserGetsOneBaseAndWinnerGetsNone()
        {
            Fixture fixture = CreateFixture(maxPosture: 3);
            FinalCombatPostureRule rule = new FinalCombatPostureRule();

            Assert.That(rule.TryApplyLoss(fixture.Session.GetCombatState(fixture.Enemy), fixture.Enemy, out CombatPostureResult result, out _), Is.True);
            Assert.That(result.PostureApplied, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentPosture, Is.Zero);
        }

        [Test]
        public void Posture_ExtraModifierIsSupported()
        {
            Fixture fixture = CreateFixture(maxPosture: 5);
            FinalCombatPostureRule rule = new FinalCombatPostureRule(2);

            rule.TryApplyLoss(fixture.Session.GetCombatState(fixture.Enemy), fixture.Enemy, out CombatPostureResult result, out _);
            Assert.That(result.PostureApplied, Is.EqualTo(3));
        }

        [Test]
        public void Posture_ClampsAtRuntimeMaxAndAppliesStun()
        {
            Fixture fixture = CreateFixture(maxPosture: 2);
            FinalCombatPostureRule rule = new FinalCombatPostureRule(5);

            Assert.That(rule.TryApplyLoss(fixture.Session.GetCombatState(fixture.Enemy), fixture.Enemy, out CombatPostureResult result, out CombatStunResult stun), Is.True);
            Assert.That(result.PostureAfter, Is.EqualTo(2));
            Assert.That(result.ReachedMaximum, Is.True);
            Assert.That(stun.StunApplied, Is.True);
            Assert.That(fixture.Enemy.IsStunned, Is.True);
        }

        [Test]
        public void Posture_StunnedActorCannotDeclareAttack()
        {
            Fixture fixture = CreateFixture();
            fixture.Ally.SetStunned(true);

            Assert.That(CombatDeclarationPolicy.CanDeclareAttack(fixture.Session, Attack(fixture)), Is.False);
        }

        [Test]
        public void Posture_StunnedActorCannotRespond()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.SetStunned(true);

            Assert.That(CombatDeclarationPolicy.CanDeclareResponse(fixture.Session, Attack(fixture), Response(fixture)), Is.False);
        }

        [Test]
        public void Posture_AllLivingEnemiesStunnedCreatesAllOutCandidate()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.SetStunned(true);
            fixture.EnemyTwo.SetStunned(true);

            Assert.That(CombatAllOutPolicy.IsCandidate(fixture.Session), Is.True);
        }

        [Test]
        public void Posture_AllOutCandidateIsNotVictory()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.SetStunned(true);
            fixture.EnemyTwo.SetStunned(true);

            Assert.That(CombatAllOutPolicy.IsCandidate(fixture.Session), Is.True);
            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void Terminal_EnemiesWipedWithLivingAllyIsVictory()
        {
            Fixture fixture = CreateFixture();
            Kill(fixture.Enemy, fixture.EnemyTwo);

            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.Victory));
        }

        [Test]
        public void Terminal_AlliesWipedWithLivingEnemyIsDefeat()
        {
            Fixture fixture = CreateFixture();
            Kill(fixture.Ally, fixture.AllyTwo);

            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.Defeat));
        }

        [Test]
        public void Terminal_BothWipedIsDefeat()
        {
            Fixture fixture = CreateFixture();
            Kill(fixture.Ally, fixture.AllyTwo, fixture.Enemy, fixture.EnemyTwo);

            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.Defeat));
        }

        [Test]
        public void Terminal_StunOnlyIsNotTerminal()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.SetStunned(true);

            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void Terminal_AllEnemiesStunnedIsNotTerminal()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.SetStunned(true);
            fixture.EnemyTwo.SetStunned(true);

            Assert.That(FinalCombatTerminalPolicy.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.None));
        }

        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        [TestCase(CombatEndReason.Scripted)]
        public void Terminal_ExplicitReasonsRemainExplicit(CombatEndReason reason)
        {
            FinalCombatTerminalPolicy policy = new FinalCombatTerminalPolicy();

            Assert.That(policy.TryResolveExplicit(reason, out CombatTerminalDecision decision), Is.True);
            Assert.That(decision.EndReason, Is.EqualTo(reason));
            Assert.That(decision.TerminalCandidate, Is.EqualTo(CombatTerminalCandidate.None));
        }

        [Test]
        public void Authority_AttackerWinKeepsWinnerSideAsNextAttackSide()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult clash = ResolveClash(fixture, 3, 1, 0, 0, 1, 1, true, 0, 0, out _);

            Assert.That(CombatAttackAuthorityPolicy.TryResolve(clash, out CombatAttackAuthorityPolicy.Result result), Is.True);
            Assert.That(result.Winner, Is.SameAs(fixture.Ally));
            Assert.That(result.NextAttackSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void Authority_DefenderWinTransfersNextAttackSide()
        {
            Fixture fixture = CreateFixture();
            CombatClashResult clash = ResolveClash(fixture, 1, 3, 0, 0, 1, 1, true, 0, 0, out _);

            Assert.That(CombatAttackAuthorityPolicy.TryResolve(clash, out CombatAttackAuthorityPolicy.Result result), Is.True);
            Assert.That(result.Winner, Is.SameAs(fixture.Enemy));
            Assert.That(result.NextAttackSide, Is.EqualTo(Side.Enemies));
        }

        [Test]
        public void Chain_AliveActorWithMpAndUsableSkillCanContinue()
        {
            Fixture fixture = CreateFixture(initialMp: 3, attackMpCost: 2);

            Assert.That(CombatChainPolicy.CanContinue(fixture.Session, fixture.Ally, fixture.AllySkill), Is.True);
        }

        [Test]
        public void Chain_InsufficientMpCannotContinueWithSkill()
        {
            Fixture fixture = CreateFixture(initialMp: 1, attackMpCost: 2);

            Assert.That(CombatChainPolicy.CanContinue(fixture.Session, fixture.Ally, fixture.AllySkill), Is.False);
        }

        [Test]
        public void Chain_PlayerMayDeclineEvenWhenContinuationIsAvailable()
        {
            Fixture fixture = CreateFixture(initialMp: 3, attackMpCost: 2);

            Assert.That(CombatChainPolicy.CanContinue(fixture.Session, fixture.Ally, fixture.AllySkill), Is.True);
            Assert.That(CombatChainPolicy.ShouldContinue(fixture.Session, fixture.Ally, fixture.AllySkill, false), Is.False);
        }

        [Test]
        public void Handoff_AllowsAtMostOneAndUsesReceivingIdentity()
        {
            Fixture fixture = CreateFixture(initialMp: 3);
            TestSkill receiverSkill = AddSkill(fixture.AllyTwo, 5, TargetingRule.SingleEnemy, 2);

            Assert.That(CombatHandoffPolicy.TryAuthorize(
                fixture.Session,
                fixture.Ally,
                fixture.AllyTwo,
                receiverSkill,
                0,
                true,
                out CombatHandoffPolicy.Result first), Is.True);
            Assert.That(first.ReceivingActor, Is.SameAs(fixture.AllyTwo));
            Assert.That(first.HandoffCount, Is.EqualTo(1));
            Assert.That(CombatHandoffPolicy.TryAuthorize(
                fixture.Session,
                fixture.AllyTwo,
                fixture.Ally,
                fixture.AllySkill,
                first.HandoffCount,
                true,
                out _), Is.False);
        }

        [Test]
        public void Handoff_EligibilityIsInjectedAndNotInferred()
        {
            Fixture fixture = CreateFixture(initialMp: 3);
            TestSkill receiverSkill = AddSkill(fixture.AllyTwo, 6, TargetingRule.SingleEnemy, 2);

            Assert.That(CombatHandoffPolicy.TryAuthorize(
                fixture.Session,
                fixture.Ally,
                fixture.AllyTwo,
                receiverSkill,
                0,
                false,
                out _), Is.False);
            Assert.That(CombatHandoffPolicy.TryAuthorize(
                fixture.Session,
                fixture.Ally,
                fixture.AllyTwo,
                receiverSkill,
                0,
                true,
                out _), Is.True);
        }

        [Test]
        public void Handoff_RejectsStunnedOrInsufficientMpReceiver()
        {
            Fixture fixture = CreateFixture(initialMp: 1);
            TestSkill receiverSkill = AddSkill(fixture.AllyTwo, 7, TargetingRule.SingleEnemy, 2);

            Assert.That(CombatHandoffPolicy.TryAuthorize(
                fixture.Session,
                fixture.Ally,
                fixture.AllyTwo,
                receiverSkill,
                0,
                true,
                out _), Is.False);

            fixture.Session.GetCombatState(fixture.AllyTwo).SetMp(3);
            fixture.AllyTwo.SetStunned(true);
            Assert.That(CombatHandoffPolicy.TryAuthorize(
                fixture.Session,
                fixture.Ally,
                fixture.AllyTwo,
                receiverSkill,
                0,
                true,
                out _), Is.False);
        }

        private static CombatAttackDeclaration Attack(Fixture fixture)
        {
            return new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllySkill);
        }

        private static CombatResponseDeclaration Response(Fixture fixture)
        {
            return new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill);
        }

        private static CombatClashResult ResolveClash(
            Fixture fixture,
            int attackPower,
            int responsePower,
            int attackModifier,
            int responseModifier,
            int attackSpeed,
            int responseSpeed,
            bool attackerOwnsAuthority,
            int attackVariance,
            int responseVariance,
            out FinalCombatClashRule rule)
        {
            return ResolveClash(
                fixture,
                attackPower,
                responsePower,
                attackModifier,
                responseModifier,
                attackSpeed,
                responseSpeed,
                attackerOwnsAuthority,
                new FakeRandomSource(attackVariance, responseVariance),
                out rule);
        }

        private static CombatClashResult ResolveClash(
            Fixture fixture,
            int attackPower,
            int responsePower,
            int attackModifier,
            int responseModifier,
            int attackSpeed,
            int responseSpeed,
            bool attackerOwnsAuthority,
            int firstAttackVariance,
            int firstResponseVariance,
            int secondAttackVariance,
            int secondResponseVariance,
            out FinalCombatClashRule rule)
        {
            return ResolveClash(
                fixture,
                attackPower,
                responsePower,
                attackModifier,
                responseModifier,
                attackSpeed,
                responseSpeed,
                attackerOwnsAuthority,
                new FakeRandomSource(
                    firstAttackVariance,
                    firstResponseVariance,
                    secondAttackVariance,
                    secondResponseVariance),
                out rule);
        }

        private static CombatClashResult ResolveClash(
            Fixture fixture,
            int attackPower,
            int responsePower,
            int attackModifier,
            int responseModifier,
            int attackSpeed,
            int responseSpeed,
            bool attackerOwnsAuthority,
            FakeRandomSource random,
            out FinalCombatClashRule rule)
        {
            CombatAttackDeclaration attack = Attack(fixture);
            CombatResponseDeclaration response = Response(fixture);
            CombatClashRequest request = new CombatClashRequest(
                attack,
                CombatResponseState.CounterDeclared,
                response);
            rule = new FinalCombatClashRule(
                new CombatClashSideInput(
                    fixture.Ally.Id,
                    fixture.AllySkill.Id,
                    attackPower,
                    attackModifier,
                    attackSpeed,
                    attackerOwnsAuthority),
                new CombatClashSideInput(
                    fixture.Enemy.Id,
                    fixture.EnemySkill.Id,
                    responsePower,
                    responseModifier,
                    responseSpeed,
                    !attackerOwnsAuthority),
                random);

            Assert.That(CombatClashResolver.TryResolve(request, rule, out CombatClashResult result), Is.True);
            return result;
        }

        private static Fixture CreateFixture(
            int initialMp = 5,
            int maxPosture = 3,
            int attackMpCost = 1,
            int responseMpCost = 1)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant allyTwo = new TestCombatant(2, Side.Allies);
            TestCombatant enemy = new TestCombatant(11, Side.Enemies);
            TestCombatant enemyTwo = new TestCombatant(12, Side.Enemies);
            TestSkill allySkill = AddSkill(ally, 1, TargetingRule.SingleEnemy, attackMpCost);
            TestSkill enemySkill = AddSkill(enemy, 11, TargetingRule.SingleEnemy, responseMpCost);
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
            session.Enemies.Add(enemyTwo);
            session.InitializeCombatStates(config);

            return new Fixture(session, ally, allyTwo, enemy, enemyTwo, allySkill, enemySkill);
        }

        private static TestSkill AddSkill(
            TestCombatant combatant,
            int id,
            TargetingRule targeting,
            int mpCost)
        {
            TestSkill skill = new TestSkill(id, targeting, mpCost);
            combatant.AddSkill(skill);
            return skill;
        }

        private static void Kill(params TestCombatant[] combatants)
        {
            for (int i = 0; i < combatants.Length; i++)
                combatants[i].ApplyDamage(int.MaxValue);
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public TestCombatant Ally { get; }
            public TestCombatant AllyTwo { get; }
            public TestCombatant Enemy { get; }
            public TestCombatant EnemyTwo { get; }
            public TestSkill AllySkill { get; }
            public TestSkill EnemySkill { get; }

            public Fixture(
                CombatSession session,
                TestCombatant ally,
                TestCombatant allyTwo,
                TestCombatant enemy,
                TestCombatant enemyTwo,
                TestSkill allySkill,
                TestSkill enemySkill)
            {
                Session = session;
                Ally = ally;
                AllyTwo = allyTwo;
                Enemy = enemy;
                EnemyTwo = enemyTwo;
                AllySkill = allySkill;
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
                    throw new InvalidOperationException("The deterministic random sequence was exhausted.");

                int value = _values.Dequeue();
                if (value < minInclusive || value > maxInclusive)
                    throw new InvalidOperationException("The deterministic random value is outside the requested range.");

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
            public int BaseDamage => 0;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 0;
            public bool ConsumesTurn => true;
            public int MpCost { get; }

            public TestSkill(int id, TargetingRule targeting, int mpCost)
            {
                Id = new SkillId(id);
                Targeting = targeting;
                MpCost = mpCost;
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
