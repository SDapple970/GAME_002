#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Core;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatAttackDeclarationTests
    {
        [Test]
        public void Declaration_ExposesImmutableParticipantsSkillAndSide()
        {
            Fixture fixture = CreateStandoffFixture();
            CombatAttackDeclaration declaration = new CombatAttackDeclaration(
                fixture.Ally,
                fixture.Enemy,
                fixture.AllySkill);

            Assert.That(declaration.Attacker, Is.SameAs(fixture.Ally));
            Assert.That(declaration.Target, Is.SameAs(fixture.Enemy));
            Assert.That(declaration.Skill, Is.SameAs(fixture.AllySkill));
            Assert.That(declaration.DeclaringSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void Declaration_RejectsNullParticipantsAndSkillWithoutMutatingState()
        {
            Fixture fixture = CreateStandoffFixture();
            fixture.StateMachine.Tick(0.5f);
            float pressure = fixture.Session.StandoffState.CurrentPressure;

            Assert.That(fixture.StateMachine.TryDeclareAttack(null, fixture.Enemy, fixture.AllySkill), Is.False);
            Assert.That(fixture.StateMachine.TryDeclareAttack(fixture.Ally, null, fixture.AllySkill), Is.False);
            Assert.That(fixture.StateMachine.TryDeclareAttack(fixture.Ally, fixture.Enemy, null), Is.False);
            AssertUnchangedStandoff(fixture, pressure);
        }

        [Test]
        public void Declaration_RejectsCombatantsOutsideRoster()
        {
            Fixture fixture = CreateStandoffFixture();
            DummyCombatant outsiderAlly = CreateCombatant(2, Side.Allies);
            outsiderAlly.AddSkill(fixture.AllySkill);
            DummyCombatant outsiderEnemy = CreateCombatant(101, Side.Enemies);

            Assert.That(
                fixture.StateMachine.TryDeclareAttack(outsiderAlly, fixture.Enemy, fixture.AllySkill),
                Is.False);
            Assert.That(
                fixture.StateMachine.TryDeclareAttack(fixture.Ally, outsiderEnemy, fixture.AllySkill),
                Is.False);
            AssertUnchangedStandoff(fixture, 0f);
        }

        [Test]
        public void Declaration_RejectsDeadAttackerAndDeadTarget()
        {
            Fixture deadAttacker = CreateStandoffFixture();
            deadAttacker.Ally.ApplyDamage(int.MaxValue);
            Assert.That(
                deadAttacker.StateMachine.TryDeclareAttack(
                    deadAttacker.Ally,
                    deadAttacker.Enemy,
                    deadAttacker.AllySkill),
                Is.False);

            Fixture deadTarget = CreateStandoffFixture();
            deadTarget.Enemy.ApplyDamage(int.MaxValue);
            Assert.That(
                deadTarget.StateMachine.TryDeclareAttack(
                    deadTarget.Ally,
                    deadTarget.Enemy,
                    deadTarget.AllySkill),
                Is.False);
        }

        [Test]
        public void Declaration_RejectsFriendlyTargetAndSkillNotOwnedByAttacker()
        {
            Fixture fixture = CreateStandoffFixture(additionalAlly: true);

            Assert.That(
                fixture.StateMachine.TryDeclareAttack(
                    fixture.Ally,
                    fixture.AdditionalAlly,
                    fixture.AllySkill),
                Is.False);
            Assert.That(
                fixture.StateMachine.TryDeclareAttack(
                    fixture.Ally,
                    fixture.Enemy,
                    fixture.EnemySkill),
                Is.False);
            AssertUnchangedStandoff(fixture, 0f);
        }

        [Test]
        public void PlayerDeclaration_TransitionsAndPreservesLegacyResources()
        {
            Fixture fixture = CreateStandoffFixture();
            int turnIndex = fixture.Session.TurnIndex;
            int inspiration = fixture.Session.Inspiration.Current;
            int mp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;

            bool declared = fixture.StateMachine.TryDeclareAttack(
                fixture.Ally,
                fixture.Enemy,
                fixture.AllySkill);

            Assert.That(declared, Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Not.Null);
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration.Attacker, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Session.ExchangeState.InitialInitiative, Is.EqualTo(Side.Enemies));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mp));
        }

        [Test]
        public void Declaration_RejectsLegacyNonStandoffAndExitedStateMachines()
        {
            Fixture legacy = CreateFixture(CombatFlowMode.LegacyPlanning);
            legacy.StateMachine.Tick();
            Assert.That(
                legacy.StateMachine.TryDeclareAttack(legacy.Ally, legacy.Enemy, legacy.AllySkill),
                Is.False);

            Fixture nonStandoff = CreateStandoffFixture();
            SetPhase(nonStandoff.StateMachine, Phase.Approach);
            Assert.That(
                nonStandoff.StateMachine.TryDeclareAttack(
                    nonStandoff.Ally,
                    nonStandoff.Enemy,
                    nonStandoff.AllySkill),
                Is.False);

            Fixture exited = CreateStandoffFixture();
            exited.StateMachine.ForceExit(CombatEndReason.Abort);
            Assert.That(
                exited.StateMachine.TryDeclareAttack(exited.Ally, exited.Enemy, exited.AllySkill),
                Is.False);
        }

        [Test]
        public void PressureReady_DoesNotTransitionWithoutEnemyDecision()
        {
            Fixture fixture = CreateStandoffFixture();

            fixture.StateMachine.Tick(1f);
            fixture.StateMachine.Tick(10f);

            Assert.That(fixture.Session.StandoffState.IsPressureReady, Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
        }

        [Test]
        public void EnemyDecision_RequiresReadyPressureAndValidExplicitPolicyResult()
        {
            Fixture fixture = CreateStandoffFixture();
            FakeEnemyDecisionPolicy policy = new FakeEnemyDecisionPolicy(
                new CombatAttackDeclaration(fixture.Enemy, fixture.Ally, fixture.EnemySkill));

            Assert.That(fixture.StateMachine.TrySubmitEnemyDecision(policy), Is.False);
            Assert.That(policy.CallCount, Is.Zero);

            fixture.StateMachine.Tick(1f);
            Assert.That(fixture.StateMachine.TrySubmitEnemyDecision(policy), Is.True);

            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration.Attacker, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
        }

        [Test]
        public void InvalidEnemyDecision_DoesNotResetReadyPressureOrChooseFallback()
        {
            Fixture fixture = CreateStandoffFixture();
            fixture.StateMachine.Tick(1f);
            FakeEnemyDecisionPolicy noDecision = new FakeEnemyDecisionPolicy(null, succeeds: false);

            Assert.That(fixture.StateMachine.TrySubmitEnemyDecision(noDecision), Is.False);

            Assert.That(noDecision.CallCount, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.StandoffState.IsPressureReady, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
        }

        [Test]
        public void SuccessfulDeclarationEndsPressureCycleAndNextStandoffClearsStaleDeclaration()
        {
            Fixture fixture = CreateStandoffFixture();
            fixture.StateMachine.Tick(0.5f);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.GreaterThan(0f));
            Assert.That(
                fixture.StateMachine.TryDeclareAttack(fixture.Ally, fixture.Enemy, fixture.AllySkill),
                Is.True);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Not.Null);

            // Simulates the future ApplyOutcome/ChainDecision transition back to EnterCombat.
            SetPhase(fixture.StateMachine, Phase.EnterCombat);
            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);

            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
        }

        [Test]
        public void Approach_ValidDeclarationTransitionsAndPreservesExchangeAndResources()
        {
            Fixture fixture = CreateStandoffFixture();
            int turnIndex = fixture.Session.TurnIndex;
            int inspiration = fixture.Session.Inspiration.Current;
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            CombatAttackDeclaration declaration = DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.SameAs(declaration));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Session.ExchangeState.InitialInitiative, Is.EqualTo(Side.Enemies));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));
        }

        [Test]
        public void Approach_RejectsMissingDeclarationWrongPhaseLegacyAndExitedSession()
        {
            Fixture missing = CreateStandoffFixture();
            SetPhase(missing.StateMachine, Phase.AttackDeclaration);
            Assert.That(missing.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(missing.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));

            Fixture standoff = CreateStandoffFixture();
            Assert.That(standoff.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(standoff.StateMachine.Phase, Is.EqualTo(Phase.Standoff));

            Fixture legacy = CreateFixture(CombatFlowMode.LegacyPlanning);
            legacy.StateMachine.Tick();
            Assert.That(legacy.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(legacy.StateMachine.Phase, Is.EqualTo(Phase.Planning));

            Fixture exited = CreateStandoffFixture();
            DeclarePlayerAttack(exited);
            exited.StateMachine.ForceExit(CombatEndReason.Abort);
            Assert.That(exited.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(exited.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
        }

        [Test]
        public void Approach_RejectsAttackerOrTargetThatDiedAfterDeclarationWithoutMutation()
        {
            Fixture deadAttacker = CreateStandoffFixture();
            CombatAttackDeclaration attackerDeclaration = DeclarePlayerAttack(deadAttacker);
            Assert.That(deadAttacker.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(deadAttacker.StateMachine.TryCommitExchange(), Is.True);
            deadAttacker.Ally.ApplyDamage(int.MaxValue);
            Assert.That(deadAttacker.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(deadAttacker.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(deadAttacker.Session.ExchangeState.CurrentDeclaration, Is.SameAs(attackerDeclaration));

            Fixture deadTarget = CreateStandoffFixture();
            CombatAttackDeclaration targetDeclaration = DeclarePlayerAttack(deadTarget);
            Assert.That(deadTarget.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(deadTarget.StateMachine.TryCommitExchange(), Is.True);
            deadTarget.Enemy.ApplyDamage(int.MaxValue);
            Assert.That(deadTarget.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(deadTarget.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(deadTarget.Session.ExchangeState.CurrentDeclaration, Is.SameAs(targetDeclaration));
        }

        [Test]
        public void ApproachCompletion_TransitionsToClashAndKeepsDeclaration()
        {
            Fixture fixture = CreateStandoffFixture();
            CombatAttackDeclaration declaration = DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);

            Assert.That(fixture.StateMachine.CompleteApproach(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.SameAs(declaration));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.StateMachine.CompleteApproach(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
        }

        [Test]
        public void ApproachCompletion_RejectsWrongPhase()
        {
            Fixture fixture = CreateStandoffFixture();
            DeclarePlayerAttack(fixture);

            Assert.That(fixture.StateMachine.CompleteApproach(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
        }

        [Test]
        public void ApproachPresentation_IsRequestedOnceAndCompletionReturnsToCore()
        {
            Fixture fixture = CreateStandoffFixture();
            CombatAttackDeclaration declaration = DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            int requests = 0;
            System.Action completion = null;
            fixture.StateMachine.OnRequireApproachPlay += (requested, onComplete) =>
            {
                requests++;
                Assert.That(requested, Is.SameAs(declaration));
                completion = onComplete;
            };

            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            Assert.That(requests, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(completion, Is.Not.Null);
            completion();
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
        }

        [Test]
        public void ApproachCoreLifecycle_DoesNotRequireGameObjectPresentation()
        {
            Fixture fixture = CreateStandoffFixture();
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
            fixture.StateMachine.Tick();
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(fixture.StateMachine.CompleteApproach(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
        }

        [Test]
        public void Response_AttackDeclarationStartsPendingWithoutCurrentResponse()
        {
            Fixture fixture = CreateStandoffFixture();

            DeclarePlayerAttack(fixture);

            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.Pending));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
        }

        [Test]
        public void Response_ValidCounterCommitsWithoutChangingExchangeOwnershipOrResources()
        {
            Fixture fixture = CreateStandoffFixture();
            int turnIndex = fixture.Session.TurnIndex;
            int inspiration = fixture.Session.Inspiration.Current;
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            Side initialInitiative = fixture.Session.ExchangeState.InitialInitiative;
            DeclarePlayerAttack(fixture);
            float pressure = fixture.Session.StandoffState.CurrentPressure;

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse.Responder, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse.Skill, Is.SameAs(fixture.EnemySkill));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Session.ExchangeState.InitialInitiative, Is.EqualTo(initialInitiative));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
        }

        [Test]
        public void Response_RejectsNonTargetFriendlyUnownedAndDeadResponderWithoutMutation()
        {
            Fixture fixture = CreateStandoffFixture(additionalAlly: true);
            TestSkill unownedSkill = new TestSkill(99);
            fixture.AdditionalAlly.AddSkill(unownedSkill);
            DeclarePlayerAttack(fixture);

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.AdditionalAlly, unownedSkill), Is.False);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Ally, fixture.AllySkill), Is.False);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, unownedSkill), Is.False);
            fixture.Enemy.ApplyDamage(int.MaxValue);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.False);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.Pending));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
        }

        [Test]
        public void Response_NoResponseIsExplicitAndAllowsApproachAndClash()
        {
            Fixture fixture = CreateStandoffFixture();
            DeclarePlayerAttack(fixture);

            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
        }

        [Test]
        public void Response_CounterAllowsApproachAndClashAndRemainsCommitted()
        {
            Fixture fixture = CreateStandoffFixture();
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            CombatResponseDeclaration response = fixture.Session.ExchangeState.CurrentResponse;
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.SameAs(response));
        }

        [Test]
        public void Response_ApproachLocksCounterAndNoResponseChanges()
        {
            Fixture counter = CreateStandoffFixture();
            DeclarePlayerAttack(counter);
            Assert.That(counter.StateMachine.TryDeclareResponse(counter.Enemy, counter.EnemySkill), Is.True);
            Assert.That(counter.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(counter.StateMachine.TryBeginApproach(), Is.True);
            Assert.That(counter.StateMachine.ConfirmNoResponse(), Is.False);
            Assert.That(counter.StateMachine.TryDeclareResponse(counter.Enemy, counter.EnemySkill), Is.False);
            Assert.That(counter.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));

            Fixture declined = CreateStandoffFixture();
            DeclarePlayerAttack(declined);
            Assert.That(declined.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(declined.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(declined.StateMachine.TryBeginApproach(), Is.True);
            Assert.That(declined.StateMachine.TryDeclareResponse(declined.Enemy, declined.EnemySkill), Is.False);
            Assert.That(declined.StateMachine.ConfirmNoResponse(), Is.False);
            Assert.That(declined.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
        }

        [Test]
        public void Response_LegacyModeRejectsCommandsAndKeepsPlanningFlow()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.False);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
        }

        [Test]
        public void Response_EnteringNextStandoffClearsStaleResponseState()
        {
            Fixture fixture = CreateStandoffFixture();
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);

            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.Pending));
        }

        [Test]
        public void MpCostContract_IsAdditiveNormalizedAndIndependentFromInspiration()
        {
            TestSkill positive = new TestSkill(10, 3);
            TestSkill negative = new TestSkill(11, -5);
            LegacySkill unsupported = new LegacySkill(12, inspirationCost: 7);

            Assert.That(CombatMpCostResolver.Resolve(positive), Is.EqualTo(3));
            Assert.That(CombatMpCostResolver.Resolve(negative), Is.Zero);
            Assert.That(CombatMpCostResolver.Resolve(unsupported), Is.Zero);
            Assert.That(unsupported.InspirationCost, Is.EqualTo(7));
        }

        [Test]
        public void SkillDefinitionMpCost_DefaultsToZeroAndSoSkillUsesAuthoredValue()
        {
            SkillDefinitionSO definition = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            try
            {
                definition.inspirationCost = 6;
                Assert.That(CombatMpCostResolver.Resolve(new SoSkill(definition)), Is.Zero);

                FieldInfo field = typeof(SkillDefinitionSO).GetField("mpCost", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(definition, 4);

                Assert.That(CombatMpCostResolver.Resolve(new SoSkill(definition)), Is.EqualTo(4));
                Assert.That(definition.inspirationCost, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ExchangeCommit_RequiresResolvedResponseAndAllowsExactlyOneCommit()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 0);
            DeclarePlayerAttack(fixture);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.False);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
        }

        [Test]
        public void ExchangeCommit_NoResponseConsumesOnlyAttackerMp()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 3, responseMpCost: 2, initialMp: 4);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(4));
            Assert.That(fixture.Session.ExchangeState.CommittedAttackMpCost, Is.EqualTo(3));
            Assert.That(fixture.Session.ExchangeState.CommittedResponseMpCost, Is.Zero);
        }

        [Test]
        public void ExchangeCommit_NoResponseAttackerShortageDoesNotConsumeMp()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 3, initialMp: 2);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
        }

        [Test]
        public void ExchangeCommit_CounterConsumesBothCostsAtomicallyAndPreservesState()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 2, responseMpCost: 3, initialMp: 4);
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            Side initiative = fixture.Session.ExchangeState.InitialInitiative;
            CombatAttackDeclaration declaration = DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            CombatResponseDeclaration response = fixture.Session.ExchangeState.CurrentResponse;
            float pressure = fixture.Session.StandoffState.CurrentPressure;

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(1));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.SameAs(declaration));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.SameAs(response));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Session.ExchangeState.InitialInitiative, Is.EqualTo(initiative));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
        }

        [TestCase(3, 1)]
        [TestCase(1, 3)]
        public void ExchangeCommit_InsufficientCounterPayerConsumesNeither(int attackMpCost, int responseMpCost)
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: attackMpCost, responseMpCost: responseMpCost, initialMp: 2);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
        }

        [Test]
        public void ExchangeCommit_ExactCounterCostsSucceedAndLockResponse()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 3, responseMpCost: 3, initialMp: 3);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.Zero);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.Zero);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.False);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.False);
        }

        [Test]
        public void ExchangeCommit_RevalidatesParticipantsAndSkillWithoutPayment()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 2, initialMp: 4);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            fixture.Enemy.ApplyDamage(int.MaxValue);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
        }

        [Test]
        public void ExchangeCommit_NextStandoffClearsCommitAndStaleExchange()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);

            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.Session.ExchangeState.CommittedAttackMpCost, Is.Zero);
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
        }

        [Test]
        public void ExchangeCommit_LegacyModeRejectsCommitAndKeepsInspirationPlanning()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning, attackMpCost: 3);
            int inspiration = fixture.Session.Inspiration.Current;
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration + 1));
        }

        [Test]
        public void UncommittedResponse_CounterCanBeReplacedWithoutSpendingMpAndInvalidReplacementPreservesIt()
        {
            Fixture fixture = CreateStandoffFixture(responseMpCost: 3);
            TestSkill replacementSkill = new TestSkill(20, mpCost: 1);
            TestSkill invalidSkill = new TestSkill(21, mpCost: 0);
            fixture.Enemy.AddSkill(replacementSkill);
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, replacementSkill), Is.True);

            CombatResponseDeclaration replacement = fixture.Session.ExchangeState.CurrentResponse;
            Assert.That(replacement.Skill, Is.SameAs(replacementSkill));
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, invalidSkill), Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.SameAs(replacement));
        }

        [Test]
        public void UncommittedResponse_CounterAndNoResponseCanReplaceEachOtherWithoutSpendingMp()
        {
            Fixture fixture = CreateStandoffFixture(responseMpCost: 2);
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse.Skill, Is.SameAs(fixture.EnemySkill));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));
        }

        [Test]
        public void FailedCounterCommit_CanReplaceWithAffordableCounterAndCommit()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, responseMpCost: 3, initialMp: 2);
            TestSkill affordable = new TestSkill(22, mpCost: 1);
            fixture.Enemy.AddSkill(affordable);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(2));

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, affordable), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(1));
        }

        [Test]
        public void FailedCounterCommit_CanChangeToNoResponseAndCommitAttackerOnly()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, responseMpCost: 3, initialMp: 2);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);

            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
        }

        [Test]
        public void UncommittedAttack_CancelReturnsToFreshStandoffAndClearsExchange()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 2);
            fixture.StateMachine.Tick(0.5f);
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            Side initialInitiative = fixture.Session.ExchangeState.InitialInitiative;
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);

            Assert.That(fixture.StateMachine.TryCancelAttackDeclaration(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(fixture.Session.ExchangeState.ResponseState, Is.EqualTo(CombatResponseState.Pending));
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.Session.ExchangeState.CommittedAttackMpCost, Is.Zero);
            Assert.That(fixture.Session.ExchangeState.CommittedResponseMpCost, Is.Zero);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Session.ExchangeState.InitialInitiative, Is.EqualTo(initialInitiative));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));

            fixture.StateMachine.Tick(0.25f);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void CommittedExchange_RejectsResponseChangesAndAttackCancel()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, responseMpCost: 1);
            TestSkill replacement = new TestSkill(23, mpCost: 0);
            fixture.Enemy.AddSkill(replacement);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            CombatResponseDeclaration committedResponse = fixture.Session.ExchangeState.CurrentResponse;

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, replacement), Is.False);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.False);
            Assert.That(fixture.StateMachine.TryCancelAttackDeclaration(), Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.SameAs(committedResponse));
            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
        }

        [Test]
        public void AttackerShortage_CanCancelWithoutMpLoss()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 3, initialMp: 2);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.StateMachine.TryCancelAttackDeclaration(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
        }

        [Test]
        public void LegacyMode_RejectsAttackCancelAndResponseEditingWhileKeepingPlanning()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.False);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.False);
            Assert.That(fixture.StateMachine.TryCancelAttackDeclaration(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
        }

        [Test]
        public void ClashRequest_CommittedCounterProvidesImmutableDeclarationViewToRule()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, responseMpCost: 1);
            PrepareCounterClash(fixture);
            FakeClashRule rule = new FakeClashRule(CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryResolveClash(rule), Is.True);

            Assert.That(rule.CallCount, Is.EqualTo(1));
            Assert.That(rule.LastRequest.AttackDeclaration, Is.SameAs(fixture.Session.ExchangeState.CurrentDeclaration));
            Assert.That(rule.LastRequest.ResponseDeclaration, Is.SameAs(fixture.Session.ExchangeState.CurrentResponse));
            Assert.That(rule.LastRequest.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
            Assert.That(rule.LastRequest.Attacker, Is.SameAs(fixture.Ally));
            Assert.That(rule.LastRequest.Target, Is.SameAs(fixture.Enemy));
            Assert.That(rule.LastRequest.AttackSkill, Is.SameAs(fixture.AllySkill));
            Assert.That(rule.LastRequest.ResponseSkill, Is.SameAs(fixture.EnemySkill));
        }

        [Test]
        public void ClashResolve_UncommittedAndPendingExchangesAreRejectedWithoutResult()
        {
            Fixture pending = CreateStandoffFixture();
            DeclarePlayerAttack(pending);
            SetPhase(pending.StateMachine, Phase.Clash);
            Assert.That(pending.StateMachine.TryResolveClash(new FakeClashRule(CombatClashOutcome.AttackerWin)), Is.False);
            Assert.That(pending.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(pending.Session.ExchangeState.CurrentClashResult, Is.Null);

            Fixture uncommitted = CreateStandoffFixture();
            DeclarePlayerAttack(uncommitted);
            Assert.That(uncommitted.StateMachine.ConfirmNoResponse(), Is.True);
            SetPhase(uncommitted.StateMachine, Phase.Clash);
            Assert.That(uncommitted.StateMachine.TryResolveClash(), Is.False);
            Assert.That(uncommitted.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(uncommitted.Session.ExchangeState.CurrentClashResult, Is.Null);
        }

        [Test]
        public void ClashResolve_NoResponseCreatesUnopposedWithoutRuleOrResourceMutation()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, initialMp: 4);
            PrepareNoResponseClash(fixture);
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int targetMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;

            Assert.That(fixture.StateMachine.TryResolveClash(), Is.True);

            CombatClashResult result = fixture.Session.ExchangeState.CurrentClashResult;
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(result.Outcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(result.AttackDeclaration, Is.SameAs(fixture.Session.ExchangeState.CurrentDeclaration));
            Assert.That(result.ResponseDeclaration, Is.Null);
            Assert.That(result.Winner, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(targetMp));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
        }

        [TestCase(CombatClashOutcome.AttackerWin)]
        [TestCase(CombatClashOutcome.ResponderWin)]
        [TestCase(CombatClashOutcome.Tie)]
        public void ClashResolve_CounterPreservesRuleOutcomeAndExpectedWinner(CombatClashOutcome outcome)
        {
            Fixture fixture = CreateStandoffFixture();
            PrepareCounterClash(fixture);

            Assert.That(fixture.StateMachine.TryResolveClash(new FakeClashRule(outcome)), Is.True);

            CombatClashResult result = fixture.Session.ExchangeState.CurrentClashResult;
            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(result.AttackDeclaration, Is.SameAs(fixture.Session.ExchangeState.CurrentDeclaration));
            Assert.That(result.ResponseDeclaration, Is.SameAs(fixture.Session.ExchangeState.CurrentResponse));
            ICombatant expectedWinner = outcome == CombatClashOutcome.AttackerWin
                ? fixture.Ally
                : outcome == CombatClashOutcome.ResponderWin ? fixture.Enemy : null;
            Assert.That(result.Winner, Is.SameAs(expectedWinner));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void ClashResolve_CounterWithoutRuleOrWithInvalidOutcomeFailsSafely()
        {
            Fixture missingRule = CreateStandoffFixture();
            PrepareCounterClash(missingRule);
            Assert.That(missingRule.StateMachine.TryResolveClash(), Is.False);
            Assert.That(missingRule.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(missingRule.Session.ExchangeState.CurrentClashResult, Is.Null);

            Fixture invalidRule = CreateStandoffFixture();
            PrepareCounterClash(invalidRule);
            Assert.That(
                invalidRule.StateMachine.TryResolveClash(new FakeClashRule(CombatClashOutcome.Unopposed)),
                Is.False);
            Assert.That(invalidRule.StateMachine.Phase, Is.EqualTo(Phase.Clash));
            Assert.That(invalidRule.Session.ExchangeState.CurrentClashResult, Is.Null);
        }

        [Test]
        public void ClashResolve_PreservesCommittedExchangeAndAllResources()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, responseMpCost: 2, initialMp: 4);
            PrepareCounterClash(fixture);
            CombatAttackDeclaration declaration = fixture.Session.ExchangeState.CurrentDeclaration;
            CombatResponseDeclaration response = fixture.Session.ExchangeState.CurrentResponse;
            Side attackSide = fixture.Session.ExchangeState.CurrentAttackSide;
            Side initiative = fixture.Session.ExchangeState.InitialInitiative;
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int responderMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            int attackCost = fixture.Session.ExchangeState.CommittedAttackMpCost;
            int responseCost = fixture.Session.ExchangeState.CommittedResponseMpCost;

            Assert.That(fixture.StateMachine.TryResolveClash(new FakeClashRule(CombatClashOutcome.Tie)), Is.True);

            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.SameAs(declaration));
            Assert.That(fixture.Session.ExchangeState.CurrentResponse, Is.SameAs(response));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(attackSide));
            Assert.That(fixture.Session.ExchangeState.InitialInitiative, Is.EqualTo(initiative));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(responderMp));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.ExchangeState.CommittedAttackMpCost, Is.EqualTo(attackCost));
            Assert.That(fixture.Session.ExchangeState.CommittedResponseMpCost, Is.EqualTo(responseCost));
        }

        [Test]
        public void ClashResult_NextStandoffAndDeclarationStartWithoutStaleResult()
        {
            Fixture fixture = CreateStandoffFixture();
            PrepareNoResponseClash(fixture);
            Assert.That(fixture.StateMachine.TryResolveClash(), Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentClashResult, Is.Not.Null);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentClashResult, Is.Null);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.Session.ExchangeState.CurrentClashResult, Is.Null);
        }

        [Test]
        public void ClashResolve_LegacyModeRejectsAndKeepsPlanningFlow()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryResolveClash(new FakeClashRule(CombatClashOutcome.AttackerWin)), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.ExchangeState.CurrentClashResult, Is.Null);
        }

        [TestCase(CombatClashOutcome.AttackerWin)]
        [TestCase(CombatClashOutcome.ResponderWin)]
        [TestCase(CombatClashOutcome.Unopposed)]
        [TestCase(CombatClashOutcome.Tie)]
        public void OutcomeSelector_MapsClashResultToExpectedAction(CombatClashOutcome outcome)
        {
            Fixture fixture = CreateStandoffFixture();
            ResolveToApplyOutcome(fixture, outcome);
            CombatClashResult result = fixture.Session.ExchangeState.CurrentClashResult;

            Assert.That(CombatOutcomeSelector.TrySelect(result, out CombatOutcomeAction action), Is.True);

            if (outcome == CombatClashOutcome.Tie)
            {
                Assert.That(action, Is.Null);
                return;
            }

            bool responderWins = outcome == CombatClashOutcome.ResponderWin;
            Assert.That(action.Actor, Is.SameAs(responderWins ? fixture.Enemy : fixture.Ally));
            Assert.That(action.Skill, Is.SameAs(responderWins ? fixture.EnemySkill : fixture.AllySkill));
            Assert.That(action.Opponent, Is.SameAs(responderWins ? fixture.Ally : fixture.Enemy));
            Assert.That(action.SourceOutcome, Is.EqualTo(outcome));
        }

        [Test]
        public void OutcomePrepare_StoresActionAndKeepsApplyOutcomePhase()
        {
            Fixture fixture = CreateStandoffFixture();
            ResolveToApplyOutcome(fixture, CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.Session.ExchangeState.IsOutcomePrepared, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentOutcomeAction, Is.Not.Null);
            Assert.That(fixture.Session.ExchangeState.CurrentOutcomeAction.Actor, Is.SameAs(fixture.Ally));
        }

        [Test]
        public void OutcomePrepare_TieCompletesPreparationWithoutAction()
        {
            Fixture fixture = CreateStandoffFixture();
            ResolveToApplyOutcome(fixture, CombatClashOutcome.Tie);

            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.Session.ExchangeState.IsOutcomePrepared, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentOutcomeAction, Is.Null);
        }

        [Test]
        public void OutcomePrepare_RejectsMissingResultWrongPhaseAndDuplicatePreparation()
        {
            Fixture missing = CreateStandoffFixture();
            SetPhase(missing.StateMachine, Phase.ApplyOutcome);
            Assert.That(missing.StateMachine.TryPrepareOutcome(), Is.False);
            Assert.That(missing.Session.ExchangeState.IsOutcomePrepared, Is.False);

            Fixture wrongPhase = CreateStandoffFixture();
            ResolveToApplyOutcome(wrongPhase, CombatClashOutcome.Unopposed);
            SetPhase(wrongPhase.StateMachine, Phase.Clash);
            Assert.That(wrongPhase.StateMachine.TryPrepareOutcome(), Is.False);
            Assert.That(wrongPhase.Session.ExchangeState.IsOutcomePrepared, Is.False);

            Fixture duplicate = CreateStandoffFixture();
            ResolveToApplyOutcome(duplicate, CombatClashOutcome.Unopposed);
            Assert.That(duplicate.StateMachine.TryPrepareOutcome(), Is.True);
            CombatOutcomeAction action = duplicate.Session.ExchangeState.CurrentOutcomeAction;
            Assert.That(duplicate.StateMachine.TryPrepareOutcome(), Is.False);
            Assert.That(duplicate.Session.ExchangeState.CurrentOutcomeAction, Is.SameAs(action));
        }

        [Test]
        public void OutcomePrepare_PreservesExchangeResourcesHpAndPosture()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, responseMpCost: 2, initialMp: 4);
            ResolveToApplyOutcome(fixture, CombatClashOutcome.ResponderWin);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            CombatClashResult result = exchange.CurrentClashResult;
            CombatAttackDeclaration declaration = exchange.CurrentDeclaration;
            CombatResponseDeclaration response = exchange.CurrentResponse;
            Side attackSide = exchange.CurrentAttackSide;
            Side initiative = exchange.InitialInitiative;
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int responderMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            int attackPosture = fixture.Session.GetCombatState(fixture.Ally).CurrentPosture;
            int responsePosture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;
            int attackerHp = fixture.Ally.HP;
            int responderHp = fixture.Enemy.HP;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            int attackCost = exchange.CommittedAttackMpCost;
            int responseCost = exchange.CommittedResponseMpCost;

            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.True);

            Assert.That(exchange.CurrentClashResult, Is.SameAs(result));
            Assert.That(exchange.CurrentDeclaration, Is.SameAs(declaration));
            Assert.That(exchange.CurrentResponse, Is.SameAs(response));
            Assert.That(exchange.CurrentAttackSide, Is.EqualTo(attackSide));
            Assert.That(exchange.InitialInitiative, Is.EqualTo(initiative));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(responderMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentPosture, Is.EqualTo(attackPosture));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(responsePosture));
            Assert.That(fixture.Ally.HP, Is.EqualTo(attackerHp));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(responderHp));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(exchange.CommittedAttackMpCost, Is.EqualTo(attackCost));
            Assert.That(exchange.CommittedResponseMpCost, Is.EqualTo(responseCost));
        }

        [Test]
        public void OutcomePrepare_NextStandoffClearsPreparedStateAndAction()
        {
            Fixture fixture = CreateStandoffFixture();
            ResolveToApplyOutcome(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);

            Assert.That(fixture.Session.ExchangeState.IsOutcomePrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentOutcomeAction, Is.Null);
        }

        [Test]
        public void OutcomePrepare_LegacyModeRejectsAndKeepsPlanningFlow()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.ExchangeState.IsOutcomePrepared, Is.False);
        }

        [Test]
        public void ExecutionRequest_PreservesActionAndUsesReadOnlyTargetSnapshot()
        {
            Fixture fixture = CreateStandoffFixture();
            PrepareOutcome(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);

            CombatSkillExecutionRequest request = fixture.Session.ExchangeState.CurrentExecutionRequest;
            Assert.That(request.Actor, Is.SameAs(fixture.Ally));
            Assert.That(request.Skill, Is.SameAs(fixture.AllySkill));
            Assert.That(request.Opponent, Is.SameAs(fixture.Enemy));
            Assert.That(request.SourceOutcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(request.Targets, Is.EqualTo(new[] { fixture.Enemy }));
            Assert.That(
                () => ((IList<ICombatant>)request.Targets).Add(fixture.Ally),
                Throws.TypeOf<System.NotSupportedException>());
        }

        [TestCase(TargetingRule.Self)]
        [TestCase(TargetingRule.SingleEnemy)]
        public void ExecutionPrepare_ResolvesExplicitSingleTargetRules(TargetingRule targeting)
        {
            Fixture fixture = CreateStandoffFixture(attackTargeting: targeting);
            PrepareOutcome(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);

            ICombatant expected = targeting == TargetingRule.Self ? fixture.Ally : fixture.Enemy;
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest.Targets, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void ExecutionPrepare_SingleEnemyForEnemyActorTargetsHostileOpponent()
        {
            Fixture fixture = CreateStandoffFixture(responseTargeting: TargetingRule.SingleEnemy);
            PrepareOutcome(fixture, CombatClashOutcome.ResponderWin);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest.Actor, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest.Targets, Is.EqualTo(new[] { fixture.Ally }));
        }

        [Test]
        public void CommitPreflight_SingleAllyWithoutSelectionFailsWithoutPayment()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 1,
                responseMpCost: 2,
                responseTargeting: TargetingRule.SingleAlly);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int responderMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);

            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(responderMp));
        }

        [Test]
        public void ExecutionPrepare_AllEnemiesSnapshotsEveryLivingEnemy()
        {
            Fixture fixture = CreateStandoffFixture(attackTargeting: TargetingRule.AllEnemies);
            PrepareOutcome(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest.Targets, Is.EqualTo(new[] { fixture.Enemy }));
        }

        [Test]
        public void ExecutionPrepare_AllEnemiesForEnemyActorTargetsHostileAllies()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalAlly: true,
                responseTargeting: TargetingRule.AllEnemies);
            PrepareOutcome(fixture, CombatClashOutcome.ResponderWin);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(
                fixture.Session.ExchangeState.CurrentExecutionRequest.Targets,
                Is.EqualTo(new[] { fixture.Ally, fixture.AdditionalAlly }));
        }

        [Test]
        public void ExecutionPrepare_AllAlliesSnapshotsEveryLivingAlly()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalAlly: true,
                attackTargeting: TargetingRule.AllAllies);
            PrepareOutcome(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(
                fixture.Session.ExchangeState.CurrentExecutionRequest.Targets,
                Is.EqualTo(new[] { fixture.Ally, fixture.AdditionalAlly }));
        }

        [Test]
        public void ExecutionPrepare_AllAlliesForEnemyActorTargetsFriendlyEnemies()
        {
            Fixture fixture = CreateStandoffFixture(responseTargeting: TargetingRule.AllAllies);
            PrepareOutcome(fixture, CombatClashOutcome.ResponderWin);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest.Targets, Is.EqualTo(new[] { fixture.Enemy }));
        }

        [Test]
        public void ExecutionPrepare_NoneCreatesRequestWithoutCombatantTargets()
        {
            Fixture fixture = CreateStandoffFixture(attackTargeting: TargetingRule.None);
            PrepareOutcome(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest.Targets, Is.Empty);
        }

        [Test]
        public void CommitPreflight_EnvironmentAttackFailsBeforePaymentAndCanCancel()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 2,
                attackTargeting: TargetingRule.Environment);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            int mp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int hp = fixture.Enemy.HP;
            int posture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(exchange.IsCommitted, Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mp));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(posture));
            Assert.That(fixture.StateMachine.TryCancelAttackDeclaration(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
        }

        [Test]
        public void CommitPreflight_EnvironmentCounterFailsAtomically()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 1,
                responseMpCost: 2,
                responseTargeting: TargetingRule.Environment);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int responderMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);

            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(responderMp));
        }

        [Test]
        public void CommitPreflight_AnySingleWithoutSelectionFailsWithoutPayment()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 2,
                attackTargeting: TargetingRule.AnySingle);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            int mp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;

            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);

            Assert.That(fixture.Session.ExchangeState.IsCommitted, Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mp));
        }

        [Test]
        public void CommitPreflight_FailedCounterCanBeReplacedAndCommitted()
        {
            Fixture fixture = CreateStandoffFixture(responseTargeting: TargetingRule.SingleAlly);
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.False);
            TestSkill replacement = new TestSkill(3, targeting: TargetingRule.SingleEnemy);
            fixture.Enemy.AddSkill(replacement);

            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, replacement), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
        }

        [Test]
        public void ExecutionPrepare_RequiresPreparedOutcomeApplyOutcomeAndRejectsDuplicate()
        {
            Fixture notPrepared = CreateStandoffFixture();
            Assert.That(notPrepared.StateMachine.TryPrepareSkillExecution(), Is.False);

            Fixture wrongPhase = CreateStandoffFixture();
            PrepareOutcome(wrongPhase, CombatClashOutcome.Unopposed);
            SetPhase(wrongPhase.StateMachine, Phase.Clash);
            Assert.That(wrongPhase.StateMachine.TryPrepareSkillExecution(), Is.False);

            Fixture duplicate = CreateStandoffFixture();
            PrepareOutcome(duplicate, CombatClashOutcome.Unopposed);
            Assert.That(duplicate.StateMachine.TryPrepareSkillExecution(), Is.True);
            CombatSkillExecutionRequest request = duplicate.Session.ExchangeState.CurrentExecutionRequest;
            Assert.That(duplicate.StateMachine.TryPrepareSkillExecution(), Is.False);
            Assert.That(duplicate.Session.ExchangeState.CurrentExecutionRequest, Is.SameAs(request));
            Assert.That(duplicate.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void ExecutionPrepare_TieCompletesWithoutRequest()
        {
            Fixture fixture = CreateStandoffFixture();
            PrepareOutcome(fixture, CombatClashOutcome.Tie);

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsExecutionPrepared, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void ExecutionPrepare_NextStandoffClearsRequestAndPreparedState()
        {
            Fixture fixture = CreateStandoffFixture();
            PrepareOutcome(fixture, CombatClashOutcome.Unopposed);
            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsExecutionPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionRequest, Is.Null);
        }

        [Test]
        public void ExecutionPrepare_LegacyModeRejectsAndKeepsPlanningFlow()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.ExchangeState.IsExecutionPrepared, Is.False);
        }

        [Test]
        public void SkillExecution_AppliesSingleTargetDamageWithoutCurrentTurnInspirationOrExtraMp()
        {
            Fixture fixture = CreateStandoffFixture(attackMpCost: 1, attackDamage: 20);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            CombatSkillExecutionRequest request = exchange.CurrentExecutionRequest;
            CombatOutcomeAction action = exchange.CurrentOutcomeAction;
            CombatClashResult clash = exchange.CurrentClashResult;
            int inspiration = fixture.Session.Inspiration.Current;
            int mp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int posture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;
            int turnIndex = fixture.Session.TurnIndex;
            SetCurrentTurn(fixture.Session, null);

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);

            CombatSkillExecutionResult result = exchange.CurrentExecutionResult;
            Assert.That(exchange.IsExecutionCompleted, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.WasExecuted, Is.True);
            Assert.That(result.Actor, Is.SameAs(fixture.Ally));
            Assert.That(result.Skill, Is.SameAs(fixture.AllySkill));
            Assert.That(result.SourceOutcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(result.TargetResults, Has.Count.EqualTo(1));
            Assert.That(result.TargetResults[0].Target, Is.SameAs(fixture.Enemy));
            Assert.That(result.TargetResults[0].HpBefore, Is.EqualTo(10));
            Assert.That(result.TargetResults[0].HpAfter, Is.Zero);
            Assert.That(result.TargetResults[0].DamageApplied, Is.EqualTo(10));
            Assert.That(fixture.Enemy.HP, Is.Zero);
            Assert.That(fixture.Session.CurrentTurn, Is.Null);
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(posture));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(exchange.CurrentExecutionRequest, Is.SameAs(request));
            Assert.That(exchange.CurrentOutcomeAction, Is.SameAs(action));
            Assert.That(exchange.CurrentClashResult, Is.SameAs(clash));
            Assert.That(exchange.CommittedAttackMpCost, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(
                () => ((IList<CombatSkillTargetResult>)result.TargetResults).Add(result.TargetResults[0]),
                Throws.TypeOf<System.NotSupportedException>());
        }

        [Test]
        public void SkillExecution_AppliesSameExistingDamageToEveryPreparedTarget()
        {
            Fixture fixture = CreateStandoffFixture(
                attackTargeting: TargetingRule.AllEnemies,
                additionalEnemy: true,
                attackDamage: 3);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);

            CombatSkillExecutionResult result = fixture.Session.ExchangeState.CurrentExecutionResult;
            Assert.That(result.TargetResults, Has.Count.EqualTo(2));
            Assert.That(result.TargetResults[0].Target, Is.SameAs(fixture.Enemy));
            Assert.That(result.TargetResults[1].Target, Is.SameAs(fixture.AdditionalEnemy));
            Assert.That(result.TargetResults[0].DamageApplied, Is.EqualTo(3));
            Assert.That(result.TargetResults[1].DamageApplied, Is.EqualTo(3));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(7));
            Assert.That(fixture.AdditionalEnemy.HP, Is.EqualTo(7));
        }

        [Test]
        public void SkillExecution_DuplicateCommandDoesNotApplyDamageTwice()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 3);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);
            CombatSkillExecutionResult result = fixture.Session.ExchangeState.CurrentExecutionResult;

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.False);

            Assert.That(fixture.Enemy.HP, Is.EqualTo(7));
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionResult, Is.SameAs(result));
        }

        [Test]
        public void SkillExecution_TieCompletesWithoutRequestOrResult()
        {
            Fixture fixture = CreateStandoffFixture();
            PrepareExecution(fixture, CombatClashOutcome.Tie);
            int enemyHp = fixture.Enemy.HP;

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);

            Assert.That(fixture.Session.ExchangeState.IsExecutionCompleted, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionResult, Is.Null);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void SkillExecution_InvalidActorFailsWithoutEffect()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 3);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            fixture.Ally.SetStunned(true);

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.False);

            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
            Assert.That(fixture.Session.ExchangeState.IsExecutionCompleted, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionResult, Is.Null);
        }

        [Test]
        public void SkillExecution_InvalidMultiTargetPreventsPartialDamage()
        {
            Fixture fixture = CreateStandoffFixture(
                attackTargeting: TargetingRule.AllEnemies,
                additionalEnemy: true,
                attackDamage: 3);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            fixture.AdditionalEnemy.ApplyDamage(int.MaxValue);

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.False);

            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
            Assert.That(fixture.Session.ExchangeState.IsExecutionCompleted, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionResult, Is.Null);
        }

        [Test]
        public void SkillExecution_TargetRemovedFromRosterFailsWithoutPartialDamage()
        {
            Fixture fixture = CreateStandoffFixture(
                attackTargeting: TargetingRule.AllEnemies,
                additionalEnemy: true,
                attackDamage: 3);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            fixture.Session.Enemies.Remove(fixture.AdditionalEnemy);

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.False);

            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
            Assert.That(fixture.AdditionalEnemy.HP, Is.EqualTo(10));
            Assert.That(fixture.Session.ExchangeState.IsExecutionCompleted, Is.False);
        }

        [Test]
        public void SkillExecution_InspectReusesKnowledgeEffectWithoutDamageOrEvent()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 10,
                attackTag: SkillTag.Inspect);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            int eventCount = fixture.Session.CurrentTurn.Events.Count;

            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);

            Assert.That(fixture.Session.Knowledge.IsWeaknessRevealed(fixture.Enemy.Id), Is.True);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
            Assert.That(fixture.Session.CurrentTurn.Events.Count, Is.EqualTo(eventCount));
        }

        [Test]
        public void SkillExecution_NextStandoffClearsCompletionAndResult()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 3);
            PrepareExecution(fixture, CombatClashOutcome.Unopposed);
            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsExecutionCompleted, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentExecutionResult, Is.Null);
        }

        [Test]
        public void SkillExecution_LegacyResolveStillSpendsInspirationAddsStaggerAndRecordsEvent()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            TestSkill skill = new TestSkill(
                30,
                baseDamage: 2,
                inspirationCost: 1,
                baseStagger: 2);
            int inspiration = fixture.Session.Inspiration.Current;

            SkillRunner.Resolve(fixture.Session, fixture.Ally, skill, fixture.Enemy);

            Assert.That(fixture.Enemy.HP, Is.EqualTo(8));
            Assert.That(fixture.Enemy.Stagger, Is.EqualTo(2));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration - 1));
            Assert.That(fixture.Session.CurrentTurn.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void PostureResolution_AttackerWinAppliesRuleToResponderAndPreservesState()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 1,
                responseMpCost: 1,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            CombatSkillExecutionResult execution = exchange.CurrentExecutionResult;
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            int allyHp = fixture.Ally.HP;
            int enemyHp = fixture.Enemy.HP;
            FakePostureRule rule = new FakePostureRule(3);

            Assert.That(fixture.StateMachine.TryResolvePosture(rule), Is.True);

            CombatPostureResult result = exchange.CurrentPostureResult;
            Assert.That(exchange.PostureResolutionState, Is.EqualTo(CombatPostureResolutionState.Applied));
            Assert.That(result.Target, Is.SameAs(fixture.Enemy));
            Assert.That(result.PostureBefore, Is.Zero);
            Assert.That(result.PostureAfter, Is.EqualTo(3));
            Assert.That(result.PostureApplied, Is.EqualTo(3));
            Assert.That(result.ReachedMaximum, Is.False);
            Assert.That(rule.LastRequest.Winner, Is.SameAs(fixture.Ally));
            Assert.That(rule.LastRequest.Loser, Is.SameAs(fixture.Enemy));
            Assert.That(rule.LastRequest.WinningSkill, Is.SameAs(fixture.AllySkill));
            Assert.That(rule.LastRequest.ClashOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(rule.LastRequest.SkillExecutionResult, Is.SameAs(execution));
            Assert.That(rule.LastRequest.LoserPostureBefore, Is.Zero);
            Assert.That(rule.LastRequest.LoserPostureMax, Is.EqualTo(10));
            Assert.That(exchange.CurrentExecutionResult, Is.SameAs(execution));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Ally.HP, Is.EqualTo(allyHp));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void PostureResolution_ResponderWinAppliesRuleToAttacker()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.ResponderWin);
            FakePostureRule rule = new FakePostureRule(4);

            Assert.That(fixture.StateMachine.TryResolvePosture(rule), Is.True);

            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult.Target, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentPosture, Is.EqualTo(4));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.Zero);
            Assert.That(rule.LastRequest.Winner, Is.SameAs(fixture.Enemy));
            Assert.That(rule.LastRequest.WinningSkill, Is.SameAs(fixture.EnemySkill));
        }

        [Test]
        public void PostureResolution_ClampsAtMaximumAndRecordsActualDeltaWithoutStun()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(10)), Is.True);

            CombatPostureResult result = fixture.Session.ExchangeState.CurrentPostureResult;
            Assert.That(result.PostureBefore, Is.EqualTo(4));
            Assert.That(result.PostureAfter, Is.EqualTo(5));
            Assert.That(result.PostureApplied, Is.EqualTo(1));
            Assert.That(result.ReachedMaximum, Is.True);
            Assert.That(fixture.Enemy.IsStunned, Is.False);
            Assert.That(fixture.Enemy.Stagger, Is.Zero);
        }

        [Test]
        public void PostureResolution_NegativeDeltaNormalizesToZero()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10,
                initialPosture: 2);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(-99)), Is.True);

            CombatPostureResult result = fixture.Session.ExchangeState.CurrentPostureResult;
            Assert.That(result.PostureBefore, Is.EqualTo(2));
            Assert.That(result.PostureAfter, Is.EqualTo(2));
            Assert.That(result.PostureApplied, Is.Zero);
        }

        [Test]
        public void PostureResolution_DuplicateCommandDoesNotApplyAgain()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(3)), Is.True);
            CombatPostureResult result = fixture.Session.ExchangeState.CurrentPostureResult;

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(3)), Is.False);

            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(3));
            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult, Is.SameAs(result));
        }

        [Test]
        public void PostureResolution_UnopposedIsNotApplicableAndDoesNotAffectTarget()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                maxPosture: 10,
                initialPosture: 2);
            PrepareCompletedExecution(fixture, CombatClashOutcome.Unopposed);

            Assert.That(fixture.StateMachine.TryResolvePosture(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.NotApplicable));
            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(2));
        }

        [Test]
        public void PostureResolution_TieRequiresPolicyWithoutMutation()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10,
                initialPosture: 2);
            PrepareCompletedExecution(fixture, CombatClashOutcome.Tie);

            Assert.That(fixture.StateMachine.TryResolvePosture(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.PolicyRequired));
            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentPosture, Is.EqualTo(2));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(2));
        }

        [Test]
        public void PostureResolution_WinWithoutRuleRemainsPending()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryResolvePosture(), Is.False);

            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.Pending));
            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.Zero);
        }

        [Test]
        public void PostureResolution_DeadLoserIsNotApplicable()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 10,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.Enemy.HP, Is.Zero);

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(5)), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.NotApplicable));
            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.Zero);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentPosture, Is.Zero);
        }

        [Test]
        public void PostureResolution_AreaExecutionOnlyAppliesToClashLoser()
        {
            Fixture fixture = CreateStandoffFixture(
                attackTargeting: TargetingRule.AllEnemies,
                additionalEnemy: true,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(4)), Is.True);

            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult.Target, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(4));
            Assert.That(fixture.Session.GetCombatState(fixture.AdditionalEnemy).CurrentPosture, Is.Zero);
        }

        [Test]
        public void PostureResolution_RejectsBeforeExecutionCompletion()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareExecution(fixture, CombatClashOutcome.AttackerWin);

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(3)), Is.False);
            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.Pending));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.Zero);
        }

        [Test]
        public void PostureResolution_NextStandoffClearsLifecycle()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(3)), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);
            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.Pending));
            Assert.That(fixture.Session.ExchangeState.CurrentPostureResult, Is.Null);
        }

        [Test]
        public void PostureResolution_LegacyModeRejectsAndKeepsPlanningFlow()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(3)), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(
                fixture.Session.ExchangeState.PostureResolutionState,
                Is.EqualTo(CombatPostureResolutionState.Pending));
        }

        [Test]
        public void StunResolution_PostureMaximumAppliesCanonicalStunAndPreservesState()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 1,
                responseMpCost: 1,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(10)), Is.True);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            CombatPostureResult postureResult = exchange.CurrentPostureResult;
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            int enemyHp = fixture.Enemy.HP;
            float pressure = fixture.Session.StandoffState.CurrentPressure;

            Assert.That(fixture.Enemy.IsStunned, Is.False);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            CombatStunResult result = exchange.CurrentStunResult;
            Assert.That(exchange.StunResolutionState, Is.EqualTo(CombatStunResolutionState.Applied));
            Assert.That(result.Target, Is.SameAs(fixture.Enemy));
            Assert.That(result.WasStunnedBefore, Is.False);
            Assert.That(result.IsStunnedAfter, Is.True);
            Assert.That(result.StunApplied, Is.True);
            Assert.That(fixture.Enemy.IsStunned, Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(5));
            Assert.That(exchange.CurrentPostureResult, Is.SameAs(postureResult));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void StunResolution_NonMaximumIsNotApplicable()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(3)), Is.True);

            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.StunResolutionState,
                Is.EqualTo(CombatStunResolutionState.NotApplicable));
            Assert.That(fixture.Session.ExchangeState.CurrentStunResult, Is.Null);
            Assert.That(fixture.Enemy.IsStunned, Is.False);
        }

        [Test]
        public void StunResolution_AlreadyStunnedRecordsNoNewTransition()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(1)), Is.True);
            fixture.Enemy.SetStunned(true);

            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            CombatStunResult result = fixture.Session.ExchangeState.CurrentStunResult;
            Assert.That(result.WasStunnedBefore, Is.True);
            Assert.That(result.IsStunnedAfter, Is.True);
            Assert.That(result.StunApplied, Is.False);
            Assert.That(fixture.Enemy.IsStunned, Is.True);
        }

        [TestCase(CombatClashOutcome.Unopposed, CombatStunResolutionState.NotApplicable)]
        [TestCase(CombatClashOutcome.Tie, CombatStunResolutionState.PolicyRequired)]
        public void StunResolution_NoPostureResultPreservesExplicitLifecycle(
            CombatClashOutcome outcome,
            CombatStunResolutionState expectedState)
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 5);
            PrepareCompletedExecution(fixture, outcome);
            Assert.That(fixture.StateMachine.TryResolvePosture(), Is.True);

            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            Assert.That(fixture.Session.ExchangeState.StunResolutionState, Is.EqualTo(expectedState));
            Assert.That(fixture.Session.ExchangeState.CurrentStunResult, Is.Null);
            Assert.That(fixture.Ally.IsStunned, Is.False);
            Assert.That(fixture.Enemy.IsStunned, Is.False);
        }

        [Test]
        public void StunResolution_DeadLoserIsNotApplicable()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 10, responseDamage: 0, maxPosture: 5);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(5)), Is.True);

            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.StunResolutionState,
                Is.EqualTo(CombatStunResolutionState.NotApplicable));
            Assert.That(fixture.Enemy.IsStunned, Is.False);
        }

        [Test]
        public void StunResolution_DuplicateCommandDoesNotMutateAgain()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(1)), Is.True);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);
            CombatStunResult result = fixture.Session.ExchangeState.CurrentStunResult;

            Assert.That(fixture.StateMachine.TryResolveStun(), Is.False);

            Assert.That(fixture.Session.ExchangeState.CurrentStunResult, Is.SameAs(result));
            Assert.That(fixture.Enemy.IsStunned, Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(5));
        }

        [Test]
        public void StunResolution_NextStandoffClearsLifecycleButNotCanonicalStun()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(1)), Is.True);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.StunResolutionState,
                Is.EqualTo(CombatStunResolutionState.Pending));
            Assert.That(fixture.Session.ExchangeState.CurrentStunResult, Is.Null);
            Assert.That(fixture.Enemy.IsStunned, Is.True);
        }

        [Test]
        public void StunResolution_RejectsBeforePostureAndInLegacyMode()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 5);
            PrepareCompletedExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.False);
            Assert.That(
                fixture.Session.ExchangeState.StunResolutionState,
                Is.EqualTo(CombatStunResolutionState.Pending));

            Fixture legacy = CreateFixture(CombatFlowMode.LegacyPlanning);
            legacy.StateMachine.Tick();
            Assert.That(legacy.StateMachine.TryResolveStun(), Is.False);
            Assert.That(legacy.StateMachine.Phase, Is.EqualTo(Phase.Planning));
        }

        [Test]
        public void AftermathSnapshot_RecordsSideSurvivalStunAndDefeatFacts()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalAlly: true,
                additionalEnemy: true,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 2);
            fixture.AdditionalAlly.ApplyDamage(int.MaxValue);
            fixture.AdditionalAlly.SetStunned(true);
            fixture.AdditionalEnemy.SetStunned(true);

            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            CombatAftermathSnapshot snapshot = fixture.Session.ExchangeState.CurrentAftermathSnapshot;
            Assert.That(snapshot.LivingAlliesCount, Is.EqualTo(1));
            Assert.That(snapshot.DefeatedAlliesCount, Is.EqualTo(1));
            Assert.That(snapshot.LivingEnemiesCount, Is.EqualTo(2));
            Assert.That(snapshot.DefeatedEnemiesCount, Is.Zero);
            Assert.That(snapshot.StunnedLivingAlliesCount, Is.Zero);
            Assert.That(snapshot.StunnedLivingEnemiesCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { fixture.AdditionalAlly }, snapshot.DefeatedAllies);
            Assert.That(snapshot.DefeatedEnemies, Is.Empty);
        }

        [TestCase(CombatClashOutcome.AttackerWin)]
        [TestCase(CombatClashOutcome.ResponderWin)]
        [TestCase(CombatClashOutcome.Unopposed)]
        [TestCase(CombatClashOutcome.Tie)]
        public void AftermathSnapshot_RecordsExchangeExecutionAndPolicyFacts(CombatClashOutcome outcome)
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(fixture, outcome, 2);

            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            CombatAftermathSnapshot snapshot = fixture.Session.ExchangeState.CurrentAftermathSnapshot;
            Assert.That(snapshot.ClashOutcome, Is.EqualTo(outcome));
            Assert.That(snapshot.AttackInitiator, Is.SameAs(fixture.Ally));
            Assert.That(snapshot.ExchangeOpponent, Is.SameAs(fixture.Enemy));
            Assert.That(snapshot.ClashWinner, Is.SameAs(
                outcome == CombatClashOutcome.AttackerWin || outcome == CombatClashOutcome.Unopposed
                    ? fixture.Ally
                    : outcome == CombatClashOutcome.ResponderWin ? fixture.Enemy : null));
            Assert.That(snapshot.HadOutcomeAction, Is.EqualTo(outcome != CombatClashOutcome.Tie));
            Assert.That(snapshot.HasExecutionResult, Is.EqualTo(outcome != CombatClashOutcome.Tie));
            Assert.That(snapshot.RequiresTiePolicy, Is.EqualTo(outcome == CombatClashOutcome.Tie));
            Assert.That(snapshot.WasPostureApplied, Is.EqualTo(
                outcome == CombatClashOutcome.AttackerWin || outcome == CombatClashOutcome.ResponderWin));
        }

        [Test]
        public void AftermathSnapshot_RecordsActualZeroHpExecutionTargetWithoutEndingCombat()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 10, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 3);

            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            CombatAftermathSnapshot snapshot = fixture.Session.ExchangeState.CurrentAftermathSnapshot;
            Assert.That(snapshot.LivingEnemiesCount, Is.Zero);
            Assert.That(snapshot.DefeatedEnemiesCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { fixture.Enemy }, snapshot.DefeatedEnemies);
            CollectionAssert.AreEqual(new[] { fixture.Enemy }, snapshot.ExecutionZeroHpTargets);
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void AftermathPreparation_RequiresCompletedExecutionPostureAndStun()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareExecution(fixture, CombatClashOutcome.AttackerWin);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.False);
            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.False);
            Assert.That(fixture.StateMachine.TryResolvePosture(new FakePostureRule(2)), Is.True);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.False);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsAftermathPrepared, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentAftermathSnapshot, Is.Not.Null);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.False);
        }

        [Test]
        public void AftermathPreparation_DoesNotMutateCombatRuntimeOrChainState()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 1,
                responseMpCost: 1,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 1);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            int allyHp = fixture.Ally.HP;
            int enemyHp = fixture.Enemy.HP;
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyMp = fixture.Session.GetCombatState(fixture.Enemy).CurrentMp;
            int posture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            float pressure = fixture.Session.StandoffState.CurrentPressure;

            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            Assert.That(fixture.Ally.HP, Is.EqualTo(allyHp));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(enemyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(posture));
            Assert.That(fixture.Enemy.IsStunned, Is.True);
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(exchange.IsChainActive, Is.False);
            Assert.That(exchange.ChainOwner, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void AftermathSnapshot_CollectionsAreReadOnlySnapshots()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 10, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 1);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            CombatAftermathSnapshot snapshot = fixture.Session.ExchangeState.CurrentAftermathSnapshot;

            Assert.That(snapshot.DefeatedEnemies, Is.InstanceOf<System.Collections.IList>());
            Assert.Throws<System.NotSupportedException>(() =>
                ((System.Collections.IList)snapshot.DefeatedEnemies).Add(fixture.Ally));
            fixture.Session.Enemies.Add(
                new DummyCombatant(999, Side.Enemies, 10, KeywordMask.None, 10));
            Assert.That(snapshot.DefeatedEnemiesCount, Is.EqualTo(1));
        }

        [Test]
        public void AftermathPreparation_NextStandoffClearsLifecycleWithoutClearingPostureOrStun()
        {
            Fixture fixture = CreateStandoffFixture(
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 1);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            SetPhase(fixture.StateMachine, Phase.EnterCombat);

            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);

            Assert.That(fixture.Session.ExchangeState.IsAftermathPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentAftermathSnapshot, Is.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(5));
            Assert.That(fixture.Enemy.IsStunned, Is.True);
        }

        [Test]
        public void AftermathPreparation_LegacyModeRejects()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.False);
            Assert.That(fixture.Session.ExchangeState.IsAftermathPrepared, Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
        }

        [TestCase(false, true, CombatTerminalCandidate.EnemiesWiped)]
        [TestCase(true, false, CombatTerminalCandidate.AlliesWiped)]
        [TestCase(true, true, CombatTerminalCandidate.BothWiped)]
        public void AftermathDecision_ClassifiesTerminalCandidates(
            bool wipeAllies,
            bool wipeEnemies,
            CombatTerminalCandidate expected)
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 0);
            if (wipeAllies)
                fixture.Ally.ApplyDamage(int.MaxValue);
            if (wipeEnemies)
                fixture.Enemy.ApplyDamage(int.MaxValue);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);

            CombatAftermathDecision decision = fixture.Session.ExchangeState.CurrentAftermathDecision;
            Assert.That(decision.Kind, Is.EqualTo(CombatAftermathDecisionKind.TerminalCandidate));
            Assert.That(decision.TerminalCandidate, Is.EqualTo(expected));
            Assert.That(fixture.StateMachine.TryEnterChainDecision(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void AftermathDecision_TerminalCandidatePrecedesTieAndAllOut()
        {
            Fixture tie = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(tie, CombatClashOutcome.Tie, 0);
            tie.Enemy.ApplyDamage(int.MaxValue);
            Assert.That(tie.StateMachine.TryPrepareAftermath(), Is.True);
            Assert.That(tie.StateMachine.TryPrepareAftermathDecision(), Is.True);
            Assert.That(
                tie.Session.ExchangeState.CurrentAftermathDecision.TerminalCandidate,
                Is.EqualTo(CombatTerminalCandidate.EnemiesWiped));

            Fixture stunned = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 5);
            PrepareResolvedStun(stunned, CombatClashOutcome.AttackerWin, 5);
            stunned.Enemy.ApplyDamage(int.MaxValue);
            Assert.That(stunned.StateMachine.TryPrepareAftermath(), Is.True);
            Assert.That(stunned.StateMachine.TryPrepareAftermathDecision(), Is.True);
            Assert.That(
                stunned.Session.ExchangeState.CurrentAftermathDecision.TerminalCandidate,
                Is.EqualTo(CombatTerminalCandidate.EnemiesWiped));
        }

        [Test]
        public void AftermathDecision_TiePrecedesAllOutAndBlocksChainDecision()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.Tie, 0);
            fixture.Enemy.SetStunned(true);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.TiePolicyRequired));
            Assert.That(fixture.StateMachine.TryEnterChainDecision(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void AftermathDecision_AllLivingEnemiesStunnedIsCandidateAndBlocksChainDecision()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalEnemy: true,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 5);
            fixture.AdditionalEnemy.SetStunned(true);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.AllOutCandidate));
            Assert.That(fixture.StateMachine.TryEnterChainDecision(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void AftermathDecision_PartialEnemyStunRequiresChainDecision()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalEnemy: true,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 5);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            bool chainActive = fixture.Session.ExchangeState.IsChainActive;
            ICombatant chainOwner = fixture.Session.ExchangeState.ChainOwner;

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);
            Assert.That(fixture.StateMachine.TryEnterChainDecision(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.ChainDecisionRequired));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Session.ExchangeState.IsChainActive, Is.EqualTo(chainActive));
            Assert.That(fixture.Session.ExchangeState.ChainOwner, Is.SameAs(chainOwner));
        }

        [Test]
        public void AftermathDecision_NormalAftermathRequiresChainDecisionWithoutReadingMp()
        {
            Fixture fixture = CreateStandoffFixture(
                attackMpCost: 1,
                responseMpCost: 0,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 0);
            fixture.Session.GetCombatState(fixture.Ally).SetMp(0);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);

            Assert.That(
                fixture.Session.ExchangeState.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.ChainDecisionRequired));
        }

        [Test]
        public void AftermathDecision_PrepareLifecycleRequiresSnapshotRejectsDuplicateAndClearsAtStandoff()
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.False);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 0);
            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.False);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsAftermathDecisionPrepared, Is.True);
            Assert.That(fixture.Session.ExchangeState.CurrentAftermathDecision, Is.Not.Null);
            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.False);

            SetPhase(fixture.StateMachine, Phase.EnterCombat);
            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsAftermathDecisionPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentAftermathDecision, Is.Null);
        }

        [Test]
        public void AftermathDecision_UsesSnapshotOnlyAndDoesNotMutateRuntimeFacts()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalEnemy: true,
                attackMpCost: 1,
                responseMpCost: 1,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 0);
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            int allyHp = fixture.Ally.HP;
            int enemyHp = fixture.Enemy.HP;
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int enemyPosture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            float pressure = fixture.Session.StandoffState.CurrentPressure;
            int attackCost = exchange.CommittedAttackMpCost;
            int responseCost = exchange.CommittedResponseMpCost;
            fixture.Enemy.SetStunned(true);
            fixture.AdditionalEnemy.SetStunned(true);

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);

            Assert.That(exchange.CurrentAftermathDecision.Kind,
                Is.EqualTo(CombatAftermathDecisionKind.ChainDecisionRequired));
            Assert.That(fixture.Ally.HP, Is.EqualTo(allyHp));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(enemyPosture));
            Assert.That(fixture.Enemy.IsStunned, Is.True);
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(exchange.CommittedAttackMpCost, Is.EqualTo(attackCost));
            Assert.That(exchange.CommittedResponseMpCost, Is.EqualTo(responseCost));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void AftermathDecision_CommandsRejectLegacyAndPreserveLegacyPhase()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.False);
            Assert.That(fixture.StateMachine.TryEnterChainDecision(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [TestCase(false, true, CombatTerminalCandidate.EnemiesWiped, CombatEndReason.Victory)]
        [TestCase(true, false, CombatTerminalCandidate.AlliesWiped, CombatEndReason.Defeat)]
        [TestCase(true, true, CombatTerminalCandidate.BothWiped, CombatEndReason.Scripted)]
        public void TerminalDecision_ExplicitPolicyStoresDecisionForEveryTerminalCandidate(
            bool wipeAllies,
            bool wipeEnemies,
            CombatTerminalCandidate expectedCandidate,
            CombatEndReason policyResult)
        {
            Fixture fixture = PrepareTerminalCandidate(wipeAllies, wipeEnemies);
            FakeTerminalPolicy policy = new FakeTerminalPolicy(
                new CombatTerminalDecision(expectedCandidate, policyResult));

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(policy), Is.True);

            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(policy.LastCandidate, Is.EqualTo(expectedCandidate));
            Assert.That(policy.LastSnapshot, Is.SameAs(fixture.Session.ExchangeState.CurrentAftermathSnapshot));
            Assert.That(fixture.Session.ExchangeState.IsTerminalDecisionPrepared, Is.True);
            Assert.That(
                fixture.Session.ExchangeState.CurrentTerminalDecision.TerminalCandidate,
                Is.EqualTo(expectedCandidate));
            Assert.That(
                fixture.Session.ExchangeState.CurrentTerminalDecision.EndReason,
                Is.EqualTo(policyResult));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void TerminalDecision_NonTerminalAftermathKindsRejectPolicy()
        {
            Fixture tie = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(tie, CombatClashOutcome.Tie, 0);
            PrepareAftermathDecision(tie);

            Fixture allOut = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 5);
            PrepareResolvedStun(allOut, CombatClashOutcome.AttackerWin, 5);
            PrepareAftermathDecision(allOut);

            Fixture chain = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(chain, CombatClashOutcome.AttackerWin, 0);
            PrepareAftermathDecision(chain);

            FakeTerminalPolicy policy = new FakeTerminalPolicy(
                new CombatTerminalDecision(CombatTerminalCandidate.EnemiesWiped, CombatEndReason.Victory));
            Assert.That(tie.StateMachine.TryPrepareTerminalDecision(policy), Is.False);
            Assert.That(allOut.StateMachine.TryPrepareTerminalDecision(policy), Is.False);
            Assert.That(chain.StateMachine.TryPrepareTerminalDecision(policy), Is.False);
            Assert.That(policy.CallCount, Is.Zero);
            Assert.That(tie.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(allOut.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(chain.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
        }

        [Test]
        public void TerminalDecision_NullPolicyRejectsWithoutFallback()
        {
            Fixture fixture = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(null), Is.False);

            Assert.That(fixture.Session.ExchangeState.IsTerminalDecisionPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [TestCase(CombatEndReason.None)]
        [TestCase((CombatEndReason)999)]
        public void TerminalDecision_InvalidEndReasonRejectsSafely(CombatEndReason invalidReason)
        {
            Fixture fixture = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);
            FakeTerminalPolicy policy = new FakeTerminalPolicy(
                new CombatTerminalDecision(CombatTerminalCandidate.EnemiesWiped, invalidReason));

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(policy), Is.False);

            Assert.That(fixture.Session.ExchangeState.IsTerminalDecisionPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void TerminalDecision_MismatchedNullAndFailedPolicyResultsRejectSafely()
        {
            Fixture mismatch = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);
            Assert.That(
                mismatch.StateMachine.TryPrepareTerminalDecision(new FakeTerminalPolicy(
                    new CombatTerminalDecision(CombatTerminalCandidate.AlliesWiped, CombatEndReason.Victory))),
                Is.False);

            Fixture nullDecision = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);
            Assert.That(
                nullDecision.StateMachine.TryPrepareTerminalDecision(new FakeTerminalPolicy(null)),
                Is.False);

            Fixture failed = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);
            Assert.That(
                failed.StateMachine.TryPrepareTerminalDecision(new FakeTerminalPolicy(
                    new CombatTerminalDecision(CombatTerminalCandidate.EnemiesWiped, CombatEndReason.Victory),
                    shouldResolve: false)),
                Is.False);

            Assert.That(mismatch.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(nullDecision.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(failed.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
        }

        [Test]
        public void TerminalDecision_PolicyExceptionLeavesStateUnchanged()
        {
            Fixture fixture = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);
            FakeTerminalPolicy policy = new FakeTerminalPolicy(null, throwOnResolve: true);

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(policy), Is.False);

            Assert.That(fixture.Session.ExchangeState.IsTerminalDecisionPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void TerminalDecision_LifecycleRejectsDuplicateAndClearsAtNextStandoff()
        {
            Fixture fixture = PrepareTerminalCandidate(wipeAllies: false, wipeEnemies: true);
            FakeTerminalPolicy policy = new FakeTerminalPolicy(
                new CombatTerminalDecision(CombatTerminalCandidate.EnemiesWiped, CombatEndReason.Victory));
            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(policy), Is.True);

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(policy), Is.False);
            Assert.That(policy.CallCount, Is.EqualTo(1));

            SetPhase(fixture.StateMachine, Phase.EnterCombat);
            SetCombatantHp(fixture.Enemy, fixture.Enemy.MaxHP);
            Assert.That(fixture.StateMachine.EnterStandoff(), Is.True);
            Assert.That(fixture.Session.ExchangeState.IsTerminalDecisionPrepared, Is.False);
            Assert.That(fixture.Session.ExchangeState.CurrentTerminalDecision, Is.Null);
        }

        [Test]
        public void TerminalDecision_PreparationDoesNotMutateCombatRuntimeOrExecuteExit()
        {
            Fixture fixture = CreateStandoffFixture(
                additionalEnemy: true,
                attackMpCost: 1,
                responseMpCost: 1,
                attackDamage: 0,
                responseDamage: 0,
                maxPosture: 5,
                initialPosture: 4);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 1);
            fixture.Enemy.ApplyDamage(int.MaxValue);
            fixture.AdditionalEnemy.ApplyDamage(int.MaxValue);
            PrepareAftermathDecision(fixture);
            CombatExchangeState exchange = fixture.Session.ExchangeState;
            int allyHp = fixture.Ally.HP;
            int enemyHp = fixture.Enemy.HP;
            int allyMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            int posture = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;
            bool stunned = fixture.Enemy.IsStunned;
            float pressure = fixture.Session.StandoffState.CurrentPressure;
            int inspiration = fixture.Session.Inspiration.Current;
            int turnIndex = fixture.Session.TurnIndex;
            bool chainActive = exchange.IsChainActive;
            ICombatant chainOwner = exchange.ChainOwner;
            int attackCost = exchange.CommittedAttackMpCost;
            int responseCost = exchange.CommittedResponseMpCost;

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(new FakeTerminalPolicy(
                new CombatTerminalDecision(CombatTerminalCandidate.EnemiesWiped, CombatEndReason.Victory))),
                Is.True);

            Assert.That(fixture.Ally.HP, Is.EqualTo(allyHp));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(enemyHp));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(allyMp));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(posture));
            Assert.That(fixture.Enemy.IsStunned, Is.EqualTo(stunned));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(turnIndex));
            Assert.That(exchange.IsChainActive, Is.EqualTo(chainActive));
            Assert.That(exchange.ChainOwner, Is.SameAs(chainOwner));
            Assert.That(exchange.CommittedAttackMpCost, Is.EqualTo(attackCost));
            Assert.That(exchange.CommittedResponseMpCost, Is.EqualTo(responseCost));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        [Test]
        public void TerminalDecision_CommandRejectsLegacyAndPreservesLegacyEvaluatorFlow()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.TryPrepareTerminalDecision(new FakeTerminalPolicy(
                new CombatTerminalDecision(CombatTerminalCandidate.EnemiesWiped, CombatEndReason.Victory))),
                Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(CombatEndEvaluator.Evaluate(fixture.Session), Is.EqualTo(CombatEndReason.None));
        }

        private static Fixture PrepareTerminalCandidate(bool wipeAllies, bool wipeEnemies)
        {
            Fixture fixture = CreateStandoffFixture(attackDamage: 0, responseDamage: 0, maxPosture: 10);
            PrepareResolvedStun(fixture, CombatClashOutcome.AttackerWin, 0);
            if (wipeAllies)
                fixture.Ally.ApplyDamage(int.MaxValue);
            if (wipeEnemies)
                fixture.Enemy.ApplyDamage(int.MaxValue);
            PrepareAftermathDecision(fixture);
            return fixture;
        }

        private static void PrepareAftermathDecision(Fixture fixture)
        {
            Assert.That(fixture.StateMachine.TryPrepareAftermath(), Is.True);
            Assert.That(fixture.StateMachine.TryPrepareAftermathDecision(), Is.True);
        }

        private static void PrepareResolvedStun(
            Fixture fixture,
            CombatClashOutcome outcome,
            int postureDelta)
        {
            PrepareCompletedExecution(fixture, outcome);
            ICombatPostureRule postureRule = outcome == CombatClashOutcome.AttackerWin ||
                                             outcome == CombatClashOutcome.ResponderWin
                ? new FakePostureRule(postureDelta)
                : null;
            Assert.That(fixture.StateMachine.TryResolvePosture(postureRule), Is.True);
            Assert.That(fixture.StateMachine.TryResolveStun(), Is.True);
        }

        private static void PrepareCompletedExecution(Fixture fixture, CombatClashOutcome outcome)
        {
            PrepareExecution(fixture, outcome);
            Assert.That(fixture.StateMachine.TryExecutePreparedSkill(), Is.True);
        }

        private static void PrepareExecution(Fixture fixture, CombatClashOutcome outcome)
        {
            PrepareOutcome(fixture, outcome);
            Assert.That(fixture.StateMachine.TryPrepareSkillExecution(), Is.True);
        }

        private static void PrepareOutcome(Fixture fixture, CombatClashOutcome outcome)
        {
            ResolveToApplyOutcome(fixture, outcome);
            Assert.That(fixture.StateMachine.TryPrepareOutcome(), Is.True);
        }

        private static void ResolveToApplyOutcome(Fixture fixture, CombatClashOutcome outcome)
        {
            if (outcome == CombatClashOutcome.Unopposed)
            {
                PrepareNoResponseClash(fixture);
                Assert.That(fixture.StateMachine.TryResolveClash(), Is.True);
                return;
            }

            PrepareCounterClash(fixture);
            Assert.That(fixture.StateMachine.TryResolveClash(new FakeClashRule(outcome)), Is.True);
        }

        private static void PrepareCounterClash(Fixture fixture)
        {
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.TryDeclareResponse(fixture.Enemy, fixture.EnemySkill), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(), Is.True);
        }

        private static void PrepareNoResponseClash(Fixture fixture)
        {
            DeclarePlayerAttack(fixture);
            Assert.That(fixture.StateMachine.ConfirmNoResponse(), Is.True);
            Assert.That(fixture.StateMachine.TryCommitExchange(), Is.True);
            Assert.That(fixture.StateMachine.TryBeginApproach(), Is.True);
            Assert.That(fixture.StateMachine.CompleteApproach(), Is.True);
        }

        private static CombatAttackDeclaration DeclarePlayerAttack(Fixture fixture)
        {
            Assert.That(
                fixture.StateMachine.TryDeclareAttack(fixture.Ally, fixture.Enemy, fixture.AllySkill),
                Is.True);
            return fixture.Session.ExchangeState.CurrentDeclaration;
        }

        private static void AssertUnchangedStandoff(Fixture fixture, float pressure)
        {
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure).Within(0.0001f));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Null);
        }

        private static Fixture CreateStandoffFixture(
            bool additionalAlly = false,
            int attackMpCost = 0,
            int responseMpCost = 0,
            int initialMp = 4,
            TargetingRule attackTargeting = TargetingRule.SingleEnemy,
            TargetingRule responseTargeting = TargetingRule.SingleEnemy,
            bool additionalEnemy = false,
            int attackDamage = 10,
            SkillTag attackTag = SkillTag.Attack,
            int responseDamage = 10,
            int maxPosture = 0,
            int initialPosture = 0)
        {
            Fixture fixture = CreateFixture(
                CombatFlowMode.StandoffClashChain,
                additionalAlly,
                attackMpCost,
                responseMpCost,
                initialMp,
                attackTargeting,
                responseTargeting,
                additionalEnemy,
                attackDamage,
                attackTag,
                responseDamage,
                maxPosture,
                initialPosture);
            fixture.StateMachine.Tick();
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            return fixture;
        }

        private static Fixture CreateFixture(
            CombatFlowMode flowMode,
            bool additionalAlly = false,
            int attackMpCost = 0,
            int responseMpCost = 0,
            int initialMp = 4,
            TargetingRule attackTargeting = TargetingRule.SingleEnemy,
            TargetingRule responseTargeting = TargetingRule.SingleEnemy,
            bool additionalEnemy = false,
            int attackDamage = 10,
            SkillTag attackTag = SkillTag.Attack,
            int responseDamage = 10,
            int maxPosture = 0,
            int initialPosture = 0)
        {
            CombatRuntimeConfig config = new CombatRuntimeConfig(
                10,
                initialMp,
                maxPosture,
                initialPosture,
                0f,
                1f,
                1f);
            CombatSession session = new CombatSession(
                StartReason.PlayerGotHit,
                Side.Enemies,
                new InspirationPool(10, 3),
                new Game.Combat.Environment.CombatEnvironment(),
                flowMode,
                config);

            TestSkill allySkill = new TestSkill(1, attackMpCost, attackTargeting, attackDamage, attackTag);
            TestSkill enemySkill = new TestSkill(2, responseMpCost, responseTargeting, responseDamage);
            DummyCombatant ally = CreateCombatant(1, Side.Allies);
            DummyCombatant enemy = CreateCombatant(100, Side.Enemies);
            ally.AddSkill(allySkill);
            enemy.AddSkill(enemySkill);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);

            DummyCombatant extra = null;
            if (additionalAlly)
            {
                extra = CreateCombatant(2, Side.Allies);
                session.Allies.Add(extra);
            }

            DummyCombatant extraEnemy = null;
            if (additionalEnemy)
            {
                extraEnemy = CreateCombatant(101, Side.Enemies);
                session.Enemies.Add(extraEnemy);
            }

            return new Fixture(
                session,
                new CombatStateMachine(session),
                ally,
                enemy,
                extra,
                extraEnemy,
                allySkill,
                enemySkill);
        }

        private static DummyCombatant CreateCombatant(int id, Side side)
        {
            return new DummyCombatant(id, side, 10, KeywordMask.None, 6);
        }

        private static void SetPhase(CombatStateMachine stateMachine, Phase phase)
        {
            PropertyInfo property = typeof(CombatStateMachine).GetProperty(
                nameof(CombatStateMachine.Phase),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            property.SetValue(stateMachine, phase);
        }

        private static void SetCombatantHp(DummyCombatant combatant, int hp)
        {
            PropertyInfo property = typeof(DummyCombatant).GetProperty(
                nameof(DummyCombatant.HP),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            property.SetValue(combatant, hp);
        }

        private static void SetCurrentTurn(CombatSession session, CombatTurn turn)
        {
            PropertyInfo property = typeof(CombatSession).GetProperty(
                nameof(CombatSession.CurrentTurn),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            property.SetValue(session, turn);
        }

        private sealed class FakeEnemyDecisionPolicy : IEnemyCombatDecisionPolicy
        {
            private readonly CombatAttackDeclaration _declaration;
            private readonly bool _succeeds;

            public int CallCount { get; private set; }

            public FakeEnemyDecisionPolicy(CombatAttackDeclaration declaration, bool succeeds = true)
            {
                _declaration = declaration;
                _succeeds = succeeds;
            }

            public bool TryCreateDeclaration(
                EnemyCombatDecisionRequest request,
                out CombatAttackDeclaration declaration)
            {
                CallCount++;
                Assert.That(request.Session, Is.Not.Null);
                Assert.That(request.StandoffState.IsPressureReady, Is.True);
                declaration = _declaration;
                return _succeeds;
            }
        }

        private sealed class FakeClashRule : ICombatClashRule
        {
            private readonly CombatClashOutcome _outcome;

            public int CallCount { get; private set; }
            public CombatClashRequest LastRequest { get; private set; }

            public FakeClashRule(CombatClashOutcome outcome)
            {
                _outcome = outcome;
            }

            public CombatClashOutcome Resolve(CombatClashRequest request)
            {
                CallCount++;
                LastRequest = request;
                return _outcome;
            }
        }

        private sealed class FakePostureRule : ICombatPostureRule
        {
            private readonly int _delta;

            public CombatPostureRequest LastRequest { get; private set; }

            public FakePostureRule(int delta)
            {
                _delta = delta;
            }

            public int ResolvePostureDelta(CombatPostureRequest request)
            {
                LastRequest = request;
                return _delta;
            }
        }

        private sealed class FakeTerminalPolicy : ICombatTerminalPolicy
        {
            private readonly CombatTerminalDecision _decision;
            private readonly bool _shouldResolve;
            private readonly bool _throwOnResolve;

            public int CallCount { get; private set; }
            public CombatTerminalCandidate LastCandidate { get; private set; }
            public CombatAftermathSnapshot LastSnapshot { get; private set; }

            public FakeTerminalPolicy(
                CombatTerminalDecision decision,
                bool shouldResolve = true,
                bool throwOnResolve = false)
            {
                _decision = decision;
                _shouldResolve = shouldResolve;
                _throwOnResolve = throwOnResolve;
            }

            public bool TryResolve(
                CombatTerminalCandidate candidate,
                CombatAftermathSnapshot snapshot,
                out CombatTerminalDecision decision)
            {
                CallCount++;
                LastCandidate = candidate;
                LastSnapshot = snapshot;
                if (_throwOnResolve)
                    throw new System.InvalidOperationException("Test terminal policy failure.");

                decision = _decision;
                return _shouldResolve;
            }
        }

        private class TestSkill : ISkill, ICombatMpCostProvider
        {
            public SkillId Id { get; }
            public string Name => "Declaration Test Skill";
            public int InspirationCost { get; }
            public int MpCost { get; }
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag { get; }
            public TargetingRule Targeting { get; }
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage { get; }
            public int BaseStagger { get; }
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;

            public TestSkill(
                int id,
                int mpCost = 0,
                TargetingRule targeting = TargetingRule.SingleEnemy,
                int baseDamage = 10,
                SkillTag tag = SkillTag.Attack,
                int inspirationCost = 5,
                int baseStagger = 0)
            {
                Id = new SkillId(id);
                MpCost = mpCost;
                Targeting = targeting;
                BaseDamage = baseDamage;
                Tag = tag;
                InspirationCost = inspirationCost;
                BaseStagger = baseStagger;
            }
        }

        private sealed class LegacySkill : ISkill
        {
            public SkillId Id { get; }
            public string Name => "Legacy Skill";
            public int InspirationCost { get; }
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag { get; }
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 0;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 0;
            public bool ConsumesTurn => true;

            public LegacySkill(int id, int inspirationCost)
            {
                Id = new SkillId(id);
                InspirationCost = inspirationCost;
            }
        }

        private sealed class Fixture
        {
            public readonly CombatSession Session;
            public readonly CombatStateMachine StateMachine;
            public readonly DummyCombatant Ally;
            public readonly DummyCombatant Enemy;
            public readonly DummyCombatant AdditionalAlly;
            public readonly DummyCombatant AdditionalEnemy;
            public readonly ISkill AllySkill;
            public readonly ISkill EnemySkill;

            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                DummyCombatant ally,
                DummyCombatant enemy,
                DummyCombatant additionalAlly,
                DummyCombatant additionalEnemy,
                ISkill allySkill,
                ISkill enemySkill)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                Enemy = enemy;
                AdditionalAlly = additionalAlly;
                AdditionalEnemy = additionalEnemy;
                AllySkill = allySkill;
                EnemySkill = enemySkill;
            }
        }
    }
}
#endif
