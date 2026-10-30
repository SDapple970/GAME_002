using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class CombatApprovedClashPlaytestV1Tests
    {
        public const string ApprovalLabel = "ApprovedPlaytestV1Numbers";
        private readonly List<Object> _created = new();
        private CombatClashPlaytestV1IntegrationTests _fixture;
        private CombatEntryPoint _entry;
        private CombatFlowOrchestrator _flow;
        private CombatSession Session => _entry.ActiveSession;
        private List<CombatClashRoundPresentationRequest> Rounds => Get<List<CombatClashRoundPresentationRequest>>("_rounds");

        // The existing test SO already contains exactly the approved numbers. Do not rewrite it.
        public static void MarkApprovedNumbers()
        {
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(CombatClashPlaytestV1IntegrationTests.ConfigPath);
            if (config == null || config.EnableProductionHandBp || config.ClashAttackerDurability != 3 ||
                config.ClashResponderDurability != 3 || config.ClashMaxComparisons != 3 || config.ClashDecisionDurationSeconds != 5f ||
                config.NormalHandCapacity != 2 || config.ClashHandCapacity != 3 || config.BpMaximum != 3 ||
                config.BpRoundCyclesPerGain != 2 || config.BpMaximumInvestment != 3)
                throw new InvalidOperationException("The test profile must match approved numbers and remain disabled before marking approval.");
            string[] labels = AssetDatabase.GetLabels(config);
            if (!labels.Contains(ApprovalLabel)) AssetDatabase.SetLabels(config, labels.Concat(new[] { ApprovalLabel }).ToArray());
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear(); _fixture?.TearDown();
        }

        [Test]
        public void ApprovedProfile_HasExactNumbers_StaysDisabled_AndDoesNotForceMpThree()
        {
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(CombatClashPlaytestV1IntegrationTests.ConfigPath);
            Assert.That(AssetDatabase.GetLabels(config), Does.Contain(ApprovalLabel));
            Assert.That(AssetDatabase.GetLabels(config), Does.Contain("TestOnly"));
            Assert.That(AssetDatabase.GetLabels(config), Does.Contain("ContentPending"));
            Assert.That(config.ClashAttackerDurability, Is.EqualTo(3)); Assert.That(config.ClashResponderDurability, Is.EqualTo(3));
            Assert.That(config.ClashMaxComparisons, Is.EqualTo(3)); Assert.That(config.ClashDecisionDurationSeconds, Is.EqualTo(5f));
            Assert.That(config.NormalHandCapacity, Is.EqualTo(2)); Assert.That(config.ClashHandCapacity, Is.EqualTo(3));
            Assert.That(config.BpMaximum, Is.EqualTo(3)); Assert.That(config.BpRoundCyclesPerGain, Is.EqualTo(2)); Assert.That(config.BpMaximumInvestment, Is.EqualTo(3));
            Assert.That(config.EnableProductionHandBp, Is.False); Assert.That(config.InitialMp, Is.EqualTo(2));
            Assert.That(config.HandCardRules, Is.Empty); Assert.That(config.UltimateSkills, Is.Empty);
            Prepare(); Assert.That(Session.GetCombatState(Session.Allies[0]).CurrentMp, Is.EqualTo(2));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ApprovedMaximumThree_AllowsEarlierCompletion_WithOnlyApprovedCosts(int comparisons)
        {
            // MP3 is a fixture budget only in the explicit three-comparison case, never a gate or grant.
            Prepare(initialMp: comparisons == 3 ? 3 : 2, advantage: comparisons == 1 ? 3 : comparisons == 2 ? 2 : 1);
            InvokeFixture("Begin", 0); var series = Session.ExchangeState.ClashSeries;
            for (int round = 1; round <= comparisons; round++)
            {
                Assert.That(series.ComparisonCount, Is.EqualTo(round));
                Assert.That(series.AttackMpSpent, Is.EqualTo(round));
                Rounds.Last().TryComplete(); if (round < comparisons) InvokeFixture("NextRound", 0, true);
            }
            Assert.That(series.IsComplete, Is.True); Assert.That(series.ComparisonCount, Is.EqualTo(comparisons));
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(Session.Enemies[0].HP, Is.EqualTo(9));
            Assert.That(Rounds.Last().TryComplete(), Is.False);
        }

        [Test]
        public void FiveSecondClock_UsesUnscaledTimeDuringSlowMotion_AndStopsDuringPause()
        {
            Prepare(); InvokeFixture("Begin", 0); Rounds.Last().TryComplete();
            var request = _flow.PendingDecisionRequest;
            Time.timeScale = 0.25f; _flow.Tick(0.25f, 1f);
            Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.EqualTo(4f).Within(0.001f));
            var gameFlow = Get<GameFlowController>("_gameFlow"); gameFlow.Pause();
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Paused));
            _flow.Tick(1f, 100f); Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.EqualTo(4f).Within(0.001f));
            gameFlow.ResumePreviousState(); Time.timeScale = 0.25f;
            _flow.Tick(0.975f, 3.9f); Assert.That(_flow.PendingDecisionRequest, Is.SameAs(request));
            _flow.Tick(0.05f, 0.2f);
            Assert.That(_flow.SubmitClashSeriesSkill(request, Session.Allies[0].Skills[1]), Is.False);
            Assert.That(Rounds.Count, Is.EqualTo(2), "A legal timeout attack still passes the existing enemy response and commit path.");
        }

        [Test]
        public void MpTwo_UIShowsInsufficientMp_AndClashDoesNotRecoverIt()
        {
            Prepare(); InvokeFixture("Begin", 0); Rounds.Last().TryComplete();
            InvokeFixture("NextRound", 0, true); Rounds.Last().TryComplete();
            var actor = Session.Allies[0]; Assert.That(Session.GetCombatState(actor).CurrentMp, Is.Zero);
            var binder = BindUi(out var skillsRoot); var controller = Controller(binder);
            Assert.That(binder.ViewState.SkillAvailability.Single(option => option.Skill.Id.Value == 942).Reason,
                Is.EqualTo(CombatSkillUnavailableReason.InsufficientMp));
            var clashButtons = skillsRoot.GetComponentsInChildren<Button>().Where(button => button.GetComponentInChildren<Text>().text.Contains("[합]")).ToArray();
            Assert.That(clashButtons, Is.Not.Empty);
            Assert.That(clashButtons.All(button => !button.interactable && button.GetComponentInChildren<Text>().text.Contains("MP 부족")), Is.True);
            Assert.That(controller.SelectSkill(actor.Skills[1]), Is.False); Assert.That(binder.ViewState.CanConfirm, Is.False);
            _flow.Tick(100f, 4f); Assert.That(Session.GetCombatState(actor).CurrentMp, Is.Zero);
        }

        [Test]
        public void MissingClashCard_UIShowsOnlyOwnedCards_AndRejectsUnavailableSkill()
        {
            Prepare(mpCost: 0, clashCopies: 1); InvokeFixture("Begin", 0); Rounds.Last().TryComplete();
            InvokeFixture("NextRound", 0, true); Rounds.Last().TryComplete();
            var binder = BindUi(out var skillsRoot); var actor = Session.Allies[0];
            Assert.That(Session.GetCombatState(actor).CurrentMp, Is.EqualTo(2), "Isolate cards from MP with transient zero-cost skills.");
            Assert.That(binder.ViewState.SkillAvailability.Single(option => option.Skill.Id.Value == 942).Reason,
                Is.EqualTo(CombatSkillUnavailableReason.NotInHand));
            Assert.That(binder.ViewState.HandCards.All(card => card.Kind == CombatCardKind.Normal), Is.True);
            Assert.That(skillsRoot.GetComponentsInChildren<Button>().Any(button => button.GetComponentInChildren<Text>().text.Contains("[합]")), Is.False);
            Assert.That(skillsRoot.GetComponentsInChildren<Button>().All(button => !button.interactable), Is.True);
            Assert.That(Controller(binder).SelectSkill(actor.Skills[1]), Is.False); Assert.That(binder.ViewState.CanConfirm, Is.False);
        }

        [Test]
        public void ThreeRounds_DoNotTickThreeTurnStatus_AndCompleteExchangeTicksOnce()
        {
            Prepare(initialMp: 3);
            var timed = Track(ScriptableObject.CreateInstance<CombatStatusDefinitionSO>());
            Set(timed, "statusId", "d1.fixture.duration"); Set(timed, "durationKind", CombatStatusDurationKind.Explicit); Set(timed, "explicitDuration", 3);
            var state = Session.GetCombatState(Session.Enemies[0]); state.ApplyStatus(timed);
            Assert.That(state.TryGetStatus(timed.StatusId, out var status), Is.True);
            InvokeFixture("Begin", 0);
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(status.RemainingDuration, Is.EqualTo(3)); Rounds.Last().TryComplete();
                if (round < 3) InvokeFixture("NextRound", 0, true);
            }
            Assert.That(status.RemainingDuration, Is.EqualTo(3));
            var outcome = Get<CombatOutcomePresentationRequest>("_outcome");
            Assert.That(outcome.TryComplete(), Is.True); Assert.That(status.RemainingDuration, Is.EqualTo(2));
            Assert.That(outcome.TryComplete(), Is.False); Assert.That(status.RemainingDuration, Is.EqualTo(2));
        }

        private void Prepare(int initialMp = 2, int advantage = 1, int mpCost = 1, int clashCopies = 6)
        {
            _fixture = new CombatClashPlaytestV1IntegrationTests(); InvokeFixture("Prepare", 1, true, true);
            var config = Get<FinalCombatRuntimeConfigSO>("_config"); Set(config, "initialMp", initialMp);
            var definitions = Get<SkillDefinitionSO[]>("_definitions");
            foreach (var definition in definitions) Set(definition, "finalMpCost", mpCost);
            Set(definitions[0], "clashPower", 2 + advantage); Set(definitions[1], "clashPower", 2 + advantage);
            var data = new SerializedObject(config);
            data.FindProperty("handCardRules").GetArrayElementAtIndex(1).FindPropertyRelative("copies").intValue = clashCopies;
            data.ApplyModifiedPropertiesWithoutUndo();
            _entry = Get<CombatEntryPoint>("_entry"); _flow = Get<CombatFlowOrchestrator>("_flow");
            Assert.That(_entry.StartCombat(Get<Game.Combat.Adapters.CombatStartRequest>("_request")), Is.True);
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.Standoff));
            var actor = Session.Allies[0]; Session.TryGetHand(actor, out var hand);
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(2));
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(Math.Min(3, clashCopies)));
            Assert.That(Session.BpRuntime.TryGetBalance(actor, out int bp), Is.True); Assert.That(bp, Is.Zero);
            Assert.That(Session.UltimateRuntime.TryGetStage(actor, out int stage), Is.True); Assert.That(stage, Is.Zero);
        }
        private FinalCombatUIBinder BindUi(out GameObject skillsRoot)
        {
            var panel = Ui("Approved.Panel"); skillsRoot = Ui("Approved.Cards", panel.transform);
            var template = Ui("Approved.Template"); var image = template.AddComponent<Image>(); var button = template.AddComponent<Button>(); button.targetGraphic = image;
            Ui("Label", template.transform).AddComponent<Text>();
            var binder = Ui("Approved.Binder").AddComponent<FinalCombatUIBinder>();
            Set(binder, "panelRoot", panel); Set(binder, "skillListRoot", skillsRoot); Set(binder, "optionButtonPrefab", button);
            binder.Bind(Session, _entry.ActiveStateMachine, _flow); Assert.That(binder.IsBound, Is.True); return binder;
        }
        private GameObject Ui(string name, Transform parent = null) { var go = Track(new GameObject(name, typeof(RectTransform))); if (parent != null) go.transform.SetParent(parent, false); return go; }
        private static FinalCombatPlayerCommandController Controller(FinalCombatUIBinder binder) => (FinalCombatPlayerCommandController)GetField(binder, "_controller");
        private T Get<T>(string field) => (T)GetField(_fixture, field);
        private object InvokeFixture(string method, params object[] args) => typeof(CombatClashPlaytestV1IntegrationTests).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_fixture, args);
        private static object GetField(object owner, string field) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
