#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Combat
{
    public sealed class CombatPartyRosterPlayModeIntegrationTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private readonly List<string> _runtimeErrors = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);

            _created.Clear();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ExplicitPartyRoster_MapsOnlySelectedMembersIntoFinalExchangeRequestAndSession(int selectedCount)
        {
            RosterFixture fixture = CreateFixture(selectedCount);
            CombatStartRequest request = fixture.CreateEncounterRequest();

            Assert.That(request, Is.Not.Null);
            Assert.That(request.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain));
            Assert.That(request.AllyFieldObjects, Has.Count.EqualTo(selectedCount));
            for (int i = 0; i < selectedCount; i++)
            {
                Assert.That(request.AllyFieldObjects[i], Is.SameAs(fixture.Allies[i]));
                Assert.That(request.TryGetAllyCharacterId(fixture.Allies[i], out string characterId), Is.True);
                Assert.That(characterId, Is.EqualTo(fixture.CharacterIds[i]));
            }

            if (selectedCount < fixture.Allies.Count)
                Assert.That(request.AllyFieldObjects.Contains(fixture.Allies[selectedCount]), Is.False);

            SkillBook book = CreateSkillBook();
            CombatRuntimeConfig config = new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f);
            CombatSession session = CreateFinalExchangeSession(request, book, config, out CombatStateMachine machine);

            Assert.That(machine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(session.Allies, Has.Count.EqualTo(selectedCount));
            for (int i = 0; i < selectedCount; i++)
            {
                FieldCombatantAdapter actor = session.Allies[i] as FieldCombatantAdapter;
                Assert.That(actor, Is.Not.Null);
                Assert.That(actor.FieldObject, Is.SameAs(fixture.Allies[i]));
                Assert.That(actor.Id.Value, Is.EqualTo(i + 1));
                Assert.That(actor.HP, Is.EqualTo(10 + i));
                Assert.That(actor.Skills, Is.Not.Empty);
                Assert.That(session.TryGetAllyCharacterId(actor, out string characterId), Is.True);
                Assert.That(characterId, Is.EqualTo(fixture.CharacterIds[i]));
            }
        }

        [Test]
        public void Adapter_RejectsTwoSelectedCharactersBoundToTheSameFieldObject()
        {
            RosterFixture fixture = CreateFixture(2);
            PartyCombatBuildResult result = new CharacterPartyCombatAdapter().BuildRequest(
                fixture.Party,
                fixture.Progression,
                new[]
                {
                    new PartyCombatantBinding(fixture.CharacterIds[0], fixture.Allies[0]),
                    new PartyCombatantBinding(fixture.CharacterIds[1], fixture.Allies[0])
                },
                new[] { fixture.Enemy },
                StartReason.PlayerFirstHit,
                Side.Allies,
                0,
                -1,
                null,
                CombatFlowMode.StandoffClashChain);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Does.Contain("more than one Character ID"));
        }

        [Test]
        public void Adapter_RejectsEmptyOrHistoricallyOversizedCombatLineupsWithoutMutatingPartyData()
        {
            RosterFixture fixture = CreateFixture(0);
            PartyCombatBuildResult empty = new CharacterPartyCombatAdapter().BuildRequest(
                fixture.Party, fixture.Progression, fixture.Bindings, new[] { fixture.Enemy },
                StartReason.PlayerFirstHit, Side.Allies, 0, -1, null, CombatFlowMode.StandoffClashChain);
            Assert.That(empty.Success, Is.False);
            Assert.That(empty.FailureReason, Does.Contain("1 to 3"));

            ForceCombatLineup(fixture.Party, fixture.CharacterIds);
            PartyCombatBuildResult oversized = new CharacterPartyCombatAdapter().BuildRequest(
                fixture.Party, fixture.Progression, fixture.Bindings, new[] { fixture.Enemy },
                StartReason.PlayerFirstHit, Side.Allies, 0, -1, null, CombatFlowMode.StandoffClashChain);
            Assert.That(oversized.Success, Is.False);
            Assert.That(oversized.FailureReason, Does.Contain("1 to 3"));
            Assert.That(fixture.Party.CombatLineup, Is.EqualTo(fixture.CharacterIds));
        }

        [Test]
        public void Adapter_RejectsZeroHpAndMissingOrDuplicateCharacterBindings()
        {
            RosterFixture fixture = CreateFixture(1);
            fixture.Allies[0].GetComponent<CombatHpComponent>().HP = 0;
            PartyCombatBuildResult zeroHp = new CharacterPartyCombatAdapter().BuildRequest(
                fixture.Party, fixture.Progression, fixture.Bindings, new[] { fixture.Enemy },
                StartReason.PlayerFirstHit, Side.Allies, 0, -1, null, CombatFlowMode.StandoffClashChain);
            Assert.That(zeroHp.Success, Is.False);
            Assert.That(zeroHp.FailureReason, Does.Contain("invalid HP"));

            fixture.Allies[0].GetComponent<CombatHpComponent>().HP = 10;
            PartyCombatBuildResult missing = new CharacterPartyCombatAdapter().BuildRequest(
                fixture.Party, fixture.Progression, Array.Empty<PartyCombatantBinding>(), new[] { fixture.Enemy },
                StartReason.PlayerFirstHit, Side.Allies, 0, -1, null, CombatFlowMode.StandoffClashChain);
            Assert.That(missing.Success, Is.False);
            Assert.That(missing.FailureReason, Does.Contain("no explicit field binding"));

            PartyCombatBuildResult duplicate = new CharacterPartyCombatAdapter().BuildRequest(
                fixture.Party,
                fixture.Progression,
                new[]
                {
                    new PartyCombatantBinding(fixture.CharacterIds[0], fixture.Allies[0]),
                    new PartyCombatantBinding(fixture.CharacterIds[0], fixture.Allies[1])
                },
                new[] { fixture.Enemy },
                StartReason.PlayerFirstHit,
                Side.Allies,
                0,
                -1,
                null,
                CombatFlowMode.StandoffClashChain);
            Assert.That(duplicate.Success, Is.False);
            Assert.That(duplicate.FailureReason, Does.Contain("duplicate field bindings"));
        }

        [UnityTest]
        [Category("CBT_PARTY_01")]
        public IEnumerator PartyRoster_HandoffUsesCompanionMpAndDoesNotLeakIntoNextSession_InActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);

            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            try
            {
                RosterFixture fixture = CreateFixture(2);
                CombatStartRequest request = fixture.CreateEncounterRequest();
                SkillBook book = CreateSkillBook();
                CombatRuntimeConfig config = new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f);
                CombatSession session = CreateFinalExchangeSession(request, book, config, out CombatStateMachine machine);
                CombatFlowOrchestrator orchestrator = Track(new GameObject("CBT_PARTY_01.Orchestrator"))
                    .AddComponent<CombatFlowOrchestrator>();
                orchestrator.ApproachPresentationRequested += presentation => presentation.TryComplete();
                orchestrator.OutcomePresentationRequested += presentation => presentation.TryComplete();
                Assert.That(orchestrator.BindFinalExchange(
                    session,
                    machine,
                    new ZeroRandom(),
                    new AuthoredCombatClashSideInputProvider(session)), Is.True);

                ICombatant leader = session.Allies[0];
                ICombatant companion = session.Allies[1];
                ICombatant enemy = session.Enemies[0];
                ISkill leaderSkill = leader.Skills[0];
                ISkill companionSkill = companion.Skills[0];

                Assert.That(orchestrator.PendingDecisionRequest, Is.Not.Null);
                Assert.That(orchestrator.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
                Assert.That(orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
                Assert.That(orchestrator.PendingDecisionRequest.ActingActor, Is.Null);
                Assert.That(orchestrator.SubmitAttackDeclaration(
                    new CombatAttackDeclaration(leader, enemy, leaderSkill),
                    orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(orchestrator.SubmitNoResponse(orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(orchestrator.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Chain));

                int companionMpBefore = session.GetCombatState(companion).CurrentMp;
                Assert.That(orchestrator.Handoff(
                    companion,
                    companionSkill,
                    true,
                    orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(orchestrator.PendingDecisionRequest.ActingActor, Is.SameAs(companion));
                Assert.That(session.ExchangeState.HandoffCount, Is.EqualTo(1));
                Assert.That(orchestrator.Handoff(
                    leader,
                    leaderSkill,
                    true,
                    orchestrator.PendingDecisionRequest.ExchangeVersion), Is.False);

                Assert.That(orchestrator.SubmitAttackDeclaration(
                    new CombatAttackDeclaration(companion, enemy, companionSkill),
                    orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(orchestrator.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
                Assert.That(orchestrator.SubmitNoResponse(
                    orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                Assert.That(session.GetCombatState(companion).CurrentMp, Is.EqualTo(companionMpBefore - 1));
                Assert.That(machine.Phase, Is.EqualTo(Phase.ExitCombat));

                CombatSession nextSession = CreateFinalExchangeSession(request, book, config, out _);
                Assert.That(nextSession.Allies, Has.Count.EqualTo(2));
                Assert.That(nextSession.GetCombatState(nextSession.Allies[1]).CurrentMp, Is.EqualTo(3));
                Assert.That(nextSession.ExchangeState.HandoffCount, Is.Zero);
                Assert.That(nextSession.TryGetAllyCharacterId(nextSession.Allies[1], out string nextCompanionId), Is.True);
                Assert.That(nextCompanionId, Is.EqualTo(fixture.CharacterIds[1]));
                Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            }
            finally
            {
                Application.logMessageReceived -= CaptureRuntimeError;
            }

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PartyRoster_CombatResultCapturesEachCombatantHpAndSurvival(int selectedCount)
        {
            RosterFixture fixture = CreateFixture(selectedCount);
            CombatSession session = CreateFinalExchangeSession(
                fixture.CreateEncounterRequest(),
                CreateSkillBook(),
                new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f),
                out _);
            ICombatant enemy = session.Enemies[0];
            session.GetCombatState(enemy).ApplyDamage(enemy.HP);

            if (selectedCount > 1)
                session.GetCombatState(session.Allies[1]).ApplyDamage(session.Allies[1].HP);

            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);

            Assert.That(result.CompletionId, Is.EqualTo(session.CompletionId));
            Assert.That(result.DefeatedEnemyIds, Is.EquivalentTo(new[] { enemy.Id.Value }));
            Assert.That(result.RemainingHpByCombatantId[enemy.Id.Value], Is.Zero);
            Assert.That(result.SurvivedAllyIds, Does.Contain(session.Allies[0].Id.Value));
            Assert.That(result.RemainingHpByCombatantId[session.Allies[0].Id.Value], Is.EqualTo(10));
            Assert.That(result.AllyParticipants, Has.Count.EqualTo(selectedCount));
            Assert.That(result.AllyParticipants[0].CharacterId, Is.EqualTo(fixture.CharacterIds[0]));
            Assert.That(result.AllyParticipants[0].CombatantId, Is.EqualTo(session.Allies[0].Id.Value));
            Assert.That(result.AllyParticipants[0].RemainingHp, Is.EqualTo(10));
            Assert.That(result.AllyParticipants[0].IsAlive, Is.True);
            if (selectedCount > 1)
            {
                Assert.That(result.SurvivedAllyIds.Contains(session.Allies[1].Id.Value), Is.False);
                Assert.That(result.RemainingHpByCombatantId[session.Allies[1].Id.Value], Is.Zero);
                Assert.That(result.AllyParticipants[1].CharacterId, Is.EqualTo(fixture.CharacterIds[1]));
                Assert.That(result.AllyParticipants[1].IsAlive, Is.False);
            }
        }

        [Test]
        public void PartyRoster_AllAlliesDefeated_ProducesDefeatWithoutRewards()
        {
            RosterFixture fixture = CreateFixture(3);
            CombatSession session = CreateFinalExchangeSession(
                fixture.CreateEncounterRequest(),
                CreateSkillBook(),
                new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f),
                out _);
            for (int i = 0; i < session.Allies.Count; i++)
                session.GetCombatState(session.Allies[i]).ApplyDamage(session.Allies[i].HP);

            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Defeat);
            RewardGrantRequest reward = RewardService.CreateCombatRewardRequest(result, null);

            Assert.That(result.IsWin, Is.False);
            Assert.That(result.SurvivedAllyIds, Is.Empty);
            Assert.That(result.AllyParticipants, Has.Count.EqualTo(3));
            for (int i = 0; i < result.AllyParticipants.Count; i++)
                Assert.That(result.AllyParticipants[i].IsAlive, Is.False);
            Assert.That(reward.Gold, Is.Zero);
            Assert.That(reward.Exp, Is.Zero);
        }

        [Test]
        public void LegacySinglePlayerResultRemainsCompatibleWhenNoCharacterMappingWasProvided()
        {
            GameObject ally = CreateActor("CBT_PARTY_04.LegacyAlly", 10, 10, 1);
            GameObject enemy = CreateActor("CBT_PARTY_04.LegacyEnemy", 10, 10, 9);
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 0, -1, null, CombatFlowMode.StandoffClashChain);
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);
            CombatSession session = CreateFinalExchangeSession(
                request, CreateSkillBook(), new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f), out _);

            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Defeat);

            Assert.That(result.AllyParticipants, Is.Empty);
            Assert.That(result.RemainingHpByCombatantId[session.Allies[0].Id.Value], Is.EqualTo(10));
            Assert.That(result.SurvivedAllyIds, Is.EquivalentTo(new[] { session.Allies[0].Id.Value }));
        }

        [UnityTest]
        [Category("CBT_PARTY_02")]
        public IEnumerator PartyRoster_EntryResult_RewardAndQuestAreAppliedOnce_InActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);

            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            try
            {
                Track(new GameObject("CBT_PARTY_02.State")).AddComponent<GameStateMachine>();
                Track(new GameObject("CBT_PARTY_02.Flow")).AddComponent<GameFlowController>();
                RosterFixture fixture = CreateFixture(2);
                SetPrivate(fixture.Trigger, "flowMode", CombatFlowMode.LegacyPlanning);

                CombatEncounterGroup encounter = Track(new GameObject("CBT_PARTY_02.Encounter"))
                    .AddComponent<CombatEncounterGroup>();
                fixture.Enemy.transform.SetParent(encounter.transform);
                SetPrivate(fixture.Trigger, "encounterGroup", encounter);

                CurrencyWallet wallet = Track(new GameObject("CBT_PARTY_02.Wallet"))
                    .AddComponent<CurrencyWallet>();
                RewardService rewards = Track(new GameObject("CBT_PARTY_02.Rewards"))
                    .AddComponent<RewardService>();
                SetPrivate(rewards, "currencyWallet", wallet);
                SetPrivate(rewards, "characterProgressionService", fixture.Progression);
                SetPrivate(fixture.Progression, "defaultRewardTargetId", fixture.CharacterIds[0]);
                CurrencyWallet activeWallet = CurrencyWallet.Instance != null
                    ? CurrencyWallet.Instance
                    : wallet;
                activeWallet.SetGold(0);

                QuestRuntime quests = Track(new GameObject("CBT_PARTY_02.Quests"))
                    .AddComponent<QuestRuntime>();
                QuestDefinitionSO quest = CreateKillQuest("test.party.quest", "test.party.quest.kill");
                quests.StartQuest(quest);
                QuestObjectiveTracker tracker = Track(new GameObject("CBT_PARTY_02.Tracker"))
                    .AddComponent<QuestObjectiveTracker>();
                SetPrivate(tracker, "questRuntime", quests);

                CombatEntryPoint entry = Track(new GameObject("CBT_PARTY_02.Entry"))
                    .AddComponent<CombatEntryPoint>();
                CombatQuestObjectivePublisher publisher = CreateQuestPublisher(
                    entry,
                    encounter,
                    quest.QuestId,
                    quest.Objectives[0].ObjectiveId);
                CombatResult published = null;
                int publishCount = 0;
                entry.OnCombatEnded += result =>
                {
                    published = result;
                    publishCount++;
                    rewards.GrantCombatResult(result);
                };

                CombatStartRequest request = fixture.CreateEncounterRequest();
                Assert.That(request.AllyFieldObjects, Has.Count.EqualTo(2));
                Assert.That(entry.StartCombat(request), Is.True);
                Assert.That(entry.ActiveSession.Allies, Has.Count.EqualTo(2));

                CombatSession session = entry.ActiveSession;
                ICombatant companion = session.Allies[1];
                ICombatant enemy = session.Enemies[0];
                session.GetCombatState(companion).ApplyDamage(companion.HP);
                session.GetCombatState(enemy).ApplyDamage(enemy.HP);
                entry.ActiveStateMachine.ForceExit(CombatEndReason.Victory);
                yield return null;

                Assert.That(publishCount, Is.EqualTo(1));
                Assert.That(published, Is.Not.Null);
                Assert.That(published.CompletionId, Is.EqualTo(session.CompletionId));
                Assert.That(published.SurvivedAllyIds, Is.EquivalentTo(new[] { session.Allies[0].Id.Value }));
                Assert.That(published.RemainingHpByCombatantId[companion.Id.Value], Is.Zero);
                Assert.That(published.AllyParticipants, Has.Count.EqualTo(2));
                Assert.That(published.AllyParticipants[1].CharacterId, Is.EqualTo(fixture.CharacterIds[1]));
                Assert.That(published.AllyParticipants[1].IsAlive, Is.False);
                Assert.That(fixture.Allies[1].GetComponent<CombatHpComponent>().HP, Is.Zero);
                Assert.That(entry.ActiveSession, Is.Null);
                Assert.That(entry.ActiveStateMachine, Is.Null);
                Assert.That(activeWallet.Gold, Is.EqualTo(50));
                Assert.That(rewards.CombatGrantLedgerCount, Is.EqualTo(1));
                Assert.That(quests.GetObjectiveProgress(quest.QuestId, quest.Objectives[0].ObjectiveId), Is.EqualTo(1));

                Invoke(entry, "RaiseCombatEnded", published);
                Assert.That(publishCount, Is.EqualTo(2));
                Assert.That(activeWallet.Gold, Is.EqualTo(50));
                Assert.That(rewards.CombatGrantLedgerCount, Is.EqualTo(1));
                Assert.That(quests.GetObjectiveProgress(quest.QuestId, quest.Objectives[0].ObjectiveId), Is.EqualTo(1));
                Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            }
            finally
            {
                Application.logMessageReceived -= CaptureRuntimeError;
            }

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        [Test]
        public void PartyExp_EntryMappingSurvivesPartyAndSessionRosterEdits()
        {
            RosterFixture fixture = CreateFixture(3);
            CombatSession session = CreateFinalExchangeSession(fixture.CreateEncounterRequest(), CreateSkillBook(),
                new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f), out _);
            ICombatant companion = session.Allies[1];
            session.GetCombatState(companion).ApplyDamage(companion.HP);
            session.Allies.Remove(companion);
            ForceCombatLineup(fixture.Party, new[] { fixture.CharacterIds[3] });
            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);
            RewardGrantRequest reward = RewardService.CreateCombatRewardRequest(result, null);
            Assert.That(reward.ExperienceRecipientIds, Is.EquivalentTo(new[]
                { fixture.CharacterIds[0], fixture.CharacterIds[1], fixture.CharacterIds[2] }));
            CombatSession next = CreateFinalExchangeSession(fixture.CreateEncounterRequest(), CreateSkillBook(),
                new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f), out _);
            Assert.That(RewardService.CreateCombatRewardRequest(CombatResultBuilder.Build(next, CombatEndReason.Victory), null)
                .ExperienceRecipientIds, Is.EquivalentTo(new[] { fixture.CharacterIds[3] }));
            Assert.That(next.CompletionId, Is.Not.EqualTo(session.CompletionId));
        }

        [Test]
        public void PartyExp_UnmappedMultiAllyResultNeverUsesLegacyFallback()
        {
            RosterFixture fixture = CreateFixture(2);
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 0, -1, null);
            request.AllyFieldObjects.AddRange(fixture.Allies.GetRange(0, 2));
            request.EnemyFieldObjects.Add(fixture.Enemy);
            CombatSession session = CreateFinalExchangeSession(request, CreateSkillBook(),
                new CombatRuntimeConfig(4, 3, 0, 0, 0f, 0f, 0f), out _);
            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);
            Assert.That(result.AllyParticipants, Is.Empty);
            Assert.That(result.RequiresExplicitExperienceRecipients, Is.True);
            Assert.That(RewardService.CreateCombatRewardRequest(result, null).ExperienceRecipientIds, Is.Empty);
        }

        [UnityTest]
        [Category("CBT_PARTY_05")]
        public IEnumerator PartyExp_FinalExchangeEntryVictory_KoRewardQuestAndSaveReplay_InActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            // AutoBootstrap creates core singletons even in an empty Play Mode scene.
            // Reuse its reward/wallet owners and replace only the isolated roster fixture data.
            Assert.That(GameStateMachine.Instance, Is.Not.Null);
            Assert.That(GameFlowController.Instance, Is.Not.Null);
            if (PartyRuntime.Instance != null) UnityEngine.Object.DestroyImmediate(PartyRuntime.Instance.gameObject);
            if (CharacterProgressionService.Instance != null)
                UnityEngine.Object.DestroyImmediate(CharacterProgressionService.Instance.gameObject);
            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            // Captured callback locals must be created after EnterPlayMode's domain reload.
            try
            {
                RosterFixture fixture = CreateFixture(3);
                CharacterProgressionDefinitionSO[] definitions = new CharacterProgressionDefinitionSO[fixture.CharacterIds.Length];
                for (int i = 0; i < definitions.Length; i++)
                {
                    definitions[i] = CreateDefinition(fixture.CharacterIds[i]);
                    SetPrivate(definitions[i], "experienceRequiredByLevel", new[] { 1000, 1000, 1000, 1000 });
                }
                Invoke(fixture.Progression, "ConfigureForTests", fixture.CharacterIds[0], definitions);
                CurrencyWallet wallet = CurrencyWallet.Instance;
                RewardService rewards = RewardService.Instance;
                Assert.That(wallet, Is.Not.Null);
                Assert.That(rewards, Is.Not.Null);
                wallet.SetGold(0); rewards.ResetForNewGame();
                SetPrivate(rewards, "currencyWallet", wallet);
                SetPrivate(rewards, "characterProgressionService", fixture.Progression);
                CombatEncounterGroup encounter = Track(new GameObject("PARTY_05.Encounter")).AddComponent<CombatEncounterGroup>();
                fixture.Enemy.transform.SetParent(encounter.transform);
                SetPrivate(fixture.Trigger, "encounterGroup", encounter);
                QuestRuntime quests = Track(new GameObject("PARTY_05.Quest")).AddComponent<QuestRuntime>();
                QuestDefinitionSO quest = CreateKillQuest("party05.quest", "party05.kill");
                quests.StartQuest(quest);
                QuestObjectiveTracker tracker = Track(new GameObject("PARTY_05.Tracker")).AddComponent<QuestObjectiveTracker>();
                SetPrivate(tracker, "questRuntime", quests);
                CombatFlowOrchestrator orchestrator = Track(new GameObject("PARTY_05.Orchestrator")).AddComponent<CombatFlowOrchestrator>();
                Game.Combat.Effects.CombatDirector director = Track(new GameObject("PARTY_05.Director")).AddComponent<Game.Combat.Effects.CombatDirector>();
                CombatEntryPoint entry = Track(new GameObject("PARTY_05.Entry")).AddComponent<CombatEntryPoint>();
                SetPrivate(entry, "flowOrchestrator", orchestrator); SetPrivate(entry, "director", director);
                SetPrivate(orchestrator, "entryPoint", entry);
                SetPrivate(entry, "finalExchangeRuntimeConfig", Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>()));
                SetPrivate(entry, "_book", CreateSkillBook());
                CreateQuestPublisher(entry, encounter, quest.QuestId, quest.Objectives[0].ObjectiveId);
                CombatResult published = null;
                entry.OnCombatEnded += result => { published = result; rewards.GrantCombatResult(result); };
                Assert.That(entry.StartCombat(fixture.CreateEncounterRequest()), Is.True);
                CombatSession session = entry.ActiveSession;
                Assert.That(session.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain));
                session.GetCombatState(session.Allies[1]).ApplyDamage(session.Allies[1].HP);
                session.GetCombatState(session.Enemies[0]).ApplyDamage(1);
                // Control enemy response only; production resolution and terminal flow decide Victory.
                FinalCombatEnemyCommandController enemyCommands = (FinalCombatEnemyCommandController)typeof(CombatEntryPoint)
                    .GetField("_finalEnemyCommandController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(entry);
                enemyCommands.Unbind();
                ICombatant attacker = session.Allies[2];
                Assert.That(orchestrator.SubmitAttackDeclaration(new CombatAttackDeclaration(attacker, session.Enemies[0], attacker.Skills[0]),
                    orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (orchestrator.PendingDecisionRequest?.Kind != CombatExchangeDecisionKind.Response && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(orchestrator.PendingDecisionRequest?.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
                Assert.That(orchestrator.SubmitNoResponse(orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
                while (published == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(published, Is.Not.Null, "FinalExchange did not publish Victory within the fixture deadline.");
                Assert.That(published.EndReason, Is.EqualTo(CombatEndReason.Victory));
                Assert.That(published.AllyParticipants[1].IsAlive, Is.False);
                for (int i = 0; i < fixture.CharacterIds.Length; i++)
                {
                    Assert.That(fixture.Progression.TryGetState(fixture.CharacterIds[i], out _, out int exp), Is.True);
                    Assert.That(exp, Is.EqualTo(i < 3 ? 150 : 0));
                }
                Assert.That(wallet.Gold, Is.EqualTo(50));
                Assert.That(quests.GetObjectiveProgress(quest.QuestId, quest.Objectives[0].ObjectiveId), Is.EqualTo(1));
                Invoke(entry, "RaiseCombatEnded", published);
                Assert.That(wallet.Gold, Is.EqualTo(50));
                Assert.That(quests.GetObjectiveProgress(quest.QuestId, quest.Objectives[0].ObjectiveId), Is.EqualTo(1));
                GameSaveData saved = new();
                fixture.Progression.CaptureSaveData(saved); rewards.CaptureSaveData(saved);
                saved = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(saved));
                fixture.Progression.RestoreSaveData(saved); rewards.RestoreSaveData(saved);
                Assert.That(rewards.GrantCombatResult(published).DuplicateBlocked, Is.True);
                Assert.That(rewards.CombatGrantLedgerCount, Is.EqualTo(1));
                for (int i = 0; i < 3; i++)
                {
                    fixture.Progression.TryGetState(fixture.CharacterIds[i], out _, out int exp);
                    Assert.That(exp, Is.EqualTo(150));
                }
                Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            }
            finally { Application.logMessageReceived -= CaptureRuntimeError; }
            yield return new ExitPlayMode();
        }

        private RosterFixture CreateFixture(int selectedCount)
        {
            PartyRuntime party = Track(new GameObject("CBT_PARTY_01.Party")).AddComponent<PartyRuntime>();
            CharacterProgressionService progression = Track(new GameObject("CBT_PARTY_01.Progression"))
                .AddComponent<CharacterProgressionService>();
            string[] ids = { "test.party.leader", "test.party.ally.one", "test.party.ally.two", "test.party.ally.three" };
            CharacterProgressionDefinitionSO[] definitions =
            {
                CreateDefinition(ids[0]),
                CreateDefinition(ids[1]),
                CreateDefinition(ids[2]),
                CreateDefinition(ids[3])
            };
            Invoke(progression, "ConfigureForTests", null, definitions);

            List<GameObject> allies = new();
            for (int i = 0; i < ids.Length; i++)
            {
                party.AddMember(ids[i]);
                if (i < selectedCount)
                    party.SelectForCombat(ids[i], true);
                allies.Add(CreateActor($"CBT_PARTY_01.Ally.{i}", 10 + i, 15 + i, i + 1));
            }

            GameObject enemy = CreateActor("CBT_PARTY_01.Enemy", 11, 11, 9);
            GameObject triggerObject = Track(new GameObject("CBT_PARTY_01.Trigger"));
            triggerObject.AddComponent<BoxCollider2D>();
            CombatEncounterTrigger2D trigger = triggerObject.AddComponent<CombatEncounterTrigger2D>();
            List<PartyCombatantReference> bindings = new();
            List<PartyCombatantBinding> combatantBindings = new();
            for (int i = 0; i < ids.Length; i++)
            {
                bindings.Add(CreateBinding(ids[i], allies[i]));
                combatantBindings.Add(new PartyCombatantBinding(ids[i], allies[i]));
            }
            SetPrivate(trigger, "usePartyCombatRoster", true);
            SetPrivate(trigger, "partyRuntime", party);
            SetPrivate(trigger, "characterProgression", progression);
            SetPrivate(trigger, "partyCombatantBindings", bindings);
            SetPrivate(trigger, "flowMode", CombatFlowMode.StandoffClashChain);
            SetPrivate(trigger, "startReason", StartReason.PlayerFirstHit);
            SetPrivate(trigger, "initiativeSide", Side.Allies);

            return new RosterFixture(party, progression, trigger, ids, allies, enemy, combatantBindings);
        }

        private static CombatSession CreateFinalExchangeSession(
            CombatStartRequest request,
            SkillBook book,
            CombatRuntimeConfig config,
            out CombatStateMachine machine)
        {
            CombatSession session = new CombatSession(
                request.Reason,
                request.InitiativeSide,
                new InspirationPool(request.InspirationMax, request.InspirationStart),
                new Game.Combat.Environment.CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                config);
            new FieldCombatantFactory(book).PopulateCombatants(session, request);
            machine = new CombatStateMachine(session, new CombatTimeline(), book);
            machine.Tick();
            return session;
        }

        private static SkillBook CreateSkillBook()
        {
            SkillBook book = new();
            book.Register(new TestSkill(1, 1));
            book.Register(new TestSkill(2, 10));
            book.Register(new TestSkill(3, 10));
            book.Register(new TestSkill(9, 1));
            return book;
        }

        private GameObject CreateActor(string name, int hp, int maxHp, int skillId)
        {
            GameObject actor = Track(new GameObject(name));
            CombatHpComponent health = actor.AddComponent<CombatHpComponent>();
            health.MaxHP = maxHp;
            health.HP = hp;
            CombatSkillLoadoutComponent loadout = actor.AddComponent<CombatSkillLoadoutComponent>();
            SetPrivate(loadout, "skillIds", new[] { skillId });
            return actor;
        }

        private CharacterProgressionDefinitionSO CreateDefinition(string id)
        {
            CharacterProgressionDefinitionSO definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            SetPrivate(definition, "characterId", id);
            SetPrivate(definition, "startingLevel", 1);
            SetPrivate(definition, "maximumLevel", 5);
            SetPrivate(definition, "experienceRequiredByLevel", new[] { 1, 2, 3, 4 });
            return definition;
        }

        private QuestDefinitionSO CreateKillQuest(string questId, string objectiveId)
        {
            QuestObjectiveDefinition objective = new QuestObjectiveDefinition();
            SetPrivate(objective, "objectiveId", objectiveId);
            SetPrivate(objective, "eventType", QuestEventType.Kill);
            SetPrivate(objective, "requiredCount", 2);
            QuestDefinitionSO definition = Track(ScriptableObject.CreateInstance<QuestDefinitionSO>());
            SetPrivate(definition, "questId", questId);
            SetPrivate(definition, "objectives", new[] { objective });
            return definition;
        }

        private CombatQuestObjectivePublisher CreateQuestPublisher(
            CombatEntryPoint entry,
            CombatEncounterGroup encounter,
            string questId,
            string objectiveId)
        {
            GameObject owner = Track(new GameObject("CBT_PARTY_02.QuestPublisher"));
            owner.SetActive(false);
            CombatQuestObjectivePublisher publisher = owner.AddComponent<CombatQuestObjectivePublisher>();
            SetPrivate(publisher, "combatEntryPoint", entry);
            SetPrivate(publisher, "targetEncounter", encounter);
            SetPrivate(publisher, "questId", questId);
            SetPrivate(publisher, "objectiveId", objectiveId);
            owner.SetActive(true);
            return publisher;
        }

        private PartyCombatantReference CreateBinding(string characterId, GameObject fieldObject)
        {
            PartyCombatantReference binding = new PartyCombatantReference();
            SetPrivate(binding, "characterId", characterId);
            SetPrivate(binding, "fieldObject", fieldObject);
            return binding;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static void ForceCombatLineup(PartyRuntime party, IReadOnlyList<string> ids)
        {
            FieldInfo field = typeof(PartyRuntime).GetField("_combatLineup", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            List<string> lineup = (List<string>)field.GetValue(party);
            lineup.Clear();
            for (int i = 0; i < ids.Count; i++)
                lineup.Add(ids[i]);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }

        private void CaptureRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _runtimeErrors.Add($"{type}: {condition}\n{stackTrace}");
        }

        private sealed class RosterFixture
        {
            public RosterFixture(
                PartyRuntime party,
                CharacterProgressionService progression,
                CombatEncounterTrigger2D trigger,
                string[] characterIds,
                List<GameObject> allies,
                GameObject enemy,
                List<PartyCombatantBinding> bindings)
            {
                Party = party;
                Progression = progression;
                Trigger = trigger;
                CharacterIds = characterIds;
                Allies = allies;
                Enemy = enemy;
                Bindings = bindings;
            }

            public PartyRuntime Party { get; }
            public CharacterProgressionService Progression { get; }
            public CombatEncounterTrigger2D Trigger { get; }
            public string[] CharacterIds { get; }
            public List<GameObject> Allies { get; }
            public GameObject Enemy { get; }
            public List<PartyCombatantBinding> Bindings { get; }

            public CombatStartRequest CreateEncounterRequest() => (CombatStartRequest)Invoke(
                Trigger,
                "CreateEncounterRequest",
                new List<GameObject> { Allies[0] },
                new List<GameObject> { Enemy });
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => 0;
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider
        {
            public TestSkill(int id, int damage)
            {
                Id = new SkillId(id);
                BaseDamage = damage;
            }

            public SkillId Id { get; }
            public string Name => $"CBT Party Skill {Id.Value}";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage { get; }
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost => 1;
        }
    }
}
#endif
