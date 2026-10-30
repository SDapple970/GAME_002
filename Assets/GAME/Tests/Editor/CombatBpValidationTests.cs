#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    public sealed class CombatBpValidationTests
    {
        // Every numeric value here is a validation fixture, not Production balance.
        [Test]
        public void RoundCycles_AreIndependentOfHandRefill_AndExcludeDuplicateEmptyAndInactiveCycles()
        {
            Fixture f = NewFixture();
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 2, 3)), Is.True);
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 2, 3)), Is.False);
            Assert.That(Balance(f, f.Ally), Is.Zero);
            Assert.That(f.Session.BpRuntime.RoundCyclesUntilGain, Is.EqualTo(2));
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 1, true), Is.False);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, 2, false), Is.False);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, 2, true), Is.True);
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(1));
            Assert.That(Balance(f, f.Ally), Is.Zero);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 3, true), Is.True);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, 4, true), Is.True);
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(2));
            Assert.That(Balance(f, f.Ally), Is.EqualTo(1));
            Assert.That(Balance(f, f.Enemy), Is.EqualTo(1));
            Assert.That(f.Session.HasHandValidation, Is.False);

            f.Enemy.SetStunned(true);
            GainRounds(f, 3, 4);
            Assert.That(Balance(f, f.Ally), Is.EqualTo(2));
            Assert.That(Balance(f, f.Enemy), Is.EqualTo(1));
            f.Enemy.SetStunned(false);
            f.Enemy.ApplyDamage(99);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 9, true), Is.False);
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(4));
        }

        [Test]
        public void Balance_IsCapped_AndEachInvestmentFromZeroThroughThreeIsApprovedOnce()
        {
            for (int investment = 0; investment <= 3; investment++)
            {
                Fixture f = NewFixture();
                EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
                if (investment > 0) GainRounds(f, 1, investment * 2);
                Assert.That(Balance(f, f.Ally), Is.EqualTo(investment));
                Assert.That(f.Session.BpRuntime.CanInvest(f.Ally, investment), Is.True);
                Assert.That(f.Session.BpRuntime.CanInvest(f.Ally, investment + 1), Is.False);
                Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                    new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllySkill, null, investment),
                    null, out CombatExchangeCommitResult result), Is.True);
                Assert.That(result.AttackBpSpent, Is.EqualTo(investment));
                Assert.That(Balance(f, f.Ally), Is.Zero);
                Assert.That(f.Session.BpRuntime.ApprovedSpends.Count,
                    Is.EqualTo(investment == 0 ? 0 : 1));
            }

            Fixture capped = NewFixture();
            EnableBp(capped, CombatBpEnhancementKind.ExtraHits, 1);
            GainRounds(capped, 1, 8);
            Assert.That(Balance(capped, capped.Ally), Is.EqualTo(3));
            Assert.That(Balance(capped, capped.Enemy), Is.EqualTo(3));
        }

        [Test]
        public void ExplicitInitialBp_IsOneTimeValidationSetup_AndDoesNotCreateAnUltimateReceipt()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
            Assert.That(f.Session.TryAssignUltimateSkill(f.Ally, f.AllySkill), Is.True);
            Assert.That(Balance(f, f.Ally), Is.Zero);

            Assert.That(f.Session.TrySetInitialBpForValidation(f.Ally, 3), Is.True);
            Assert.That(Balance(f, f.Ally), Is.EqualTo(3));
            Assert.That(f.Session.BpRuntime.ApprovedSpends, Is.Empty);
            Assert.That(f.Session.UltimateRuntime.TryGetStage(f.Ally, out int stage), Is.True);
            Assert.That(stage, Is.Zero);
            Assert.That(f.Session.TrySetInitialBpForValidation(f.Ally, 0), Is.False);
            Assert.That(f.Session.TrySetInitialBpForValidation(f.Enemy, 4), Is.False);

            Assert.That(f.StateMachine.TryDeclareAttack(new CombatAttackDeclaration(
                f.Ally, f.Enemy, f.AllySkill), f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.Session.TrySetInitialBpForValidation(f.Enemy, 1), Is.False);
        }

        [Test]
        public void RepeatedOneSideCycles_WaitForMatchingOppositeCycles()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ClashPower, 1);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, 2, true), Is.True);
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.Zero);
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, 3, true), Is.True);
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(1));
            Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, 4, true), Is.True);
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(2));
            Assert.That(Balance(f, f.Ally), Is.EqualTo(1));
            Assert.That(Balance(f, f.Enemy), Is.EqualTo(1));
        }

        [Test]
        public void InsufficientMp_RejectsBpAndCardTogether()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
            GainRounds(f, 1, 2);
            Assert.That(f.Session.TryEnableHandValidation(f.Ally,
                new[] { new CombatHandEntry(f.AllySkill, CombatCardKind.Normal) }, 1, 0, 7), Is.True);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            long cardId = hand.Hand[0].RuntimeId;
            CombatantCombatState state = f.Session.GetCombatState(f.Ally);
            Assert.That(state.TrySpendMp(state.CurrentMp), Is.True);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllySkill, cardId, 1),
                null, out CombatExchangeCommitResult denied), Is.False);
            Assert.That(denied.Failure, Is.EqualTo(CombatExchangeCommitFailure.InsufficientAttackMp));
            Assert.That(Balance(f, f.Ally), Is.EqualTo(1));
            Assert.That(hand.Hand[0].RuntimeId, Is.EqualTo(cardId));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(f.Session.BpRuntime.ApprovedSpends.Count, Is.Zero);
        }

        [Test]
        public void Commit_PaysMpCardAndBpOnce_WhilePreviewStaleAndOverinvestmentPayNothing()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
            GainRounds(f, 1, 2);
            Assert.That(f.Session.TryEnableHandValidation(f.Ally,
                new[] { new CombatHandEntry(f.AllySkill, CombatCardKind.Normal) }, 1, 0, 7), Is.True);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            long cardId = hand.Hand[0].RuntimeId;
            int mp = f.Session.GetCombatState(f.Ally).CurrentMp;
            Assert.That(Balance(f, f.Ally), Is.EqualTo(1));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);

            CombatAttackDeclaration expensive = new(f.Ally, f.Enemy, f.AllySkill, cardId, 2);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session, expensive, null,
                out CombatExchangeCommitResult denied), Is.False);
            Assert.That(denied.Failure, Is.EqualTo(CombatExchangeCommitFailure.InsufficientAttackBp));
            Assert.That(Balance(f, f.Ally), Is.EqualTo(1));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(mp));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);

            int staleVersion = f.Session.ExchangeState.Version;
            CombatAttackDeclaration attack = new(f.Ally, f.Enemy, f.AllySkill, cardId, 1);
            Assert.That(f.StateMachine.TryDeclareAttack(attack, staleVersion), Is.True);
            Assert.That(f.StateMachine.TryDeclareAttack(attack, staleVersion), Is.False);
            Assert.That(f.StateMachine.ConfirmNoResponse(f.Session.ExchangeState.Version), Is.True);
            int commitVersion = f.Session.ExchangeState.Version;
            Assert.That(f.StateMachine.TryCommitExchange(commitVersion), Is.True);
            Assert.That(f.StateMachine.TryCommitExchange(commitVersion), Is.False);
            Assert.That(Balance(f, f.Ally), Is.Zero);
            Assert.That(f.Session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(1));
            Assert.That(f.Session.BpRuntime.ApprovedSpends[0].Amount, Is.EqualTo(1));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(mp - 1));
            Assert.That(hand.NormalDiscard[0].RuntimeId, Is.EqualTo(cardId));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
        }

        [Test]
        public void ThreeClashComparisons_CountNoCycles_AndBpBoostsOnlyConfiguredPower()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ClashPower, 2);
            GainRounds(f, 1, 2);
            CombatClashSeriesState series = BeginSeries(f, 1, 3, 4, 3);
            int hp = f.Enemy.HP;
            Assert.That(series.TryResolveRound(series.CommandToken,
                Input(f.Ally, f.AllySkill, 2, true),
                Input(f.Enemy, f.EnemySkill, 2, false), out CombatClashRoundResult first), Is.True);
            Assert.That(first.AttackerPower, Is.EqualTo(4));
            Assert.That(first.ResponderPower, Is.EqualTo(2));
            Assert.That(first.ResponderDurabilityAfter, Is.EqualTo(2));
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(2));
            Assert.That(f.Enemy.HP, Is.EqualTo(hp));
            Assert.That(series.TryCommitNextRound(series.CommandToken,
                f.AllySkill, f.EnemySkill, out _), Is.True);
            Assert.That(series.TryResolveRound(series.CommandToken,
                Input(f.Ally, f.AllySkill, 2, true),
                Input(f.Enemy, f.EnemySkill, 2, false), out CombatClashRoundResult second), Is.True);
            Assert.That(second.AttackerPower, Is.EqualTo(2));
            Assert.That(second.FinalOutcome, Is.Null);
            Assert.That(series.TryCommitNextRound(series.CommandToken,
                f.AllySkill, f.EnemySkill, out _), Is.True);
            Assert.That(series.TryResolveRound(series.CommandToken,
                Input(f.Ally, f.AllySkill, 3, true),
                Input(f.Enemy, f.EnemySkill, 2, false), out CombatClashRoundResult third), Is.True);
            Assert.That(third.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(f.Session.BpRuntime.RoundCycleCount, Is.EqualTo(2));
            Assert.That(f.Enemy.HP, Is.EqualTo(hp));
            Assert.That(f.Session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(1));
        }

        [Test]
        public void PanicRejectsDeclarationWithoutSpendingMpBpOrCard()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
            GainRounds(f, 1, 2);
            Assert.That(f.Session.TryEnableHandValidation(f.Ally,
                new[] { new CombatHandEntry(f.AllySkill, CombatCardKind.Normal) }, 1, 0, 7), Is.True);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            long cardId = hand.Hand[0].RuntimeId;
            CombatantCombatState state = f.Session.GetCombatState(f.Ally);
            state.ApplyMentalDelta(-state.MaxMental);
            int mp = state.CurrentMp;
            int bp = Balance(f, f.Ally);
            CombatAttackDeclaration declaration = new(f.Ally, f.Enemy, f.AllySkill, cardId, 1);
            Assert.That(f.StateMachine.TryDeclareAttack(declaration), Is.False);
            Assert.That(f.StateMachine.TryDeclareAttack(declaration, f.Session.ExchangeState.Version), Is.False);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session, declaration, null, out _), Is.False);
            Assert.That(state.CurrentMp, Is.EqualTo(mp));
            Assert.That(Balance(f, f.Ally), Is.EqualTo(bp));
            Assert.That(hand.Hand[0].RuntimeId, Is.EqualTo(cardId));
            Assert.That(hand.NormalDiscard, Is.Empty);
            Assert.That(f.Session.BpRuntime.ApprovedSpends, Is.Empty);
        }

        [TestCase(CombatBpEnhancementKind.ExtraHits, 8, 1)]
        [TestCase(CombatBpEnhancementKind.StatusApplications, 9, 2)]
        public void MultiHitAndExplicitStatusApplications_UseTheirSeparateContracts(
            CombatBpEnhancementKind kind, int expectedHp, int expectedStacks)
        {
            Fixture f = NewFixture();
            CombatStatusDefinitionSO status = CreateStatusFixture();
            try
            {
                f.AllySkill.AppliedStatusEffects = new[] { status };
                EnableBp(f, kind, 1);
                GainRounds(f, 1, 2);
                CombatSkillExecutionRequest request = ResolveSeriesAction(f);
                Assert.That(SkillRunner.TryExecute(f.Session, request, out CombatSkillExecutionResult result), Is.True);
                Assert.That(f.Enemy.HP, Is.EqualTo(expectedHp));
                Assert.That(f.Session.GetCombatState(f.Enemy).TryGetStatus("06b.bp.status", out CombatStatusRuntime current), Is.True);
                Assert.That(current.StackCount, Is.EqualTo(expectedStacks));
                Assert.That(result.AppliedStatusResults.Count, Is.EqualTo(expectedStacks));
            }
            finally { UnityEngine.Object.DestroyImmediate(status); }
        }

        [TestCase(CombatBpEnhancementKind.ExtraHits)]
        [TestCase(CombatBpEnhancementKind.Healing)]
        public void LethalDamage_DoesNotApplyStatusOrReviveTarget(CombatBpEnhancementKind kind)
        {
            Fixture f = NewFixture();
            CombatStatusDefinitionSO status = CreateStatusFixture();
            try
            {
                f.AllySkill.AppliedStatusEffects = new[] { status };
                EnableBp(f, kind, 1);
                GainRounds(f, 1, 2);
                f.Enemy.ApplyDamage(9);
                CombatSkillExecutionRequest request = ResolveSeriesAction(f);
                Assert.That(SkillRunner.TryExecute(f.Session, request, out CombatSkillExecutionResult result), Is.True);
                Assert.That(f.Enemy.HP, Is.Zero);
                Assert.That(f.Session.GetCombatState(f.Enemy).HasStatus("06b.bp.status"), Is.False);
                Assert.That(result.AppliedStatusResults, Is.Empty);
                Assert.That(f.Session.GetCombatState(f.Enemy).CanApplyStatus(status), Is.False);
                Assert.That(f.Session.GetCombatState(f.Enemy).ApplyStatus(status), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(status); }
        }

        [Test]
        public void AbortDuringDamage_StopsRemainingHitsStatusAndDelayedReplay()
        {
            Fixture f = NewFixture();
            CombatStatusDefinitionSO status = CreateStatusFixture();
            try
            {
                f.AllySkill.AppliedStatusEffects = new[] { status };
                EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
                GainRounds(f, 1, 2);
                CombatSkillExecutionRequest request = ResolveSeriesAction(f);
                f.Enemy.DamageCallback = () => f.StateMachine.ForceExit(CombatEndReason.Abort);
                Assert.That(SkillRunner.TryExecute(f.Session, request, out CombatSkillExecutionResult result), Is.True);
                Assert.That(f.Enemy.HP, Is.EqualTo(9));
                Assert.That(result.AppliedStatusResults, Is.Empty);
                Assert.That(f.Session.GetCombatState(f.Enemy).HasStatus("06b.bp.status"), Is.False);
                Assert.That(SkillRunner.TryExecute(f.Session, request, out _), Is.False);
                Assert.That(f.Enemy.HP, Is.EqualTo(9));
            }
            finally { UnityEngine.Object.DestroyImmediate(status); }
        }

        private static CombatStatusDefinitionSO CreateStatusFixture()
        {
            CombatStatusDefinitionSO status = UnityEngine.ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            typeof(CombatStatusDefinitionSO).GetField("statusId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(status, "06b.bp.status");
            typeof(CombatStatusDefinitionSO).GetField("stackPolicy", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(status, CombatStatusStackPolicy.Stack);
            typeof(CombatStatusDefinitionSO).GetField("maximumStacks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(status, 5);
            return status;
        }

        private static CombatSkillExecutionRequest ResolveSeriesAction(Fixture f)
        {
            CombatClashSeriesState series = BeginSeries(f, 1, 1, 2, 1);
            Assert.That(series.TryResolveRound(series.CommandToken,
                Input(f.Ally, f.AllySkill, 4, true), Input(f.Enemy, f.EnemySkill, 2, false), out _), Is.True);
            Assert.That(series.TryClaimFinalOutcome(series.CommandToken, f.AllySkill, out CombatClashResult final), Is.True);
            Assert.That(CombatOutcomeSelector.TrySelect(final, out CombatOutcomeAction action), Is.True);
            Assert.That(CombatOutcomeTargetResolver.TryResolve(action, f.Session, out CombatSkillExecutionRequest request), Is.True);
            return request;
        }

        [Test]
        public void ExtraHits_ExecuteWithinOneApprovedAction_WithoutRepeatingCostsOrFinalOutcome()
        {
            Fixture f = NewFixture();
            EnableBp(f, CombatBpEnhancementKind.ExtraHits, 1);
            GainRounds(f, 1, 2);
            CombatClashSeriesState series = BeginSeries(f, 1, 1, 2, 1);
            Assert.That(series.TryResolveRound(series.CommandToken,
                Input(f.Ally, f.AllySkill, 4, true),
                Input(f.Enemy, f.EnemySkill, 2, false), out _), Is.True);
            long token = series.CommandToken;
            Assert.That(series.TryClaimFinalOutcome(token, f.AllySkill, out CombatClashResult final), Is.True);
            Assert.That(CombatOutcomeSelector.TrySelect(final, out CombatOutcomeAction action), Is.True);
            Assert.That(CombatOutcomeTargetResolver.TryResolve(action, f.Session,
                out CombatSkillExecutionRequest request), Is.True);
            Assert.That(request.BpInvestment, Is.EqualTo(1));
            int hp = f.Enemy.HP;
            Assert.That(SkillRunner.TryExecute(f.Session, request, out _), Is.True);
            Assert.That(f.Enemy.HP, Is.EqualTo(hp - 2));
            Assert.That(series.TryClaimFinalOutcome(token, f.AllySkill, out _), Is.False);
            Assert.That(f.Session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(1));
        }

        [Test]
        public void V1AndUnconfiguredSkill_KeepZeroBpAndOriginalEffect()
        {
            Fixture f = NewFixture();
            Assert.That(f.Session.BpRuntime, Is.Null);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllySkill), null, out _), Is.True);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllySkill, null, 1), null,
                out CombatExchangeCommitResult rejected), Is.False);
            Assert.That(rejected.Failure, Is.EqualTo(CombatExchangeCommitFailure.InsufficientAttackBp));
            Assert.That(CombatBpEnhancementPolicy.Magnitude(f.Session, f.AllySkill, 1,
                CombatBpEnhancementKind.ClashPower), Is.Zero);
        }

        private static Fixture NewFixture()
        {
            Actor ally = new(1, Side.Allies);
            Actor enemy = new(2, Side.Enemies);
            Skill allySkill = new(1);
            Skill enemySkill = new(2);
            ally.AddSkill(allySkill);
            enemy.AddSkill(enemySkill);
            CombatSession session = new(StartReason.PlayerFirstHit, Side.Allies,
                new InspirationPool(10), new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain, new CombatRuntimeConfig(10, 8, 3, 0));
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new(session);
            stateMachine.Tick();
            return new Fixture(session, stateMachine, ally, enemy, allySkill, enemySkill);
        }

        private static void EnableBp(Fixture f, CombatBpEnhancementKind kind, int perPoint)
        {
            Assert.That(f.Session.TryEnableBpValidation(new CombatBpValidationConfig(3, 2, 3),
                new[] { new CombatBpEnhancementEntry(f.AllySkill, kind, perPoint) }), Is.True);
        }

        private static int Balance(Fixture f, Actor actor)
        {
            Assert.That(f.Session.BpRuntime.TryGetBalance(actor, out int amount), Is.True);
            return amount;
        }

        private static void GainRounds(Fixture f, int firstRound, int lastRound)
        {
            for (int round = firstRound; round <= lastRound; round++)
            {
                Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Allies, round * 2 - 1, true), Is.True);
                Assert.That(f.Session.TryRecordCompletedBpAttackCycle(Side.Enemies, round * 2, true), Is.True);
            }
        }

        private static CombatClashSeriesState BeginSeries(Fixture f, int investment,
            int attackerDurability, int responderDurability, int comparisons)
        {
            Assert.That(f.StateMachine.TryDeclareAttack(new CombatAttackDeclaration(
                f.Ally, f.Enemy, f.AllySkill, null, investment),
                f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.StateMachine.TryDeclareResponse(new CombatResponseDeclaration(
                f.Enemy, f.EnemySkill), f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.StateMachine.TryCommitExchange(f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.StateMachine.TryBeginApproach(f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.StateMachine.CompleteApproach(f.Session.ExchangeState.Version), Is.True);
            return new CombatClashSeriesState(f.Session, f.StateMachine,
                new CombatClashSeriesConfig(attackerDurability, responderDurability, comparisons));
        }

        private static CombatClashSideInput Input(Actor actor, Skill skill, int power, bool owner) =>
            new(actor.Id, skill.Id, power, 1, owner);

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public Actor Ally { get; }
            public Actor Enemy { get; }
            public Skill AllySkill { get; }
            public Skill EnemySkill { get; }
            public Fixture(CombatSession session, CombatStateMachine stateMachine, Actor ally,
                Actor enemy, Skill allySkill, Skill enemySkill)
            {
                Session = session; StateMachine = stateMachine; Ally = ally;
                Enemy = enemy; AllySkill = allySkill; EnemySkill = enemySkill;
            }
        }

        private sealed class Skill : ISkill, ICombatMpCostProvider, ICombatStatusEffectProvider
        {
            public SkillId Id { get; }
            public string Name => "BP fixture";
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
            public Skill(int id) => Id = new SkillId(id);
        }

        private sealed class Actor : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new();
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
            public Actor(int id, Side side) { Id = new CombatantId(id); Side = side; }
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
