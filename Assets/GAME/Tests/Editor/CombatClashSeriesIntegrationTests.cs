#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    public sealed class CombatClashSeriesIntegrationTests
    {
        private GameObject _owner;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_owner != null)
                UnityEngine.Object.DestroyImmediate(_owner);
        }

        // Durability, power, costs, and timers here are fixture values, not game balance.
        [Test]
        public void ThreeRounds_MentalAndStatusAdvanceOnceAtCompletedExchange()
        {
            Fixture f = CreateFixture(3, 2, 2, 3, 4, 2);
            CombatStatusDefinitionSO status = ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            try
            {
                SetPrivateField(status, "statusId", "06b.duration");
                SetPrivateField(status, "durationKind", CombatStatusDurationKind.Explicit);
                SetPrivateField(status, "explicitDuration", 3);
                CombatantCombatState enemy = f.Session.GetCombatState(f.Enemy);
                enemy.ApplyStatus(status);
                Assert.That(f.Enable(4, 3, 3), Is.True);
                List<CombatClashRoundPresentationRequest> rounds = new();
                CombatOutcomePresentationRequest outcome = null;
                f.Flow.ClashRoundPresentationRequested += rounds.Add;
                f.Flow.OutcomePresentationRequested += value => outcome = value;
                f.DeclareAndApproach();
                Assert.That(enemy.CurrentMental, Is.EqualTo(100));
                Assert.That(enemy.TryGetStatus("06b.duration", out CombatStatusRuntime current), Is.True);
                Assert.That(current.RemainingDuration, Is.EqualTo(3));
                Assert.That(rounds[0].TryComplete(), Is.True);
                Assert.That(f.Flow.SubmitClashSeriesSkill(f.Flow.PendingDecisionRequest, f.AllySkills[1]), Is.True);
                Assert.That(f.Flow.SubmitClashSeriesSkill(f.Flow.PendingDecisionRequest, f.EnemySkills[1]), Is.True);
                Assert.That(enemy.CurrentMental, Is.EqualTo(100));
                Assert.That(current.RemainingDuration, Is.EqualTo(3));
                Assert.That(rounds[1].TryComplete(), Is.True);
                Assert.That(f.Flow.SubmitClashSeriesSkill(f.Flow.PendingDecisionRequest, f.AllySkills[2]), Is.True);
                Assert.That(f.Flow.SubmitClashSeriesSkill(f.Flow.PendingDecisionRequest, f.EnemySkills[2]), Is.True);
                Assert.That(enemy.CurrentMental, Is.EqualTo(100));
                Assert.That(current.RemainingDuration, Is.EqualTo(3));
                int damageCallbacks = 0;
                f.Enemy.DamageCallback = () =>
                {
                    damageCallbacks++;
                    Assert.That(f.State.TryExecutePreparedSkill(), Is.False);
                    Assert.That(f.State.TryExecutePreparedSkill(f.Session.ExchangeState.Version), Is.False);
                };
                Assert.That(rounds[2].TryComplete(), Is.True);
                Assert.That(damageCallbacks, Is.EqualTo(1));
                Assert.That(enemy.CurrentHp, Is.EqualTo(9));
                Assert.That(enemy.CurrentMental, Is.EqualTo(85));
                Assert.That(current.RemainingDuration, Is.EqualTo(3));
                Assert.That(outcome.TryComplete(), Is.True);
                Assert.That(current.RemainingDuration, Is.EqualTo(2));
                Assert.That(enemy.CurrentMental, Is.EqualTo(85));
                Assert.That(outcome.TryComplete(), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(status); }
        }

        [Test]
        public void Ultimate_StatusAndDamageApplyOnceDespiteSynchronousReentry()
        {
            Fixture f = CreateFixture(Side.Allies, 2, 2);
            CombatStatusDefinitionSO status = ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            try
            {
                SetPrivateField(status, "statusId", "06b.ultimate");
                SetPrivateField(status, "stackPolicy", CombatStatusStackPolicy.Stack);
                SetPrivateField(status, "maximumStacks", 3);
                f.AllySkills[0].AppliedStatusEffects = new[] { status };
                Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.True);
                Assert.That(f.Session.TryAssignUltimateSkill(f.Ally, f.AllySkills[0]), Is.True);
                Assert.That(f.Session.TrySetInitialBpForValidation(f.Ally, 3), Is.True);
                CommitBpFixture(f.Session.BpRuntime, f.Ally, 3);
                CombatExchangeDecisionRequest request = f.Flow.PendingDecisionRequest;
                f.Enemy.DamageCallback = () =>
                {
                    CombatantCombatState allyState = f.Session.GetCombatState(f.Ally);
                    allyState.ApplyMentalDelta(-allyState.MaxMental);
                    Assert.That(f.Flow.TryOvercome(f.Ally, request.ExchangeVersion, out _), Is.False);
                    Assert.That(allyState.CurrentMental, Is.Zero);
                    Assert.That(f.State.TryExecuteUltimate(f.Ally, f.Enemy, request.ExchangeVersion, out _), Is.False);
                    Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
                };
                Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.True);
                Assert.That(f.Enemy.HP, Is.EqualTo(9));
                Assert.That(f.Session.GetCombatState(f.Enemy).TryGetStatus("06b.ultimate", out CombatStatusRuntime applied), Is.True);
                Assert.That(applied.StackCount, Is.EqualTo(1));
                Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(status); }
        }

        [Test]
        public void ThreeRounds_GateEachMotion_ThenApplyHpOnceAndReachChain()
        {
            Fixture f = CreateFixture(3, 2, 2, 3, 4, 2);
            Assert.That(f.Enable(4, 3, 3), Is.True);
            List<CombatClashRoundPresentationRequest> rounds = new List<CombatClashRoundPresentationRequest>();
            CombatOutcomePresentationRequest outcome = null;
            f.Flow.ClashRoundPresentationRequested += rounds.Add;
            f.Flow.OutcomePresentationRequested += value => outcome = value;

            f.DeclareAndApproach();
            Assert.That(rounds.Count, Is.EqualTo(1));
            Assert.That(rounds[0].Result.ResponderDurabilityAfter, Is.EqualTo(2));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(4));
            f.Flow.AdvanceUntilBlocked();
            Assert.That(rounds.Count, Is.EqualTo(1));
            Assert.That(rounds[0].TryComplete(), Is.True);
            Assert.That(rounds[0].TryComplete(), Is.False);

            CombatExchangeDecisionRequest attack2 = f.Flow.PendingDecisionRequest;
            Assert.That(attack2.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashAttack));
            Assert.That(f.Flow.SubmitClashSeriesSkill(attack2, f.AllySkills[1]), Is.True);
            CombatExchangeDecisionRequest response2 = f.Flow.PendingDecisionRequest;
            Assert.That(f.Flow.SubmitClashSeriesSkill(attack2, f.AllySkills[1]), Is.False);
            Assert.That(f.Flow.SubmitClashSeriesSkill(response2, f.EnemySkills[1]), Is.True);
            Assert.That(rounds.Count, Is.EqualTo(2));
            Assert.That(rounds[1].Result.AttackerDurabilityAfter, Is.EqualTo(3));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(3));
            Assert.That(rounds[1].TryComplete(), Is.True);

            CombatExchangeDecisionRequest attack3 = f.Flow.PendingDecisionRequest;
            Assert.That(f.Flow.SubmitClashSeriesSkill(attack3, f.AllySkills[2]), Is.True);
            CombatExchangeDecisionRequest response3 = f.Flow.PendingDecisionRequest;
            Assert.That(f.Flow.SubmitClashSeriesSkill(response3, f.EnemySkills[2]), Is.True);
            Assert.That(rounds.Count, Is.EqualTo(3));
            Assert.That(rounds[2].Result.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
            Assert.That(rounds[2].TryComplete(), Is.True);
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.IsClashSeries, Is.True);
            Assert.That(outcome.WinningAction.Skill, Is.SameAs(f.AllySkills[2]));
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
            Assert.That(outcome.TryComplete(), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
            Assert.That(f.Flow.EndChain(f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
        }

        [Test]
        public void TieAtLimit_ReturnsToStandoffWithoutDamageOrOutcome()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(5, 5, 1), Is.True);
            CombatClashRoundPresentationRequest round = null;
            int outcomeCount = 0;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.Flow.OutcomePresentationRequested += _ => outcomeCount++;
            f.DeclareAndApproach();

            Assert.That(round.Result.FinalOutcome, Is.EqualTo(CombatClashOutcome.Tie));
            Assert.That(round.TryComplete(), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
            Assert.That(f.Ally.HP, Is.EqualTo(10));
            Assert.That(outcomeCount, Is.Zero);
        }

        [Test]
        public void SeriesWin_ContinueChainStartsNextExchangeWithoutStandoff()
        {
            Fixture f = CreateFixture(3, 2);
            Assert.That(f.Enable(2, 1, 2), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.DeclareAndApproach();
            Assert.That(round.TryComplete(), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.ChainDecision));

            Assert.That(f.Flow.ContinueChain(f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(f.Session.ExchangeState.IsChainActive, Is.True);
            Assert.That(f.Session.ExchangeState.ClashSeries, Is.Null);
            Assert.That(f.Flow.PendingDecisionRequest.Kind,
                Is.EqualTo(CombatExchangeDecisionKind.Attack));
        }

        [Test]
        public void NoResponseAfterFirstRound_UsesSingleFinalOutcomeAndNoExtraResponseCost()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            CombatClashRoundPresentationRequest round = null;
            CombatOutcomePresentationRequest outcome = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.Flow.OutcomePresentationRequested += value => outcome = value;
            f.DeclareAndApproach();
            Assert.That(round.TryComplete(), Is.True);
            Assert.That(f.Flow.SubmitClashSeriesSkill(
                f.Flow.PendingDecisionRequest, f.AllySkills[0]), Is.True);
            CombatExchangeDecisionRequest response = f.Flow.PendingDecisionRequest;
            int enemyMp = f.Session.GetCombatState(f.Enemy).CurrentMp;
            Assert.That(f.Flow.SubmitClashSeriesNoResponse(response), Is.True);
            Assert.That(f.Flow.SubmitClashSeriesNoResponse(response), Is.False);
            Assert.That(f.Session.GetCombatState(f.Enemy).CurrentMp, Is.EqualTo(enemyMp));
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.ClashResult.Outcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(outcome.ResponseDeclaration, Is.Null);
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
        }

        [Test]
        public void TimeoutAndPause_AcceptOnlyOneVersionedDecision()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.DeclareAndApproach();
            round.TryComplete();
            CombatExchangeDecisionRequest timedRequest = f.Flow.PendingDecisionRequest;
            Time.timeScale = 0f;
            f.Flow.Tick(0f, 10f);
            Assert.That(f.Flow.PendingDecisionRequest, Is.SameAs(timedRequest));
            Time.timeScale = 1f;
            f.Flow.Tick(0f, 10f);
            Assert.That(f.Flow.PendingDecisionRequest.Kind,
                Is.EqualTo(CombatExchangeDecisionKind.ClashResponse));
            Assert.That(f.Flow.SubmitClashSeriesSkill(timedRequest, f.AllySkills[0]), Is.False);
        }

        [Test]
        public void Availability_ReportsInsufficientMpAndBlocksSubmission()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.DeclareAndApproach();
            round.TryComplete();
            CombatExchangeDecisionRequest request = f.Flow.PendingDecisionRequest;
            f.Session.GetCombatState(f.Ally).SetMp(0);
            CombatSkillAvailability option = f.Flow.GetSkillAvailability(request, f.Ally, f.AllySkills[0]);
            Assert.That(option.Reason, Is.EqualTo(CombatSkillUnavailableReason.InsufficientMp));
            Assert.That(f.Flow.SubmitClashSeriesSkill(request, f.AllySkills[0]), Is.False);
            Assert.That(f.Flow.GetAvailableClashSeriesSkills(request), Is.Empty);
        }

        [Test]
        public void EnemyNextAttackWithoutMp_TimesOutToStandoffWithoutAnotherCommit()
        {
            Fixture f = CreateFixture(Side.Enemies, 2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            f.Session.GetCombatState(f.Enemy).SetMp(1);
            CombatApproachPresentationRequest approach = null;
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ApproachPresentationRequested += value => approach = value;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            Assert.That(f.Flow.SubmitAttackDeclaration(
                new CombatAttackDeclaration(f.Enemy, f.Ally, f.EnemySkills[0]),
                f.Flow.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(f.Flow.SubmitResponse(
                new CombatResponseDeclaration(f.Ally, f.AllySkills[0]),
                f.Flow.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(round.TryComplete(), Is.True);

            CombatExchangeDecisionRequest blocked = f.Flow.PendingDecisionRequest;
            Assert.That(blocked.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashAttack));
            Assert.That(f.Flow.GetAvailableClashSeriesSkills(blocked), Is.Empty);
            Time.timeScale = 0f;
            f.Flow.Tick(0f, 3f);
            Assert.That(f.Flow.PendingDecisionRequest, Is.SameAs(blocked));
            Time.timeScale = 1f;
            f.Flow.Tick(0f, 3f);

            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(f.Session.ExchangeState.ClashSeries, Is.Null);
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
            Assert.That(f.Ally.HP, Is.EqualTo(10));
            Assert.That(f.Session.GetCombatState(f.Enemy).CurrentMp, Is.Zero);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(f.Flow.SubmitClashSeriesSkill(blocked, f.EnemySkills[0]), Is.False);
            Assert.That(round.TryComplete(), Is.False);
        }

        [Test]
        public void NextAttackWithoutClashCard_TimesOutWithoutDoubleDiscard()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, new[]
            {
                new CombatHandEntry(f.AllySkills[0], CombatCardKind.Normal)
            }, 1, 1, 23), Is.True);
            Assert.That(f.Session.TryEnableHandValidation(f.Enemy, new[]
            {
                new CombatHandEntry(f.EnemySkills[0], CombatCardKind.Clash)
            }, 1, 1, 24), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.DeclareAndApproach();
            Assert.That(round.TryComplete(), Is.True);
            CombatExchangeDecisionRequest blocked = f.Flow.PendingDecisionRequest;
            Assert.That(f.Flow.GetAvailableClashSeriesSkills(blocked), Is.Empty);
            Assert.That(f.Session.TryGetHand(f.Ally, out CombatHandRuntime allyHand), Is.True);
            Assert.That(f.Session.TryGetHand(f.Enemy, out CombatHandRuntime enemyHand), Is.True);
            Assert.That(allyHand.NormalDiscard.Count, Is.EqualTo(1));
            Assert.That(enemyHand.ClashDiscard.Count, Is.EqualTo(1));

            f.Flow.Tick(0f, 3f);

            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(allyHand.NormalDiscard.Count, Is.EqualTo(1));
            Assert.That(enemyHand.ClashDiscard.Count, Is.EqualTo(1));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(f.Session.GetCombatState(f.Enemy).CurrentMp, Is.EqualTo(4));
            Assert.That(f.Ally.HP, Is.EqualTo(10));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
        }

        [Test]
        public void EnemyInitiative_PlayerResponds_AndEnemySelectsNextRoundSkill()
        {
            Fixture f = CreateFixture(Side.Enemies, 2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            CombatApproachPresentationRequest approach = null;
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ApproachPresentationRequested += value => approach = value;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            FinalCombatPlayerCommandController player = new FinalCombatPlayerCommandController();
            FinalCombatEnemyCommandController enemy = new FinalCombatEnemyCommandController();
            Assert.That(player.Bind(f.Flow, f.Session), Is.True);
            Assert.That(enemy.Bind(f.Flow, f.Session), Is.True);

            Assert.That(player.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Response));
            Assert.That(player.SelectSkill(f.AllySkills[0]), Is.True);
            Assert.That(player.SelectTarget(f.Enemy), Is.True);
            Assert.That(player.Confirm(), Is.True);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(round.Result.AttackerDurabilityAfter, Is.EqualTo(2));
            Assert.That(round.Result.ResponderDurabilityAfter, Is.EqualTo(2));
            Assert.That(f.Ally.HP, Is.EqualTo(10));
            Assert.That(round.TryComplete(), Is.True);

            Assert.That(player.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.ClashResponse));
            Assert.That(player.SelectSkill(f.AllySkills[0]), Is.True);
            Assert.That(player.Confirm(), Is.True);
            Assert.That(round.Result.ComparisonNumber, Is.EqualTo(2));
            Assert.That(f.Ally.HP, Is.EqualTo(10));
            enemy.Unbind();
            player.Unbind();
        }

        [Test]
        public void EnemyDeclarationBeforeOptIn_StillStartsSeriesAfterPlayerResponse()
        {
            Fixture f = CreateFixture(Side.Enemies, 2, 2);
            Assert.That(f.Flow.SubmitAttackDeclaration(
                new CombatAttackDeclaration(f.Enemy, f.Ally, f.EnemySkills[0]),
                f.Flow.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(f.Enable(3, 3, 2), Is.True);
            CombatApproachPresentationRequest approach = null;
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ApproachPresentationRequested += value => approach = value;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            Assert.That(f.Flow.SubmitResponse(
                new CombatResponseDeclaration(f.Ally, f.AllySkills[0]),
                f.Flow.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(round, Is.Not.Null);
            Assert.That(f.Session.ExchangeState.ClashSeries, Is.Not.Null);
        }

        [Test]
        public void PlayerView_ShowsOffReasonAndRefreshesAfterMpRecovery()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            FinalCombatPlayerCommandController player = new FinalCombatPlayerCommandController();
            Assert.That(player.Bind(f.Flow, f.Session), Is.True);
            f.DeclareAndApproach();
            round.TryComplete();
            f.Session.GetCombatState(f.Ally).SetMp(0);
            player.RefreshAvailability();
            Assert.That(player.ViewState.SkillAvailability[0].Reason,
                Is.EqualTo(CombatSkillUnavailableReason.InsufficientMp));
            Assert.That(player.SelectSkill(f.AllySkills[0]), Is.False);
            f.Session.GetCombatState(f.Ally).RestoreMp(1);
            player.RefreshAvailability();
            Assert.That(player.ViewState.SkillAvailability[0].IsAvailable, Is.True);
            Assert.That(player.SelectSkill(f.AllySkills[0]), Is.True);
            player.Unbind();
        }

        [Test]
        public void StandoffAllOff_StillAllowsActorSelectionAndMpRecovery()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            FinalCombatPlayerCommandController player = new FinalCombatPlayerCommandController();
            Assert.That(player.Bind(f.Flow, f.Session), Is.True);
            f.Session.GetCombatState(f.Ally).SetMp(0);
            player.RefreshAvailability();

            Assert.That(player.SelectActor(f.Ally), Is.True);
            Assert.That(player.ViewState.SelectableSkills, Is.Empty);
            Assert.That(player.ViewState.SkillAvailability[0].Reason,
                Is.EqualTo(CombatSkillUnavailableReason.InsufficientMp));
            Assert.That(player.SelectSkill(f.AllySkills[0]), Is.False);

            f.Session.GetCombatState(f.Ally).RestoreMp(1);
            player.RefreshAvailability();
            Assert.That(player.ViewState.SkillAvailability[0].IsAvailable, Is.True);
            Assert.That(player.SelectSkill(f.AllySkills[0]), Is.True);
            player.Unbind();
        }

        [Test]
        public void OptInAfterPlayerUiBind_RefreshesAllOffActorList()
        {
            Fixture f = CreateFixture(2, 2);
            FinalCombatPlayerCommandController player = new FinalCombatPlayerCommandController();
            Assert.That(player.Bind(f.Flow, f.Session), Is.True);
            f.Session.GetCombatState(f.Ally).SetMp(0);
            player.RefreshAvailability();
            Assert.That(player.ViewState.SelectableActors, Is.Empty);

            Assert.That(f.Enable(3, 3, 2), Is.True);
            Assert.That(player.ViewState.SelectableActors, Does.Contain(f.Ally));
            player.Unbind();
        }

        [Test]
        public void SessionUnbind_ClearsPendingTimerAndRejectsOldSeriesCommand()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.DeclareAndApproach();
            round.TryComplete();
            CombatExchangeDecisionRequest old = f.Flow.PendingDecisionRequest;
            Assert.That(old.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashAttack));

            Assert.That(f.Flow.BindFinalExchange(null, null, null), Is.False);
            Assert.That(f.Flow.PendingDecisionRequest, Is.Null);
            Assert.That(f.Flow.ClashSeriesDecisionTimeRemaining, Is.Zero);
            Assert.That(f.Flow.IsClashSeriesValidationEnabled, Is.False);
            Assert.That(f.Flow.SubmitClashSeriesSkill(old, f.AllySkills[0]), Is.False);
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_RoundMotionCueIsSingleAndFinalDamageIsOnce()
        {
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            yield return RunActualPlayModeScenario();
            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        private IEnumerator RunActualPlayModeScenario()
        {
            Fixture f = CreateFixture(3, 2);
            Assert.That(f.Enable(2, 1, 2), Is.True);
            CombatClashRoundPresentationRequest round = null;
            f.Flow.ClashRoundPresentationRequested += value => round = value;
            f.DeclareAndApproach();
            Assert.That(round, Is.Not.Null);
            Assert.That(f.Enemy.HP, Is.EqualTo(10));

            CombatDirector director = _owner.AddComponent<CombatDirector>();
            int roundAttackCues = 0;
            int finalSkillCues = 0;
            director.FinalPresentationCueRaised += cue =>
            {
                if (cue.Kind == CombatFinalPresentationCueKind.AttackAction)
                    roundAttackCues++;
                if (cue.Kind == CombatFinalPresentationCueKind.WinningSkill)
                    finalSkillCues++;
            };
            Assert.That(director.BindFinalExchange(f.Flow, f.Session), Is.True);
            f.Flow.AdvanceUntilBlocked();
            f.Flow.AdvanceUntilBlocked();
            Assert.That(roundAttackCues, Is.EqualTo(1));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));

            float deadline = Time.realtimeSinceStartup + 3f;
            while (f.State.Phase != Phase.ChainDecision && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(f.State.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(roundAttackCues, Is.EqualTo(1));
            Assert.That(finalSkillCues, Is.EqualTo(1));
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
            director.UnbindFinalExchange();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_UiRendersOffReasonAndMpRecoveryTurnsSkillOn()
        {
            yield return new EnterPlayMode();
            yield return RunUiPlayModeScenario();
            yield return new ExitPlayMode();
        }

        private IEnumerator RunUiPlayModeScenario()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            f.Session.GetCombatState(f.Ally).SetMp(0);

            GameObject panel = NewUiObject("Panel");
            GameObject actors = NewUiObject("Actors", panel.transform);
            GameObject skills = NewUiObject("Skills", panel.transform);
            GameObject buttonObject = NewUiObject("OptionTemplate");
            Image image = buttonObject.AddComponent<Image>();
            Button template = buttonObject.AddComponent<Button>();
            template.targetGraphic = image;
            NewUiObject("Label", buttonObject.transform).AddComponent<Text>();
            FinalCombatUIBinder binder = _owner.AddComponent<FinalCombatUIBinder>();
            SetPrivateField(binder, "panelRoot", panel);
            SetPrivateField(binder, "actorListRoot", actors);
            SetPrivateField(binder, "skillListRoot", skills);
            SetPrivateField(binder, "optionButtonPrefab", template);
            binder.Bind(f.Session, f.State, f.Flow);

            Button actorOption = actors.GetComponentInChildren<Button>();
            Assert.That(actorOption, Is.Not.Null);
            actorOption.onClick.Invoke();
            Button off = skills.GetComponentInChildren<Button>();
            Assert.That(off, Is.Not.Null);
            Assert.That(off.interactable, Is.False);
            StringAssert.Contains("OFF (MP 부족)", off.GetComponentInChildren<Text>().text);

            f.Session.GetCombatState(f.Ally).RestoreMp(1);
            yield return null;
            yield return null;
            Button[] options = skills.GetComponentsInChildren<Button>(true);
            bool foundOn = false;
            for (int i = 0; i < options.Length; i++)
            {
                Text label = options[i].GetComponentInChildren<Text>();
                if (label != null && label.text.Contains("ON") && options[i].interactable)
                    foundOn = true;
            }
            Assert.That(foundOn, Is.True);
            binder.Unbind();
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_OptInHandShowsCardTypeCostAndOffReason()
        {
            yield return new EnterPlayMode();
            yield return RunHandUiPlayModeScenario();
            yield return new ExitPlayMode();
        }

        private IEnumerator RunHandUiPlayModeScenario()
        {
            // The third allied skill stays outside the hand; it must not create a pseudo-card button.
            Fixture f = CreateFixture(2, 2, 2, 3, 4);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, new[]
            {
                new CombatHandEntry(f.AllySkills[0], CombatCardKind.Normal),
                new CombatHandEntry(f.AllySkills[1], CombatCardKind.Clash)
            }, 2, 3, 23), Is.True);

            GameObject panel = NewUiObject("HandPanel");
            GameObject actors = NewUiObject("HandActors", panel.transform);
            GameObject skills = NewUiObject("HandSkills", panel.transform);
            GameObject buttonObject = NewUiObject("HandOptionTemplate");
            Image image = buttonObject.AddComponent<Image>();
            Button template = buttonObject.AddComponent<Button>();
            template.targetGraphic = image;
            NewUiObject("Label", buttonObject.transform).AddComponent<Text>();
            FinalCombatUIBinder binder = _owner.AddComponent<FinalCombatUIBinder>();
            SetPrivateField(binder, "panelRoot", panel);
            SetPrivateField(binder, "actorListRoot", actors);
            SetPrivateField(binder, "skillListRoot", skills);
            SetPrivateField(binder, "optionButtonPrefab", template);
            binder.Bind(f.Session, f.State, f.Flow);

            Button actorOption = actors.GetComponentInChildren<Button>();
            Assert.That(actorOption, Is.Not.Null);
            actorOption.onClick.Invoke();
            Button[] cards = skills.GetComponentsInChildren<Button>(true);
            Assert.That(cards.Length, Is.EqualTo(2));
            Assert.That(cards[0].GetComponentInChildren<Text>().text, Does.Contain("[합]"));
            Assert.That(cards[0].GetComponentInChildren<Text>().text, Does.Contain("ID "));
            Assert.That(cards[1].GetComponentInChildren<Text>().text, Does.Contain("[일반]"));
            Assert.That(cards[1].GetComponentInChildren<Text>().text, Does.Contain("ID "));
            Assert.That(cards[0].interactable, Is.False);
            Assert.That(cards[1].interactable, Is.True);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            int mpBeforeSelection = f.Session.GetCombatState(f.Ally).CurrentMp;
            cards[1].onClick.Invoke();
            Assert.That(hand.Hand.Count, Is.EqualTo(2));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(mpBeforeSelection));

            f.Session.GetCombatState(f.Ally).SetMp(0);
            yield return null;
            yield return null;
            cards = skills.GetComponentsInChildren<Button>(true);
            Assert.That(cards[1].interactable, Is.False);
            Assert.That(cards[1].GetComponentInChildren<Text>().text, Does.Contain("MP 부족"));
            binder.Unbind();
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_BpUiShowsBalanceEffectAndInvestmentLimitWithoutSpending()
        {
            yield return new EnterPlayMode();
            yield return RunBpUiPlayModeScenario();
            yield return new ExitPlayMode();
        }

        private IEnumerator RunBpUiPlayModeScenario()
        {
            Fixture f = CreateFixture(2, 2);
            Assert.That(f.Enable(3, 3, 3), Is.True);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3),
                new[]
                {
                    new CombatBpEnhancementEntry(f.AllySkills[0],
                        CombatBpEnhancementKind.ExtraHits, 2)
                }), Is.True);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, 1, true), Is.True);
            Assert.That(f.Session.BpRuntime.TryGetBalance(f.Ally, out int currentBp), Is.True);
            Assert.That(currentBp, Is.EqualTo(1));

            GameObject panel = NewUiObject("BpPanel");
            GameObject actors = NewUiObject("BpActors", panel.transform);
            GameObject skills = NewUiObject("BpSkills", panel.transform);
            GameObject commands = NewUiObject("BpCommands", panel.transform);
            GameObject buttonObject = NewUiObject("BpOptionTemplate");
            Image image = buttonObject.AddComponent<Image>();
            Button template = buttonObject.AddComponent<Button>();
            template.targetGraphic = image;
            NewUiObject("Label", buttonObject.transform).AddComponent<Text>();
            FinalCombatUIBinder binder = _owner.AddComponent<FinalCombatUIBinder>();
            SetPrivateField(binder, "panelRoot", panel);
            SetPrivateField(binder, "actorListRoot", actors);
            SetPrivateField(binder, "skillListRoot", skills);
            SetPrivateField(binder, "handoffListRoot", commands);
            SetPrivateField(binder, "optionButtonPrefab", template);
            binder.Bind(f.Session, f.State, f.Flow);

            Button actorOption = actors.GetComponentInChildren<Button>();
            Assert.That(actorOption, Is.Not.Null);
            actorOption.onClick.Invoke();
            Button skillOption = FindButtonContaining(skills, f.AllySkills[0].Name);
            Assert.That(skillOption, Is.Not.Null);
            skillOption.onClick.Invoke();

            Button noInvestment = FindButtonContaining(commands, "BP 0 · 기본 효과");
            Button oneInvestment = FindButtonContaining(commands, "BP 1 · 추가 타격 +2 ON");
            Button overBalance = FindButtonContaining(commands, "BP 2 · 추가 타격 +4 OFF");
            Assert.That(noInvestment, Is.Not.Null);
            Assert.That(oneInvestment, Is.Not.Null);
            Assert.That(oneInvestment.interactable, Is.True);
            Assert.That(overBalance, Is.Not.Null);
            Assert.That(overBalance.interactable, Is.False);

            oneInvestment.onClick.Invoke();
            Assert.That(f.Session.BpRuntime.TryGetBalance(f.Ally, out currentBp), Is.True);
            Assert.That(currentBp, Is.EqualTo(1));
            Assert.That(f.Session.BpRuntime.ApprovedSpends, Is.Empty);
            yield return null;
            binder.Unbind();
        }

        [Test]
        public void UltimateGauge_OnlyApprovedUniqueReceiptsAdvanceIndependentStages()
        {
            Fixture f = CreateFixture(Side.Allies, 2, 2);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.True);
            CombatUltimateRuntime gauge = f.Session.UltimateRuntime;
            Assert.That(gauge.TryGetStage(f.Ally, out int stage), Is.True);
            Assert.That(stage, Is.Zero);
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, cycle, true), Is.True);
                Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, cycle, true), Is.True);
            }

            CommitBpFixture(f.Session.BpRuntime, f.Ally, 1);
            gauge.TryGetStage(f.Ally, out stage);
            Assert.That(stage, Is.EqualTo(1));
            Assert.That(gauge.TryAcceptApprovedSpend(f.Session.BpRuntime.ApprovedSpends[0]), Is.False);
            CommitBpFixture(f.Session.BpRuntime, f.Ally, 1);
            gauge.TryGetStage(f.Ally, out stage);
            Assert.That(stage, Is.EqualTo(2));
            CommitBpFixture(f.Session.BpRuntime, f.Ally, 1);
            gauge.TryGetStage(f.Ally, out stage);
            Assert.That(stage, Is.EqualTo(3));
            gauge.TryGetStage(f.Enemy, out int enemyStage);
            Assert.That(enemyStage, Is.Zero);
            CommitBpFixture(f.Session.BpRuntime, f.Enemy, 3);
            gauge.TryGetStage(f.Enemy, out enemyStage);
            Assert.That(enemyStage, Is.EqualTo(3));
        }

        [Test]
        public void InitialBp_ChargesUltimateOnlyAfterAThreePointApproval_ThenUltimateResetsIt()
        {
            Fixture f = CreateFixture(Side.Allies, 2, 2);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3),
                new[] { new CombatBpEnhancementEntry(f.AllySkills[0],
                    CombatBpEnhancementKind.ExtraHits, 1) }), Is.True);
            Assert.That(f.Session.TryAssignUltimateSkill(f.Ally, f.AllySkills[0]), Is.True);
            Assert.That(f.Session.TrySetInitialBpForValidation(f.Ally, 3), Is.True);
            Assert.That(f.Session.UltimateRuntime.TryGetStage(f.Ally, out int stage), Is.True);
            Assert.That(stage, Is.Zero);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllySkills[0], null, 3),
                null, out CombatExchangeCommitResult committed), Is.True);
            Assert.That(committed.AttackBpSpent, Is.EqualTo(3));
            Assert.That(f.Session.BpRuntime.ApprovedSpends, Has.Count.EqualTo(1));
            Assert.That(f.Session.UltimateRuntime.TryGetStage(f.Ally, out stage), Is.True);
            Assert.That(stage, Is.EqualTo(3));
            Assert.That(f.Flow.ExecuteUltimate(f.Flow.PendingDecisionRequest, f.Ally, f.Enemy), Is.True);
            Assert.That(f.Session.UltimateRuntime.TryGetStage(f.Ally, out stage), Is.True);
            Assert.That(stage, Is.Zero);
        }

        [Test]
        public void UltimateCommand_RejectsInvalidConditions_ThenRunsSkillOnceAndReturnsToStandoff()
        {
            Fixture f = CreateFixture(Side.Allies, 2, 2);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.True);
            Assert.That(f.Session.TryAssignUltimateSkill(f.Ally, f.AllySkills[0]), Is.True);
            CombatExchangeDecisionRequest request = f.Flow.PendingDecisionRequest;
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, cycle, true);
                f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, cycle, true);
            }
            CommitBpFixture(f.Session.BpRuntime, f.Ally, 3);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, null), Is.False);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Ally), Is.False);
            f.Ally.SetStunned(true);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
            f.Ally.SetStunned(false);
            CombatantCombatState allyState = f.Session.GetCombatState(f.Ally);
            allyState.ApplyMentalDelta(-allyState.MaxMental);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
            allyState.ApplyMentalDelta(allyState.MaxMental);
            allyState.SetMp(0);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
            allyState.SetMp(5);
            Assert.That(f.Flow.CanExecuteUltimate(request, f.Ally, f.Enemy), Is.True);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.True);
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(f.Session.UltimateRuntime.TryGetStage(f.Ally, out int stage), Is.True);
            Assert.That(stage, Is.Zero);
            Assert.That(f.Flow.ExecuteUltimate(request, f.Ally, f.Enemy), Is.False);
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(f.Flow.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Enemies));
        }

        [Test]
        public void UltimateCommand_EnemyAuthorityAndTerminalVictoryFollowCanonicalFlow()
        {
            Fixture enemyFirst = CreateFixture(Side.Enemies, 2, 2);
            Assert.That(enemyFirst.Session.TryEnableBpValidation(
                new CombatBpValidationConfig(3, 1, 3)), Is.True);
            Assert.That(enemyFirst.Session.TryAssignUltimateSkill(
                enemyFirst.Enemy, enemyFirst.EnemySkills[0]), Is.True);
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                enemyFirst.Session.TryRecordCompletedBpAttackCycle(Side.Allies, cycle, true);
                enemyFirst.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, cycle, true);
            }
            CommitBpFixture(enemyFirst.Session.BpRuntime, enemyFirst.Enemy, 3);
            CombatExchangeDecisionRequest enemyRequest = enemyFirst.Flow.PendingDecisionRequest;
            Assert.That(enemyFirst.Flow.ExecuteUltimate(enemyRequest,
                enemyFirst.Enemy, enemyFirst.Ally), Is.True);
            Assert.That(enemyFirst.Ally.HP, Is.EqualTo(9));
            Assert.That(enemyFirst.Flow.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));

            UnityEngine.Object.DestroyImmediate(_owner);
            _owner = null;
            Fixture lethal = CreateFixture(Side.Allies, 2, 2);
            Assert.That(lethal.Session.TryEnableBpValidation(
                new CombatBpValidationConfig(3, 1, 3)), Is.True);
            Assert.That(lethal.Session.TryAssignUltimateSkill(lethal.Ally, lethal.AllySkills[0]), Is.True);
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                lethal.Session.TryRecordCompletedBpAttackCycle(Side.Allies, cycle, true);
                lethal.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, cycle, true);
            }
            CommitBpFixture(lethal.Session.BpRuntime, lethal.Ally, 3);
            lethal.Enemy.ApplyDamage(9);
            Assert.That(lethal.Flow.ExecuteUltimate(lethal.Flow.PendingDecisionRequest,
                lethal.Ally, lethal.Enemy), Is.True);
            Assert.That(lethal.State.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(lethal.State.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(lethal.Session.UltimateRuntime.TryGetStage(lethal.Ally, out _), Is.False);
        }

        [Test]
        public void UltimateGauge_IsDisposedOnExplicitCombatExit()
        {
            Fixture f = CreateFixture(Side.Allies, 2, 2);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.True);
            CombatUltimateRuntime gauge = f.Session.UltimateRuntime;
            Assert.That(f.State.TryTerminateExplicit(CombatEndReason.Abort,
                f.Session.ExchangeState.Version), Is.True);
            Assert.That(gauge.TryGetStage(f.Ally, out _), Is.False);
            Assert.That(gauge.TryAcceptApprovedSpend(null), Is.False);
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_UltimateHudChargesExecutesAndResets()
        {
            yield return new EnterPlayMode();
            Fixture f = CreateFixture(Side.Allies, 2, 2);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.True);
            Assert.That(f.Session.TryAssignUltimateSkill(f.Ally, f.AllySkills[0]), Is.True);
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, cycle, true);
                f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, cycle, true);
            }

            GameObject panel = NewUiObject("UltimatePanel");
            GameObject actors = NewUiObject("UltimateActors", panel.transform);
            GameObject commands = NewUiObject("UltimateCommands", panel.transform);
            GameObject templateObject = NewUiObject("UltimateOptionTemplate");
            Image image = templateObject.AddComponent<Image>();
            Button template = templateObject.AddComponent<Button>();
            template.targetGraphic = image;
            NewUiObject("Label", templateObject.transform).AddComponent<Text>();
            Text resources = NewUiObject("UltimateResources", panel.transform).AddComponent<Text>();
            FinalCombatUIBinder binder = _owner.AddComponent<FinalCombatUIBinder>();
            SetPrivateField(binder, "panelRoot", panel);
            SetPrivateField(binder, "actorListRoot", actors);
            SetPrivateField(binder, "handoffListRoot", commands);
            SetPrivateField(binder, "playerMpText", resources);
            SetPrivateField(binder, "optionButtonPrefab", template);
            binder.Bind(f.Session, f.State, f.Flow);
            actors.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(resources.text, Does.Contain("궁극기 0/3 [---]"));
            Assert.That(FindButtonContaining(actors, "궁극기 0/3").interactable, Is.False);

            CommitBpFixture(f.Session.BpRuntime, f.Ally, 3);
            yield return null;
            Assert.That(resources.text, Does.Contain("궁극기 3/3 [===]"));
            Button ultimate = FindButtonContaining(actors, "궁극기 3/3");
            Assert.That(ultimate, Is.Not.Null);
            Assert.That(ultimate.interactable, Is.True);
            ultimate.onClick.Invoke();
            Assert.That(f.Enemy.HP, Is.EqualTo(9));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(resources.text, Does.Contain("궁극기 0/3 [---]"));
            binder.Unbind();
            yield return new ExitPlayMode();
        }

        private static void CommitBpFixture(CombatBpRuntime runtime, ICombatant actor, int amount)
        {
            typeof(CombatBpRuntime).GetMethod("CommitInvestment",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { actor, amount });
        }

        private static Button FindButtonContaining(GameObject host, string text)
        {
            Button[] buttons = host.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>();
                if (label != null && label.text.Contains(text))
                    return buttons[i];
            }

            return null;
        }

        private GameObject NewUiObject(string name, Transform parent = null)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent ?? _owner.transform, false);
            return created;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private Fixture CreateFixture(params int[] powers)
        {
            return CreateFixture(Side.Allies, powers);
        }

        private Fixture CreateFixture(Side initiative, params int[] powers)
        {
            _owner = new GameObject("CombatClashSeriesIntegrationTests");
            CombatFlowOrchestrator flow = _owner.AddComponent<CombatFlowOrchestrator>();
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant enemy = new TestCombatant(2, Side.Enemies);
            List<TestSkill> allySkills = new List<TestSkill>();
            List<TestSkill> enemySkills = new List<TestSkill>();
            for (int i = 0; i < powers.Length; i++)
            {
                TestSkill skill = new TestSkill(i + 1, powers[i]);
                if (i % 2 == 0)
                {
                    ally.AddSkill(skill);
                    allySkills.Add(skill);
                }
                else
                {
                    enemy.AddSkill(skill);
                    enemySkills.Add(skill);
                }
            }
            CombatSession session = new CombatSession(
                initiative == Side.Allies ? StartReason.PlayerFirstHit : StartReason.PlayerGotHit,
                initiative,
                new InspirationPool(10), new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 5, 3, 0, 0f, 10f, 1f));
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            CombatStateMachine state = new CombatStateMachine(session);
            Assert.That(flow.BindFinalExchange(session, state, new ZeroRandom(), new SkillPowerProvider()), Is.True);
            return new Fixture(flow, session, state, ally, enemy, allySkills, enemySkills);
        }

        private sealed class Fixture
        {
            public CombatFlowOrchestrator Flow { get; }
            public CombatSession Session { get; }
            public CombatStateMachine State { get; }
            public TestCombatant Ally { get; }
            public TestCombatant Enemy { get; }
            public List<TestSkill> AllySkills { get; }
            public List<TestSkill> EnemySkills { get; }
            public Fixture(CombatFlowOrchestrator flow, CombatSession session, CombatStateMachine state,
                TestCombatant ally, TestCombatant enemy, List<TestSkill> allySkills, List<TestSkill> enemySkills)
            {
                Flow = flow; Session = session; State = state; Ally = ally; Enemy = enemy;
                AllySkills = allySkills; EnemySkills = enemySkills;
            }

            public bool Enable(int attackerDurability, int responderDurability, int maxComparisons)
            {
                return Flow.TryEnableClashSeriesValidation(
                    new CombatClashSeriesConfig(attackerDurability, responderDurability, maxComparisons),
                    2f,
                    series => series.FinalOutcome == CombatClashOutcome.ResponderWin
                        ? series.CommittedResponseSkills[series.CommittedResponseSkills.Count - 1]
                        : series.CommittedAttackSkills[series.CommittedAttackSkills.Count - 1],
                    request =>
                    {
                        IReadOnlyList<ISkill> available = Flow.GetAvailableClashSeriesSkills(request);
                        return available.Count > 0 ? available[0] : null;
                    });
            }

            public void DeclareAndApproach()
            {
                CombatApproachPresentationRequest approach = null;
                Flow.ApproachPresentationRequested += value => approach = value;
                Assert.That(Flow.SubmitAttackDeclaration(
                    new CombatAttackDeclaration(Ally, Enemy, AllySkills[0]),
                    Flow.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(Flow.SubmitResponse(
                    new CombatResponseDeclaration(Enemy, EnemySkills[0]),
                    Flow.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(approach, Is.Not.Null);
                Assert.That(approach.TryComplete(), Is.True);
            }
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => minInclusive;
        }

        private sealed class SkillPowerProvider : ICombatClashSideInputProvider
        {
            public bool TryCreate(ICombatant actor, ISkill skill, bool isCurrentAttackOwner,
                out CombatClashSideInput input)
            {
                input = default;
                if (actor == null || !(skill is TestSkill test))
                    return false;
                input = new CombatClashSideInput(actor.Id, skill.Id, test.Power,
                    skill.Speed, isCurrentAttackOwner);
                return true;
            }
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider, ICombatStatusEffectProvider
        {
            public SkillId Id { get; }
            public int Power { get; }
            public string Name => $"Fixture-{Id.Value}";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 1;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost => 1;
            public IReadOnlyList<CombatStatusDefinitionSO> AppliedStatusEffects { get; set; } = Array.Empty<CombatStatusDefinitionSO>();
            public TestSkill(int id, int power) { Id = new SkillId(id); Power = power; }
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;
            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? 10;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;
            public Action DamageCallback;
            public TestCombatant(int id, Side side) { Id = new CombatantId(id); Side = side; }
            public void AddSkill(ISkill skill) => _skills.Add(skill);
            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount) { _state?.ApplyDamage(amount); DamageCallback?.Invoke(); }
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
