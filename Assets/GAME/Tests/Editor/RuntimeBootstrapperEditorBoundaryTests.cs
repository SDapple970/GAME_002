using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core;
using Game.EditorTools;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class RuntimeBootstrapperEditorBoundaryTests
    {
        private const string LegacyPath = "Assets/GAME/Data/Test/CP_TestHero.asset";

        [Test]
        public void LookupAssembly_DependsOnRuntime_WithoutReverseEditorOrTestReference()
        {
            var runtime = typeof(RuntimeBootstrapper).Assembly;
            var editor = typeof(RuntimeBootstrapperLegacyDefinitionLookup).Assembly;
            Assert.That(editor, Is.Not.SameAs(runtime));
            Assert.That(editor.GetName().Name, Is.EqualTo("Assembly-CSharp-Editor"));
            Assert.That(editor.GetReferencedAssemblies().Select(reference => reference.Name), Does.Contain(runtime.GetName().Name));
            var dependencies = runtime.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
            Assert.That(dependencies, Has.None.StartsWith("UnityEditor"));
            Assert.That(dependencies, Has.None.Contains("nunit"));
            Assert.That(dependencies, Has.No.Member(editor.GetName().Name));
        }

        [Test]
        public void UnconfiguredBootstrap_UsesEditorLookupOnce_WithoutSeedingSession()
        {
            using var fixture = new Fixture();
            var original = RuntimeBootstrapper.EditorLegacyDefinitionResolver;
            Assert.That(original, Is.Not.Null);
            int lookups = 0;
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = () => { lookups++; return original(); };
            fixture.Register(); fixture.Register();
            Assert.That(lookups, Is.EqualTo(1));
            fixture.AssertLookupOnly(true);
        }

        [Test]
        public void ExplicitReference_RegistersWithoutAnyEditorLookup()
        {
            using var fixture = new Fixture();
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = null;
            fixture.SetDefinition(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>(LegacyPath));
            fixture.Register();
            fixture.AssertLookupOnly(true);
        }

        [Test]
        public void InvalidExplicitReference_IsRejectedWithoutEditorFallback()
        {
            using var fixture = new Fixture();
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = () => throw new InvalidOperationException("Unexpected fallback for an explicit reference.");
            fixture.SetDefinition(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Character/CP_AnandDeMorte.asset"));
            LogAssert.Expect(LogType.Warning, new Regex("Legacy definition registration rejected: The existing hero.test definition is required"));
            fixture.Register(true);
            fixture.AssertLookupOnly(false);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOptionalLookup_DoesNotCreateCharacterState(bool providerPresent)
        {
            using var fixture = new Fixture();
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = providerPresent ? () => null : null;
            fixture.Register();
            fixture.AssertLookupOnly(false);
        }

        [Test]
        public void MissingEditorAsset_RetainsTheBootstrapRegistrationDiagnostic()
        {
            using var fixture = new Fixture();
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = () => null;
            LogAssert.Expect(LogType.Warning, new Regex("Legacy definition registration rejected: The existing hero.test definition is required"));
            fixture.Register(true);
            fixture.AssertLookupOnly(false);
        }

        [Test]
        public void EnterPlayCallback_ReinstallsLookupWhenDomainReloadIsDisabled()
        {
            using var fixture = new Fixture();
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = null;
            var callback = typeof(RuntimeBootstrapperLegacyDefinitionLookup).GetMethod("OnEnterPlayMode", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            Assert.That(callback.IsDefined(typeof(InitializeOnEnterPlayModeAttribute), false), Is.True);
            callback.Invoke(null, new object[] { EnterPlayModeOptions.DisableDomainReload });
            Assert.That(RuntimeBootstrapper.EditorLegacyDefinitionResolver, Is.Not.Null);
            fixture.Register();
            fixture.AssertLookupOnly(true);
        }

        [UnityTest]
        public IEnumerator UnconfiguredTemplate_EditorDirectStart_ResolvesLegacyWithoutGrant()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/Dungeon_Template.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            var progression = CharacterProgressionService.Instance;
            Assert.That(progression, Is.Not.Null);
            Assert.That(progression.TryGetDefinition("hero.test", out var definition), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(definition), Is.EqualTo(LegacyPath));
            Assert.That(progression.TryGetState("hero.test", out _, out _), Is.False);
            Assert.That(PartyRuntime.Instance.Members, Is.Empty);
            Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetAcquiredSkills("hero.test"), Is.Empty);
            Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetAcquiredSkills("char.anand_de_morte"), Is.Empty);
            yield return new ExitPlayMode();
        }

        // Optional command-line validation. Build output is a new external artifact, never a source asset.
        public static void BuildPlayerForBoundaryValidation()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-c2b2PlayerOutput");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new InvalidOperationException("An absolute external Player output directory is required.");
            string output = Path.GetFullPath(args[index + 1]);
            if (output.StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Player output must be outside Assets and must be empty.");
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(scenes, Is.EquivalentTo(new[] { ProductionInitializationConnectionTests.TitlePath, ProductionInitializationConnectionTests.DungeonPath }));
            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "GAME_002_EditorBoundary.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText(Path.Combine(output, "BuildEvidence.txt"),
                $"Result={report.summary.result}; Errors={report.summary.totalErrors}; Warnings={report.summary.totalWarnings}; Platform={report.summary.platform}; Output={report.summary.outputPath}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Player build failed: {report.summary.result}, errors={report.summary.totalErrors}.");
            Debug.Log($"[C2B-2] Player build succeeded: errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.");
        }

        [UnityTearDown]
        public IEnumerator CloseScene()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> _objects = new();
            private readonly Func<CharacterProgressionDefinitionSO> _originalResolver;
            private readonly RuntimeBootstrapper _bootstrap;
            private readonly CharacterProgressionService _progression;
            private readonly PartyRuntime _party;
            private readonly CharacterSkillSaveParticipant _skills;

            internal Fixture()
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _originalResolver = RuntimeBootstrapper.EditorLegacyDefinitionResolver;
                _progression = Create<CharacterProgressionService>();
                _party = Create<PartyRuntime>();
                _skills = Create<CharacterSkillSaveParticipant>();
                _bootstrap = Create<RuntimeBootstrapper>(false);
            }

            internal void SetDefinition(CharacterProgressionDefinitionSO definition)
            {
                var data = new SerializedObject(_bootstrap);
                data.FindProperty("legacyCharacterDefinition").objectReferenceValue = definition;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            internal void Register(bool warn = false) => typeof(RuntimeBootstrapper)
                .GetMethod("RegisterExistingLegacyDefinition", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_bootstrap, new object[] { _progression, warn });

            internal void AssertLookupOnly(bool registered)
            {
                Assert.That(_progression.TryGetDefinition("hero.test", out var definition), Is.EqualTo(registered));
                if (registered) Assert.That(definition, Is.SameAs(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>(LegacyPath)));
                Assert.That(_progression.TryGetState("hero.test", out _, out _), Is.False);
                Assert.That(_party.Members, Is.Empty);
                Assert.That(_skills.Runtime.GetAcquiredSkills("hero.test"), Is.Empty);
                Assert.That(_skills.Runtime.GetAcquiredSkills("char.anand_de_morte"), Is.Empty);
            }

            private T Create<T>(bool initialize = true) where T : Component
            {
                var host = new GameObject("TestOnly.EditorBoundary." + typeof(T).Name);
                _objects.Add(host); host.SetActive(false);
                T component = host.AddComponent<T>();
                if (initialize) typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
                return component;
            }

            public void Dispose()
            {
                for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
                RuntimeBootstrapper.EditorLegacyDefinitionResolver = _originalResolver;
            }
        }
    }
}
