using System.Linq;
using System.Reflection;
using Game.Core;
using Game.EditorTools;
using Game.Interaction;
using Game.Quest;
using Game.Story;
using Game.Story.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Integration
{
    public sealed class DungeonOneProductionSceneTests
    {
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (System.Type type in typeof(SaveLoadService).Assembly.GetTypes())
            {
                FieldInfo instance = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                if (instance != null && typeof(Object).IsAssignableFrom(instance.FieldType))
                    instance.SetValue(null, null);
            }
        }

        [Test]
        public void ProductionDungeonOne_IsCleanEnvironmentMigration()
        {
            Scene scene = EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.path, Is.EqualTo(DungeonOneProductionMigrationUtility.ProductionScenePath));
            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            string[] roots = scene.GetRootGameObjects().Select(item => item.name).ToArray();
            Assert.That(roots, Does.Contain("Runtime"));
            Assert.That(roots, Does.Contain("World"));
            Assert.That(roots, Does.Contain("Actors"));
            Assert.That(roots, Does.Contain("Main Camera"));
            Assert.That(roots, Does.Not.Contain("Systems"));
            Assert.That(roots, Does.Not.Contain("Mission"));
            Assert.That(roots, Does.Not.Contain("Enemies"));
            Assert.That(roots, Does.Not.Contain("UI"));
        }

        [Test]
        public void ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            InteractableObject[] interactables = Object.FindObjectsByType<InteractableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(interactables, Has.Length.EqualTo(1));

            InteractableObject npc = interactables[0];
            Assert.That(GetHierarchyPath(npc.transform), Is.EqualTo("Actors/NPCs/Dungeon1_FirstTalkNpc"));
            Assert.That(Vector3.Distance(npc.transform.position, new Vector3(187.105f, 7.698f, 0f)), Is.LessThan(0.001f));
            Assert.That(npc.PromptText, Is.EqualTo("F: 대화"));
            Assert.That(npc.InteractionId, Is.EqualTo("dungeon1.npc.first-talk"));
            Assert.That(npc.UsePolicy, Is.EqualTo(InteractionUsePolicy.Repeatable));
            Assert.That(npc.Events, Has.Count.EqualTo(1));
            Assert.That(npc.Events[0], Is.TypeOf<StoryInteractionEventSO>());
            Assert.That(npc.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(npc.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(npc.GetComponent<InteractionController>(), Is.Null);
            Assert.That(npc.GetComponent<QuestRuntime>(), Is.Null);
            Assert.That(npc.GetComponentInChildren<StoryInteractable2D>(true), Is.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(npc.gameObject), Is.EqualTo(
                "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab"));
        }

        [Test]
        public void ProductionDungeonOne_NpcFirstTalkUsesChoiceFreeLegacyContentThroughStoryContract()
        {
            StoryInteractionEventSO interactionEvent = AssetDatabase.LoadAssetAtPath<StoryInteractionEventSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkStoryInteraction.asset");
            StoryEventDefinitionSO dialogue = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkDialogue.asset");

            Assert.That(interactionEvent, Is.Not.Null);
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(interactionEvent.EventDefinition, Is.SameAs(dialogue));
            Assert.That(interactionEvent.ActionId, Is.EqualTo("dungeon1.npc.first-talk.interact"));
            Assert.That(dialogue.EventId, Is.EqualTo("dungeon1.npc.first-talk.story"));
            Assert.That(dialogue.Nodes, Has.Count.EqualTo(1));

            StoryNode firstTalk = dialogue.Nodes[0];
            Assert.That(firstTalk.NodeId, Is.EqualTo("first-talk"));
            Assert.That(firstTalk.SpeakerName, Is.EqualTo("NPC"));
            Assert.That(firstTalk.Body, Is.EqualTo("여기 ㅈㄴ 위험한 곳임. 들어갈거임?"));
            Assert.That(firstTalk.Choices, Is.Empty);
            Assert.That(firstTalk.Effects, Has.Count.EqualTo(1));
            Assert.That(firstTalk.EndEvent, Is.True);

            SerializedProperty effect = new SerializedObject(dialogue)
                .FindProperty("nodes").GetArrayElementAtIndex(0)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Assert.That(effect.FindPropertyRelative("type").intValue, Is.EqualTo((int)StoryEffectType.PublishQuestEvent));
            Assert.That(effect.FindPropertyRelative("missionId").stringValue, Is.EqualTo("ch01.find-first-npc"));
            Assert.That(effect.FindPropertyRelative("objectiveId").stringValue, Is.EqualTo("talk_first_npc"));
            Assert.That(effect.FindPropertyRelative("questEventType").intValue, Is.EqualTo((int)QuestEventType.Talk));
            Assert.That(effect.FindPropertyRelative("intValue").intValue, Is.EqualTo(1));
        }

        [Test]
        public void ProductionDungeonOne_HasApprovedQuestDefinitionOnItsCanonicalRuntime()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.QuestId, Is.EqualTo("ch01.find-first-npc"));
            Assert.That(definition.QuestTitle, Is.EqualTo("낯선 장소"));
            Assert.That(definition.MissionDayCost, Is.Zero);
            Assert.That(definition.RewardGold, Is.Zero);
            Assert.That(definition.RewardExp, Is.Zero);
            Assert.That(definition.Objectives, Has.Length.EqualTo(1));
            Assert.That(definition.Objectives[0].ObjectiveId, Is.EqualTo("talk_first_npc"));
            Assert.That(definition.Objectives[0].EventType, Is.EqualTo(QuestEventType.Talk));
            Assert.That(definition.Objectives[0].TargetId, Is.EqualTo("dungeon1.npc.first-talk"));
            Assert.That(definition.Objectives[0].RequiredCount, Is.EqualTo(1));

            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);
            QuestRuntime[] runtimes = Object.FindObjectsByType<QuestRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(runtimes, Has.Length.EqualTo(1));

            SerializedProperty definitions = new SerializedObject(runtimes[0]).FindProperty("questDefinitions");
            Assert.That(definitions.arraySize, Is.EqualTo(1));
            Assert.That(definitions.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(definition));
        }

        [Test]
        public void ProductionDungeonOne_FirstTalkStoryStartsAndRestoresExploration()
        {
            StoryInteractionEventSO interactionEvent = AssetDatabase.LoadAssetAtPath<StoryInteractionEventSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkStoryInteraction.asset");
            GameObject core = new("DungeonOneFirstTalkTestCore");
            GameObject narrative = new("DungeonOneFirstTalkTestNarrative");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                InvokeAwake(stateMachine);
                InvokeAwake(flow);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);

                Assert.That(runner.TryStartEvent(interactionEvent.EventDefinition), Is.True);
                Assert.That(stateMachine.Current, Is.EqualTo(GameState.Dialogue));

                runner.Advance();

                Assert.That(runner.IsRunning, Is.False);
                Assert.That(stateMachine.Current, Is.EqualTo(GameState.Exploration));
            }
            finally
            {
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        public void ProductionDungeonOne_FirstTalkPublishesItsQuestObjectiveOnlyOnce()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");
            StoryEventDefinitionSO dialogue = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkDialogue.asset");
            GameObject core = new("DungeonOneQuestTestCore");
            GameObject narrative = new("DungeonOneQuestTestNarrative");
            GameObject trackerObject = new("DungeonOneQuestTestTracker");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                QuestRuntime runtime = core.AddComponent<QuestRuntime>();
                trackerObject.SetActive(false);
                QuestObjectiveTracker tracker = trackerObject.AddComponent<QuestObjectiveTracker>();
                SerializedObject serializedTracker = new(tracker);
                serializedTracker.FindProperty("questRuntime").objectReferenceValue = runtime;
                serializedTracker.ApplyModifiedPropertiesWithoutUndo();
                trackerObject.SetActive(true);
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeAwake(tracker);
                InvokeIfPresent(tracker, "OnEnable");
                runtime.StartQuest(definition);

                Assert.That(runtime.ApplyEvent(new QuestEvent(
                    QuestEventType.Talk,
                    definition.QuestId,
                    "talk_first_npc",
                    1,
                    null,
                    "unrelated-talk",
                    "other.npc")), Is.False);
                Assert.That(runtime.GetObjectiveProgress(definition.QuestId, "talk_first_npc"), Is.Zero);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);

                Assert.That(runner.TryStartEvent(dialogue), Is.True);
                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Completed));
                Assert.That(runtime.GetObjectiveProgress(definition.QuestId, "talk_first_npc"), Is.EqualTo(1));

                runner.Advance();
                Assert.That(runner.TryStartEvent(dialogue), Is.True);
                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Completed));
                Assert.That(runtime.GetObjectiveProgress(definition.QuestId, "talk_first_npc"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(trackerObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        public void ProductionDungeonOne_SceneStartAdapterUsesCanonicalRunnerAndIntroStory()
        {
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");

            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            SceneStartStoryEventAdapter[] adapters = Object.FindObjectsByType<SceneStartStoryEventAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            StoryEventRunner[] runners = Object.FindObjectsByType<StoryEventRunner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(adapters, Has.Length.EqualTo(1));
            Assert.That(runners, Has.Length.EqualTo(1));
            Assert.That(intro, Is.Not.Null);
            Assert.That(intro.EventId, Is.EqualTo("dungeon1.intro.story"));
            Assert.That(intro.Nodes, Has.Count.EqualTo(2));

            SerializedObject adapter = new(adapters[0]);
            Assert.That(adapter.FindProperty("runner").objectReferenceValue, Is.SameAs(runners[0]));
            Assert.That(adapter.FindProperty("eventDefinition").objectReferenceValue, Is.SameAs(intro));

            SerializedProperty effect = new SerializedObject(intro)
                .FindProperty("nodes").GetArrayElementAtIndex(1)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Assert.That(effect.FindPropertyRelative("type").intValue, Is.EqualTo((int)StoryEffectType.StartQuest));
            Assert.That(effect.FindPropertyRelative("questDefinition").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                    "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset")));
        }

        [Test]
        public void ProductionDungeonOne_SceneStartAdapterStartsIntroOnceAndRestoredQuestRemainsActive()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");
            GameObject core = new("DungeonOneIntroTestCore");
            GameObject narrative = new("DungeonOneIntroTestNarrative");
            GameObject adapterObject = new("DungeonOneIntroTestAdapter");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                QuestRuntime runtime = core.AddComponent<QuestRuntime>();
                SetReference(runtime, "questDefinitions", definition);
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeAwake(runtime);
                runtime.StartQuest(definition);

                Game.NonCombat.Save.GameSaveData snapshot = new();
                runtime.CaptureSaveData(snapshot);
                runtime.RestoreSaveData(snapshot);
                int starts = 0;
                runtime.OnQuestStarted += _ => starts++;

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                SceneStartStoryEventAdapter adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");
                InvokeIfPresent(adapter, "Start");

                Assert.That(runner.IsRunning, Is.True);
                runner.Advance();

                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Active));
                Assert.That(starts, Is.Zero);

                runner.EndEvent();
                runtime.CompleteQuest(definition.QuestId);
                runtime.CaptureSaveData(snapshot);
                runtime.RestoreSaveData(snapshot);

                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                narrative = new GameObject("DungeonOneCompletedIntroTestNarrative");
                adapterObject = new GameObject("DungeonOneCompletedIntroTestAdapter");
                runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");
                runner.Advance();

                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Completed));
                Assert.That(starts, Is.Zero);
                runner.EndEvent();
            }
            finally
            {
                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            InvokeIfPresent(component, "Awake");
        }

        private static void InvokeIfPresent(MonoBehaviour component, string methodName)
        {
            MethodInfo awake = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(component, null);
        }

        private static void SetReference(UnityEngine.Object owner, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property.isArray)
            {
                property.arraySize = 1;
                property.GetArrayElementAtIndex(0).objectReferenceValue = value;
            }
            else
            {
                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
