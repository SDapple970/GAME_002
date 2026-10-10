using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
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
    public sealed class CharacterProductionStartupTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp] public void SetUp() => CleanupRuntime();
        [TearDown] public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            CleanupRuntime();
        }

        [Test]
        public void UnconfiguredStart_DoesNotInventCharacterOrInitialParty()
        {
            CharacterStartDefinitionSO start = Track(ScriptableObject.CreateInstance<CharacterStartDefinitionSO>());
            PartyRuntime party = Component<PartyRuntime>();
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            Assert.That(start.TryValidate(out _), Is.False);
            Assert.That(party.TryConfigureStartDefinition(start, out _), Is.False);
            Assert.That(progression.TryConfigureStartDefinition(start, out _), Is.False);
            Assert.That(party.Members, Is.Empty);
            Assert.That(progression.DefaultRewardTargetId, Is.Null);
        }

        [TestCase("definitions")]
        [TestCase("initialMemberIds")]
        [TestCase("initialLeaderId")]
        [TestCase("initialCombatMemberIds")]
        [TestCase("defaultRewardTargetId")]
        public void InvalidAuthoredReferences_AreRejectedBeforeStateMutation(string field)
        {
            CharacterStartDefinitionSO start = Start();
            if (field == "definitions") Set(start, field, new[] { start.Definitions[0], start.Definitions[0] });
            else if (field == "initialLeaderId" || field == "defaultRewardTargetId") Set(start, field, "unknown");
            else Set(start, field, new[] { "unknown", "unknown" });
            PartyRuntime party = Component<PartyRuntime>(); party.AddMember("saved.character");
            Assert.That(party.TryConfigureStartDefinition(start, out _), Is.False);
            Assert.That(party.Members, Is.EqualTo(new[] { "saved.character" }));
        }

        [Test]
        public void InvalidLaterExperienceCurve_IsRejected()
        {
            CharacterStartDefinitionSO start = Start();
            Set(start.Definitions[0], "experienceRequiredByLevel", new[] { 10, 0 });
            Assert.That(start.TryValidate(out string message), Is.False);
            Assert.That(message, Does.Contain("invalid EXP curve"));
        }

        [Test]
        public void NewGame_SeedsOrderedPartyAndResetsProgressionWithoutDuplicateMembers()
        {
            CharacterStartDefinitionSO start = Start();
            PartyRuntime party = Component<PartyRuntime>(); CharacterProgressionService progression = Component<CharacterProgressionService>();
            Assert.That(progression.TryConfigureStartDefinition(start, out _), Is.True);
            Assert.That(party.TryConfigureStartDefinition(start, out _), Is.True);
            Assert.That(party.Members, Is.EqualTo(new[] { "b02.test.a", "b02.test.b" }));
            Assert.That(party.LeaderCharacterId, Is.EqualTo("b02.test.a"));
            Assert.That(party.CombatLineup, Is.EqualTo(new[] { "b02.test.a" }));
            progression.ApplyExperience("b02.test.a", 17); party.AddMember("old.session");
            party.SetLeader("b02.test.b"); party.SelectForCombat("b02.test.b", true);
            for (int i = 0; i < 2; i++) { party.ResetForNewGame(); progression.ResetForNewGame(); }
            Assert.That(party.Members, Is.EqualTo(new[] { "b02.test.a", "b02.test.b" }));
            Assert.That(party.LeaderCharacterId, Is.EqualTo("b02.test.a"));
            Assert.That(party.CombatLineup, Is.EqualTo(new[] { "b02.test.a" }));
            State(progression, "b02.test.a", 1, 0);
        }

        [Test]
        public void RepeatedBootstrap_PreservesLevelExperienceAndChangedParty()
        {
            CharacterStartDefinitionSO start = Start();
            PartyRuntime party = Component<PartyRuntime>(); CharacterProgressionService progression = Component<CharacterProgressionService>();
            RuntimeBootstrapper bootstrap = Track(new GameObject("Bootstrap")).AddComponent<RuntimeBootstrapper>();
            Set(bootstrap, "characterStartDefinition", start);
            Invoke(bootstrap, "ConfigureCharacterServices", party, progression, false);
            progression.ApplyExperience("b02.test.a", 17); party.SetLeader("b02.test.b"); party.SelectForCombat("b02.test.a", false);
            Invoke(bootstrap, "ConfigureCharacterServices", party, progression, false);
            Invoke(bootstrap, "ConfigureCharacterServices", party, progression, false);
            State(progression, "b02.test.a", 2, 7);
            Assert.That(party.LeaderCharacterId, Is.EqualTo("b02.test.b")); Assert.That(party.CombatLineup, Is.Empty);
        }

        [Test]
        public void RestoredEmptyParty_IsNeverReseededByConfiguration()
        {
            PartyRuntime party = Component<PartyRuntime>(); party.RestoreSaveData(new GameSaveData());
            Assert.That(party.TryConfigureStartDefinition(Start(), out _), Is.True);
            Assert.That(party.Members, Is.Empty); Assert.That(party.CombatLineup, Is.Empty); Assert.That(party.LeaderCharacterId, Is.Null);
        }

        [Test]
        public void UnknownSavedIdentity_GainsItsAuthoredDefinitionWithoutLosingProgress()
        {
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            GameSaveData save = new(); save.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = "b02.test.a", level = 3, experience = 7 });
            progression.RestoreSaveData(save);
            Assert.That(progression.TryGetDefinition("b02.test.a", out _), Is.False);
            Assert.That(progression.TryConfigureStartDefinition(Start(), out _), Is.True);
            State(progression, "b02.test.a", 3, 7);
            Assert.That(progression.TryGetDefinition("b02.test.a", out _), Is.True);
        }

        [Test]
        public void ConflictingDefinitions_RejectWholeBootstrapConfiguration()
        {
            CharacterStartDefinitionSO start = Start();
            CharacterProgressionService progression = Component<CharacterProgressionService>(); PartyRuntime party = Component<PartyRuntime>();
            Invoke(progression, "ConfigureForTests", "b02.test.a", new[] { Definition("b02.test.a") });
            progression.ApplyExperience("b02.test.a", 3);
            RuntimeBootstrapper bootstrap = Track(new GameObject("Bootstrap")).AddComponent<RuntimeBootstrapper>(); Set(bootstrap, "characterStartDefinition", start);
            Invoke(bootstrap, "ConfigureCharacterServices", party, progression, false);
            Assert.That(party.StartDefinition, Is.Null); Assert.That(party.Members, Is.Empty);
            Assert.That(progression.StartDefinition, Is.Null); State(progression, "b02.test.a", 1, 3);
            Assert.That(progression.TryGetState("b02.test.b", out _, out _), Is.False);
        }

        [Test]
        public void ExistingTestHeroSave_IsPreservedAlongsideConfiguredCharacters()
        {
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            CharacterProgressionDefinitionSO legacy = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            Invoke(progression, "ConfigureForTests", "hero.test", new[] { legacy });
            GameSaveData save = new(); save.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = "hero.test", level = 2, experience = 5 });
            progression.RestoreSaveData(save);
            Assert.That(progression.TryConfigureStartDefinition(Start(), out _), Is.True);
            State(progression, "hero.test", 2, 5);
            Assert.That(progression.TryGetDefinition("hero.test", out CharacterProgressionDefinitionSO definition), Is.True);
            Assert.That(definition, Is.SameAs(legacy));
        }

        [Test]
        public void ColdDungeonConfiguration_WithLegacyDefinition_RestoresTestHeroCombatIdentity()
        {
            CharacterStartDefinitionSO start = Start();
            CharacterProgressionDefinitionSO legacy = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            Set(start, "definitions", new[] { start.Definitions[0], start.Definitions[1], legacy });
            PartyRuntime party = Component<PartyRuntime>(); CharacterProgressionService progression = Component<CharacterProgressionService>();
            party.TryConfigureStartDefinition(start, out _); progression.TryConfigureStartDefinition(start, out _);
            GameSaveData save = new(); save.party.memberIds.Add("hero.test"); save.party.leaderCharacterId = "hero.test"; save.party.selectedCombatMemberIds.Add("hero.test");
            save.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = "hero.test", level = 2, experience = 5 });
            party.RestoreSaveData(save); progression.RestoreSaveData(save);
            GameObject actor = Track(new GameObject("Player")); actor.AddComponent<CombatHpComponent>();
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null); request.AllyFieldObjects.Add(actor);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, actor, party, progression, out string message), Is.True, message);
            Assert.That(request.TryGetAllyCharacterId(actor, out string id), Is.True); Assert.That(id, Is.EqualTo("hero.test")); State(progression, "hero.test", 2, 5);
        }

        [Test]
        public void SchemaEleven_ColdRestoreAndRepeatedLoadPreservePartyProgressAndB01Flags()
        {
            CharacterStartDefinitionSO start = Start();
            PartyRuntime party = Component<PartyRuntime>(); CharacterProgressionService progression = Component<CharacterProgressionService>();
            party.TryConfigureStartDefinition(start, out _); progression.TryConfigureStartDefinition(start, out _);
            progression.ApplyExperience("b02.test.a", 17); party.SetLeader("b02.test.b"); party.SelectForCombat("b02.test.a", false); party.SelectForCombat("b02.test.b", true);
            GameSaveData save = new(); party.CaptureSaveData(save); progression.CaptureSaveData(save);
            save.story.flags.Add(new SaveBoolEntry { id = "b01.false", value = false }); save.story.intFlags.Add(new SaveIntEntry { id = "b01.count", value = 17 });
            GameSaveData restored = Migrate(JsonUtility.ToJson(save));
            Invoke(party, "OnDestroy"); Invoke(progression, "OnDestroy"); UnityEngine.Object.DestroyImmediate(party.gameObject); UnityEngine.Object.DestroyImmediate(progression.gameObject);
            party = Component<PartyRuntime>(); progression = Component<CharacterProgressionService>();
            party.TryConfigureStartDefinition(start, out _); progression.TryConfigureStartDefinition(start, out _);
            for (int i = 0; i < 2; i++)
            {
                party.AddMember("stale"); progression.ApplyExperience("b02.test.a", 99);
                party.RestoreSaveData(restored); progression.RestoreSaveData(restored);
                State(progression, "b02.test.a", 2, 7); Assert.That(party.Members, Is.EqualTo(new[] { "b02.test.a", "b02.test.b" }));
                Assert.That(party.LeaderCharacterId, Is.EqualTo("b02.test.b")); Assert.That(party.CombatLineup, Is.EqualTo(new[] { "b02.test.b" }));
            }
            Assert.That(restored.header.schemaVersion, Is.EqualTo(11)); Assert.That(restored.story.flags[0].value, Is.False); Assert.That(restored.story.intFlags[0].value, Is.EqualTo(17));
        }

        [TestCase(5)]
        [TestCase(10)]
        [TestCase(11)]
        public void PreviousSchemas_KeepTestHeroIdentityAndProgression(int schema)
        {
            string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":" + schema + "},\"party\":{\"memberIds\":[\"hero.test\"],\"memberLevels\":[{\"id\":\"hero.test\",\"value\":2}]},\"progression\":{\"characters\":[{\"characterId\":\"hero.test\",\"level\":2,\"experience\":5}]},\"story\":{\"flags\":[{\"id\":\"keep\",\"value\":true}]}}";
            GameSaveData save = Migrate(json);
            Assert.That(save.header.schemaVersion, Is.EqualTo(11)); Assert.That(save.party.memberIds, Contains.Item("hero.test"));
            Assert.That(save.progression.characters[0].characterId, Is.EqualTo("hero.test")); Assert.That(save.story.flags[0].value, Is.True);
        }

        [Test]
        public void SinglePlayerBinding_SnapshotsExpIdentityAndPreservesEncounterFlowAndFieldHp()
        {
            CharacterStartDefinitionSO start = Start();
            PartyRuntime party = Component<PartyRuntime>(); CharacterProgressionService progression = Component<CharacterProgressionService>();
            party.TryConfigureStartDefinition(start, out _); progression.TryConfigureStartDefinition(start, out _);
            GameObject actor = Track(new GameObject("Unrelated display name")); CombatHpComponent hp = actor.AddComponent<CombatHpComponent>(); hp.MaxHP = 30; hp.HP = 23;
            CombatStartRequest request = new(StartReason.PlayerGotHit, Side.Enemies, 3, 2, null, CombatFlowMode.StandoffClashChain); request.AllyFieldObjects.Add(actor);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, actor, party, progression, out string message), Is.True, message);
            Assert.That(request.TryGetAllyCharacterId(actor, out string id), Is.True); Assert.That(id, Is.EqualTo("b02.test.a"));
            Assert.That(request.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain)); Assert.That(hp.HP, Is.EqualTo(23)); Assert.That(hp.MaxHP, Is.EqualTo(30));
            SkillBook book = new(); SkillDefinitionSO skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); skill.skillId = 1; book.Register(new SoSkill(skill));
            CombatSession session = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book)).session;
            Assert.That(session.ProgressionTargetCharacterId, Is.EqualTo("b02.test.a"));
            party.SetLeader("b02.test.b"); // Authored default target is B too; entry identity must win.
            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory); result.TotalGold = 0; result.TotalExp = 17;
            RewardGrantRequest reward = RewardService.CreateCombatRewardRequest(result, null);
            Assert.That(reward.ProgressionTargetId, Is.EqualTo("b02.test.a"));
            RewardService rewards = Component<RewardService>(); Assert.That(rewards.GrantReward(reward).Exp, Is.EqualTo(17));
            State(progression, "b02.test.a", 2, 7); State(progression, "b02.test.b", 1, 0);
            Assert.That(rewards.GrantReward(reward).DuplicateBlocked, Is.True);
        }

        [Test]
        public void ConfiguredParty_RejectsMissingFieldBindingsInsteadOfStartingWrongAllies()
        {
            CharacterStartDefinitionSO start = Start();
            PartyRuntime party = Component<PartyRuntime>(); CharacterProgressionService progression = Component<CharacterProgressionService>();
            party.TryConfigureStartDefinition(start, out _); progression.TryConfigureStartDefinition(start, out _); party.SelectForCombat("b02.test.b", true);
            GameObject actor = Track(new GameObject("Player")); actor.AddComponent<CombatHpComponent>();
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null); request.AllyFieldObjects.Add(actor);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, actor, party, progression, out string message), Is.False);
            Assert.That(message, Does.Contain("explicit field binding")); Assert.That(request.TryGetAllyCharacterId(actor, out _), Is.False);
        }

        private CharacterStartDefinitionSO Start()
        {
            CharacterStartDefinitionSO start = Track(ScriptableObject.CreateInstance<CharacterStartDefinitionSO>());
            Set(start, "definitions", new[] { Definition("b02.test.a"), Definition("b02.test.b") });
            Set(start, "initialMemberIds", new[] { "b02.test.a", "b02.test.b" }); Set(start, "initialLeaderId", "b02.test.a");
            Set(start, "initialCombatMemberIds", new[] { "b02.test.a" }); Set(start, "defaultRewardTargetId", "b02.test.b");
            return start;
        }
        private CharacterProgressionDefinitionSO Definition(string id)
        {
            CharacterProgressionDefinitionSO definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            Set(definition, "characterId", id); Set(definition, "startingLevel", 1); Set(definition, "maximumLevel", 5); Set(definition, "experienceRequiredByLevel", new[] { 10, 20, 30, 40 });
            return definition;
        }
        private T Track<T>(T value) where T : UnityEngine.Object { _created.Add(value); return value; }
        private T Component<T>() where T : MonoBehaviour
        { T value = Track(new GameObject(typeof(T).Name)).AddComponent<T>(); Invoke(value, "Awake"); return value; }
        private static void State(CharacterProgressionService progression, string id, int level, int exp)
        { Assert.That(progression.TryGetState(id, out int actualLevel, out int actualExp), Is.True); Assert.That(actualLevel, Is.EqualTo(level)); Assert.That(actualExp, Is.EqualTo(exp)); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, args);
        private static GameSaveData Migrate(string json)
        { Type type = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator"); object[] args = { json, null, false, null }; Assert.That((bool)type.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True, args[3] as string); return (GameSaveData)args[1]; }
        private static void CleanupRuntime()
        {
            HashSet<GameObject> objects = new();
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (behaviour != null && !EditorUtility.IsPersistent(behaviour) && behaviour.GetType().Namespace?.StartsWith("Game.", StringComparison.Ordinal) == true) objects.Add(behaviour.gameObject);
            foreach (GameObject go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
