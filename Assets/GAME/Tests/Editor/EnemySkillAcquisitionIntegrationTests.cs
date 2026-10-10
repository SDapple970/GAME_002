using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.Enemies;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Quest;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class EnemySkillAcquisitionIntegrationTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp() => CleanupRuntime();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            CleanupRuntime();
        }

        [Test]
        public void Victory_NormalizesRepeatedProvenanceAndSharedKeysAndIsIdempotentAcrossCallbacks()
        {
            CharacterSkillRuntime runtime = new();
            EnemySourceSnapshot first = new("enemy.test.a", new[] { "skill.z", "skill.a", "skill.z" });
            CombatResult combat = Result(CombatEndReason.Victory, "hero.a", first, first,
                new EnemySourceSnapshot("enemy.test.b", new[] { "skill.a" }));
            EnemySkillAcquisitionRequest request = EnemySkillAcquisitionRequest.FromCombatResult(combat);
            EnemySkillAcquisitionResult result = EnemySkillAcquisitionProcessor.Process(request, runtime, new[] { "skill.a", "skill.z" });
            Assert.That(result.AcquiredCount, Is.EqualTo(2));
            Assert.That(result.Skills.Select(skill => skill.PersistentSkillKey), Is.EqualTo(new[] { "skill.z", "skill.a" }));
            EnemySkillAcquisitionResult repeated = EnemySkillAcquisitionProcessor.Process(request, runtime, new[] { "skill.a", "skill.z" });
            Assert.That(repeated.AcquiredCount, Is.Zero);
            Assert.That(repeated.Skills.All(skill => skill.Status == CharacterSkillAcquireStatus.AlreadyOwned), Is.True);
            Assert.That(runtime.GetAcquiredSkills("hero.a"), Is.EqualTo(new[] { "skill.a", "skill.z" }));
            Assert.That(runtime.GetAcquiredSkills("hero.b"), Is.Empty);
        }

        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        [TestCase(CombatEndReason.Scripted)]
        [TestCase(CombatEndReason.None)]
        public void NonVictory_DoesNotAcquireEvenIfEnemiesDiedOrLegacyIsWinIsTrue(CombatEndReason reason)
        {
            CharacterSkillRuntime runtime = new();
            CombatResult combat = Result(reason, "hero", new EnemySourceSnapshot("enemy.test", new[] { "skill.test" }));
            combat.IsWin = true;
            EnemySkillAcquisitionResult result = EnemySkillAcquisitionProcessor.Process(
                EnemySkillAcquisitionRequest.FromCombatResult(combat), runtime, new[] { "skill.test" });
            Assert.That(result.Status, Is.EqualTo(EnemySkillAcquisitionStatus.NotVictory));
            Assert.That(runtime.GetAcquiredSkills("hero"), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidRecipient_IsSafelyRejected(string recipient)
        {
            CharacterSkillRuntime runtime = new();
            CombatResult combat = Result(CombatEndReason.Victory, recipient, new EnemySourceSnapshot("enemy.test", new[] { "skill.test" }));
            Assert.That(EnemySkillAcquisitionProcessor.Process(EnemySkillAcquisitionRequest.FromCombatResult(combat),
                runtime, new[] { "skill.test" }).Status, Is.EqualTo(EnemySkillAcquisitionStatus.InvalidRecipient));
            CharacterSkillCollectionSaveData captured = new();
            runtime.Capture(captured);
            Assert.That(captured.characters, Is.Empty);
        }

        [Test]
        public void UnknownAndInvalidMapping_IsSkippedWithDiagnosticWhileUnknownSavedKeysArePreserved()
        {
            CharacterSkillRuntime runtime = new();
            runtime.TryAcquire("hero", "skill.unknown.saved");
            List<string> diagnostics = new();
            CombatResult combat = Result(CombatEndReason.Victory, "hero",
                new EnemySourceSnapshot("enemy.test", new[] { " ", "skill.unknown.authored", "skill.good" }),
                new EnemySourceSnapshot(" ", new[] { "skill.invalid-source" }));
            EnemySkillAcquisitionResult result = EnemySkillAcquisitionProcessor.Process(
                EnemySkillAcquisitionRequest.FromCombatResult(combat), runtime,
                new[] { "skill.good", "skill.invalid-source" }, diagnostics.Add);
            Assert.That(result.AcquiredCount, Is.EqualTo(1));
            Assert.That(result.SkippedSkillPersistentKeys, Is.EqualTo(new[] { "skill.unknown.authored" }));
            Assert.That(diagnostics.Single(), Does.Contain("enemy.test").And.Contain("skill.unknown.authored"));
            Assert.That(runtime.GetAcquiredSkills("hero"), Is.EqualTo(new[] { "skill.good", "skill.unknown.saved" }));
        }

        [Test]
        public void MissingRequestOrRuntime_IsSafelyRejectedWithoutMutatingState()
        {
            Assert.That(EnemySkillAcquisitionProcessor.Process(null, new CharacterSkillRuntime(), null).Status,
                Is.EqualTo(EnemySkillAcquisitionStatus.InvalidRequest));
            CombatResult combat = Result(CombatEndReason.Victory, "hero",
                new EnemySourceSnapshot("enemy.test", new[] { "skill.test" }));
            Assert.That(EnemySkillAcquisitionProcessor.Process(EnemySkillAcquisitionRequest.FromCombatResult(combat),
                null, new[] { "skill.test" }).Status, Is.EqualTo(EnemySkillAcquisitionStatus.RuntimeUnavailable));
        }

        [Test]
        public void Acquisition_RoundTripsWithExistingSaveSchemaAndPreservesEquippedState()
        {
            CharacterSkillSaveParticipant participant = Component<CharacterSkillSaveParticipant>();
            participant.Runtime.TryAcquire("hero", "skill.old");
            participant.Runtime.TryEquip("hero", "skill.old");
            CombatResult combat = Result(CombatEndReason.Victory, "hero",
                new EnemySourceSnapshot("enemy.test", new[] { "skill.new" }));
            EnemySkillAcquisitionProcessor.Process(EnemySkillAcquisitionRequest.FromCombatResult(combat),
                participant.Runtime, new[] { "skill.new" });
            GameSaveData data = new();
            data.currency.gold = 19;
            participant.CaptureSaveData(data);
            Assert.That(data.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(data.characterSkills.characters.Single().acquiredSkillKeys, Is.EqualTo(new[] { "skill.new", "skill.old" }));
            participant.ResetForNewGame();
            participant.RestoreSaveData(SaveSerializer.FromGameSaveJson(SaveSerializer.ToJson(data)));
            Assert.That(participant.Runtime.GetEquippedSkills("hero"), Is.EqualTo(new[] { "skill.old" }));
            Assert.That(participant.Runtime.HasAcquired("hero", "skill.new"), Is.True);
            Assert.That(data.currency.gold, Is.EqualTo(19));
        }

        [Test]
        public void ProductionResolver_UsesPersistentKeyNotMatchingDemoRuntimeIdAndRejectsAmbiguity()
        {
            CharacterSkillSaveParticipant participant = Component<CharacterSkillSaveParticipant>();
            SkillDefinitionSO production = Skill("skill.production.test", 11);
            SkillDefinitionSO demo = Skill("skill.demo.test", 11);
            CombatResult combat = Result(CombatEndReason.Victory, "hero",
                new EnemySourceSnapshot("enemy.test", new[] { production.PersistentKey }));
            Assert.That(EnemySkillAcquisitionIntegration.ProcessCompletion(combat, new[] { demo }, null).AcquiredCount, Is.Zero);
            Assert.That(EnemySkillAcquisitionIntegration.ProcessCompletion(combat, new[] { production, demo }, null).AcquiredCount, Is.EqualTo(1));
            Assert.That(participant.Runtime.HasAcquired("hero", demo.PersistentKey), Is.False);
            participant.Runtime.Reset();
            Assert.That(EnemySkillAcquisitionIntegration.ProcessCompletion(combat,
                new[] { production, Skill(production.PersistentKey, 12) }, null).AcquiredCount, Is.Zero);
        }

        [Test]
        public void CanonicalCompletion_AcquiresBeforeQuestAndRewardCallbacksWithNoSecondCompletionOwner()
        {
            Component<GameStateMachine>();
            GameFlowController flow = Component<GameFlowController>();
            CharacterSkillSaveParticipant participant = Component<CharacterSkillSaveParticipant>();
            PartyRuntime party = Component<PartyRuntime>();
            party.AddMember("hero.leader");
            party.AddMember("hero.other");
            CombatEntryPoint entry = Component<CombatEntryPoint>();
            SkillDefinitionSO skill = Skill("skill.production.test", 1);
            SerializedObject entryData = new(entry);
            SerializedProperty registry = entryData.FindProperty("skillDefinitions");
            registry.arraySize = 1;
            registry.GetArrayElementAtIndex(0).objectReferenceValue = skill;
            entryData.ApplyModifiedPropertiesWithoutUndo();
            Invoke(entry, "Awake");

            GameObject ally = Actor();
            GameObject enemy = Actor();
            CombatEncounterGroup group = enemy.AddComponent<CombatEncounterGroup>();
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            request.AllyFieldObjects.Add(ally);
            request.BindAllyCharacter(ally, party.LeaderCharacterId);
            request.EnemyFieldObjects.Add(enemy);
            request.EncounterOwnerOrNull = group;
            EnemyDefinitionSO definition = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(definition);
            SerializedObject enemyData = new(definition);
            enemyData.FindProperty("persistentKey").stringValue = "enemy.test";
            enemyData.FindProperty("acquirableSkillPersistentKeys").arraySize = 1;
            enemyData.FindProperty("acquirableSkillPersistentKeys").GetArrayElementAtIndex(0).stringValue = skill.PersistentKey;
            enemyData.ApplyModifiedPropertiesWithoutUndo();
            EnemySourceComponent sourceComponent = enemy.AddComponent<EnemySourceComponent>();
            SerializedObject sourceData = new(sourceComponent);
            sourceData.FindProperty("definition").objectReferenceValue = definition;
            sourceData.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(entry.StartCombat(request), Is.True);
            party.SetLeader("hero.other"); // Acquisition retains the recipient captured at combat entry.

            CombatQuestObjectivePublisher quest = Component<CombatQuestObjectivePublisher>();
            SerializedObject questData = new(quest);
            questData.FindProperty("combatEntryPoint").objectReferenceValue = entry;
            questData.FindProperty("targetEncounter").objectReferenceValue = group;
            questData.FindProperty("questId").stringValue = "quest.test";
            questData.FindProperty("objectiveId").stringValue = "kill.test";
            questData.ApplyModifiedPropertiesWithoutUndo();
            Invoke(quest, "OnEnable");
            CombatRewardUIBinder reward = Component<CombatRewardUIBinder>();
            Invoke(reward, "OnEnable");

            int questEvents = 0;
            Action<QuestEvent> observer = questEvent =>
            {
                if (questEvent.QuestId != "quest.test") return;
                Assert.That(participant.Runtime.HasAcquired("hero.leader", skill.PersistentKey), Is.True);
                questEvents++;
            };
            QuestEventChannel.OnEventRaised += observer;
            CombatResult finalized = null;
            entry.OnCombatEnded += result => finalized = result;
            try
            {
                entry.ActiveSession.Enemies.Single().ApplyDamage(10);
                Invoke(entry, "ForceFinishCombat", CombatEndReason.Victory);
                Assert.That(questEvents, Is.EqualTo(1));
                Assert.That(finalized, Is.Not.Null);
                Assert.That(entry.ActiveSession, Is.Null);
                Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
                Assert.That(participant.Runtime.GetAcquiredSkills("hero.other"), Is.Empty);
                Assert.That(EnemySkillAcquisitionIntegration.ProcessCompletion(finalized, new[] { skill }, null).AcquiredCount, Is.Zero);
            }
            finally
            {
                QuestEventChannel.OnEventRaised -= observer;
            }
        }

        [Test]
        public void CombatCoreAndModel_DoNotReferencePersistentRuntimeOrEnemyAuthoring()
        {
            foreach (string root in new[] { "Assets/GAME/Scripts/Combat/Runtime/Core", "Assets/GAME/Scripts/Combat/Runtime/Model" })
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(file);
                    Assert.That(source, Does.Not.Contain("CharacterSkillRuntime"), file);
                    Assert.That(source, Does.Not.Contain("EnemyDefinitionSO"), file);
                    Assert.That(source, Does.Not.Contain("EnemySourceComponent"), file);
                }
        }

        private CombatResult Result(CombatEndReason reason, string recipient, params EnemySourceSnapshot[] sources)
        {
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            request.SetSkillAcquisitionRecipient(recipient);
            request.AllyFieldObjects.Add(Actor());
            foreach (EnemySourceSnapshot source in sources)
            {
                GameObject enemy = Actor();
                request.EnemyFieldObjects.Add(enemy);
                request.SetEnemySourceSnapshot(enemy, source);
            }
            SkillBook book = new();
            book.Register(new SoSkill(Skill("skill.fallback", 1)));
            CombatSession session = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book)).session;
            foreach (ICombatant enemy in session.Enemies) enemy.ApplyDamage(10);
            return CombatResultBuilder.Build(session, reason);
        }

        private SkillDefinitionSO Skill(string key, int id)
        {
            SkillDefinitionSO skill = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            _created.Add(skill);
            skill.skillId = id;
            SerializedObject serialized = new(skill);
            serialized.FindProperty("persistentKey").stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private GameObject Actor()
        {
            GameObject actor = new("Unrelated actor name");
            actor.AddComponent<CombatHpComponent>();
            _created.Add(actor);
            return actor;
        }

        private T Component<T>() where T : MonoBehaviour
        {
            GameObject go = new(typeof(T).Name);
            _created.Add(go);
            T component = go.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }

        private static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static void CleanupRuntime()
        {
            HashSet<GameObject> objects = new();
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (behaviour != null && !EditorUtility.IsPersistent(behaviour) &&
                    behaviour.GetType().Namespace?.StartsWith("Game.", StringComparison.Ordinal) == true)
                    objects.Add(behaviour.gameObject);
            foreach (GameObject go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
