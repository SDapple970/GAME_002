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
using Game.Combat.Environment;
using Game.Combat.Integration;
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
    [Category("CBT_ENEMY_04A")]
    public sealed class FinalCombatFeintCueTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void ExplicitCue_IsOptInDeduplicatedAndLeavesFinalExchangeStateUntouched()
        {
            BoundFixture enabled = CreateBoundFixture(feintEnabled: true);
            List<CombatFinalPresentationCue> cues = new();
            enabled.Director.FinalPresentationCueRaised += cues.Add;

            CombatantCombatState enemyState = enabled.Session.GetCombatState(enabled.Enemy);
            int version = enabled.Session.ExchangeState.Version;
            Phase phase = enabled.StateMachine.Phase;
            float pressure = enabled.Session.StandoffState.CurrentPressure;
            int mp = enemyState.CurrentMp;
            CombatExchangeDecisionRequest pending = enabled.Orchestrator.PendingDecisionRequest;

            Assert.That(enabled.Orchestrator.TryRequestFeintCue(enabled.Enemy.Id, version), Is.True);
            Assert.That(enabled.Orchestrator.TryRequestFeintCue(enabled.Enemy.Id, version), Is.False,
                "A cue may be displayed once per enemy and exchange version.");

            Assert.That(cues, Has.Count.EqualTo(1));
            Assert.That(cues[0].Kind, Is.EqualTo(CombatFinalPresentationCueKind.Feint));
            Assert.That(cues[0].Actor, Is.SameAs(enabled.Enemy));
            Assert.That(cues[0].Target, Is.Null);
            Assert.That(cues[0].Skill, Is.Null);
            Assert.That(enabled.StateMachine.Phase, Is.EqualTo(phase));
            Assert.That(enabled.Session.ExchangeState.Version, Is.EqualTo(version));
            Assert.That(enabled.Session.ExchangeState.CurrentDeclaration, Is.Null);
            Assert.That(enabled.Session.ExchangeState.CurrentResponse, Is.Null);
            Assert.That(enabled.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(enemyState.CurrentMp, Is.EqualTo(mp));
            Assert.That(enabled.Orchestrator.PendingDecisionRequest, Is.SameAs(pending));

            BoundFixture disabled = CreateBoundFixture(feintEnabled: false);
            Assert.That(disabled.Orchestrator.TryRequestFeintCue(
                disabled.Enemy.Id,
                disabled.Session.ExchangeState.Version), Is.False);
        }

        [Test]
        public void Cue_IsRejectedForStaleBindingAndDoesNotLeakToNextSession()
        {
            BoundFixture first = CreateBoundFixture(feintEnabled: true);
            List<CombatFinalPresentationCue> cues = new();
            first.Director.FinalPresentationCueRaised += cues.Add;
            int firstVersion = first.Session.ExchangeState.Version;
            Assert.That(first.Orchestrator.TryRequestFeintCue(first.Enemy.Id, firstVersion), Is.True);
            Assert.That(cues, Has.Count.EqualTo(1));

            first.Director.UnbindFinalExchange();
            first.Orchestrator.BindSession(null);
            Assert.That(first.Orchestrator.TryRequestFeintCue(first.Enemy.Id, firstVersion), Is.False);
            Assert.That(cues, Has.Count.EqualTo(1));

            BoundFixture second = CreateBoundFixture(feintEnabled: true);
            second.Director.FinalPresentationCueRaised += cues.Add;
            Assert.That(cues, Has.Count.EqualTo(1), "A new Director binding must not replay cues from the prior session.");
            Assert.That(second.Orchestrator.TryRequestFeintCue(
                second.Enemy.Id,
                second.Session.ExchangeState.Version), Is.True);
            Assert.That(cues, Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ExplicitCue_ReachesProductionDirectorWithoutChangingCommandOrUnopposedResolution()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(GameStateMachine.Instance, Is.Not.Null);
            Assert.That(GameFlowController.Instance, Is.Not.Null);

            RuntimeFixture fixture = CreateRuntimeFixture();
            List<CombatFinalPresentationCue> cues = new();
            fixture.Director.FinalPresentationCueRaised += cues.Add;
            Assert.That(fixture.Entry.StartCombat(fixture.Request), Is.True);

            CombatSession session = fixture.Entry.ActiveSession;
            ICombatant enemy = session.Enemies[0];
            ICombatant ally = session.Allies[0];
            int version = session.ExchangeState.Version;
            int enemyMp = session.GetCombatState(enemy).CurrentMp;
            float pressure = session.StandoffState.CurrentPressure;
            CombatExchangeDecisionRequest pending = fixture.Orchestrator.PendingDecisionRequest;
            int cueCountBeforeFeint = cues.Count;

            Assert.That(fixture.Orchestrator.TryRequestFeintCue(enemy.Id, version), Is.True);
            Assert.That(cues, Has.Count.EqualTo(cueCountBeforeFeint + 1));
            CombatFinalPresentationCue feintCue = cues[cues.Count - 1];
            Assert.That(feintCue.Kind, Is.EqualTo(CombatFinalPresentationCueKind.Feint));
            Assert.That(feintCue.Actor, Is.SameAs(enemy));
            Assert.That(session.ExchangeState.Version, Is.EqualTo(version));
            Assert.That(session.ExchangeState.CurrentDeclaration, Is.Null);
            Assert.That(session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(enemyMp));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest, Is.SameAs(pending));

            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(ally, enemy, fixture.AllyFinisher), version), Is.True);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (fixture.Result == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(fixture.Result, Is.Not.Null, "The ordinary FinalExchange command did not complete.");
            Assert.That(fixture.Result.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(fixture.Entry.ActiveSession, Is.Null);
            Assert.That(cues.FindAll(cue => cue.Kind == CombatFinalPresentationCueKind.Feint), Has.Count.EqualTo(1));

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        private BoundFixture CreateBoundFixture(bool feintEnabled)
        {
            SkillDefinitionSO allySkill = Skill(1, damage: 10, mpCost: 0);
            SkillDefinitionSO enemySkill = Skill(2, damage: 1, mpCost: 3);
            FinalCombatEnemyBehaviorProfileSO profile = Profile(feintEnabled);
            GameObject ally = Actor("Ally", 10, allySkill);
            GameObject enemy = Actor("Enemy", 10, enemySkill);
            AttachEnemyDefinition(enemy, profile, "test.feint.bound");

            CombatStartRequest request = Request(Side.Allies, ally, enemy);
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);
            SkillBook book = Book(allySkill, enemySkill);
            CombatSession session = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book)).session;
            CombatStateMachine stateMachine = new(session);
            CombatFlowOrchestrator orchestrator = Track(new GameObject("Feint.Orchestrator"))
                .AddComponent<CombatFlowOrchestrator>();
            CombatDirector director = Track(new GameObject("Feint.Director")).AddComponent<CombatDirector>();

            Assert.That(orchestrator.BindFinalExchange(session, stateMachine, new ZeroRandom(), new ClashInputs()), Is.True);
            Assert.That(director.BindFinalExchange(orchestrator, session), Is.True);
            return new BoundFixture(session, stateMachine, orchestrator, director, session.Allies[0], session.Enemies[0]);
        }

        private RuntimeFixture CreateRuntimeFixture()
        {
            SkillDefinitionSO allySkill = Skill(1, damage: 10, mpCost: 0);
            SkillDefinitionSO enemySkill = Skill(2, damage: 1, mpCost: 3);
            FinalCombatEnemyBehaviorProfileSO profile = Profile(feintEnabled: true);
            GameObject ally = Actor("Runtime.Ally", 10, allySkill);
            GameObject enemy = Actor("Runtime.Enemy", 10, enemySkill);
            AttachEnemyDefinition(enemy, profile, "test.feint.runtime");

            CombatFlowOrchestrator orchestrator = Track(new GameObject("Runtime.Orchestrator"))
                .AddComponent<CombatFlowOrchestrator>();
            CombatDirector director = Track(new GameObject("Runtime.Director")).AddComponent<CombatDirector>();
            CombatEntryPoint entry = Track(new GameObject("Runtime.Entry")).AddComponent<CombatEntryPoint>();
            FinalCombatRuntimeConfigSO config = Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>());
            SetField(config, "maxMp", 2);
            SetField(config, "initialMp", 2);
            SetField(config, "mpRecoveryPerSecond", 0f);
            SetField(config, "pressureMax", 10f);
            SetField(config, "pressurePerSecond", 0.1f);

            SkillBook book = Book(allySkill, enemySkill);
            SetField(entry, "flowOrchestrator", orchestrator);
            SetField(entry, "director", director);
            SetField(entry, "finalExchangeRuntimeConfig", config);
            SetField(entry, "_book", book);
            SetField(orchestrator, "entryPoint", entry);
            SetField(director, "entryPoint", entry);

            RuntimeFixture fixture = new(
                entry,
                orchestrator,
                director,
                Request(Side.Allies, ally, enemy),
                book.Get(new SkillId(allySkill.skillId)));
            entry.OnCombatEnded += result => fixture.Result = result;
            return fixture;
        }

        private CombatStartRequest Request(Side initiative, GameObject ally, GameObject enemy)
        {
            CombatStartRequest request = new(
                StartReason.PlayerFirstHit,
                initiative,
                10,
                3,
                null,
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 2, 3, 0, 0f, 10f, 0f));
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);
            return request;
        }

        private FinalCombatEnemyBehaviorProfileSO Profile(bool feintEnabled)
        {
            FinalCombatEnemyBehaviorProfileSO profile = Track(ScriptableObject.CreateInstance<FinalCombatEnemyBehaviorProfileSO>());
            SerializedObject serialized = new(profile);
            serialized.FindProperty("feintCueEnabled").boolValue = feintEnabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private GameObject Actor(string name, int hp, SkillDefinitionSO skill)
        {
            GameObject actor = Track(new GameObject(name));
            CombatHpComponent health = actor.AddComponent<CombatHpComponent>();
            health.MaxHP = hp;
            health.HP = hp;
            CombatSkillLoadoutComponent loadout = actor.AddComponent<CombatSkillLoadoutComponent>();
            SetField(loadout, "skillIds", new[] { skill.skillId });
            return actor;
        }

        private void AttachEnemyDefinition(GameObject enemy, FinalCombatEnemyBehaviorProfileSO profile, string key)
        {
            EnemyDefinitionSO definition = Track(ScriptableObject.CreateInstance<EnemyDefinitionSO>());
            SetField(definition, "persistentKey", key);
            SetField(definition, "finalCombatBehaviorProfile", profile);
            EnemySourceComponent source = enemy.AddComponent<EnemySourceComponent>();
            SetField(source, "definition", definition);
        }

        private SkillDefinitionSO Skill(int id, int damage, int mpCost)
        {
            SkillDefinitionSO skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            skill.skillId = id;
            skill.baseDamage = damage;
            skill.targeting = TargetingRule.SingleEnemy;
            SetField(skill, "finalMpCost", mpCost);
            return skill;
        }

        private static SkillBook Book(params SkillDefinitionSO[] definitions)
        {
            SkillBook book = new();
            for (int i = 0; i < definitions.Length; i++)
                book.Register(new SoSkill(definitions[i]));
            return book;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }

        private sealed class BoundFixture
        {
            public BoundFixture(CombatSession session, CombatStateMachine stateMachine, CombatFlowOrchestrator orchestrator, CombatDirector director, ICombatant ally, ICombatant enemy)
            {
                Session = session;
                StateMachine = stateMachine;
                Orchestrator = orchestrator;
                Director = director;
                Ally = ally;
                Enemy = enemy;
            }

            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public CombatDirector Director { get; }
            public ICombatant Ally { get; }
            public ICombatant Enemy { get; }
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(CombatEntryPoint entry, CombatFlowOrchestrator orchestrator, CombatDirector director, CombatStartRequest request, ISkill allyFinisher)
            {
                Entry = entry;
                Orchestrator = orchestrator;
                Director = director;
                Request = request;
                AllyFinisher = allyFinisher;
            }

            public CombatEntryPoint Entry { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public CombatDirector Director { get; }
            public CombatStartRequest Request { get; }
            public ISkill AllyFinisher { get; }
            public CombatResult Result { get; set; }
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }

        private sealed class ClashInputs : ICombatClashSideInputProvider
        {
            public bool TryCreate(ICombatant actor, ISkill skill, bool isInitiativeOwner, out CombatClashSideInput input)
            {
                input = new CombatClashSideInput(actor.Id, skill.Id, 1, skill.Speed, isInitiativeOwner);
                return true;
            }
        }
    }
}
#endif
