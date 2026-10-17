#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Model;
using Game.Core;
using Game.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Runs the authored enemy behavior profile through the production FinalExchange entry path
    /// in an unsaved Play Mode scene. Test assets exist only for this fixture.
    /// </summary>
    public sealed class FinalCombatEnemyBehaviorPlayModeIntegrationTests
    {
        private const string AppliedStatusId = "test.enemy.behavior.applied";
        private readonly List<UnityEngine.Object> _created = new();
        private readonly List<string> _runtimeErrors = new();

        [UnityTest]
        [Category("CBT_ENEMY_02V")]
        public IEnumerator ProfileSnapshot_ControlsRealEnemyCommandAndFinalExchangeResolution_InActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(GameStateMachine.Instance, Is.Not.Null);
            Assert.That(GameFlowController.Instance, Is.Not.Null);

            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            try
            {
                RuntimeFixture conditional = CreateFixture("Conditional", enemyHp: 5);
                Assert.That(conditional.Entry.StartCombat(conditional.Request), Is.True);
                CombatSession conditionalSession = conditional.Entry.ActiveSession;
                Assert.That(conditionalSession, Is.Not.Null);
                Assert.That(conditionalSession.TryGetEnemyBehavior(conditionalSession.Enemies[0], out EnemyCombatBehaviorSnapshot snapshot), Is.True);
                Assert.That(snapshot.AttackPriorities[0].SkillId, Is.EqualTo(2));

                ConfigureProfile(conditional.Profile, (1, 1f));
                Assert.That(snapshot.AttackPriorities[0].SkillId, Is.EqualTo(2),
                    "The active session must retain its entry snapshot after the authored Profile changes.");

                yield return ResolveEnemyOpeningAndWin(conditional, expectedSkillId: 2, expectStatus: true);

                GameFlowController.Instance.EnterExploration();
                RuntimeFixture aboveThreshold = CreateFixture("AboveThreshold", enemyHp: 10);
                Assert.That(aboveThreshold.Entry.StartCombat(aboveThreshold.Request), Is.True);
                yield return ResolveEnemyOpeningAndWin(aboveThreshold, expectedSkillId: 1, expectStatus: false);

                Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            }
            finally
            {
                Application.logMessageReceived -= CaptureRuntimeError;
            }

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        private IEnumerator ResolveEnemyOpeningAndWin(
            RuntimeFixture fixture,
            int expectedSkillId,
            bool expectStatus)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            CombatSession session = fixture.Entry.ActiveSession;
            while ((session.ExchangeState.CurrentDeclaration == null ||
                    fixture.Orchestrator.PendingDecisionRequest?.Kind != CombatExchangeDecisionKind.Response ||
                    fixture.Orchestrator.PendingDecisionRequest.ActingSide != Side.Allies) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            CombatAttackDeclaration declaration = session.ExchangeState.CurrentDeclaration;
            Assert.That(declaration, Is.Not.Null, "Enemy command did not submit an attack declaration.");
            Assert.That(declaration.Skill.Id.Value, Is.EqualTo(expectedSkillId));
            int declarationVersion = session.ExchangeState.Version;
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(declaration, declarationVersion), Is.False,
                "A command already accepted for this exchange must not be submitted again.");

            Assert.That(fixture.Orchestrator.SubmitNoResponse(declarationVersion), Is.True);
            while ((session.GetCombatState(fixture.EnemyCombatant).CurrentMp != 0 ||
                    session.GetCombatState(fixture.AllyCombatant).CurrentHp >= 10 ||
                    (expectStatus && !session.GetCombatState(fixture.AllyCombatant).HasStatus(AppliedStatusId))) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(session.GetCombatState(fixture.EnemyCombatant).CurrentMp, Is.EqualTo(0));
            Assert.That(session.GetCombatState(fixture.AllyCombatant).CurrentHp, Is.LessThan(10));
            Assert.That(session.GetCombatState(fixture.AllyCombatant).HasStatus(AppliedStatusId), Is.EqualTo(expectStatus));

            while ((fixture.Orchestrator.PendingDecisionRequest?.Kind != CombatExchangeDecisionKind.Attack ||
                    fixture.Orchestrator.PendingDecisionRequest.ActingSide != Side.Allies) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            CombatExchangeDecisionRequest playerRequest = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(playerRequest, Is.Not.Null, "FinalExchange did not return attack authority to the ally.");
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.AllyCombatant, fixture.EnemyCombatant, fixture.AllySkill),
                playerRequest.ExchangeVersion), Is.True);

            while (fixture.Result == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(fixture.Result, Is.Not.Null, "CombatEntryPoint did not publish a terminal result.");
            Assert.That(fixture.Result.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(fixture.Entry.ActiveSession, Is.Null);
            Assert.That(fixture.Entry.ActiveStateMachine, Is.Null);
        }

        private RuntimeFixture CreateFixture(string label, int enemyHp)
        {
            SkillDefinitionSO basic = Skill(1, "Basic", damage: 1, finalMpCost: 2);
            CombatStatusDefinitionSO status = Status(AppliedStatusId);
            SkillDefinitionSO conditional = Skill(2, "Conditional", damage: 3, finalMpCost: 2, status);
            SkillDefinitionSO allyFinisher = Skill(3, "Finisher", damage: 10, finalMpCost: 0);
            FinalCombatEnemyBehaviorProfileSO profile = Profile((2, 0.5f));
            EnemyDefinitionSO definition = EnemyDefinition(profile, $"test.enemy.behavior.{label}");

            GameObject ally = Actor($"{label}.Ally", 10, new[] { allyFinisher });
            GameObject enemy = Actor($"{label}.Enemy", enemyHp, new[] { basic, conditional });
            EnemySourceComponent source = enemy.AddComponent<EnemySourceComponent>();
            SetField(source, "definition", definition);

            CombatFlowOrchestrator orchestrator = Track(new GameObject($"{label}.Orchestrator"))
                .AddComponent<CombatFlowOrchestrator>();
            CombatDirector director = Track(new GameObject($"{label}.Director")).AddComponent<CombatDirector>();
            CombatEntryPoint entry = Track(new GameObject($"{label}.Entry")).AddComponent<CombatEntryPoint>();
            FinalCombatRuntimeConfigSO config = RuntimeConfig();
            SkillBook book = new();
            book.Register(new SoSkill(basic));
            book.Register(new SoSkill(conditional));
            book.Register(new SoSkill(allyFinisher));
            SetField(entry, "flowOrchestrator", orchestrator);
            SetField(entry, "director", director);
            SetField(entry, "finalExchangeRuntimeConfig", config);
            SetField(entry, "_book", book);
            SetField(orchestrator, "entryPoint", entry);
            SetField(director, "entryPoint", entry);

            CombatStartRequest request = new(
                StartReason.PlayerFirstHit,
                Side.Enemies,
                10,
                3,
                null,
                CombatFlowMode.StandoffClashChain,
                config.CreateRuntimeConfig());
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);

            RuntimeFixture fixture = new(entry, orchestrator, request, profile, book.Get(new SkillId(3)));
            entry.OnCombatStarted += session =>
            {
                fixture.AllyCombatant = session.Allies[0];
                fixture.EnemyCombatant = session.Enemies[0];
            };
            entry.OnCombatEnded += result => fixture.Result = result;
            return fixture;
        }

        private GameObject Actor(string name, int hp, IReadOnlyList<SkillDefinitionSO> skills)
        {
            GameObject actor = Track(new GameObject(name));
            CombatHpComponent health = actor.AddComponent<CombatHpComponent>();
            health.MaxHP = 10;
            health.HP = hp;
            CombatSkillLoadoutComponent loadout = actor.AddComponent<CombatSkillLoadoutComponent>();
            int[] ids = new int[skills.Count];
            for (int i = 0; i < skills.Count; i++)
                ids[i] = skills[i].skillId;
            SetField(loadout, "skillIds", ids);
            return actor;
        }

        private SkillDefinitionSO Skill(
            int id,
            string displayName,
            int damage,
            int finalMpCost,
            CombatStatusDefinitionSO appliedStatus = null)
        {
            SkillDefinitionSO skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            skill.skillId = id;
            skill.displayName = displayName;
            skill.baseDamage = damage;
            skill.targeting = TargetingRule.SingleEnemy;
            SetField(skill, "finalMpCost", finalMpCost);
            SetField(skill, "appliedStatusEffects", appliedStatus == null
                ? Array.Empty<CombatStatusDefinitionSO>()
                : new[] { appliedStatus });
            return skill;
        }

        private CombatStatusDefinitionSO Status(string id)
        {
            CombatStatusDefinitionSO status = Track(ScriptableObject.CreateInstance<CombatStatusDefinitionSO>());
            SetField(status, "statusId", id);
            SetField(status, "durationKind", CombatStatusDurationKind.UntilRemoved);
            return status;
        }

        private FinalCombatEnemyBehaviorProfileSO Profile(params (int skillId, float maximumHpRatio)[] priorities)
        {
            FinalCombatEnemyBehaviorProfileSO profile = Track(ScriptableObject.CreateInstance<FinalCombatEnemyBehaviorProfileSO>());
            ConfigureProfile(profile, priorities);
            return profile;
        }

        private EnemyDefinitionSO EnemyDefinition(FinalCombatEnemyBehaviorProfileSO profile, string key)
        {
            EnemyDefinitionSO definition = Track(ScriptableObject.CreateInstance<EnemyDefinitionSO>());
            SetField(definition, "persistentKey", key);
            SetField(definition, "finalCombatBehaviorProfile", profile);
            return definition;
        }

        private FinalCombatRuntimeConfigSO RuntimeConfig()
        {
            FinalCombatRuntimeConfigSO config = Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>());
            SetField(config, "maxMp", 2);
            SetField(config, "initialMp", 2);
            SetField(config, "mpRecoveryPerSecond", 0f);
            SetField(config, "pressureMax", 1f);
            SetField(config, "pressurePerSecond", 0.1f);
            return config;
        }

        private static void ConfigureProfile(
            FinalCombatEnemyBehaviorProfileSO profile,
            params (int skillId, float maximumHpRatio)[] priorities)
        {
            SerializedObject serialized = new(profile);
            SerializedProperty values = serialized.FindProperty("attackPriorities");
            values.arraySize = priorities.Length;
            for (int i = 0; i < priorities.Length; i++)
            {
                SerializedProperty item = values.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("skillId").intValue = priorities[i].skillId;
                item.FindPropertyRelative("maximumHpRatio").floatValue = priorities[i].maximumHpRatio;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }

        private void CaptureRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _runtimeErrors.Add($"{type}: {condition}\n{stackTrace}");
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(
                CombatEntryPoint entry,
                CombatFlowOrchestrator orchestrator,
                CombatStartRequest request,
                FinalCombatEnemyBehaviorProfileSO profile,
                ISkill allySkill)
            {
                Entry = entry;
                Orchestrator = orchestrator;
                Request = request;
                Profile = profile;
                AllySkill = allySkill;
            }

            public CombatEntryPoint Entry { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public CombatStartRequest Request { get; }
            public FinalCombatEnemyBehaviorProfileSO Profile { get; }
            public ISkill AllySkill { get; }
            public ICombatant AllyCombatant { get; set; }
            public ICombatant EnemyCombatant { get; set; }
            public CombatResult Result { get; set; }
        }
    }
}
#endif
