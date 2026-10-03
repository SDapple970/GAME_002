#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Game.Tests.Combat
{
    public sealed class CombatFinalExchangeProductionWiringTests
    {
        private const string ConfigPath = "Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset";
        private const string RuntimePrefabPath = "Assets/GAME/Prefabs/CombatRuntime.prefab";
        private const string UiPrefabPath = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
        private const string TemplateScenePath = "Assets/GAME/Scenes/Dungeon_Template.unity";
        private const string DungeonOneScenePath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";

        [Test]
        public void FinalRuntimeConfigAsset_HasApprovedFunctionalV1Values()
        {
            FinalCombatRuntimeConfigSO config = LoadConfig();
            CombatRuntimeConfig runtime = config.CreateRuntimeConfig();

            Assert.That(runtime.MaxMp, Is.EqualTo(3));
            Assert.That(runtime.InitialMp, Is.EqualTo(2));
            Assert.That(runtime.MaxPosture, Is.EqualTo(3));
            Assert.That(runtime.InitialPosture, Is.Zero);
            Assert.That(runtime.MpRecoveryPerSecond, Is.EqualTo(1f));
            Assert.That(runtime.PressureMax, Is.EqualTo(1f));
            Assert.That(runtime.PressurePerSecond, Is.EqualTo(0.25f));
            Assert.That(config.SupportsFinalExchangeSkills(), Is.True);
        }

        [Test]
        public void FunctionalV1Pressure_ReachesThresholdAfterFourSeconds()
        {
            CombatRuntimeConfig config = LoadConfig().CreateRuntimeConfig();
            StandoffRuntimeState pressure = new StandoffRuntimeState(config.PressureMax);

            Assert.That(pressure.Advance(config.PressurePerSecond * 4f), Is.True);
            Assert.That(pressure.CurrentPressure, Is.EqualTo(1f));
            Assert.That(pressure.IsPressureReady, Is.True);
        }

        [Test]
        public void CombatRuntimePrefab_ReferencesFinalRuntimeConfig()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath);
            CombatEntryPoint entry = prefab.GetComponent<CombatEntryPoint>();

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.FinalExchangeRuntimeConfig, Is.SameAs(LoadConfig()));
        }

        [Test]
        public void ExplicitFinalRequest_UsesAuthoredConfig_AndLegacyUsesCompatibility()
        {
            GameObject owner = new GameObject("Combat08.RuntimeConfigSelection");
            GameObject ally = new GameObject("Ally");
            GameObject enemy = new GameObject("Enemy");
            try
            {
                CombatEntryPoint entry = owner.AddComponent<CombatEntryPoint>();
                SetField(entry, "finalExchangeRuntimeConfig", LoadConfig());

                CombatRuntimeConfig finalConfig = Normalize(entry, CreateRequest(
                    CombatFlowMode.StandoffClashChain,
                    ally,
                    enemy));
                CombatRuntimeConfig legacyConfig = Normalize(entry, CreateRequest(
                    CombatFlowMode.LegacyPlanning,
                    ally,
                    enemy));

                Assert.That(finalConfig.MaxMp, Is.EqualTo(3));
                Assert.That(finalConfig.InitialMp, Is.EqualTo(2));
                Assert.That(legacyConfig.MaxMp, Is.Zero);
                Assert.That(legacyConfig.InitialMp, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(ally);
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void ProductionDungeonUi_HasOneCanonicalCombatRootAndFinalPanelWiring()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            CombatUIRootController combatRoot = prefab.GetComponentInChildren<CombatUIRootController>(true);
            CombatPlanningHUD planningHud = prefab.GetComponentInChildren<CombatPlanningHUD>(true);
            FinalCombatUIBinder binder = prefab.GetComponentInChildren<FinalCombatUIBinder>(true);

            Assert.That(prefab.GetComponentsInChildren<CombatUIRootController>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<CombatPlanningHUD>(true), Has.Length.EqualTo(1));
            Assert.That(binder, Is.Not.Null);
            Assert.That(planningHud, Is.Not.Null);
            Assert.That(binder.transform.IsChildOf(combatRoot.transform), Is.True);
            Assert.That(binder.gameObject.name, Is.EqualTo("FinalExchangePanel"));
            Assert.That(binder.gameObject.activeSelf, Is.False);
            Assert.That(Reference(combatRoot, "planningHUD"), Is.SameAs(planningHud));
            Assert.That(Reference(combatRoot, "finalCombatUI"), Is.SameAs(binder));
            Assert.That(prefab.GetComponentsInChildren<Canvas>(true)
                .Any(canvas => canvas.gameObject.name == "FinalCombatCanvas"), Is.False);
        }

        [Test]
        public void FinalBinder_HasAllRequiredExplicitReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            FinalCombatUIBinder binder = prefab.GetComponentInChildren<FinalCombatUIBinder>(true);
            string[] required =
            {
                "panelRoot", "actorListRoot", "skillListRoot", "targetListRoot", "handoffListRoot",
                "optionButtonPrefab", "confirmButton", "cancelButton", "noResponseButton",
                "continueButton", "handoffButton", "endButton", "displayFont", "phaseText", "contextText", "playerHpText", "enemyHpText", "playerMpText",
                "enemyMpText", "pressureText", "playerPostureText", "enemyPostureText"
            };

            Assert.That(binder, Is.Not.Null);
            for (int i = 0; i < required.Length; i++)
                Assert.That(Reference(binder, required[i]), Is.Not.Null, required[i]);

            Assert.That(Reference(binder, "confirmButton"), Is.TypeOf<Button>());
            Assert.That(Reference(binder, "noResponseButton"), Is.TypeOf<Button>());
            Assert.That(Reference(binder, "continueButton"), Is.TypeOf<Button>());
            Assert.That(Reference(binder, "handoffButton"), Is.TypeOf<Button>());
            Assert.That(Reference(binder, "endButton"), Is.TypeOf<Button>());
            Assert.That(Reference(binder, "contextText"), Is.AssignableTo<TMP_Text>());
            Assert.That(Reference(binder, "playerHpText"), Is.TypeOf<Text>());
            Assert.That(Reference(binder, "enemyHpText"), Is.TypeOf<Text>());
        }

        [Test]
        public void FinalBinder_FormatsCurrentAndMaxHpFromCombatantRuntimeState()
        {
            DummyCombatant player = new DummyCombatant(1, Side.Allies, 10, KeywordMask.None, 0);
            DummyCombatant enemy = new DummyCombatant(2, Side.Enemies, 8, KeywordMask.None, 0);
            MethodInfo formatHp = typeof(FinalCombatUIBinder).GetMethod(
                "FormatHp",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(formatHp, Is.Not.Null);
            Assert.That((string)formatHp.Invoke(null, new object[] { "PLAYER", player }),
                Is.EqualTo("PLAYER  HP 10 / 10"));

            enemy.ApplyDamage(3);

            Assert.That((string)formatHp.Invoke(null, new object[] { "ENEMY", enemy }),
                Is.EqualTo("ENEMY  HP 5 / 8"));
        }

        [TestCase(Phase.Standoff, null, "대치")]
        [TestCase(Phase.AttackDeclaration, null, "공격 준비")]
        [TestCase(Phase.AttackDeclaration, CombatExchangeDecisionKind.Response, "대응 선택")]
        [TestCase(Phase.Approach, null, "접근")]
        [TestCase(Phase.Clash, null, "합 진행")]
        [TestCase(Phase.ApplyOutcome, null, "공격 결과")]
        [TestCase(Phase.ChainDecision, null, "연계 선택")]
        public void FinalBinder_MapsInternalPhasesToPlayerFacingTitles(
            Phase phase,
            CombatExchangeDecisionKind? decisionKind,
            string expected)
        {
            MethodInfo method = typeof(FinalCombatUIBinder).GetMethod(
                "GetPhaseTitle",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That((string)method.Invoke(null, new object[] { phase, decisionKind }), Is.EqualTo(expected));
        }

        [Test]
        public void DungeonTemplate_IsExplicitFinalExchangeValidationTarget()
        {
            Scene scene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);
            try
            {
                CombatEncounterTrigger2D[] encounters = UnityEngine.Object.FindObjectsByType<CombatEncounterTrigger2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                Assert.That(scene.IsValid(), Is.True);
                Assert.That(encounters.Length, Is.GreaterThan(0));
                for (int i = 0; i < encounters.Length; i++)
                {
                    Assert.That(Reference(encounters[i], "flowMode"), Is.EqualTo(CombatFlowMode.StandoffClashChain));
                    AssertSharedGroupFlowMode(encounters[i]);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void DungeonOneProduction_CutsOverAllEncounterEntriesToFinalExchange()
        {
            Scene scene = EditorSceneManager.OpenScene(DungeonOneScenePath, OpenSceneMode.Single);
            try
            {
                CombatEncounterTrigger2D[] encounters = UnityEngine.Object.FindObjectsByType<CombatEncounterTrigger2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                Assert.That(scene.IsValid(), Is.True);
                Assert.That(encounters, Has.Length.EqualTo(3));
                Assert.That(encounters.Select(encounter => encounter.EncounterId), Is.EquivalentTo(new[]
                {
                    "dungeon1.encounter.01",
                    "dungeon1.encounter.02",
                    "dungeon1.encounter.03"
                }));
                for (int i = 0; i < encounters.Length; i++)
                {
                    Assert.That(Reference(encounters[i], "flowMode"), Is.EqualTo(CombatFlowMode.StandoffClashChain));
                    AssertSharedGroupFlowMode(encounters[i]);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void ImplicitCombatStartRequest_RemainsLegacyPlanningForCompatibility()
        {
            CombatStartRequest request = new CombatStartRequest(
                StartReason.PlayerFirstHit,
                Side.Allies,
                0,
                -1,
                null);

            Assert.That(request.FlowMode, Is.EqualTo(CombatFlowMode.LegacyPlanning));
        }

        private static FinalCombatRuntimeConfigSO LoadConfig()
        {
            FinalCombatRuntimeConfigSO config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath);
            Assert.That(config, Is.Not.Null);
            return config;
        }

        private static CombatStartRequest CreateRequest(
            CombatFlowMode mode,
            GameObject ally,
            GameObject enemy)
        {
            CombatStartRequest request = new CombatStartRequest(
                StartReason.PlayerFirstHit,
                Side.Allies,
                0,
                -1,
                null,
                mode);
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);
            return request;
        }

        private static CombatRuntimeConfig Normalize(CombatEntryPoint entry, CombatStartRequest request)
        {
            MethodInfo method = typeof(CombatEntryPoint).GetMethod(
                "TryNormalizeRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { request, null, null, null };
            Assert.That((bool)method.Invoke(entry, arguments), Is.True, arguments[2] as string);
            object normalized = arguments[1];
            FieldInfo runtimeConfig = normalized.GetType().GetField("RuntimeConfig");
            return (CombatRuntimeConfig)runtimeConfig.GetValue(normalized);
        }

        private static object Reference(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void AssertSharedGroupFlowMode(CombatEncounterTrigger2D trigger)
        {
            CombatEncounterGroup group = Reference(trigger, "encounterGroup") as CombatEncounterGroup;
            Assert.That(group, Is.Not.Null, trigger.EncounterId);
            Assert.That(Reference(group, "useEncounterFlowMode"), Is.EqualTo(true), trigger.EncounterId);
            Assert.That(Reference(group, "encounterFlowMode"),
                Is.EqualTo(CombatFlowMode.StandoffClashChain), trigger.EncounterId);
        }
    }
}
#endif
