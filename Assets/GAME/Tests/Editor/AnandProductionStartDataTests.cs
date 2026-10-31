using System;
using System.Linq;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class AnandProductionStartDataTests
    {
        private const string Folder = "Assets/GAME/Data/Character";
        private const string ProgressPath = Folder + "/CP_AnandDeMorte.asset";
        private const string StartPath = Folder + "/CharacterStart_AnandDeMorte.asset";
        private const string Id = "char.anand_de_morte";
        private GameObject _host;

        // Explicit one-time authoring command; it never overwrites existing files or saves unrelated assets.
        public static void CreateApprovedAssets()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ProgressPath) != null || AssetDatabase.LoadMainAssetAtPath(StartPath) != null ||
                System.IO.File.Exists(ProgressPath) || System.IO.File.Exists(StartPath))
                throw new InvalidOperationException("Character asset path already exists; inspect it rather than overwrite it.");
            if (AssetDatabase.FindAssets("t:CharacterProgressionDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>)
                .Any(value => value != null && value.CharacterId == Id))
                throw new InvalidOperationException("Character ID already exists.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GAME/Data", "Character");
            var progress = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
            var p = new SerializedObject(progress);
            p.FindProperty("characterId").stringValue = Id;
            p.FindProperty("startingLevel").intValue = 1;
            p.FindProperty("maximumLevel").intValue = 60;
            var curve = p.FindProperty("experienceRequiredByLevel"); curve.arraySize = 59;
            for (int i = 0; i < 59; i++) curve.GetArrayElementAtIndex(i).intValue = (i + 1) * 100;
            p.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(progress, ProgressPath);
            var start = ScriptableObject.CreateInstance<CharacterStartDefinitionSO>();
            var s = new SerializedObject(start);
            var definitions = s.FindProperty("definitions"); definitions.arraySize = 1; definitions.GetArrayElementAtIndex(0).objectReferenceValue = progress;
            foreach (string field in new[] { "initialMemberIds", "initialCombatMemberIds" })
            { var values = s.FindProperty(field); values.arraySize = 1; values.GetArrayElementAtIndex(0).stringValue = Id; }
            s.FindProperty("initialLeaderId").stringValue = Id;
            s.FindProperty("defaultRewardTargetId").stringValue = Id;
            s.ApplyModifiedPropertiesWithoutUndo();
            if (!start.TryValidate(out string message)) throw new InvalidOperationException(message);
            AssetDatabase.CreateAsset(start, StartPath);
            AssetDatabase.SetLabels(progress, new[] { "PlaytestV1", "GrowthBalancePending" });
            AssetDatabase.SetLabels(start, new[] { "PlaytestV1", "ProductionWiringPending" });
            AssetDatabase.SaveAssetIfDirty(progress); AssetDatabase.SaveAssetIfDirty(start);
        }

        [TearDown] public void TearDown() { if (_host != null) Object.DestroyImmediate(_host); }

        [Test]
        public void AuthoredAssets_HaveUniqueIdentityAndEveryApprovedExperienceInterval()
        {
            var progress = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>(ProgressPath);
            var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
            Assert.That(progress, Is.Not.Null); Assert.That(start, Is.Not.Null);
            Assert.That(progress.CharacterId, Is.EqualTo(Id)); Assert.That(progress.StartingLevel, Is.EqualTo(1)); Assert.That(progress.MaximumLevel, Is.EqualTo(60));
            var serialized = new SerializedObject(progress).FindProperty("experienceRequiredByLevel");
            Assert.That(serialized.arraySize, Is.EqualTo(59));
            for (int level = 1; level <= 59; level++)
            { Assert.That(progress.TryGetRequiredExperience(level, out int required), Is.True); Assert.That(required, Is.EqualTo(100 * level)); }
            Assert.That(progress.TryGetRequiredExperience(60, out _), Is.False);
            Assert.That(start.TryValidate(out string message), Is.True, message);
            Assert.That(start.Definitions.Single(), Is.SameAs(progress));
            Assert.That(start.InitialMemberIds, Is.EqualTo(new[] { Id })); Assert.That(start.InitialCombatMemberIds, Is.EqualTo(new[] { Id }));
            Assert.That(start.InitialLeaderId, Is.EqualTo(Id)); Assert.That(start.DefaultRewardTargetId, Is.EqualTo(Id));
            var all = AssetDatabase.FindAssets("t:CharacterProgressionDefinitionSO", new[] { "Assets/GAME" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>).Where(value => value != null && value.CharacterId == Id).ToArray();
            Assert.That(all, Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.AssetPathToGUID(ProgressPath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(StartPath)));
        }

        [Test]
        public void ExistingPartyProgressionAndSaveOwners_UseTheAuthoredDataAndCapExperience()
        {
            var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
            _host = new GameObject("AnandStartFixture");
            var party = _host.AddComponent<PartyRuntime>(); var progress = _host.AddComponent<CharacterProgressionService>();
            Assert.That(party.TryConfigureStartDefinition(start, out string partyError), Is.True, partyError);
            Assert.That(progress.TryConfigureStartDefinition(start, out string progressError), Is.True, progressError);
            Assert.That(party.Members, Is.EqualTo(new[] { Id })); Assert.That(party.LeaderCharacterId, Is.EqualTo(Id)); Assert.That(party.CombatLineup, Is.EqualTo(new[] { Id }));
            Assert.That(progress.TryGetState(Id, out int level, out int exp), Is.True); Assert.That(level, Is.EqualTo(1)); Assert.That(exp, Is.Zero);
            progress.ApplyExperience(Id, 350);
            Assert.That(progress.TryGetState(Id, out level, out exp), Is.True); Assert.That(level, Is.EqualTo(3)); Assert.That(exp, Is.EqualTo(50));
            var save = new GameSaveData(); party.CaptureSaveData(save); progress.CaptureSaveData(save);
            party.ResetForNewGame(); progress.ResetForNewGame(); party.RestoreSaveData(save); progress.RestoreSaveData(save);
            Assert.That(party.LeaderCharacterId, Is.EqualTo(Id)); Assert.That(progress.TryGetState(Id, out level, out exp), Is.True);
            Assert.That(level, Is.EqualTo(3)); Assert.That(exp, Is.EqualTo(50));
            var applied = progress.ApplyExperience(Id, 200000);
            Assert.That(applied.AppliedExperience, Is.EqualTo(176650));
            Assert.That(progress.TryGetState(Id, out level, out exp), Is.True); Assert.That(level, Is.EqualTo(60)); Assert.That(exp, Is.Zero);
            Assert.That(progress.ApplyExperience(Id, 100).AppliedExperience, Is.Zero);
        }
    }
}
