#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.DemoMission.Data;
using Game.DemoMission.Runtime;
using Game.NonCombat.Inventory;
using Game.Reward;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    public sealed class CombatCompletionRewardFlowTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [SetUp]
        public void SetUp()
        {
            DestroyRuntimeObjects();
            CombatRewardUIBinder.ResetOwnershipForTests();
        }

        [TearDown]
        public void TearDown()
        {
            CombatRewardUIBinder[] binders = UnityEngine.Object.FindObjectsByType<CombatRewardUIBinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < binders.Length; i++)
                InvokeIfPresent(binders[i], "OnDisable");

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
            CombatRewardUIBinder.ResetOwnershipForTests();
            DestroyRuntimeObjects();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SessionCompletionId_IsStable()
        {
            CombatSession session = CreateSession();
            string completionId = session.CompletionId;

            session.BeginNewTurn();

            Assert.That(completionId, Is.Not.Empty);
            Assert.That(session.CompletionId, Is.EqualTo(completionId));
        }

        [Test]
        public void SeparateSessions_HaveDifferentCompletionIds()
        {
            Assert.That(CreateSession().CompletionId, Is.Not.EqualTo(CreateSession().CompletionId));
        }

        [Test]
        public void RebuildingResult_PreservesSessionCompletionId()
        {
            CombatSession session = CreateSession();

            CombatResult first = CombatResultBuilder.Build(session, CombatEndReason.Victory);
            CombatResult second = CombatResultBuilder.Build(session, CombatEndReason.Victory);

            Assert.That(second.CompletionId, Is.EqualTo(first.CompletionId));
        }

        [Test]
        public void ResultBuilder_CopiesSessionCompletionId()
        {
            CombatSession session = CreateSession();
            Assert.That(CombatResultBuilder.Build(session, CombatEndReason.Defeat).CompletionId, Is.EqualTo(session.CompletionId));
        }

        [Test]
        public void LegacyResultWithoutId_UsesCompatibilitySourceId()
        {
            CombatResult result = CreateResult(CombatEndReason.Victory, completionId: null);
            RewardGrantRequest request = RewardService.CreateCombatRewardRequest(result, null);

            Assert.That(request.SourceId, Is.Not.Empty);
            Assert.That(request.SourceId, Is.Not.EqualTo("combat:null"));
        }

        [Test]
        public void EntryVictory_PublishesOneResult()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out _);
            int count = 0;
            entry.OnCombatEnded += _ => count++;

            FinishEntry(entry, CombatEndReason.Victory);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void EntryDefeat_PublishesOneResult()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out _);
            CombatResult published = null;
            entry.OnCombatEnded += result => published = result;

            FinishEntry(entry, CombatEndReason.Defeat);

            Assert.That(published.EndReason, Is.EqualTo(CombatEndReason.Defeat));
        }

        [Test]
        public void EntryDuplicateFinish_PublishesOnce()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out _);
            int count = 0;
            entry.OnCombatEnded += _ => count++;

            FinishEntry(entry, CombatEndReason.Victory);
            FinishEntry(entry, CombatEndReason.Victory);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void EntrySubscribers_ObserveClearedActiveReferences()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out _);
            bool cleared = false;
            entry.OnCombatEnded += _ => cleared = entry.ActiveSession == null && entry.ActiveStateMachine == null;

            FinishEntry(entry, CombatEndReason.Victory);

            Assert.That(cleared, Is.True);
        }

        [Test]
        public void ThrowingCompletionSubscriber_DoesNotBlockLaterSubscriber()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out _);
            bool laterSubscriberCalled = false;
            entry.OnCombatEnded += _ => throw new InvalidOperationException("subscriber test");
            entry.OnCombatEnded += _ => laterSubscriberCalled = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("OnCombatEnded subscriber failed.*subscriber test"));

            FinishEntry(entry, CombatEndReason.Victory);

            Assert.That(laterSubscriberCalled, Is.True);
        }

        [Test]
        public void EntryCleanup_RemainsCompleteAfterSubscriberException()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out _);
            entry.OnCombatEnded += _ => throw new InvalidOperationException("cleanup test");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("OnCombatEnded subscriber failed.*cleanup test"));

            FinishEntry(entry, CombatEndReason.Victory);

            Assert.That(entry.ActiveSession, Is.Null);
            Assert.That(entry.ActiveStateMachine, Is.Null);
        }

        [Test]
        public void EntryResult_PreservesOutcomeAndRosterIds()
        {
            CombatEntryPoint entry = CreateEntryWithSession(out CombatSession session);
            session.Allies.Add(new FakeCombatant(1, Side.Allies, 10));
            session.Enemies.Add(new FakeCombatant(100, Side.Enemies, 0));
            CombatResult result = null;
            entry.OnCombatEnded += value => result = value;

            FinishEntry(entry, CombatEndReason.Victory);

            Assert.That(result.CompletionId, Is.EqualTo(session.CompletionId));
            CollectionAssert.AreEqual(new[] { 100 }, result.DefeatedEnemyIds);
            CollectionAssert.AreEqual(new[] { 1 }, result.SurvivedAllyIds);
        }

        [Test]
        public void VictoryRequest_UsesCompletionId()
        {
            CombatResult result = CreateResult(CombatEndReason.Victory);
            Assert.That(RewardService.CreateCombatRewardRequest(result, "ignored").SourceId, Is.EqualTo(result.CompletionId));
        }

        [Test]
        public void VictoryRequest_PreservesGoldAndExp()
        {
            CombatResult result = CreateResult(CombatEndReason.Victory, gold: 50, exp: 150);
            RewardGrantRequest request = RewardService.CreateCombatRewardRequest(result, null);

            Assert.That(request.Gold, Is.EqualTo(50));
            Assert.That(request.Exp, Is.EqualTo(150));
        }

        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        public void NonVictoryRequest_HasNoVictoryReward(CombatEndReason reason)
        {
            CombatResult result = CreateResult(reason, gold: 50, exp: 150);
            RewardGrantRequest request = RewardService.CreateCombatRewardRequest(result, null);

            Assert.That(request.Gold, Is.Zero);
            Assert.That(request.Exp, Is.Zero);
        }

        [Test]
        public void NegativeRewardAmounts_ClampInRewardService()
        {
            RewardService service = CreateRewardService(null, null);
            RewardGrantResult result = service.GrantReward(new RewardGrantRequest(
                RewardSourceType.Combat, "negative", -10, -20, "item", -3));

            Assert.That(result.Gold, Is.Zero);
            Assert.That(result.Exp, Is.Zero);
            Assert.That(result.ItemCount, Is.Zero);
        }

        [Test]
        public void BinderAndService_UseSameCanonicalSourceId()
        {
            BinderFixture fixture = CreateBinderFixture();
            CombatResult result = CreateResult(CombatEndReason.Victory);

            Publish(fixture.Entry, result);
            RewardGrantResult duplicate = fixture.Service.GrantReward(RewardService.CreateCombatRewardRequest(result, null));

            Assert.That(fixture.Binder.ActiveCompletionId, Is.EqualTo(result.CompletionId));
            Assert.That(duplicate.SourceId, Is.EqualTo(result.CompletionId));
            Assert.That(duplicate.DuplicateBlocked, Is.True);
        }

        [Test]
        public void FirstCombatRequest_AppliesGoldOnce()
        {
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet, null);

            service.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "gold", gold: 25));

            Assert.That(wallet.Gold, Is.EqualTo(25));
        }

        [Test]
        public void DuplicateCombatRequest_DoesNotApplySecondGold()
        {
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet, null);
            RewardGrantRequest request = new(RewardSourceType.Combat, "gold-duplicate", gold: 25);

            service.GrantReward(request);
            service.GrantReward(request);

            Assert.That(wallet.Gold, Is.EqualTo(25));
        }

        [Test]
        public void FirstCombatItemRequest_AppliesItemOnce()
        {
            InventoryService inventory = CreateInventory();
            RewardService service = CreateRewardService(null, inventory);

            service.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "item", itemId: "potion", itemCount: 2));

            Assert.That(inventory.GetCount("potion"), Is.EqualTo(2));
        }

        [Test]
        public void DuplicateCombatItemRequest_DoesNotApplySecondItem()
        {
            InventoryService inventory = CreateInventory();
            RewardService service = CreateRewardService(null, inventory);
            RewardGrantRequest request = new(RewardSourceType.Combat, "item-duplicate", itemId: "potion", itemCount: 2);

            service.GrantReward(request);
            service.GrantReward(request);

            Assert.That(inventory.GetCount("potion"), Is.EqualTo(2));
        }

        [Test]
        public void DuplicateResponse_IsMarkedBlocked()
        {
            RewardService service = CreateRewardService(CreateWallet(), null);
            RewardGrantRequest request = new(RewardSourceType.Combat, "blocked", gold: 1);
            service.GrantReward(request);

            Assert.That(service.GrantReward(request).DuplicateBlocked, Is.True);
        }

        [Test]
        public void DuplicateResponse_RetainsOriginalDiagnostics()
        {
            RewardService service = CreateRewardService(CreateWallet(), CreateInventory());
            RewardGrantRequest request = new(RewardSourceType.Combat, "diagnostic", 7, 3, "potion", 2);
            RewardGrantResult first = service.GrantReward(request);
            RewardGrantResult duplicate = service.GrantReward(request);

            Assert.That(duplicate.Gold, Is.EqualTo(first.Gold));
            Assert.That(duplicate.Exp, Is.EqualTo(first.Exp));
            Assert.That(duplicate.ItemId, Is.EqualTo(first.ItemId));
            Assert.That(duplicate.ItemCount, Is.EqualTo(first.ItemCount));
        }

        [Test]
        public void DifferentCombatSourceIds_GrantIndependently()
        {
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet, null);

            service.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "one", gold: 5));
            service.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "two", gold: 5));

            Assert.That(wallet.Gold, Is.EqualTo(10));
            Assert.That(service.CombatGrantLedgerCount, Is.EqualTo(2));
        }

        [Test]
        public void EmptyCombatRequest_IsSafeAndConsumed()
        {
            RewardService service = CreateRewardService(null, null);
            RewardGrantRequest request = new(RewardSourceType.Combat, null);

            RewardGrantResult first = service.GrantReward(request);
            RewardGrantResult duplicate = service.GrantReward(request);

            Assert.That(first.HasAnyReward, Is.False);
            Assert.That(first.SourceId, Is.EqualTo("combat:legacy-empty"));
            Assert.That(duplicate.DuplicateBlocked, Is.True);
        }

        [Test]
        public void MissingCurrencyWallet_ReportsZeroAppliedGold()
        {
            RewardService service = CreateRewardService(null, null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("CurrencyWallet is missing"));

            RewardGrantResult result = service.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "no-wallet", gold: 10));

            Assert.That(result.Gold, Is.Zero);
        }

        [Test]
        public void MissingInventoryService_ReportsZeroAppliedItems()
        {
            RewardService service = CreateRewardService(null, null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InventoryService is missing"));

            RewardGrantResult result = service.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "no-inventory", itemId: "potion", itemCount: 1));

            Assert.That(result.ItemCount, Is.Zero);
        }

        [Test]
        public void PartialCombatAttempt_IsConsumedWithoutRetry()
        {
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet, null);
            RewardGrantRequest request = new(RewardSourceType.Combat, "partial", 10, 0, "potion", 1);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InventoryService is missing"));

            RewardGrantResult first = service.GrantReward(request);
            RewardGrantResult duplicate = service.GrantReward(request);

            Assert.That(wallet.Gold, Is.EqualTo(10));
            Assert.That(first.Gold, Is.EqualTo(10));
            Assert.That(first.ItemCount, Is.Zero);
            Assert.That(duplicate.DuplicateBlocked, Is.True);
        }

        [Test]
        public void QuestAndMissionCompatibilityApis_StillGrant()
        {
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet, null);

            service.GrantQuestCompletion("quest", 3, 0);
            service.GrantMissionCompletion("mission", 4, 0);

            Assert.That(wallet.Gold, Is.EqualTo(7));
        }

        [Test]
        public void BinderOneResult_CallsRewardServiceOnce()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Service.CombatGrantLedgerCount, Is.EqualTo(1));
            Assert.That(fixture.Binder.ProcessedCompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void BinderOneResult_ShowsPanelOnce()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Binder.PanelShowCount, Is.EqualTo(1));
            Assert.That(fixture.Panel.IsOpen, Is.True);
        }

        [Test]
        public void BinderOneResult_RequestsRewardStateOnce()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Binder.RewardStateRequestCount, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Current, Is.EqualTo(GameState.Reward));
        }

        [Test]
        public void BinderDuplicateSameResult_IsIgnored()
        {
            BinderFixture fixture = CreateBinderFixture();
            CombatResult result = CreateResult(CombatEndReason.Victory);

            Publish(fixture.Entry, result);
            Publish(fixture.Entry, result);

            Assert.That(fixture.Binder.ProcessedCompletionCount, Is.EqualTo(1));
            Assert.That(fixture.Binder.PanelShowCount, Is.EqualTo(1));
        }

        [Test]
        public void BinderDifferentResultWhileAwaitingClose_IsRejected()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory, completionId: "first"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Completion rejected while another reward is awaiting close"));

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory, completionId: "second"));

            Assert.That(fixture.Binder.ProcessedCompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void BinderClose_RestoresExplorationOnce()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            fixture.CloseButton.onClick.Invoke();

            Assert.That(fixture.StateMachine.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(fixture.Binder.CloseCompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void BinderDuplicateClose_IsIgnored()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            fixture.CloseButton.onClick.Invoke();
            fixture.CloseButton.onClick.Invoke();

            Assert.That(fixture.Binder.CloseCompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void RapidCloseButton_RaisesPanelClosedOnce()
        {
            RewardUIPanel panel = CreatePanel(out Button closeButton, out _);
            int closed = 0;
            panel.OnClosed += () => closed++;
            panel.Show(CreateResult(CombatEndReason.Victory));

            closeButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.That(closed, Is.EqualTo(1));
        }

        [Test]
        public void BinderEnableDisableCycles_DoNotDuplicateEntrySubscription()
        {
            BinderFixture fixture = CreateBinderFixture();
            Invoke(fixture.Binder, "OnDisable");
            Invoke(fixture.Binder, "OnEnable");
            Invoke(fixture.Binder, "OnEnable");

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Binder.ProcessedCompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void BinderReenableWhileAwaiting_DoesNotRegrant()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));
            int ledgerCount = fixture.Service.CombatGrantLedgerCount;

            Invoke(fixture.Binder, "OnDisable");
            Invoke(fixture.Binder, "OnEnable");

            Assert.That(fixture.Service.CombatGrantLedgerCount, Is.EqualTo(ledgerCount));
        }

        [Test]
        public void BinderReenableWhileAwaiting_DoesNotReenterReward()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));
            int requestCount = fixture.Binder.RewardStateRequestCount;

            Invoke(fixture.Binder, "OnDisable");
            Invoke(fixture.Binder, "OnEnable");

            Assert.That(fixture.Binder.RewardStateRequestCount, Is.EqualTo(requestCount));
        }

        [Test]
        public void BinderAfterCompletion_CanProcessNewCombat()
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory, completionId: "one"));
            fixture.CloseButton.onClick.Invoke();

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory, completionId: "two"));

            Assert.That(fixture.Binder.ProcessedCompletionCount, Is.EqualTo(2));
            Assert.That(fixture.Panel.IsOpen, Is.True);
        }

        [Test]
        public void DuplicateBinders_ProduceOneOwner()
        {
            BinderFixture fixture = CreateBinderFixture();
            CombatRewardUIBinder duplicate = CreateBinder(fixture.Entry, fixture.Panel, fixture.Service, enable: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate completion owner blocked"));
            Invoke(duplicate, "OnEnable");

            Assert.That(fixture.Binder.OwnsEntryPoint, Is.True);
            Assert.That(duplicate.OwnsEntryPoint, Is.False);
        }

        [Test]
        public void DuplicateBinder_DoesNotGrant()
        {
            BinderFixture fixture = CreateFixtureWithDuplicate(out CombatRewardUIBinder duplicate);
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Service.CombatGrantLedgerCount, Is.EqualTo(1));
            Assert.That(duplicate.ProcessedCompletionCount, Is.Zero);
        }

        [Test]
        public void DuplicateBinder_DoesNotShow()
        {
            BinderFixture fixture = CreateFixtureWithDuplicate(out CombatRewardUIBinder duplicate);
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Binder.PanelShowCount, Is.EqualTo(1));
            Assert.That(duplicate.PanelShowCount, Is.Zero);
        }

        [Test]
        public void DuplicateBinder_DoesNotRequestState()
        {
            BinderFixture fixture = CreateFixtureWithDuplicate(out CombatRewardUIBinder duplicate);
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Binder.RewardStateRequestCount, Is.EqualTo(1));
            Assert.That(duplicate.RewardStateRequestCount, Is.Zero);
        }

        [Test]
        public void BinderOwnership_ReleasesWhenOwnerDisabled()
        {
            BinderFixture fixture = CreateBinderFixture();
            CombatRewardUIBinder replacement = CreateBinder(fixture.Entry, fixture.Panel, fixture.Service, enable: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate completion owner blocked"));
            Invoke(replacement, "OnEnable");

            Invoke(fixture.Binder, "OnDisable");
            Invoke(replacement, "OnEnable");

            Assert.That(replacement.OwnsEntryPoint, Is.True);
        }

        [Test]
        public void DestroyedBinderRegistryEntry_IsRemovedSafely()
        {
            BinderFixture fixture = CreateBinderFixture();
            CombatRewardUIBinder replacement = CreateBinder(fixture.Entry, fixture.Panel, fixture.Service, enable: false);

            UnityEngine.Object.DestroyImmediate(fixture.Binder.gameObject);
            Invoke(replacement, "OnEnable");

            Assert.That(replacement.OwnsEntryPoint, Is.True);
        }

        [Test]
        public void MissingRewardService_StillShowsResult()
        {
            BinderFixture fixture = CreateBinderFixture(createService: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RewardService is missing"));

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Panel.IsOpen, Is.True);
        }

        [Test]
        public void MissingRewardService_StillClosesToExploration()
        {
            BinderFixture fixture = CreateBinderFixture(createService: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RewardService is missing"));
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            fixture.CloseButton.onClick.Invoke();

            Assert.That(fixture.StateMachine.Current, Is.EqualTo(GameState.Exploration));
        }

        [Test]
        public void MissingRewardPanel_DoesNotLeaveRewardStuck()
        {
            BinderFixture fixture = CreateBinderFixture(createPanel: false, enableBinder: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RewardUIPanel is missing"));
            Invoke(fixture.Binder, "Awake");
            Invoke(fixture.Binder, "OnEnable");

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.StateMachine.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(fixture.Binder.CloseCompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingRewardPanel_StillProcessesGrantOnce()
        {
            BinderFixture fixture = CreateBinderFixture(createPanel: false, enableBinder: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RewardUIPanel is missing"));
            Invoke(fixture.Binder, "Awake");
            Invoke(fixture.Binder, "OnEnable");

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(fixture.Service.CombatGrantLedgerCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingGameFlowController_DoesNotThrow()
        {
            BinderFixture fixture = CreateBinderFixture(createFlow: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("GameFlowController is missing"));

            Assert.DoesNotThrow(() => Publish(fixture.Entry, CreateResult(CombatEndReason.Victory)));
            Assert.That(fixture.Panel.IsOpen, Is.True);
        }

        [Test]
        public void MissingEntryPoint_WarnsOnce()
        {
            RewardUIPanel panel = CreatePanel(out _, out _);
            RewardService service = CreateRewardService(CreateWallet(), CreateInventory());
            CombatRewardUIBinder binder = CreateBinder(null, panel, service, enable: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("CombatEntryPoint is missing"));

            Invoke(binder, "Awake");
            Invoke(binder, "OnEnable");
            Invoke(binder, "OnEnable");
        }

        [Test]
        public void PanelShowThenClose_InvokesClosedOnce()
        {
            RewardUIPanel panel = CreatePanel(out Button close, out _);
            int count = 0;
            panel.OnClosed += () => count++;
            panel.Show(CreateResult(CombatEndReason.Victory));

            close.onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
            Assert.That(panel.IsOpen, Is.False);
        }

        [Test]
        public void PanelDoubleClose_InvokesClosedOnce()
        {
            RewardUIPanel panel = CreatePanel(out Button close, out _);
            int count = 0;
            panel.OnClosed += () => count++;
            panel.Show(CreateResult(CombatEndReason.Victory));

            close.onClick.Invoke();
            close.onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void PanelHide_IsIdempotentAndDoesNotClose()
        {
            RewardUIPanel panel = CreatePanel(out Button close, out _);
            int count = 0;
            panel.OnClosed += () => count++;
            panel.Show(CreateResult(CombatEndReason.Victory));

            panel.Hide();
            panel.Hide();
            close.onClick.Invoke();

            Assert.That(count, Is.Zero);
        }

        [Test]
        public void PanelSecondShowAfterCompletion_CanCloseAgain()
        {
            RewardUIPanel panel = CreatePanel(out Button close, out _);
            int count = 0;
            panel.OnClosed += () => count++;
            panel.Show(CreateResult(CombatEndReason.Victory, completionId: "one"));
            close.onClick.Invoke();

            panel.Show(CreateResult(CombatEndReason.Victory, completionId: "two"));
            close.onClick.Invoke();

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void FieldRewardMessage_IsRejectedDuringCombatResult()
        {
            RewardUIPanel panel = CreatePanel(out _, out _);
            panel.Show(CreateResult(CombatEndReason.Victory));

            Assert.That(panel.TryShowFieldRewardMessage("field"), Is.False);
        }

        [Test]
        public void FieldRewardTimeout_CannotHideCombatResult()
        {
            RewardUIPanel panel = CreatePanel(out _, out GameObject root, includeSimpleMessage: true);
            IEnumerator fieldRoutine = (IEnumerator)InvokeResult(panel, "Co_ShowFieldRewardMessage", "field");
            Assert.That(fieldRoutine.MoveNext(), Is.True);
            panel.Show(CreateResult(CombatEndReason.Victory));

            fieldRoutine.MoveNext();

            Assert.That(root.activeSelf, Is.True);
            Assert.That(panel.HasActiveCombatResult, Is.True);
        }

        [Test]
        public void CombatResult_SupersedesFieldRewardMessage()
        {
            RewardUIPanel panel = CreatePanel(out _, out _, includeSimpleMessage: true);
            Text message = GetField<Text>(panel, "simpleMessageText");
            IEnumerator fieldRoutine = (IEnumerator)InvokeResult(panel, "Co_ShowFieldRewardMessage", "field");
            fieldRoutine.MoveNext();
            Assert.That(message.text, Is.EqualTo("field"));

            panel.Show(CreateResult(CombatEndReason.Victory));

            Assert.That(message.text, Is.Empty);
            Assert.That(panel.HasActiveCombatResult, Is.True);
        }

        [Test]
        public void FieldRewardMessage_WorksAgainAfterCombatClose()
        {
            RewardUIPanel panel = CreatePanel(out Button close, out _, includeSimpleMessage: true);
            panel.Show(CreateResult(CombatEndReason.Victory));
            close.onClick.Invoke();

            Assert.That(panel.TryShowFieldRewardMessage("field-after"), Is.True);
        }

        [Test]
        public void PanelEnableDisable_DoesNotDuplicateCloseListener()
        {
            RewardUIPanel panel = CreatePanel(out Button close, out _);
            int count = 0;
            panel.OnClosed += () => count++;
            Invoke(panel, "OnEnable");
            Invoke(panel, "OnDisable");
            Invoke(panel, "OnEnable");
            panel.Show(CreateResult(CombatEndReason.Victory));

            close.onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void PanelPresentation_DoesNotActivateCombatOrFieldRoots()
        {
            RewardUIPanel panel = CreatePanel(out _, out _);
            GameObject combat = CreateGameObject("CombatGlobal");
            GameObject field = CreateGameObject("FieldGlobal");
            combat.SetActive(false);
            field.SetActive(false);

            panel.Show(CreateResult(CombatEndReason.Victory));

            Assert.That(combat.activeSelf, Is.False);
            Assert.That(field.activeSelf, Is.False);
        }

        [Test]
        public void PanelContent_IsPreparedBeforeRewardStateEvent()
        {
            BinderFixture fixture = CreateBinderFixture();
            bool openWhenRewardEntered = false;
            fixture.StateMachine.OnStateChanged += (_, next) =>
            {
                if (next == GameState.Reward)
                    openWhenRewardEntered = fixture.Panel.IsOpen;
            };

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(openWhenRewardEntered, Is.True);
        }

        [TestCase(CombatEndReason.Victory, true)]
        [TestCase(CombatEndReason.Defeat, false)]
        [TestCase(CombatEndReason.Escape, false)]
        [TestCase(CombatEndReason.Abort, false)]
        public void Outcomes_PresentAndGrantOnlyVictory(CombatEndReason reason, bool grants)
        {
            BinderFixture fixture = CreateBinderFixture();
            Publish(fixture.Entry, CreateResult(reason, gold: 10));

            Assert.That(fixture.Panel.IsOpen, Is.True);
            Assert.That(fixture.Wallet.Gold, Is.EqualTo(grants ? 10 : 0));
        }

        [TestCase(CombatEndReason.Victory)]
        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        public void DefaultOutcomes_CloseToExplorationOnce(CombatEndReason reason)
        {
            BinderFixture fixture = CreateBinderFixture();
            int explorationTransitions = 0;
            fixture.StateMachine.OnStateChanged += (_, next) =>
            {
                if (next == GameState.Exploration)
                    explorationTransitions++;
            };
            Publish(fixture.Entry, CreateResult(reason));

            fixture.CloseButton.onClick.Invoke();
            fixture.CloseButton.onClick.Invoke();

            Assert.That(fixture.StateMachine.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(explorationTransitions, Is.EqualTo(1));
        }

        [Test]
        public void RestoreExplorationFalse_DoesNotInventDestination()
        {
            BinderFixture fixture = CreateBinderFixture();
            SetField(fixture.Binder, "restoreExplorationAfterRewardClosed", false);
            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            fixture.CloseButton.onClick.Invoke();

            Assert.That(fixture.StateMachine.Current, Is.EqualTo(GameState.Reward));
            Assert.That(fixture.Binder.Lifecycle, Is.EqualTo(CombatRewardUIBinder.CompletionLifecycle.Completed));
        }

        [Test]
        public void GameFlowRewardEntry_IsIdempotent()
        {
            GameStateMachine state = CreateStateMachine();
            GameFlowController flow = CreateFlow();
            CombatResult result = CreateResult(CombatEndReason.Victory);

            Assert.That(flow.TryHandleCombatResult(result), Is.True);
            Assert.That(flow.TryHandleCombatResult(result), Is.True);
            Assert.That(state.Current, Is.EqualTo(GameState.Reward));
        }

        [Test]
        public void GameFlowRewardCloseFromUnrelatedState_IsRejected()
        {
            CreateStateMachine();
            GameFlowController flow = CreateFlow();

            Assert.That(flow.TryHandleRewardClosed(), Is.False);
        }

        [Test]
        public void DemoMissionVictory_CountsOneUniqueEnemy()
        {
            DemoMissionRuntime mission = CreateDemoMission();
            BinderFixture fixture = CreateBinderFixture();
            CombatResult result = CreateResult(CombatEndReason.Victory);
            result.DefeatedEnemyIds.Add(100);

            Publish(fixture.Entry, result);

            Assert.That(mission.EnemyDefeatCount, Is.EqualTo(1));
        }

        [Test]
        public void DemoMissionDuplicateResult_DoesNotCountTwice()
        {
            DemoMissionRuntime mission = CreateDemoMission();
            BinderFixture fixture = CreateBinderFixture();
            CombatResult result = CreateResult(CombatEndReason.Victory);
            result.DefeatedEnemyIds.Add(100);

            Publish(fixture.Entry, result);
            Publish(fixture.Entry, result);

            Assert.That(mission.EnemyDefeatCount, Is.EqualTo(1));
        }

        [Test]
        public void DemoMissionMultiEnemyVictory_CountsEveryUniqueEnemy()
        {
            DemoMissionRuntime mission = CreateDemoMission();
            BinderFixture fixture = CreateBinderFixture();
            CombatResult result = CreateResult(CombatEndReason.Victory);
            result.DefeatedEnemyIds.AddRange(new[] { 100, 101, 100, 102 });

            Publish(fixture.Entry, result);

            Assert.That(mission.EnemyDefeatCount, Is.EqualTo(3));
        }

        [Test]
        public void DemoMissionDefeat_DoesNotRegisterEnemyDefeat()
        {
            DemoMissionRuntime mission = CreateDemoMission();
            BinderFixture fixture = CreateBinderFixture();
            CombatResult result = CreateResult(CombatEndReason.Defeat);
            result.DefeatedEnemyIds.Add(100);

            Publish(fixture.Entry, result);

            Assert.That(mission.EnemyDefeatCount, Is.Zero);
        }

        [Test]
        public void DemoMissionEmptyDefeatedList_DoesNotInventCount()
        {
            DemoMissionRuntime mission = CreateDemoMission();
            BinderFixture fixture = CreateBinderFixture();

            Publish(fixture.Entry, CreateResult(CombatEndReason.Victory));

            Assert.That(mission.EnemyDefeatCount, Is.Zero);
        }

        private BinderFixture CreateFixtureWithDuplicate(out CombatRewardUIBinder duplicate)
        {
            BinderFixture fixture = CreateBinderFixture();
            duplicate = CreateBinder(fixture.Entry, fixture.Panel, fixture.Service, enable: false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate completion owner blocked"));
            Invoke(duplicate, "OnEnable");
            return fixture;
        }

        private BinderFixture CreateBinderFixture(
            bool createService = true,
            bool createPanel = true,
            bool createFlow = true,
            bool enableBinder = true)
        {
            GameStateMachine state = CreateStateMachine();
            GameFlowController flow = createFlow ? CreateFlow() : null;
            CurrencyWallet wallet = createService ? CreateWallet() : null;
            InventoryService inventory = createService ? CreateInventory() : null;
            RewardService service = createService ? CreateRewardService(wallet, inventory) : null;
            Button close = null;
            RewardUIPanel panel = createPanel ? CreatePanel(out close, out _) : null;
            CombatEntryPoint entry = CreateComponent<CombatEntryPoint>("Entry");
            CombatRewardUIBinder binder = CreateBinder(entry, panel, service, enableBinder);

            return new BinderFixture(state, flow, wallet, inventory, service, panel, close, entry, binder);
        }

        private CombatRewardUIBinder CreateBinder(
            CombatEntryPoint entry,
            RewardUIPanel panel,
            RewardService service,
            bool enable)
        {
            CombatRewardUIBinder binder = CreateComponent<CombatRewardUIBinder>("CombatRewardUIBinder");
            SetField(binder, "entryPoint", entry);
            SetField(binder, "rewardPanel", panel);
            SetField(binder, "rewardService", service);
            if (enable)
            {
                Invoke(binder, "Awake");
                Invoke(binder, "OnEnable");
            }

            return binder;
        }

        private RewardUIPanel CreatePanel(out Button closeButton, out GameObject root, bool includeSimpleMessage = false)
        {
            RewardUIPanel panel = CreateComponent<RewardUIPanel>("RewardPanel");
            root = CreateGameObject("RewardContent");
            RectTransform rows = CreateGameObject("RewardRows", typeof(RectTransform)).GetComponent<RectTransform>();
            closeButton = CreateGameObject("CloseButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            SetField(panel, "root", root);
            SetField(panel, "rewardRowRoot", rows);
            SetField(panel, "closeButton", closeButton);
            if (includeSimpleMessage)
            {
                Text message = CreateGameObject("FieldMessage", typeof(RectTransform), typeof(Text))
                    .GetComponent<Text>();
                SetField(panel, "simpleMessageText", message);
            }

            return panel;
        }

        private RewardService CreateRewardService(CurrencyWallet wallet, InventoryService inventory)
        {
            RewardService service = CreateComponent<RewardService>("RewardService");
            SetField(service, "currencyWallet", wallet);
            SetField(service, "inventoryService", inventory);
            Invoke(service, "Awake");
            return service;
        }

        private CurrencyWallet CreateWallet()
        {
            CurrencyWallet wallet = CreateComponent<CurrencyWallet>("CurrencyWallet");
            Invoke(wallet, "Awake");
            wallet.SetGold(0);
            return wallet;
        }

        private InventoryService CreateInventory()
        {
            InventoryService inventory = CreateComponent<InventoryService>("InventoryService");
            Invoke(inventory, "Awake");
            return inventory;
        }

        private GameStateMachine CreateStateMachine()
        {
            GameStateMachine state = CreateComponent<GameStateMachine>("GameStateMachine");
            Invoke(state, "Awake");
            return state;
        }

        private GameFlowController CreateFlow()
        {
            GameFlowController flow = CreateComponent<GameFlowController>("GameFlowController");
            Invoke(flow, "Awake");
            return flow;
        }

        private DemoMissionRuntime CreateDemoMission()
        {
            DemoMissionDefinitionSO definition = ScriptableObject.CreateInstance<DemoMissionDefinitionSO>();
            definition.requiredEnemyKills = 20;
            definition.missionId = "batch6-test";
            _objects.Add(definition);

            DemoMissionRuntime runtime = CreateComponent<DemoMissionRuntime>("DemoMissionRuntime");
            SetField(runtime, "dontDestroyOnLoad", false);
            SetField(runtime, "bridgeToQuestRuntime", false);
            Invoke(runtime, "Awake");
            runtime.SetCurrentMission(definition);
            return runtime;
        }

        private CombatEntryPoint CreateEntryWithSession(out CombatSession session)
        {
            CombatEntryPoint entry = CreateComponent<CombatEntryPoint>("CompletionEntry");
            session = CreateSession();
            SetAutoProperty(entry, "ActiveSession", session);
            SetAutoProperty(entry, "ActiveStateMachine", new CombatStateMachine(session));
            return entry;
        }

        private static void FinishEntry(CombatEntryPoint entry, CombatEndReason reason)
        {
            Invoke(entry, "FinishCombat", reason);
        }

        private static void Publish(CombatEntryPoint entry, CombatResult result)
        {
            Invoke(entry, "RaiseCombatEnded", result);
        }

        private static CombatSession CreateSession()
        {
            return new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 0),
                new CombatEnvironment());
        }

        private static CombatResult CreateResult(
            CombatEndReason reason,
            int gold = 10,
            int exp = 0,
            string completionId = null)
        {
            return new CombatResult
            {
                CompletionId = completionId ?? Guid.NewGuid().ToString("N"),
                EndReason = reason,
                IsWin = reason == CombatEndReason.Victory,
                EscapeSucceeded = reason == CombatEndReason.Escape,
                TotalGold = gold,
                TotalExp = exp
            };
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            return CreateGameObject(name).AddComponent<T>();
        }

        private GameObject CreateGameObject(string name, params Type[] components)
        {
            GameObject gameObject = components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static object InvokeResult(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static void InvokeIfPresent(object target, string methodName)
        {
            target?.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<CombatRewardUIBinder>();
            DestroyAll<RewardUIPanel>();
            DestroyAll<RewardService>();
            DestroyAll<CurrencyWallet>();
            DestroyAll<InventoryService>();
            DestroyAll<CombatEntryPoint>();
            DestroyAll<GameFlowController>();
            DestroyAll<GameStateMachine>();
            DestroyAll<DemoMissionRuntime>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    UnityEngine.Object.DestroyImmediate(components[i].gameObject);
            }
        }

        private readonly struct BinderFixture
        {
            public readonly GameStateMachine StateMachine;
            public readonly GameFlowController Flow;
            public readonly CurrencyWallet Wallet;
            public readonly InventoryService Inventory;
            public readonly RewardService Service;
            public readonly RewardUIPanel Panel;
            public readonly Button CloseButton;
            public readonly CombatEntryPoint Entry;
            public readonly CombatRewardUIBinder Binder;

            public BinderFixture(
                GameStateMachine stateMachine,
                GameFlowController flow,
                CurrencyWallet wallet,
                InventoryService inventory,
                RewardService service,
                RewardUIPanel panel,
                Button closeButton,
                CombatEntryPoint entry,
                CombatRewardUIBinder binder)
            {
                StateMachine = stateMachine;
                Flow = flow;
                Wallet = wallet;
                Inventory = inventory;
                Service = service;
                Panel = panel;
                CloseButton = closeButton;
                Entry = entry;
                Binder = binder;
            }
        }

        private sealed class FakeCombatant : ICombatant
        {
            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP { get; private set; }
            public int MaxHP { get; } = 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger { get; private set; }
            public int StaggerMax => 10;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills { get; } = Array.Empty<ISkill>();

            public FakeCombatant(int id, Side side, int hp)
            {
                Id = new CombatantId(id);
                Side = side;
                HP = hp;
            }

            public void ApplyDamage(int amount) => HP = Mathf.Max(0, HP - amount);
            public void AddStagger(int amount) => Stagger += amount;
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() => Stagger = 0;
        }
    }
}
#endif
