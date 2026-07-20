using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Core;
using Game.Input;
using Game.Quest;
using Game.Story;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class DebugLegacySeparationTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [TestCase("Assets/GAME/Scripts/Core", "Game.Debugging")]
        [TestCase("Assets/GAME/Scripts/Combat/Runtime", "Game.Combat.Debugging")]
        [TestCase("Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs", "DialogueRunner")]
        [TestCase("Assets/GAME/Scripts/Quest", "DemoMissionRuntime")]
        [TestCase("Assets/GAME/Scripts/Core", "UnityEditor")]
        public void ProductionSources_DoNotReferenceForbiddenOwners(string relativeDirectory, string forbiddenText)
        {
            string target = Path(relativeDirectory);
            IEnumerable<string> files = File.Exists(target)
                ? new[] { target }
                : Directory.GetFiles(target, "*.cs", SearchOption.AllDirectories);
            foreach (string file in files)
                Assert.That(File.ReadAllText(file), Does.Not.Contain(forbiddenText), file);
        }

        [Test]
        public void RuntimeBootstrapper_CreatesOnlyCanonicalServices()
        {
            string source = Read("Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs");
            Assert.That(source, Does.Not.Contain("AddComponent<CombatAutoPlanner"));
            Assert.That(source, Does.Not.Contain("AddComponent<StoryInteractionDebugHotkey"));
            Assert.That(source, Does.Not.Contain("DemoMission"));
            Assert.That(source, Does.Not.Contain("MissionManager"));
            Assert.That(source, Does.Not.Contain("SeamlessBattleManager"));
        }

        [TestCase("Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs")]
        [TestCase("Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs")]
        [TestCase("Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs")]
        [TestCase("Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs")]
        [TestCase("Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs")]
        public void ProductionSceneCapableDebugBehaviours_AreEditorGuarded(string relativePath)
        {
            Assert.That(Read(relativePath), Does.Contain("#if UNITY_EDITOR"), relativePath);
        }

        [Test]
        public void DebugHotkeys_AreNotPartOfInputService()
        {
            string source = Read("Assets/GAME/Scripts/Input/InputService.cs");
            Assert.That(source, Does.Not.Contain("F9"));
            Assert.That(source, Does.Not.Contain("F10"));
            Assert.That(source, Does.Not.Contain("Debug"));
            Assert.That(source, Does.Not.Contain("Keyboard.current"));
        }

        [Test]
        public void CanonicalInputOwner_RemainsUnique()
        {
            string installer = Read("Assets/GAME/Scripts/Input/GameInputInstaller.cs");
            Assert.That(installer, Does.Contain("new GameInput"));
            Assert.That(Directory.GetFiles(Path("Assets/GAME/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith("GameInputInstaller.cs"))
                .Select(File.ReadAllText), Has.None.Contains("new GameInput"));
        }

        [Test]
        public void ProductionEncounterSources_UseCombatEntryPoint()
        {
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs"),
                Does.Contain("CombatEntryPoint"));
            Assert.That(Read("Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs"),
                Does.Contain("CombatEntryPoint"));
        }

        [Test]
        public void DirectCombatSessionUtilities_AreConfinedToDebugAndTests()
        {
            IEnumerable<string> offenders = FindSourcesContaining("new CombatSession")
                .Where(file => !Normalize(file).Contains("/Tests/") && !Normalize(file).Contains("/Debugging/") &&
                               !Normalize(file).EndsWith("/CombatBootstrapper.cs"));
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void LegacyTransitionController_IsNeutralizedByCanonicalEntry()
        {
            string source = Read("Assets/GAME/Scripts/UI/BattleTransitionController.cs");
            Assert.That(source, Does.Contain("FindFirstObjectByType<CombatEntryPoint>"));
            Assert.That(source.IndexOf("FindFirstObjectByType<CombatEntryPoint>", StringComparison.Ordinal),
                Is.LessThan(source.IndexOf("SetState", StringComparison.Ordinal)));
        }

        [Test]
        public void CombatDemoController_UsesCanonicalFallbackDetection()
        {
            string source = Read("Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs");
            Assert.That(source, Does.Contain("_canonicalRouting"));
            Assert.That(source, Does.Contain("_canonicalWorldLifecycle"));
            Assert.That(source, Does.Not.Contain("new CombatSession"));
            Assert.That(source, Does.Not.Contain("SetState("));
        }

        [Test]
        public void CanonicalOwners_RetainExpectedPublicApis()
        {
            Assert.That(HasPublicMethod(typeof(CombatEntryPoint), "StartCombat"), Is.True);
            Assert.That(HasPublicMethod(typeof(GameFlowController), "RequestState"), Is.True);
            Assert.That(HasPublicMethod(typeof(StoryEventRunner), "TryStartEvent"), Is.True);
            Assert.That(HasPublicMethod(typeof(QuestRuntime), "ApplyEvent"), Is.True);
        }

        [Test]
        public void CompatibilityPublicApis_RemainCompiled()
        {
            Assert.That(FindType("Game.Story.Core.DialogueRunner"), Is.Not.Null);
            Assert.That(FindType("Game.Mission.MissionManager"), Is.Not.Null);
            Assert.That(FindType("Game.DemoMission.Runtime.DemoMissionRuntime"), Is.Not.Null);
        }

        [Test]
        public void RuntimeAssemblies_DoNotReferenceEditorOrTestAssemblies()
        {
            Assembly runtime = typeof(GameStateMachine).Assembly;
            Assert.That(runtime.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.StartsWith("UnityEditor"));
            Assert.That(runtime.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.Contains("nunit.framework"));
        }

        [Test]
        public void ExistingSerializedFieldNames_ArePreserved()
        {
            Assert.That(Read("Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs"), Does.Contain("f1StartPlayerFirstHit"));
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs"), Does.Contain("combatCanvasRoot"));
            Assert.That(Read("Assets/GAME/Scripts/UI/BattleTransitionController.cs"), Does.Contain("loadBattleSceneSingle"));
        }

        [Test]
        public void ProjectMetaGuids_AreUniqueExceptMalformedKnownIssue()
        {
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string meta in Directory.GetFiles(Path("Assets"), "*.meta", SearchOption.AllDirectories))
            {
                string guidLine = File.ReadLines(meta).FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal));
                if (guidLine == null)
                    continue;

                string guid = guidLine.Substring(6).Trim();
                if (guid == "6bd14988aa4a45a794929d9f59e463c")
                    continue;

                Assert.That(seen.TryGetValue(guid, out string prior), Is.False,
                    $"Duplicate GUID {guid}: {prior} and {meta}");
                seen.Add(guid, meta);
            }
        }

        private static IEnumerable<string> FindSourcesContaining(string value)
        {
            return Directory.GetFiles(Path("Assets/GAME"), "*.cs", SearchOption.AllDirectories)
                .Where(file => File.ReadAllText(file).Contains(value));
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static bool HasPublicMethod(Type type, string name)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                .Any(method => method.Name == name);
        }

        private static string Read(string relativePath) => File.ReadAllText(Path(relativePath));
        private static string Path(string relativePath) => System.IO.Path.Combine(ProjectRoot, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        private static string Normalize(string path) => path.Replace('\\', '/');
    }
}
