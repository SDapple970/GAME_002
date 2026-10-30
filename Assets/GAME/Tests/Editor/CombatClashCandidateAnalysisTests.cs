using System;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    /// <summary>Proposed numbers only. Reuses B3B's isolated Production entry fixture; writes no assets.</summary>
    public sealed class CombatClashCandidateAnalysisTests
    {
        private CombatClashPlaytestV1IntegrationTests _fixture;
        private CombatEntryPoint _entry;
        private CombatFlowOrchestrator _flow;
        private CombatSession Session => _entry.ActiveSession;
        private ICombatant Ally => Session.Allies[0];
        private ICombatant Enemy => Session.Enemies[0];
        private System.Collections.Generic.List<CombatClashRoundPresentationRequest> Rounds =>
            Get<System.Collections.Generic.List<CombatClashRoundPresentationRequest>>("_rounds");
        private CombatOutcomePresentationRequest Outcome => Get<CombatOutcomePresentationRequest>("_outcome");

        [TearDown]
        public void TearDown() { Time.timeScale = 1f; _fixture?.TearDown(); }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void ConstantAdvantage_IsolatesDurability_WithZeroCostAndOrdinaryEnemy(int durability, int cap)
        {
            Prepare(durability, cap); Begin(); var series = Session.ExchangeState.ClashSeries;
            for (int round = 1; round <= durability; round++)
            {
                Assert.That(series.ComparisonCount, Is.EqualTo(round));
                Assert.That(series.ResponderDurability, Is.EqualTo(durability - round));
                Assert.That(Enemy.HP, Is.EqualTo(10));
                Assert.That(Rounds.Last().TryComplete(), Is.True);
                if (round < durability) CommitNext();
            }
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(series.AttackMpSpent, Is.Zero); Assert.That(series.ResponseMpSpent, Is.Zero);
            Session.TryGetHand(Ally, out var hand);
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(1)); Assert.That(hand.ClashDiscard.Count, Is.EqualTo(durability - 1));
            Assert.That(Enemy.HP, Is.EqualTo(9));
            Assert.That(Rounds.Last().TryComplete(), Is.False);
            Assert.That(Outcome.TryComplete(), Is.True); Assert.That(Outcome.TryComplete(), Is.False);
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(Session.GetCombatState(Enemy).CurrentMental, Is.EqualTo(85));
            Record("delta1_zero_cost", durability, cap, series);
        }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void PowerDifferenceAtLeastDurability_EndsInOneComparison(int durability, int cap)
        {
            Prepare(durability, cap, advantage: durability); Begin(); var series = Session.ExchangeState.ClashSeries;
            Assert.That(series.ComparisonCount, Is.EqualTo(1)); Assert.That(series.ResponderDurability, Is.Zero);
            Assert.That(Rounds.Last().TryComplete(), Is.True); Assert.That(Enemy.HP, Is.EqualTo(9));
            Assert.That(Rounds.Last().TryComplete(), Is.False); Assert.That(Enemy.HP, Is.EqualTo(9));
            Record("large_delta", durability, cap, series);
        }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void EqualPower_BothZeroTakesPriorityOverComparisonCap(int durability, int cap)
        {
            Prepare(durability, cap, advantage: 0); Begin(); var series = Session.ExchangeState.ClashSeries;
            for (int round = 1; round <= durability; round++)
            {
                Assert.That(series.AttackerDurability, Is.EqualTo(durability - round));
                Assert.That(series.ResponderDurability, Is.EqualTo(durability - round));
                Rounds.Last().TryComplete(); if (round < durability) CommitNext();
            }
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin), "Equal speed uses attack authority; the cap does not replace a zero-durability result.");
            Assert.That(Enemy.HP, Is.EqualTo(9)); Record("equal_power", durability, cap, series);
        }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void AlternatingApprovedBpAdvantage_ReachesCapDrawWithoutDamage(int durability, int cap)
        {
            Prepare(durability, cap, advantage: 0); InvokeFixture("PrimeBp");
            Begin(1, 0); var series = Session.ExchangeState.ClashSeries;
            for (int round = 1; round <= cap; round++)
            {
                Assert.That(Enemy.HP, Is.EqualTo(10)); Assert.That(Ally.HP, Is.EqualTo(10));
                Rounds.Last().TryComplete();
                if (round < cap) { bool nextIsOdd = (round + 1) % 2 == 1; CommitNext(nextIsOdd ? 1 : 0, nextIsOdd ? 0 : 1); }
            }
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.Tie));
            Assert.That(series.AttackerDurability, Is.EqualTo(durability - cap / 2));
            Assert.That(series.ResponderDurability, Is.EqualTo(durability - (cap + 1) / 2));
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(Outcome, Is.Null); Assert.That(Session.GetCombatState(Enemy).CurrentMental, Is.EqualTo(100));
            Assert.That(Session.BpRuntime.RoundCycleCount, Is.EqualTo(6), "Fixture priming only; comparisons grant no cycles.");
            Record("alternating_bp", durability, cap, series);
        }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void ExistingInitialMpTwo_WithCostOne_LimitsImmediateComparisons(int durability, int cap)
        {
            Prepare(durability, cap, mpCost: 1, initialMp: 2, enemyHand: true); Begin();
            var series = Session.ExchangeState.ClashSeries;
            Rounds.Last().TryComplete(); CommitNext(); Rounds.Last().TryComplete();
            Assert.That(series.ComparisonCount, Is.EqualTo(2));
            Assert.That(series.AttackMpSpent, Is.EqualTo(2)); Assert.That(series.ResponseMpSpent, Is.EqualTo(2));
            if (durability == 2)
            {
                Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin)); Assert.That(Enemy.HP, Is.EqualTo(9));
            }
            else AssertBlockedAttackTimesOutToDraw(series);
            Record("initial_mp2_cost1", durability, cap, series);
        }

        [Test]
        public void LongCandidate_ExistingMaxMpThree_StillCannotPayFourCostOneAttacks()
        {
            Prepare(4, 4, mpCost: 1, initialMp: 3); Begin(); var series = Session.ExchangeState.ClashSeries;
            Rounds.Last().TryComplete(); CommitNext(); Rounds.Last().TryComplete(); CommitNext(); Rounds.Last().TryComplete();
            Assert.That(series.ComparisonCount, Is.EqualTo(3)); Assert.That(series.AttackMpSpent, Is.EqualTo(3));
            AssertBlockedAttackTimesOutToDraw(series); Record("max_mp3_cost1", 4, 4, series);
        }

        [Test]
        public void LongCandidate_EnemyThreeClashCards_MakesFourthAttackUnopposed()
        {
            Prepare(4, 4, enemyHand: true); Begin(); var series = Session.ExchangeState.ClashSeries;
            Rounds.Last().TryComplete(); CommitNext(); Rounds.Last().TryComplete(); CommitNext(); Rounds.Last().TryComplete();
            Session.TryGetHand(Enemy, out var enemyHand); Assert.That(enemyHand.ClashDiscard.Count, Is.EqualTo(3));
            SubmitNextAttack();
            var response = _flow.PendingDecisionRequest;
            Assert.That(_flow.GetSkillAvailability(response, Enemy, Enemy.Skills[1]).Reason, Is.EqualTo(CombatSkillUnavailableReason.NotInHand));
            Assert.That(_flow.SubmitClashSeriesNoResponse(response), Is.True);
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(series.ComparisonCount, Is.EqualTo(3)); Assert.That(series.ResponderDurability, Is.EqualTo(1));
            Session.TryGetHand(Ally, out var hand); Assert.That(hand.ClashDiscard.Count, Is.EqualTo(3));
            Assert.That(Enemy.HP, Is.EqualTo(9)); Record("enemy_hand3", 4, 4, series);
        }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void Timeout_IsPerDecision_UnscaledPauseAware_AndRejectsOldRequest(int durability, int cap)
        {
            Prepare(durability, cap); Begin(); var series = Session.ExchangeState.ClashSeries;
            Rounds.Last().TryComplete(); var attack = _flow.PendingDecisionRequest;
            _flow.Tick(0f, 4.9f); Assert.That(_flow.PendingDecisionRequest, Is.SameAs(attack));
            Time.timeScale = 0f; _flow.Tick(0f, 100f); Assert.That(_flow.PendingDecisionRequest, Is.SameAs(attack));
            Time.timeScale = 1f; _flow.Tick(0f, 0.2f);
            Assert.That(_flow.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashResponse));
            Assert.That(_flow.SubmitClashSeriesSkill(attack, Ally.Skills[1]), Is.False);
            _flow.Tick(0f, 5.1f);
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(Enemy.HP, Is.EqualTo(9)); Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.Zero);
            Assert.That(Outcome.TryComplete(), Is.True); Assert.That(Outcome.TryComplete(), Is.False);
            Record("timeout_response", durability, cap, series);
        }

        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void Abort_ClearsCandidateTimerAndDelayedCommands(int durability, int cap)
        {
            Prepare(durability, cap); Begin(); var session = Session; var series = session.ExchangeState.ClashSeries;
            Rounds.Last().TryComplete(); var old = _flow.PendingDecisionRequest;
            _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Invoke(_entry, "Update");
            Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_flow.PendingDecisionRequest, Is.Null);
            Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.Zero); Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
            Assert.That(_flow.SubmitClashSeriesSkill(old, session.Allies[0].Skills[1]), Is.False);
            Assert.That(session.Enemies[0].HP, Is.EqualTo(10));
            Record("abort", durability, cap, series);
        }

        private void Prepare(int durability, int cap, int advantage = 1, int mpCost = 0, int initialMp = 3, bool enemyHand = false)
        {
            _fixture = new CombatClashPlaytestV1IntegrationTests(); InvokeFixture("Prepare", 1, enemyHand, true);
            var config = Get<FinalCombatRuntimeConfigSO>("_config");
            Set(config, "clashAttackerDurability", durability); Set(config, "clashResponderDurability", durability); Set(config, "clashMaxComparisons", cap);
            Set(config, "initialMp", initialMp);
            var definitions = Get<SkillDefinitionSO[]>("_definitions");
            foreach (var definition in definitions) Set(definition, "finalMpCost", mpCost);
            Set(definitions[0], "clashPower", 2 + advantage); Set(definitions[1], "clashPower", 2 + advantage);
            _entry = Get<CombatEntryPoint>("_entry"); _flow = Get<CombatFlowOrchestrator>("_flow"); InvokeFixture("Start");
            // The analysis chooses explicit responses/BP to reproduce known power sequences.
            // Existing B3B tests separately exercise the actual enemy controller and Director.
            ((FinalCombatEnemyCommandController)GetField(_entry, "_finalEnemyCommandController")).Unbind();
        }

        private void Begin(int attackBp = 0, int responseBp = 0)
        {
            InvokeFixture("Declare", attackBp);
            Assert.That(_flow.SubmitResponse(new CombatResponseDeclaration(Enemy, Enemy.Skills[1], ResponseCardId(), responseBp), Session.ExchangeState.Version), Is.True);
            Assert.That(Get<CombatApproachPresentationRequest>("_approach").TryComplete(), Is.True);
        }
        private long? ResponseCardId() => Session.TryGetHand(Enemy, out var hand) ? hand.Hand.First(card => card.Kind == CombatCardKind.Clash).RuntimeId : null;
        private void SubmitNextAttack(int bp = 0)
        {
            var request = _flow.PendingDecisionRequest; Session.TryGetHand(Ally, out var hand);
            var card = hand.Hand.First(value => value.Kind == CombatCardKind.Clash);
            int mp = Session.GetCombatState(Ally).CurrentMp;
            Assert.That(_flow.SubmitClashSeriesSkill(request, card.Skill, card.RuntimeId, bp), Is.True);
            int committedMp = Session.GetCombatState(Ally).CurrentMp;
            Assert.That(_flow.SubmitClashSeriesSkill(request, card.Skill, card.RuntimeId, bp), Is.False);
            Assert.That(Session.GetCombatState(Ally).CurrentMp, Is.EqualTo(committedMp));
            Assert.That(committedMp, Is.LessThanOrEqualTo(mp));
        }
        private void CommitNext(int attackBp = 0, int responseBp = 0)
        {
            SubmitNextAttack(attackBp); var request = _flow.PendingDecisionRequest;
            Assert.That(_flow.SubmitClashSeriesSkill(request, Enemy.Skills[1], ResponseCardId(), responseBp), Is.True);
        }
        private void AssertBlockedAttackTimesOutToDraw(CombatClashSeriesState series)
        {
            var old = _flow.PendingDecisionRequest;
            Assert.That(_flow.GetSkillAvailability(old, Ally, Ally.Skills[1]).Reason, Is.EqualTo(CombatSkillUnavailableReason.InsufficientMp));
            int mp = Session.GetCombatState(Ally).CurrentMp;
            _flow.Tick(100f, 4f); Assert.That(Session.GetCombatState(Ally).CurrentMp, Is.EqualTo(mp), "Clash time does not recover MP.");
            Assert.That(_flow.PendingDecisionRequest, Is.SameAs(old)); _flow.Tick(0f, 1.1f);
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.Tie)); Assert.That(Enemy.HP, Is.EqualTo(10));
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(_flow.SubmitClashSeriesSkill(old, Ally.Skills[1]), Is.False);
        }
        private static void Record(string scenario, int durability, int cap, CombatClashSeriesState series) =>
            TestContext.WriteLine($"CANDIDATE,{scenario},{durability},{cap},{series.ComparisonCount},{series.AttackerDurability},{series.ResponderDurability},{series.FinalOutcome},{series.AttackMpSpent},{series.ResponseMpSpent}");
        private T Get<T>(string field) => (T)GetField(_fixture, field);
        private object InvokeFixture(string method, params object[] args) => typeof(CombatClashPlaytestV1IntegrationTests).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_fixture, args);
        private static object GetField(object owner, string field) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
    }
}
