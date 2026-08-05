using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Reward;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class Batch6CharacterPartyRuntimeTests
    {
        [SetUp] public void SetUp() => Cleanup();
        [TearDown] public void TearDown() => Cleanup();

        [Test]
        public void StableIdentity_NormalizesAndRejectsMissingWithoutNameFallback()
        {
            Assert.That(CharacterIdentity.Normalize(" hero.a "), Is.EqualTo("hero.a"));
            Assert.That(CharacterIdentity.Normalize("  "), Is.Null);
            PartyRuntime party = new GameObject("hero.from.name").AddComponent<PartyRuntime>();
            Assert.That(party.AddMember(" ").Status, Is.EqualTo(PartyMutationStatus.InvalidCharacterId));
            Assert.That(party.Members, Is.Empty);
        }

        [Test]
        public void PartyMutation_EnforcesMembershipLeaderLineupAndEventRules()
        {
            PartyRuntime party = new GameObject("Party").AddComponent<PartyRuntime>();
            int changes = 0; party.Changed += _ => changes++;
            Assert.That(party.AddMember(" b ").Changed, Is.True);
            Assert.That(party.AddMember("a").Changed, Is.True);
            Assert.That(party.AddMember("b").Changed, Is.False);
            Assert.That(party.LeaderCharacterId, Is.EqualTo("b"));
            Assert.That(party.SetLeader("missing").Status, Is.EqualTo(PartyMutationStatus.InvalidLeader));
            Assert.That(party.SelectForCombat("missing", true).Status, Is.EqualTo(PartyMutationStatus.NotOwned));
            Assert.That(party.SelectForCombat("b", true).Changed, Is.True);
            Assert.That(party.SelectForCombat("b", true).Changed, Is.False);
            Assert.That(party.RemoveMember("b").Changed, Is.True);
            Assert.That(party.LeaderCharacterId, Is.EqualTo("a"));
            Assert.That(party.CombatLineup, Is.Empty);
            Assert.That(changes, Is.EqualTo(4));
        }

        [Test]
        public void PartySaveRestore_PreservesStateSilentlyAndDoesNotWriteLevels()
        {
            PartyRuntime party = new GameObject("Party").AddComponent<PartyRuntime>();
            GameSaveData save = new(); save.party.memberIds.AddRange(new[] { " b ", "a", "b", "invalid-lineup-owner" });
            save.party.leaderCharacterId = "missing"; save.party.selectedCombatMemberIds.AddRange(new[] { "a", "not-owned", "a" });
            int changes = 0, refreshes = 0; party.Changed += _ => changes++; party.Refreshed += () => refreshes++;
            party.RestoreSaveData(save);
            Assert.That(party.Members, Is.EqualTo(new[] { "b", "a", "invalid-lineup-owner" }));
            Assert.That(party.LeaderCharacterId, Is.EqualTo("b")); Assert.That(party.CombatLineup, Is.EqualTo(new[] { "a" }));
            Assert.That(changes, Is.Zero); Assert.That(refreshes, Is.EqualTo(1));
            GameSaveData captured = new(); captured.party.memberLevels.Add(new SaveIntEntry { id = "a", value = 9 }); party.CaptureSaveData(captured);
            Assert.That(captured.party.memberLevels.Single().value, Is.EqualTo(9));
        }

        [Test]
        public void SchemaSix_MigratesLeaderLineupAndPreservesMoreThan256Members()
        {
            List<string> members = Enumerable.Range(0, 300).Select(i => $"c{i:000}").ToList();
            string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":6},\"party\":{\"memberIds\":[\"" + string.Join("\",\"", members) + "\"]}}";
            GameSaveData data = Migrate(json);
            Assert.That(data.header.schemaVersion, Is.EqualTo(7)); Assert.That(data.party.memberIds, Has.Count.EqualTo(300));
            Assert.That(data.party.leaderCharacterId, Is.EqualTo("c000")); Assert.That(data.party.selectedCombatMemberIds, Has.Count.EqualTo(300));
        }

        [Test]
        public void PartyMembershipNeverMutatesProgression()
        {
            CharacterProgressionService progression = CreateProgression(CreateDefinition("hero", 1, 5, 10)); progression.ApplyExperience("hero", 7);
            PartyRuntime party = new GameObject("Party").AddComponent<PartyRuntime>(); party.AddMember("hero"); party.RemoveMember("hero");
            Assert.That(progression.TryGetState("hero", out int level, out int experience), Is.True); Assert.That(level, Is.EqualTo(1)); Assert.That(experience, Is.EqualTo(7));
        }

        [Test]
        public void CombatAdapter_BuildsDeterministicIsolatedSnapshotsFromSelectedParty()
        {
            CharacterProgressionService progression = CreateProgression(CreateDefinition("hero.b", 1, 5, 10), CreateDefinition("hero.a", 1, 5, 10));
            progression.ApplyExperience("hero.a", 12);
            PartyRuntime party = new GameObject("Party").AddComponent<PartyRuntime>(); party.AddMember("hero.b"); party.AddMember("hero.a"); party.SelectForCombat("hero.a", true); party.SelectForCombat("hero.b", true);
            GameObject a = CreateCombatActor("unrelated-name-a", 20, 25, 3, 4); GameObject b = CreateCombatActor("unrelated-name-b", 15, 15, 5);
            CharacterPartyCombatAdapter adapter = new();
            PartyCombatBuildResult result = adapter.BuildRequest(party, progression,
                new[] { new PartyCombatantBinding("hero.b", b), new PartyCombatantBinding("hero.a", a) },
                Array.Empty<GameObject>(), StartReason.PlayerFirstHit, Side.Allies, 10, 3);
            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.Snapshots.Select(x => x.CharacterId), Is.EqualTo(new[] { "hero.a", "hero.b" }));
            Assert.That(result.Snapshots[0].Level, Is.EqualTo(2)); Assert.That(result.Snapshots[0].InitialHp, Is.EqualTo(20));
            Assert.That(result.Snapshots[0].SkillIds[0], Is.EqualTo(3));
            Assert.That(a.GetComponent<CombatSkillLoadoutComponent>().SkillIds[0], Is.EqualTo(3));
            Assert.That(a.GetComponent<CombatHpComponent>().HP, Is.EqualTo(20));
        }

        [Test]
        public void CombatAdapter_RejectsMissingDefinition()
        {
            CharacterProgressionService progression = CreateProgression(CreateDefinition("known", 1, 5, 10));
            PartyRuntime party = new GameObject("Party").AddComponent<PartyRuntime>(); party.AddMember("missing"); party.SelectForCombat("missing", true);
            PartyCombatBuildResult result = new CharacterPartyCombatAdapter().BuildRequest(party, progression,
                new[] { new PartyCombatantBinding("missing", CreateCombatActor("actor", 10, 10, 1)) }, Array.Empty<GameObject>(), StartReason.PlayerFirstHit, Side.Allies, 10, 3);
            Assert.That(result.Success, Is.False); Assert.That(result.FailureReason, Does.Contain("no authored definition"));
        }

        [Test]
        public void Bootstrap_InstallsOnePersistentPartyAndRepeatedCallsPreserveState()
        {
            RuntimeBootstrapper bootstrapper = new GameObject("Bootstrap").AddComponent<RuntimeBootstrapper>(); Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);
            PartyRuntime party = UnityEngine.Object.FindFirstObjectByType<PartyRuntime>(); party.AddMember("hero"); Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);
            Assert.That(UnityEngine.Object.FindObjectsByType<PartyRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1)); Assert.That(party.Contains("hero"), Is.True);
            Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Assets/GAME/Scripts/NonCombat/Party/PartyRuntime.cs")), Does.Contain("DontDestroyOnLoad(gameObject)"));
        }

        private static GameObject CreateCombatActor(string name, int hp, int maxHp, params int[] skills)
        { GameObject actor = new(name); CombatHpComponent source = actor.AddComponent<CombatHpComponent>(); source.MaxHP = maxHp; source.HP = hp; CombatSkillLoadoutComponent loadout = actor.AddComponent<CombatSkillLoadoutComponent>(); SerializedObject serialized = new(loadout); SerializedProperty ids = serialized.FindProperty("skillIds"); ids.arraySize = skills.Length; for (int i = 0; i < skills.Length; i++) ids.GetArrayElementAtIndex(i).intValue = skills[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); return actor; }
        private static CharacterProgressionService CreateProgression(params CharacterProgressionDefinitionSO[] definitions)
        { CharacterProgressionService service = new GameObject("Progression").AddComponent<CharacterProgressionService>(); Invoke(service, "ConfigureForTests", null, definitions); return service; }
        private static CharacterProgressionDefinitionSO CreateDefinition(string id, int start, int max, params int[] curve)
        { CharacterProgressionDefinitionSO definition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>(); SerializedObject serialized = new(definition); serialized.FindProperty("characterId").stringValue = id; serialized.FindProperty("startingLevel").intValue = start; serialized.FindProperty("maximumLevel").intValue = max; SerializedProperty values = serialized.FindProperty("experienceRequiredByLevel"); values.arraySize = curve.Length; for (int i = 0; i < curve.Length; i++) values.GetArrayElementAtIndex(i).intValue = curve[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); return definition; }
        private static GameSaveData Migrate(string json) { Type type = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator"); object[] args = { json, null, false, null }; Assert.That((bool)type.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True, args[3] as string); return (GameSaveData)args[1]; }
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Cleanup()
        {
            HashSet<GameObject> gameObjects = new();
            foreach (MonoBehaviour value in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (value == null || EditorUtility.IsPersistent(value)) continue;
                string componentNamespace = value.GetType().Namespace;
                if (!string.IsNullOrEmpty(componentNamespace) && componentNamespace.StartsWith("Game.", StringComparison.Ordinal))
                    gameObjects.Add(value.gameObject);
            }

            foreach (GameObject value in gameObjects)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);

            foreach (CharacterProgressionDefinitionSO value in Resources.FindObjectsOfTypeAll<CharacterProgressionDefinitionSO>())
                if (!EditorUtility.IsPersistent(value)) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
