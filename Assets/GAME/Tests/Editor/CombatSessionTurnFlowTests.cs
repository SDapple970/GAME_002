#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Combat
{
    public sealed class CombatSessionTurnFlowTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void EntrySubmission_RejectsMissingSessionAndOutsidePlanning()
        {
            CombatEntryPoint entry = CreateEntry();
            Assert.That(entry.SubmitCurrentTurn(), Is.False);

            Fixture fixture = CreateFixture();
            BindEntry(entry, fixture);
            PrepareAndResolve(fixture.Session, fixture.PlayerPlan(), fixture.EnemyNone());
            Assert.That(fixture.StateMachine.ConfirmPlanning(), Is.True);
            Assert.That(entry.SubmitCurrentTurn(), Is.False);
        }

        [Test]
        public void EntrySubmission_ResolvesOnceAndDuplicateDoesNotMutateAgain()
        {
            Fixture fixture = CreateFixture(playerDamage: 3, inspiration: 5, playerCost: 2);
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);
            CommitNormalized(fixture.Session, fixture.PlayerPlan(), fixture.EnemyNone());

            Assert.That(entry.SubmitCurrentTurn(), Is.True);
            int hp = fixture.Enemy.HP;
            int inspiration = fixture.Session.Inspiration.Current;
            Assert.That(entry.SubmitCurrentTurn(), Is.False);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hp));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspiration));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Resolved));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Resolution));
        }

        [Test]
        public void InvalidEntrySubmission_DoesNotResolveOrChangePhase()
        {
            Fixture fixture = CreateFixture();
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);
            fixture.Session.CurrentTurn.SetPlan(fixture.Player.Id, fixture.PlayerPlan());

            Assert.That(entry.SubmitCurrentTurn(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Planning));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP));
        }

        [Test]
        public void PlansCannotChangeAfterSubmission()
        {
            Fixture fixture = CreateFixture();
            CommitNormalized(fixture.Session, fixture.PlayerPlan(), fixture.EnemyNone());
            Assert.That(fixture.Session.CurrentTurn.TrySubmit(), Is.True);

            ActionPlan replacement = fixture.PlayerNone();
            Assert.That(fixture.Session.CurrentTurn.TrySetPlan(fixture.Player.Id, replacement), Is.False);
            fixture.Session.CurrentTurn.TryGetPlan(fixture.Player.Id, out ActionPlan retained);
            Assert.That(retained.Slot1.IsNone, Is.False);
        }

        [Test]
        public void CompatibilityConfirmPlanning_CannotEnterResolutionWithoutResolvedTurn()
        {
            Fixture fixture = CreateFixture();
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);

            entry.ConfirmPlanningFromUI();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Planning));
        }

        [Test]
        public void Orchestrator_UsesCanonicalEntryAndPlansEveryLivingEnemy()
        {
            Fixture fixture = CreateFixture(enemyCount: 2);
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);
            CombatFlowOrchestrator orchestrator = CreateComponent<CombatFlowOrchestrator>("Orchestrator");
            SetField(orchestrator, "entryPoint", entry);
            orchestrator.BindSession(fixture.Session);

            CombatPlanDraft draft = new CombatPlanDraft();
            PlannedAction fake = fixture.Action(fixture.Player, fixture.PlayerSkill, fixture.Enemy, speed: -999, tag: SkillTag.Utility);
            draft.SetSlot(fixture.Player.Id, 0, fake);

            Assert.That(orchestrator.SubmitPlayerDraftAndAdvance(draft, fixture.Player, out string error), Is.True, error);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Resolution));
            Assert.That(fixture.Session.CurrentTurn.TryGetPlan(fixture.Enemy.Id, out ActionPlan enemyOne), Is.True);
            Assert.That(fixture.Session.CurrentTurn.TryGetPlan(fixture.Enemies[1].Id, out ActionPlan enemyTwo), Is.True);
            Assert.That(enemyOne.Slot1.IsNone, Is.False);
            Assert.That(enemyTwo.Slot1.IsNone, Is.False);
        }

        [Test]
        public void PlayerDraft_NormalizesAuthoritativeMetadataWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            CombatPlanDraft draft = new CombatPlanDraft();
            PlannedAction fake = new PlannedAction(
                fixture.PlayerSkill.Id,
                SkillTag.Utility,
                TargetingRule.Self,
                fixture.Enemy.Id,
                -500,
                !fixture.PlayerSkill.ConsumesTurn);
            draft.SetSlot(fixture.Player.Id, 0, fake);

            Assert.That(CombatPlanValidator.TryNormalizePlayerDraft(
                fixture.Session, draft, fixture.Player, out ActionPlan normalized, out string error), Is.True, error);
            Assert.That(normalized.Slot1.Tag, Is.EqualTo(fixture.PlayerSkill.Tag));
            Assert.That(normalized.Slot1.Targeting, Is.EqualTo(fixture.PlayerSkill.Targeting));
            Assert.That(normalized.Slot1.PlannedSpeed, Is.EqualTo(fixture.PlayerSkill.Speed));
            Assert.That(normalized.Slot1.ConsumesTurn, Is.EqualTo(fixture.PlayerSkill.ConsumesTurn));
            Assert.That(draft.TryGetPlan(fixture.Player.Id, out ActionPlan original), Is.True);
            Assert.That(original.Slot1.Tag, Is.EqualTo(SkillTag.Utility));
            Assert.That(original.Slot1.PlannedSpeed, Is.EqualTo(-500));
        }

        [Test]
        public void PlayerDraft_RejectsActorOutsideSessionAndAnotherActorPlan()
        {
            Fixture fixture = CreateFixture();
            TestCombatant outsider = new TestCombatant(999, Side.Allies, 10);
            outsider.AddSkill(fixture.PlayerSkill);
            CombatPlanDraft draft = new CombatPlanDraft();
            draft.SetSlot(outsider.Id, 0, fixture.Action(outsider, fixture.PlayerSkill, fixture.Enemy));

            Assert.That(CombatPlanValidator.ValidatePlayerDraft(fixture.Session, draft, outsider, out _), Is.False);

            draft = new CombatPlanDraft();
            draft.SetSlot(fixture.Player.Id, 0, fixture.PlayerAction());
            draft.SetSlot(fixture.Enemy.Id, 0, fixture.EnemyAction());
            Assert.That(CombatPlanValidator.ValidatePlayerDraft(fixture.Session, draft, fixture.Player, out _), Is.False);
        }

        [Test]
        public void PlanValidation_RejectsUnknownSkillInvalidTargetWrongSideAndDeadTarget()
        {
            Fixture fixture = CreateFixture();
            ActionPlan unknown = new ActionPlan(
                new PlannedAction(new SkillId(999), SkillTag.Attack, TargetingRule.SingleEnemy, fixture.Enemy.Id, 1, true),
                PlannedAction.None);
            Assert.That(CombatPlanValidator.TryNormalizePlan(fixture.Session, fixture.Player, unknown, out _, out _), Is.False);

            ActionPlan invalidTarget = new ActionPlan(
                fixture.Action(fixture.Player, fixture.PlayerSkill, new TestCombatant(888, Side.Enemies, 10)),
                PlannedAction.None);
            Assert.That(CombatPlanValidator.TryNormalizePlan(fixture.Session, fixture.Player, invalidTarget, out _, out _), Is.False);

            ActionPlan wrongSide = new ActionPlan(
                fixture.Action(fixture.Player, fixture.PlayerSkill, fixture.Player),
                PlannedAction.None);
            Assert.That(CombatPlanValidator.TryNormalizePlan(fixture.Session, fixture.Player, wrongSide, out _, out _), Is.False);

            fixture.Enemy.ApplyDamage(999);
            Assert.That(CombatPlanValidator.TryNormalizePlan(
                fixture.Session, fixture.Player, fixture.PlayerPlan(), out _, out _), Is.False);
        }

        [Test]
        public void DeadOrStunnedActor_NormalizesToExplicitNone()
        {
            Fixture deadFixture = CreateFixture();
            deadFixture.Player.ApplyDamage(999);
            Assert.That(CombatPlanValidator.TryNormalizePlan(
                deadFixture.Session, deadFixture.Player, deadFixture.PlayerPlan(), out ActionPlan deadPlan, out _), Is.True);
            Assert.That(deadPlan.Slot1.IsNone && deadPlan.Slot2.IsNone, Is.True);

            Fixture stunnedFixture = CreateFixture();
            stunnedFixture.Player.SetStunned(true);
            Assert.That(CombatPlanValidator.TryNormalizePlan(
                stunnedFixture.Session, stunnedFixture.Player, stunnedFixture.PlayerPlan(), out ActionPlan stunnedPlan, out _), Is.True);
            Assert.That(stunnedPlan.Slot1.IsNone && stunnedPlan.Slot2.IsNone, Is.True);
        }

        [Test]
        public void AdditionalAllyWithoutPlan_ReceivesExplicitNone()
        {
            Fixture fixture = CreateFixture(additionalAlly: true);
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);
            CombatFlowOrchestrator orchestrator = CreateComponent<CombatFlowOrchestrator>("Orchestrator");
            SetField(orchestrator, "entryPoint", entry);
            orchestrator.BindSession(fixture.Session);
            CombatPlanDraft draft = fixture.PlayerDraft();

            Assert.That(orchestrator.SubmitPlayerDraftAndAdvance(draft, fixture.Player, out string error), Is.True, error);
            Assert.That(fixture.Session.CurrentTurn.TryGetPlan(fixture.Allies[1].Id, out ActionPlan plan), Is.True);
            Assert.That(plan.Slot1.IsNone && plan.Slot2.IsNone, Is.True);
        }

        [Test]
        public void ExistingValidEnemyPlan_IsNotOverwritten()
        {
            Fixture fixture = CreateFixture();
            TestSkill alternate = new TestSkill(22, TargetingRule.SingleAlly, speed: 99, damage: 0);
            fixture.Enemy.AddSkill(alternate);
            ActionPlan supplied = new ActionPlan(fixture.Action(fixture.Enemy, alternate, fixture.Player), PlannedAction.None);
            fixture.Session.CurrentTurn.SetPlan(fixture.Enemy.Id, supplied);

            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);
            CombatFlowOrchestrator orchestrator = CreateComponent<CombatFlowOrchestrator>("Orchestrator");
            SetField(orchestrator, "entryPoint", entry);
            orchestrator.BindSession(fixture.Session);

            Assert.That(orchestrator.SubmitPlayerDraftAndAdvance(
                fixture.PlayerDraft(), fixture.Player, out string error), Is.True, error);
            Assert.That(fixture.Session.CurrentTurn.TryGetPlan(fixture.Enemy.Id, out ActionPlan committed), Is.True);
            Assert.That(committed.Slot1.SkillId.Value, Is.EqualTo(alternate.Id.Value));
        }

        [Test]
        public void EnemyPlanner_TargetsLivingAllyWhenFirstAllyIsDead()
        {
            Fixture fixture = CreateFixture(additionalAlly: true);
            fixture.Player.ApplyDamage(999);
            TestCombatant livingAlly = fixture.Allies[1];
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, fixture);
            CombatFlowOrchestrator orchestrator = CreateComponent<CombatFlowOrchestrator>("Orchestrator");
            SetField(orchestrator, "entryPoint", entry);
            orchestrator.BindSession(fixture.Session);
            CombatPlanDraft draft = new CombatPlanDraft();
            draft.SetSlot(fixture.Player.Id, 0, PlannedAction.None);

            Assert.That(orchestrator.SubmitPlayerDraftAndAdvance(draft, fixture.Player, out string error), Is.True, error);
            fixture.Session.CurrentTurn.TryGetPlan(fixture.Enemy.Id, out ActionPlan enemyPlan);
            Assert.That(enemyPlan.Slot1.TargetCombatantId.Value, Is.EqualTo(livingAlly.Id.Value));
        }

        [Test]
        public void DeadEnemyAndEnemyWithoutSkills_ReceiveExplicitNone()
        {
            Fixture deadFixture = CreateFixture();
            deadFixture.Enemy.ApplyDamage(999);
            Assert.That(CombatPlanValidator.TryNormalizePlan(
                deadFixture.Session, deadFixture.Enemy, deadFixture.EnemyPlan(), out ActionPlan deadPlan, out _), Is.True);
            Assert.That(deadPlan.Slot1.IsNone, Is.True);

            Fixture noSkillFixture = CreateFixture();
            noSkillFixture.Enemy.ClearSkills();
            CombatEntryPoint entry = CreateEntry();
            BindEntry(entry, noSkillFixture);
            noSkillFixture.Session.CurrentTurn.SetPlan(noSkillFixture.Player.Id, noSkillFixture.PlayerPlan());
            Assert.That(entry.SubmitCurrentTurn(), Is.True);
            noSkillFixture.Session.CurrentTurn.TryGetPlan(noSkillFixture.Enemy.Id, out ActionPlan noSkillPlan);
            Assert.That(noSkillPlan.Slot1.IsNone, Is.True);
        }

        [Test]
        public void Resolver_RejectsNullUnsubmittedAndDuplicateTurns()
        {
            LogAssert.Expect(LogType.Error, "[CombatTurnResolver] Cannot resolve a null session.");
            Assert.That(CombatTurnResolver.ResolveTurn(null), Is.False);
            Fixture fixture = CreateFixture();
            Assert.That(CombatTurnResolver.ResolveTurn(fixture.Session), Is.False);

            CommitNormalized(fixture.Session, fixture.PlayerPlan(), fixture.EnemyNone());
            Assert.That(fixture.Session.CurrentTurn.TrySubmit(), Is.True);
            Assert.That(CombatTurnResolver.ResolveTurn(fixture.Session), Is.True);
            int hp = fixture.Enemy.HP;
            Assert.That(CombatTurnResolver.ResolveTurn(fixture.Session), Is.False);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hp));
        }

        [Test]
        public void ResolverException_MarksFailureAndCannotResolveAgain()
        {
            Fixture fixture = CreateFixture();
            fixture.Enemy.ThrowOnDamage = true;
            CommitNormalized(fixture.Session, fixture.PlayerPlan(), fixture.EnemyNone());
            fixture.Session.CurrentTurn.TrySubmit();
            LogAssert.Expect(LogType.Error, new Regex("Turn 1 failed during slot 1 actor 1 skill 1"));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            Assert.That(CombatTurnResolver.ResolveTurn(
                fixture.Session, new FixedCombatRandomSource()), Is.False);
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.ResolutionFailed));
            Assert.That(CombatTurnResolver.ResolveTurn(fixture.Session), Is.False);
        }

        [Test]
        public void Ordering_SlotOnePrecedesSlotTwoAndHigherSpeedPrecedesLowerSpeed()
        {
            Fixture fixture = CreateFixture(enemyCount: 2, playerSpeed: 2, enemySpeed: 9);
            ActionPlan player = new ActionPlan(fixture.PlayerAction(), fixture.PlayerAction());
            ActionPlan enemy = new ActionPlan(fixture.EnemyAction(), PlannedAction.None);
            Dictionary<CombatantId, ActionPlan> plans = new()
            {
                [fixture.Player.Id] = new ActionPlan(
                    fixture.Action(fixture.Player, fixture.PlayerSkill, fixture.Enemies[1]),
                    fixture.Action(fixture.Player, fixture.PlayerSkill, fixture.Enemies[1])),
                [fixture.Enemy.Id] = enemy,
                [fixture.Enemies[1].Id] = fixture.EnemyNone()
            };
            Resolve(fixture.Session, plans, new FixedCombatRandomSource());

            Assert.That(fixture.Session.CurrentTurn.Playbook.Count, Is.EqualTo(3));
            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[0]), Is.SameAs(fixture.Enemy));
            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[1]), Is.SameAs(fixture.Player));
            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[2]), Is.SameAs(fixture.Player));
        }

        [Test]
        public void Ordering_InitiativeThenRosterOrderBreaksEqualSpeedTiesDeterministically()
        {
            Fixture fixture = CreateFixture(additionalAlly: true, enemyCount: 2, playerSpeed: 5, enemySpeed: 5);
            TestCombatant allyTwo = fixture.Allies[1];
            allyTwo.AddSkill(fixture.PlayerSkill);
            Dictionary<CombatantId, ActionPlan> plans = new()
            {
                [fixture.Player.Id] = new ActionPlan(fixture.Action(fixture.Player, fixture.PlayerSkill, fixture.Enemies[1]), PlannedAction.None),
                [allyTwo.Id] = new ActionPlan(fixture.Action(allyTwo, fixture.PlayerSkill, fixture.Enemies[1]), PlannedAction.None),
                [fixture.Enemy.Id] = new ActionPlan(fixture.Action(fixture.Enemy, fixture.EnemySkill, fixture.Player), PlannedAction.None),
                [fixture.Enemies[1].Id] = new ActionPlan(PlannedAction.None, PlannedAction.None)
            };
            Resolve(fixture.Session, plans, new FixedCombatRandomSource(1, 1));

            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[0]), Is.SameAs(fixture.Player));
            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[1]), Is.SameAs(allyTwo));
            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[2]), Is.SameAs(fixture.Enemy));
        }

        [Test]
        public void Ordering_SameSeedProducesSameClashResult()
        {
            Fixture first = CreateFixture();
            Resolve(first.Session, first.PlayerPlan(), first.EnemyPlan(), new SeededCombatRandomSource(42));
            Event_Clash firstResult = (Event_Clash)first.Session.CurrentTurn.Playbook[0];

            Fixture second = CreateFixture();
            Resolve(second.Session, second.PlayerPlan(), second.EnemyPlan(), new SeededCombatRandomSource(42));
            Event_Clash secondResult = (Event_Clash)second.Session.CurrentTurn.Playbook[0];

            Assert.That(secondResult.PowerA, Is.EqualTo(firstResult.PowerA));
            Assert.That(secondResult.PowerB, Is.EqualTo(firstResult.PowerB));
        }

        [Test]
        public void StableTieOrdering_DoesNotDependOnPriorResolutions()
        {
            Fixture warmup = CreateFixture();
            Resolve(warmup.Session, warmup.PlayerPlan(), warmup.EnemyNone());

            Fixture fixture = CreateFixture(additionalAlly: true, enemyCount: 2, playerSpeed: 5, enemySpeed: 5);
            TestCombatant allyTwo = fixture.Allies[1];
            allyTwo.AddSkill(fixture.PlayerSkill);
            Dictionary<CombatantId, ActionPlan> plans = new()
            {
                [fixture.Player.Id] = new ActionPlan(fixture.Action(fixture.Player, fixture.PlayerSkill, fixture.Enemies[1]), PlannedAction.None),
                [allyTwo.Id] = new ActionPlan(fixture.Action(allyTwo, fixture.PlayerSkill, fixture.Enemies[1]), PlannedAction.None),
                [fixture.Enemy.Id] = new ActionPlan(fixture.EnemyAction(), PlannedAction.None),
                [fixture.Enemies[1].Id] = fixture.EnemyNone()
            };
            Resolve(fixture.Session, plans, new FixedCombatRandomSource());

            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[0]), Is.SameAs(fixture.Player));
            Assert.That(ActorOf(fixture.Session.CurrentTurn.Playbook[1]), Is.SameAs(allyTwo));
        }

        [TestCase(3, 1, Side.Allies)]
        [TestCase(1, 3, Side.Enemies)]
        [TestCase(2, 2, null)]
        public void Clash_FixedRollControlsWinnerOrDraw(int rollA, int rollB, Side? winnerSide)
        {
            Fixture fixture = CreateFixture(playerDamage: 2, enemyDamage: 2);
            Resolve(fixture.Session, fixture.PlayerPlan(), fixture.EnemyPlan(), new FixedCombatRandomSource(rollA, rollB));

            Assert.That(fixture.Session.CurrentTurn.Playbook.Count, Is.EqualTo(1));
            Event_Clash clash = fixture.Session.CurrentTurn.Playbook[0] as Event_Clash;
            Assert.That(clash, Is.Not.Null);
            Assert.That(clash.Winner?.Side, Is.EqualTo(winnerSide));
        }

        [Test]
        public void Clash_IsOneEventAndDoesNotAlsoResolveUnopposed()
        {
            Fixture fixture = CreateFixture();
            Resolve(fixture.Session, fixture.PlayerPlan(), fixture.EnemyPlan(), new FixedCombatRandomSource(3, 1));
            Assert.That(fixture.Session.CurrentTurn.Playbook.Count, Is.EqualTo(1));
            Assert.That(fixture.Session.CurrentTurn.Playbook[0], Is.TypeOf<Event_Clash>());
        }

        [Test]
        public void Clash_CancelledForCombinedCostSpendsNothing()
        {
            Fixture fixture = CreateFixture(inspiration: 2, playerCost: 2, enemyCost: 2);
            int inspirationBefore = fixture.Session.Inspiration.Current;
            Resolve(fixture.Session, fixture.PlayerPlan(), fixture.EnemyPlan(), new FixedCombatRandomSource(3, 1));
            Event_Clash clash = (Event_Clash)fixture.Session.CurrentTurn.Playbook[0];
            Assert.That(clash.IsCancelled, Is.True);
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspirationBefore));
            Assert.That(fixture.Player.HP, Is.EqualTo(fixture.Player.MaxHP));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP));
        }

        [Test]
        public void Clash_SuccessSpendsCombinedCostAndAppliesDamageOnce()
        {
            Fixture fixture = CreateFixture(inspiration: 5, playerCost: 2, enemyCost: 1, playerDamage: 4, enemyDamage: 1);
            int inspirationBefore = fixture.Session.Inspiration.Current;
            Resolve(fixture.Session, fixture.PlayerPlan(), fixture.EnemyPlan(), new FixedCombatRandomSource(3, 1));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(inspirationBefore - 3));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP - 4));
        }

        [Test]
        public void ActorKilledInSlotOne_CannotExecuteSlotTwoAndCancellationIsRecorded()
        {
            Fixture fixture = CreateFixture(playerDamage: 99, enemyDamage: 5, playerSpeed: 10, enemySpeed: 1);
            ActionPlan player = new ActionPlan(fixture.PlayerAction(), PlannedAction.None);
            ActionPlan enemy = new ActionPlan(PlannedAction.None, fixture.EnemyAction());
            Resolve(fixture.Session, player, enemy);

            Assert.That(fixture.Enemy.HP, Is.Zero);
            Assert.That(fixture.Player.HP, Is.EqualTo(fixture.Player.MaxHP));
            Assert.That(fixture.Session.CurrentTurn.Playbook.Count, Is.EqualTo(2));
            Assert.That(((Event_Unopposed)fixture.Session.CurrentTurn.Playbook[1]).IsCancelled, Is.True);
        }

        [Test]
        public void ActorStunnedInSlotOne_CannotExecuteSlotTwo()
        {
            Fixture fixture = CreateFixture(playerDamage: 0, playerStagger: 10, enemyDamage: 5);
            fixture.Enemy.StaggerMaximum = 5;
            Resolve(
                fixture.Session,
                new ActionPlan(fixture.PlayerAction(), PlannedAction.None),
                new ActionPlan(PlannedAction.None, fixture.EnemyAction()));

            Assert.That(fixture.Enemy.IsStunned, Is.True);
            Assert.That(fixture.Player.HP, Is.EqualTo(fixture.Player.MaxHP));
            Assert.That(((Event_Unopposed)fixture.Session.CurrentTurn.Playbook[1]).IsCancelled, Is.True);
        }

        [Test]
        public void TargetKilledEarlier_CancelsLaterActionBeforeSpending()
        {
            Fixture fixture = CreateFixture(additionalAlly: true, playerDamage: 99, playerCost: 0);
            TestCombatant allyTwo = fixture.Allies[1];
            TestSkill costly = new TestSkill(31, TargetingRule.SingleEnemy, speed: 1, damage: 3, cost: 2);
            allyTwo.AddSkill(costly);
            Dictionary<CombatantId, ActionPlan> plans = new()
            {
                [fixture.Player.Id] = new ActionPlan(fixture.PlayerAction(), PlannedAction.None),
                [allyTwo.Id] = new ActionPlan(fixture.Action(allyTwo, costly, fixture.Enemy), PlannedAction.None),
                [fixture.Enemy.Id] = fixture.EnemyNone()
            };
            int before = fixture.Session.Inspiration.Current;
            Resolve(fixture.Session, plans, new FixedCombatRandomSource());

            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(before));
            Assert.That(((Event_Unopposed)fixture.Session.CurrentTurn.Playbook[1]).IsCancelled, Is.True);
        }

        [Test]
        public void NoneAction_HasNoCostEffectOrPlaybookEvent()
        {
            Fixture fixture = CreateFixture();
            Resolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            Assert.That(fixture.Session.CurrentTurn.Playbook, Is.Empty);
            Assert.That(fixture.Player.HP, Is.EqualTo(fixture.Player.MaxHP));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP));
        }

        [Test]
        public void AreaAction_SpendsOnceAndHitsEveryLivingTargetOnce()
        {
            Fixture fixture = CreateFixture(enemyCount: 2, playerDamage: 0);
            TestSkill area = new TestSkill(50, TargetingRule.AllEnemies, speed: 10, damage: 2, cost: 1);
            fixture.Player.AddSkill(area);
            ActionPlan player = new ActionPlan(fixture.Action(fixture.Player, area, null), PlannedAction.None);
            Dictionary<CombatantId, ActionPlan> plans = new()
            {
                [fixture.Player.Id] = player,
                [fixture.Enemy.Id] = fixture.EnemyNone(),
                [fixture.Enemies[1].Id] = fixture.EnemyNone()
            };
            int before = fixture.Session.Inspiration.Current;
            Resolve(fixture.Session, plans, new FixedCombatRandomSource());

            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(before - 1));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP - 2));
            Assert.That(fixture.Enemies[1].HP, Is.EqualTo(fixture.Enemies[1].MaxHP - 2));
            Assert.That(fixture.Session.CurrentTurn.Playbook[0], Is.TypeOf<Event_Area>());
        }

        [Test]
        public void PresentationHandshake_StartsAndCompletesExactlyOnce()
        {
            Fixture fixture = CreateFixture();
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            Assert.That(fixture.StateMachine.ConfirmPlanning(), Is.True);
            int starts = 0;
            Action completion = null;
            fixture.StateMachine.OnRequireResolutionPlay += (_, callback) =>
            {
                starts++;
                completion = callback;
            };

            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Presenting));

            completion();
            completion();
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.EndTurn));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Presented));
        }

        [Test]
        public void MissingPresenter_UsesImmediateFallbackOnce()
        {
            Fixture fixture = CreateFixture();
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            fixture.StateMachine.ConfirmPlanning();

            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.EndTurn));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Presented));
        }

        [Test]
        public void StaleCompletionAfterForceExit_IsIgnored()
        {
            Fixture fixture = CreateFixture();
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            fixture.StateMachine.ConfirmPlanning();
            Action completion = null;
            fixture.StateMachine.OnRequireResolutionPlay += (_, callback) => completion = callback;
            fixture.StateMachine.Tick();

            fixture.StateMachine.ForceExit(CombatEndReason.Abort);
            completion();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Abort));
        }

        [Test]
        public void PreviousTurnCompletion_IsIgnoredAfterNextPlanningBegins()
        {
            Fixture fixture = CreateFixture();
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            fixture.StateMachine.ConfirmPlanning();
            Action completion = null;
            fixture.StateMachine.OnRequireResolutionPlay += (_, callback) => completion = callback;
            fixture.StateMachine.Tick();
            completion();
            fixture.StateMachine.Tick();
            int currentTurn = fixture.Session.TurnIndex;

            completion();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(currentTurn));
        }

        [Test]
        public void EndTurn_IncrementsTurnAndInspirationOnceAndReturnsToPlanning()
        {
            Fixture fixture = CreateFixture(inspiration: 2);
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            fixture.StateMachine.ConfirmPlanning();
            fixture.StateMachine.Tick();
            int beforeTurn = fixture.Session.TurnIndex;
            int beforeInspiration = fixture.Session.Inspiration.Current;

            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(beforeTurn + 1));
            Assert.That(fixture.Session.Inspiration.Current, Is.EqualTo(beforeInspiration + 1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(fixture.Session.CurrentTurn.Lifecycle, Is.EqualTo(CombatTurnLifecycle.Planning));
        }

        [Test]
        public void EndTurn_ClearsStunExactlyOnce()
        {
            Fixture fixture = CreateFixture();
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            fixture.Enemy.SetStunned(true);
            fixture.Enemy.AddStagger(5);
            fixture.StateMachine.ConfirmPlanning();
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            Assert.That(fixture.Enemy.IsStunned, Is.False);
            Assert.That(fixture.Enemy.Stagger, Is.Zero);
        }

        [TestCase(true, false, CombatEndReason.Victory)]
        [TestCase(false, true, CombatEndReason.Defeat)]
        [TestCase(true, true, CombatEndReason.Abort)]
        public void EndEvaluator_UsesDocumentedSemantics(bool enemyDead, bool allyDead, CombatEndReason expected)
        {
            Fixture fixture = CreateFixture();
            if (enemyDead) fixture.Enemy.ApplyDamage(999);
            if (allyDead) fixture.Player.ApplyDamage(999);
            Assert.That(CombatEndEvaluator.Evaluate(fixture.Session), Is.EqualTo(expected));
        }

        [Test]
        public void VictoryAfterPresentation_ExitsWithoutCreatingNewTurn()
        {
            Fixture fixture = CreateFixture(playerDamage: 99);
            PrepareAndResolve(fixture.Session, fixture.PlayerPlan(), fixture.EnemyNone());
            int resolvedTurn = fixture.Session.TurnIndex;
            fixture.StateMachine.ConfirmPlanning();
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(resolvedTurn));
        }

        [Test]
        public void DefeatAfterPresentation_ExitsWithoutCreatingNewTurn()
        {
            Fixture fixture = CreateFixture(enemyDamage: 99);
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyPlan());
            int resolvedTurn = fixture.Session.TurnIndex;
            fixture.StateMachine.ConfirmPlanning();
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Defeat));
            Assert.That(fixture.Session.TurnIndex, Is.EqualTo(resolvedTurn));
        }

        [Test]
        public void InitialInvalidSession_AbortsOnce()
        {
            CombatSession session = new CombatSession(
                StartReason.SpecialSkill,
                Side.Allies,
                new InspirationPool(10, 0),
                new CombatEnvironment());
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            int exitEvents = 0;
            stateMachine.OnPhaseChanged += (_, next) =>
            {
                if (next == Phase.ExitCombat) exitEvents++;
            };

            stateMachine.Tick();
            stateMachine.Tick();

            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(stateMachine.EndReason, Is.EqualTo(CombatEndReason.Abort));
            Assert.That(exitEvents, Is.EqualTo(1));
        }

        [Test]
        public void PhaseEvents_HaveNoDuplicatesAcrossLivingTurn()
        {
            Fixture fixture = CreateFixture();
            List<Phase> entered = new();
            fixture.StateMachine.OnPhaseChanged += (_, next) => entered.Add(next);
            PrepareAndResolve(fixture.Session, fixture.PlayerNone(), fixture.EnemyNone());
            fixture.StateMachine.ConfirmPlanning();
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();
            fixture.StateMachine.Tick();

            CollectionAssert.AreEqual(new[] { Phase.Resolution, Phase.EndTurn, Phase.Planning }, entered);
        }

        private CombatEntryPoint CreateEntry()
        {
            return CreateComponent<CombatEntryPoint>("Entry");
        }

        private void BindEntry(CombatEntryPoint entry, Fixture fixture)
        {
            SetProperty(entry, "ActiveSession", fixture.Session);
            SetProperty(entry, "ActiveStateMachine", fixture.StateMachine);
        }

        private Fixture CreateFixture(
            int enemyCount = 1,
            bool additionalAlly = false,
            int inspiration = 10,
            int playerDamage = 2,
            int enemyDamage = 2,
            int playerCost = 0,
            int enemyCost = 0,
            int playerSpeed = 5,
            int enemySpeed = 4,
            int playerStagger = 0)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(20, inspiration),
                new CombatEnvironment());

            TestSkill playerSkill = new TestSkill(
                1,
                TargetingRule.SingleEnemy,
                playerSpeed,
                playerDamage,
                playerCost,
                playerStagger);
            TestSkill enemySkill = new TestSkill(
                11,
                TargetingRule.SingleAlly,
                enemySpeed,
                enemyDamage,
                enemyCost);
            TestCombatant player = new TestCombatant(1, Side.Allies, 20);
            player.AddSkill(playerSkill);
            session.Allies.Add(player);

            if (additionalAlly)
                session.Allies.Add(new TestCombatant(2, Side.Allies, 20));

            List<TestCombatant> enemies = new();
            for (int i = 0; i < enemyCount; i++)
            {
                TestCombatant enemy = new TestCombatant(100 + i, Side.Enemies, 20);
                enemy.AddSkill(enemySkill);
                enemies.Add(enemy);
                session.Enemies.Add(enemy);
            }

            CombatStateMachine stateMachine = new CombatStateMachine(session);
            stateMachine.Tick();
            return new Fixture(session, stateMachine, player, enemies, playerSkill, enemySkill);
        }

        private static void Resolve(
            CombatSession session,
            ActionPlan playerPlan,
            ActionPlan enemyPlan,
            ICombatRandomSource randomSource = null)
        {
            Dictionary<CombatantId, ActionPlan> plans = new()
            {
                [session.Allies[0].Id] = playerPlan,
                [session.Enemies[0].Id] = enemyPlan
            };
            Resolve(session, plans, randomSource ?? new FixedCombatRandomSource(1, 1));
        }

        private static void Resolve(
            CombatSession session,
            Dictionary<CombatantId, ActionPlan> plans,
            ICombatRandomSource randomSource)
        {
            CommitNormalized(session, plans);
            Assert.That(session.CurrentTurn.TrySubmit(), Is.True);
            Assert.That(CombatTurnResolver.ResolveTurn(session, randomSource), Is.True);
        }

        private static void PrepareAndResolve(CombatSession session, ActionPlan player, ActionPlan enemy)
        {
            Resolve(session, player, enemy, new FixedCombatRandomSource(1, 1));
        }

        private static void CommitNormalized(CombatSession session, ActionPlan player, ActionPlan enemy)
        {
            CommitNormalized(session, new Dictionary<CombatantId, ActionPlan>
            {
                [session.Allies[0].Id] = player,
                [session.Enemies[0].Id] = enemy
            });
        }

        private static void CommitNormalized(CombatSession session, Dictionary<CombatantId, ActionPlan> plans)
        {
            Assert.That(CombatPlanValidator.TryNormalizeCommittedPlans(
                session, plans, out Dictionary<CombatantId, ActionPlan> normalized, out string error), Is.True, error);
            Assert.That(session.CurrentTurn.TryReplacePlans(normalized), Is.True);
        }

        private static ICombatant ActorOf(PlaybookEvent playbookEvent)
        {
            if (playbookEvent is Event_Unopposed unopposed) return unopposed.Actor;
            if (playbookEvent is Event_Utility utility) return utility.Actor;
            if (playbookEvent is Event_Area area) return area.Actor;
            if (playbookEvent is Event_Clash clash) return clash.ActorA;
            return null;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.SetValue(target, value);
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public TestCombatant Player { get; }
            public TestCombatant Enemy => Enemies[0];
            public List<TestCombatant> Allies { get; }
            public List<TestCombatant> Enemies { get; }
            public TestSkill PlayerSkill { get; }
            public TestSkill EnemySkill { get; }

            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                TestCombatant player,
                List<TestCombatant> enemies,
                TestSkill playerSkill,
                TestSkill enemySkill)
            {
                Session = session;
                StateMachine = stateMachine;
                Player = player;
                Enemies = enemies;
                PlayerSkill = playerSkill;
                EnemySkill = enemySkill;
                Allies = new List<TestCombatant>();
                for (int i = 0; i < session.Allies.Count; i++)
                    Allies.Add((TestCombatant)session.Allies[i]);
            }

            public PlannedAction Action(
                TestCombatant actor,
                TestSkill skill,
                ICombatant target,
                int? speed = null,
                SkillTag? tag = null)
            {
                return new PlannedAction(
                    skill.Id,
                    tag ?? skill.Tag,
                    skill.Targeting,
                    target?.Id ?? default,
                    speed ?? skill.Speed,
                    skill.ConsumesTurn);
            }

            public PlannedAction PlayerAction() => Action(Player, PlayerSkill, Enemy);
            public PlannedAction EnemyAction(ICombatant target = null) => Action(Enemy, EnemySkill, target ?? Player);
            public ActionPlan PlayerPlan() => new ActionPlan(PlayerAction(), PlannedAction.None);
            public ActionPlan EnemyPlan() => new ActionPlan(EnemyAction(), PlannedAction.None);
            public ActionPlan PlayerNone() => new ActionPlan(PlannedAction.None, PlannedAction.None);
            public ActionPlan EnemyNone() => new ActionPlan(PlannedAction.None, PlannedAction.None);

            public CombatPlanDraft PlayerDraft()
            {
                CombatPlanDraft draft = new CombatPlanDraft();
                draft.SetSlot(Player.Id, 0, PlayerAction());
                return draft;
            }
        }

        private sealed class TestCombatant : ICombatant
        {
            private readonly List<ISkill> _skills = new();

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP { get; private set; }
            public int MaxHP { get; }
            public KeywordMask Weakness { get; set; }
            public KeywordMask Resist { get; set; }
            public int Stagger { get; private set; }
            public int StaggerMaximum { get; set; } = 10;
            public int StaggerMax => StaggerMaximum;
            public bool IsStunned { get; private set; }
            public bool ThrowOnDamage { get; set; }
            public IReadOnlyList<ISkill> Skills => _skills;

            public TestCombatant(int id, Side side, int hp)
            {
                Id = new CombatantId(id);
                Side = side;
                HP = hp;
                MaxHP = hp;
            }

            public void AddSkill(ISkill skill) => _skills.Add(skill);
            public void ClearSkills() => _skills.Clear();
            public void ApplyDamage(int amount)
            {
                if (ThrowOnDamage)
                    throw new InvalidOperationException("Deterministic test damage failure.");

                HP = Math.Max(0, HP - Math.Max(0, amount));
            }
            public void AddStagger(int amount) => Stagger = Math.Min(StaggerMax, Stagger + Math.Max(0, amount));
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() => Stagger = 0;
        }

        private sealed class TestSkill : ISkill
        {
            public SkillId Id { get; }
            public string Name => $"Skill {Id.Value}";
            public int InspirationCost { get; }
            public KeywordMask Keywords { get; }
            public SkillTag Tag { get; }
            public TargetingRule Targeting { get; }
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 1f;
            public float MoveSpeed => 1f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage { get; }
            public int BaseStagger { get; }
            public int WeaknessStaggerBonus { get; }
            public int Speed { get; }
            public bool ConsumesTurn { get; }

            public TestSkill(
                int id,
                TargetingRule targeting,
                int speed,
                int damage,
                int cost = 0,
                int stagger = 0,
                SkillTag tag = SkillTag.Attack,
                bool consumesTurn = true)
            {
                Id = new SkillId(id);
                Targeting = targeting;
                Speed = speed;
                BaseDamage = damage;
                InspirationCost = cost;
                BaseStagger = stagger;
                Tag = tag;
                ConsumesTurn = consumesTurn;
                Keywords = KeywordMask.None;
                WeaknessStaggerBonus = 0;
            }
        }
    }
}
#endif
