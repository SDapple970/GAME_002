#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Guards the CBT-01 production ownership contract without creating a second combat path.
    /// These tests deliberately inspect source boundaries that Unity cannot express as runtime assertions.
    /// </summary>
    public sealed class CombatProductionOwnershipTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void ProductionFieldEntries_DelegateToCombatEntryPoint()
        {
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs"),
                Does.Contain("entryPoint.StartCombat(request)"));
            Assert.That(Read("Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs"),
                Does.Contain("entryPoint.StartCombat(request)"));
        }

        [Test]
        public void ProductionRuntime_CreatesCombatSessionsOnlyThroughCanonicalEntry()
        {
            IEnumerable<string> offenders = Directory
                .GetFiles(Path("Assets/GAME/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(file => !Normalize(file).Contains("/Debugging/"))
                .Where(file => !Normalize(file).Contains("/Legacy/"))
                .Where(file => !Normalize(file).EndsWith("/CombatEntryPoint.cs"))
                .Where(file => File.ReadAllText(file).Contains("CombatBootstrapper.StartCombat("));

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void CoreSimulation_DoesNotReferenceSceneObjects()
        {
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs"),
                Does.Not.Contain("GameObject"));
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs"),
                Does.Not.Contain("GameObject"));
        }

        [Test]
        public void ResolutionAndPresentation_OwnDifferentResponsibilities()
        {
            string resolver = Read("Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs");
            string director = Read("Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs");

            Assert.That(resolver, Does.Contain("ApplyDamageAndStagger"));
            Assert.That(resolver, Does.Not.Contain("Animator"));
            Assert.That(resolver, Does.Not.Contain("AudioSource"));
            Assert.That(director, Does.Contain("PlayResolution"));
            Assert.That(director, Does.Contain("CombatantAnimationDriver"));
            Assert.That(director, Does.Not.Contain("ApplyDamage("));
        }

        [Test]
        public void CombatResult_UsesRewardServiceAndGameFlowInsteadOfDirectProgressionMutation()
        {
            string binder = Read("Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs");
            string entry = Read("Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs");
            string resultBuilder = Read("Assets/GAME/Scripts/Combat/Runtime/Core/CombatResultBuilder.cs");

            Assert.That(binder, Does.Contain("RewardService.CreateCombatRewardRequest"));
            Assert.That(binder, Does.Contain("TryHandleCombatResult"));
            Assert.That(binder, Does.Contain("TryHandleRewardClosed"));
            Assert.That(entry, Does.Not.Contain("InventoryService"));
            Assert.That(entry, Does.Not.Contain("CurrencyWallet"));
            Assert.That(entry, Does.Not.Contain("QuestRuntime"));
            Assert.That(resultBuilder, Does.Not.Contain("RewardService"));
        }

        [Test]
        public void PlayerPlanningAndEnemyDeterminism_HaveExplicitOwners()
        {
            string planningHud = Read("Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs");
            string flow = Read("Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs");
            string autoPlanner = Read("Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs");

            Assert.That(planningHud, Does.Contain("SubmitPlayerDraftAndAdvance"));
            Assert.That(planningHud, Does.Contain("confirmButton.onClick.AddListener(Confirm)"));
            Assert.That(flow, Does.Contain("BuildDeterministicEnemyPlan"));
            Assert.That(flow, Does.Contain("_session.TurnIndex % enemy.Skills.Count"));
            Assert.That(autoPlanner, Does.Contain("#if UNITY_EDITOR"));
        }

        private static string Read(string relativePath) => File.ReadAllText(Path(relativePath));

        private static string Path(string relativePath) =>
            System.IO.Path.Combine(ProjectRoot, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

        private static string Normalize(string path) => path.Replace('\\', '/');
    }
}
#endif
