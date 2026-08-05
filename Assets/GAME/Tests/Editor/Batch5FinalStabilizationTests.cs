using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Reward;
using Game.Systems.Persona;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class Batch5FinalStabilizationTests
    {
        [SetUp] public void SetUp() => Cleanup();
        [TearDown] public void TearDown() => Cleanup();

        [Test]
        public void RepeatedBootstrap_PreservesAllStateAndDoesNotDuplicateOwners()
        {
            RuntimeBootstrapper bootstrapper = new GameObject("Bootstrap").AddComponent<RuntimeBootstrapper>();
            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);
            CurrencyWallet wallet = UnityEngine.Object.FindFirstObjectByType<CurrencyWallet>();
            InventoryService inventory = UnityEngine.Object.FindFirstObjectByType<InventoryService>();
            CharacterProgressionService progression = UnityEngine.Object.FindFirstObjectByType<CharacterProgressionService>();
            RewardService rewards = UnityEngine.Object.FindFirstObjectByType<RewardService>();
            InteractionRuntime interactions = UnityEngine.Object.FindFirstObjectByType<InteractionRuntime>();
            CharacterProgressionDefinitionSO definition = CreateDefinition("hero", 1, 5, 10);
            Invoke(progression, "ConfigureForTests", "hero", new[] { definition });
            wallet.SetGold(17); inventory.AddItem("item", 3); progression.ApplyExperience("hero", 6);
            interactions.MarkConsumed("door", InteractionUsePolicy.PersistentOnce);
            SetField(rewards, "currencyWallet", wallet); SetField(rewards, "inventoryService", inventory); SetField(rewards, "characterProgressionService", progression);
            rewards.GrantReward(new RewardGrantRequest(RewardSourceType.QuestCompletion, "quest:stable", 1));

            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);

            Assert.That(wallet.Gold, Is.EqualTo(18)); Assert.That(inventory.GetCount("item"), Is.EqualTo(3));
            Assert.That(progression.TryGetState("hero", out int level, out int xp), Is.True); Assert.That(level, Is.EqualTo(1)); Assert.That(xp, Is.EqualTo(6));
            Assert.That(interactions.IsConsumed("door", InteractionUsePolicy.PersistentOnce), Is.True);
            Assert.That(rewards.GrantReward(new RewardGrantRequest(RewardSourceType.QuestCompletion, "quest:stable", 1)).DuplicateBlocked, Is.True);
            AssertOne<CurrencyWallet>(); AssertOne<InventoryService>(); AssertOne<CharacterProgressionService>(); AssertOne<RewardService>(); AssertOne<InteractionRuntime>();
        }

        [Test]
        public void StatefulProductionServices_DeclarePlayModePersistence()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            foreach (string relative in new[] {
                "Assets/GAME/Scripts/NonCombat/Inventory/CurrencyWallet.cs",
                "Assets/GAME/Scripts/NonCombat/Inventory/InventoryService.cs",
                "Assets/GAME/Scripts/NonCombat/Progress/CharacterProgressionService.cs",
                "Assets/GAME/Scripts/Reward/RewardService.cs",
                "Assets/GAME/Scripts/Interaction/InteractionRuntime.cs" })
                Assert.That(File.ReadAllText(Path.Combine(root, relative)), Does.Contain("DontDestroyOnLoad(gameObject)"), relative);
        }

        [TestCase(10, 2, 0)]
        [TestCase(25, 3, 5)]
        [TestCase(int.MaxValue, 5, 0)]
        public void Restore_NormalizesExperienceAgainstAuthoredCurve(int savedExperience, int expectedLevel, int expectedExperience)
        {
            CharacterProgressionService progression = CreateProgression(CreateDefinition("hero", 1, 5, 10));
            int refreshes = 0, mutations = 0; progression.Refreshed += () => refreshes++; progression.ProgressionChanged += _ => mutations++;
            progression.RestoreSaveData(SaveState("hero", 1, savedExperience));
            Assert.That(progression.TryGetState("hero", out int level, out int experience), Is.True);
            Assert.That(level, Is.EqualTo(expectedLevel)); Assert.That(experience, Is.EqualTo(expectedExperience));
            Assert.That(refreshes, Is.EqualTo(1)); Assert.That(mutations, Is.Zero);
            GameSaveData captured = new(); progression.CaptureSaveData(captured);
            Assert.That(captured.progression.characters.Single().level, Is.EqualTo(expectedLevel));
            Assert.That(captured.progression.characters.Single().experience, Is.EqualTo(expectedExperience));
        }

        [Test]
        public void Restore_MaxLevelAlwaysClearsExperience()
        {
            CharacterProgressionService progression = CreateProgression(CreateDefinition("hero", 1, 3, 10));
            progression.RestoreSaveData(SaveState("hero", 3, int.MaxValue));
            Assert.That(progression.TryGetState("hero", out int level, out int experience), Is.True);
            Assert.That(level, Is.EqualTo(3)); Assert.That(experience, Is.Zero);
        }

        [Test]
        public void InvalidCurveAfterValidLevel_IsTransactionalAndReportsNoNegativeValues()
        {
            CharacterProgressionService progression = CreateProgression(CreateDefinition("hero", 1, 4, 10, 0));
            int events = 0; progression.ProgressionChanged += _ => events++;
            ExperienceApplyResult result = progression.ApplyExperience("hero", int.MaxValue);
            Assert.That(result.Status, Is.EqualTo(ExperienceApplyStatus.InvalidDefinition));
            Assert.That(result.AppliedExperience, Is.Zero); Assert.That(result.LevelsGained, Is.Zero); Assert.That(events, Is.Zero);
            Assert.That(progression.TryGetState("hero", out int level, out int experience), Is.True); Assert.That(level, Is.EqualTo(1)); Assert.That(experience, Is.Zero);
        }

        [Test]
        public void BootstrapGuaranteesPersonaBeforeAdapterAndRestoreIsSilent()
        {
            RuntimeBootstrapper bootstrapper = new GameObject("Bootstrap").AddComponent<RuntimeBootstrapper>();
            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);
            PersonaStatusManager persona = UnityEngine.Object.FindFirstObjectByType<PersonaStatusManager>();
            PersonaSaveAdapter adapter = UnityEngine.Object.FindFirstObjectByType<PersonaSaveAdapter>();
            Assert.That(persona, Is.Not.Null); Assert.That(adapter, Is.Not.Null);
            int gains = 0, levels = 0; persona.OnStatXpGained += (_, _) => gains++; persona.OnStatLevelUp += (_, _, _) => levels++;
            GameSaveData save = new(); save.progression.personaStats.Add(new PersonaStatSaveData { stat = PersonaStat.Courage.ToString(), level = 3, xp = 4 });
            adapter.RestoreSaveData(save);
            Assert.That(persona.GetLevel(PersonaStat.Courage), Is.EqualTo(3)); Assert.That(persona.GetXp(PersonaStat.Courage), Is.EqualTo(4));
            Assert.That(gains, Is.Zero); Assert.That(levels, Is.Zero);
        }

        private static CharacterProgressionService CreateProgression(CharacterProgressionDefinitionSO definition)
        { CharacterProgressionService service = new GameObject("Progression").AddComponent<CharacterProgressionService>(); Invoke(service, "ConfigureForTests", "hero", new[] { definition }); return service; }
        private static GameSaveData SaveState(string id, int level, int experience)
        { GameSaveData save = new(); save.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = id, level = level, experience = experience }); return save; }
        private static CharacterProgressionDefinitionSO CreateDefinition(string id, int start, int max, params int[] curve)
        { CharacterProgressionDefinitionSO definition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>(); SerializedObject serialized = new(definition); serialized.FindProperty("characterId").stringValue = id; serialized.FindProperty("startingLevel").intValue = start; serialized.FindProperty("maximumLevel").intValue = max; SerializedProperty values = serialized.FindProperty("experienceRequiredByLevel"); values.arraySize = curve.Length; for (int i = 0; i < curve.Length; i++) values.GetArrayElementAtIndex(i).intValue = curve[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); return definition; }
        private static void AssertOne<T>() where T : UnityEngine.Object => Assert.That(UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static void Cleanup()
        {
            foreach (MonoBehaviour value in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (value != null && (value is RuntimeBootstrapper || value is CurrencyWallet || value is InventoryService || value is CharacterProgressionService || value is RewardService || value is InteractionRuntime || value is InteractionRunner || value is PersonaStatusManager || value is PersonaSaveAdapter || value is SaveLoadService || value is GameStateMachine || value is GameFlowController || value is SceneFlowController || value is Game.UI.GameUIRootController || value is Game.UI.UIScreenRouter || value.GetType().Name == "GameInputInstaller"))
                    UnityEngine.Object.DestroyImmediate(value.gameObject);
            foreach (CharacterProgressionDefinitionSO value in Resources.FindObjectsOfTypeAll<CharacterProgressionDefinitionSO>()) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
