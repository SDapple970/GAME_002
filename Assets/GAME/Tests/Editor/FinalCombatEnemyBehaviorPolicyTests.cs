#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Combat
{
    [Category("CBT_ENEMY_02V")]
    public sealed class FinalCombatEnemyBehaviorPolicyTests
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
        public void NoProfile_DelegatesToFirstValidSelection()
        {
            Fixture fixture = CreateFixture(enemyHp: 10, profile: null, Skill(1), Skill(2));
            ConditionalFinalCombatEnemyDecisionPolicy conditional = new();
            FirstValidFinalCombatEnemyDecisionPolicy baseline = new();

            Assert.That(conditional.TryCreateAttack(fixture.Session, fixture.Enemy, out CombatAttackDeclaration conditionalDeclaration), Is.True);
            Assert.That(baseline.TryCreateAttack(fixture.Session, fixture.Enemy, out CombatAttackDeclaration baselineDeclaration), Is.True);
            Assert.That(conditionalDeclaration.Attacker, Is.SameAs(baselineDeclaration.Attacker));
            Assert.That(conditionalDeclaration.Target, Is.SameAs(baselineDeclaration.Target));
            Assert.That(conditionalDeclaration.Skill, Is.SameAs(baselineDeclaration.Skill));
            Assert.That(conditionalDeclaration.Skill.Id.Value, Is.EqualTo(1));
        }

        [Test]
        public void MatchingHpProfile_PrefersItsUsableSkill()
        {
            FinalCombatEnemyBehaviorProfileSO profile = Profile((2, 0.5f));
            Fixture fixture = CreateFixture(enemyHp: 5, profile: profile, Skill(1), Skill(2));

            Assert.That(new ConditionalFinalCombatEnemyDecisionPolicy().TryCreateAttack(
                fixture.Session, fixture.Enemy, out CombatAttackDeclaration declaration), Is.True);
            Assert.That(declaration.Skill.Id.Value, Is.EqualTo(2));
            Assert.That(declaration.Target, Is.SameAs(fixture.Ally));
        }

        [Test]
        public void AuthoredPriority_UsesOnlyCardPresentInEnemyHand()
        {
            Fixture fixture = CreateFixture(enemyHp: 5,
                profile: Profile((2, 1f)), Skill(1), Skill(2));
            ICombatant enemy = fixture.Enemy;
            Assert.That(fixture.Session.TryEnableHandValidation(enemy,
                new[] { new CombatHandEntry(enemy.Skills[0], CombatCardKind.Normal) },
                1, 0, 23), Is.True);

            Assert.That(AttackSkillId(fixture), Is.EqualTo(1));
        }

        [Test]
        public void UnmetOrUnusablePriority_FallsBackToAnotherLegalSkill()
        {
            FinalCombatEnemyBehaviorProfileSO thresholdProfile = Profile((2, 0.5f));
            Fixture aboveThreshold = CreateFixture(enemyHp: 6, profile: thresholdProfile, Skill(1), Skill(2));
            Assert.That(AttackSkillId(aboveThreshold), Is.EqualTo(1));

            FinalCombatEnemyBehaviorProfileSO missingProfile = Profile((99, 1f));
            Fixture missingSkill = CreateFixture(enemyHp: 5, profile: missingProfile, Skill(1));
            Assert.That(AttackSkillId(missingSkill), Is.EqualTo(1));
        }

        [Test]
        public void InsufficientMpAndInvalidTargetPriority_AreNotDeclared()
        {
            FinalCombatEnemyBehaviorProfileSO expensiveProfile = Profile((2, 1f));
            Fixture insufficientMp = CreateFixture(
                enemyHp: 5,
                profile: expensiveProfile,
                Skill(1),
                Skill(2, mpCost: 6));
            Assert.That(AttackSkillId(insufficientMp), Is.EqualTo(1));

            FinalCombatEnemyBehaviorProfileSO invalidTargetProfile = Profile((2, 1f));
            Fixture invalidTarget = CreateFixture(
                enemyHp: 5,
                profile: invalidTargetProfile,
                Skill(1),
                Skill(2, targeting: TargetingRule.SingleAlly));
            Assert.That(AttackSkillId(invalidTarget), Is.EqualTo(1));
        }

        [Test]
        public void HpBoundaryAndOverlappingRules_AreDeterministic()
        {
            FinalCombatEnemyBehaviorProfileSO boundaryProfile = Profile((2, 0.5f));
            Fixture boundary = CreateFixture(enemyHp: 5, profile: boundaryProfile, Skill(1), Skill(2));
            Fixture above = CreateFixture(enemyHp: 6, profile: boundaryProfile, Skill(1), Skill(2));
            Assert.That(AttackSkillId(boundary), Is.EqualTo(2));
            Assert.That(AttackSkillId(above), Is.EqualTo(1));

            FinalCombatEnemyBehaviorProfileSO overlapProfile = Profile((3, 1f), (2, 1f));
            Fixture overlap = CreateFixture(enemyHp: 5, profile: overlapProfile, Skill(1), Skill(2), Skill(3));
            Assert.That(AttackSkillId(overlap), Is.EqualTo(3));
        }

        [Test]
        public void BehaviorSnapshot_IsSessionIsolatedAndDoesNotReuseDefinitionState()
        {
            FinalCombatEnemyBehaviorProfileSO profile = Profile((2, 0.5f));
            Fixture first = CreateFixture(enemyHp: 5, profile: profile, Skill(1), Skill(2));
            Fixture second = CreateFixture(enemyHp: 10, profile: profile, Skill(1), Skill(2));

            ConfigureProfile(profile, (1, 1f));

            Assert.That(AttackSkillId(first), Is.EqualTo(2));
            Assert.That(AttackSkillId(second), Is.EqualTo(1));
        }

        [Test]
        public void DifferentEnemyProfiles_AreIsolatedWithinOneSession()
        {
            SkillDefinitionSO firstSkill = Skill(1);
            SkillDefinitionSO secondSkill = Skill(2);
            SkillDefinitionSO thirdSkill = Skill(3);
            GameObject ally = Actor("Ally", 10);
            GameObject firstEnemy = Actor("First Enemy", 5);
            GameObject secondEnemy = Actor("Second Enemy", 5);
            AddLoadout(firstEnemy, new[] { firstSkill, secondSkill, thirdSkill });
            AddLoadout(secondEnemy, new[] { firstSkill, secondSkill, thirdSkill });
            AttachDefinition(firstEnemy, Profile((2, 1f)));
            AttachDefinition(secondEnemy, Profile((3, 1f)));

            CombatStartRequest request = new(
                StartReason.PlayerFirstHit,
                Side.Enemies,
                10,
                3,
                null,
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 5, 3, 0, 0f, 10f, 1f));
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(firstEnemy);
            request.EnemyFieldObjects.Add(secondEnemy);
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);

            SkillBook book = new();
            book.Register(new SoSkill(firstSkill));
            book.Register(new SoSkill(secondSkill));
            book.Register(new SoSkill(thirdSkill));
            CombatSession session = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book)).session;
            ConditionalFinalCombatEnemyDecisionPolicy policy = new();

            Assert.That(policy.TryCreateAttack(session, session.Enemies[0], out CombatAttackDeclaration first), Is.True);
            Assert.That(policy.TryCreateAttack(session, session.Enemies[1], out CombatAttackDeclaration second), Is.True);
            Assert.That(first.Skill.Id.Value, Is.EqualTo(2));
            Assert.That(second.Skill.Id.Value, Is.EqualTo(3));
        }

        [Test]
        public void DefaultEnemyController_SubmitsProfileSelectedAttackThroughExistingCommandPath()
        {
            Fixture fixture = CreateFixture(enemyHp: 5, profile: Profile((2, 0.5f)), Skill(1), Skill(2));
            GameObject owner = Track(new GameObject("FinalCombatEnemyBehaviorPolicyTests"));
            CombatFlowOrchestrator orchestrator = owner.AddComponent<CombatFlowOrchestrator>();
            CombatStateMachine stateMachine = new(fixture.Session);
            Assert.That(orchestrator.BindFinalExchange(
                fixture.Session,
                stateMachine,
                new ZeroRandom(),
                new ClashInputs()), Is.True);

            FinalCombatEnemyCommandController controller = new();
            Assert.That(controller.Bind(orchestrator, fixture.Session), Is.True);

            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration, Is.Not.Null);
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration.Skill.Id.Value, Is.EqualTo(2));
            controller.Unbind();
        }

        private int AttackSkillId(Fixture fixture)
        {
            Assert.That(new ConditionalFinalCombatEnemyDecisionPolicy().TryCreateAttack(
                fixture.Session, fixture.Enemy, out CombatAttackDeclaration declaration), Is.True);
            return declaration.Skill.Id.Value;
        }

        private Fixture CreateFixture(
            int enemyHp,
            FinalCombatEnemyBehaviorProfileSO profile,
            params SkillDefinitionSO[] skills)
        {
            GameObject ally = Actor("Ally", 10);
            GameObject enemy = Actor("Enemy", enemyHp);
            AddLoadout(enemy, skills);
            EnemyDefinitionSO definition = Track(ScriptableObject.CreateInstance<EnemyDefinitionSO>());
            SetDefinition(definition, profile);
            AttachDefinition(enemy, definition);

            CombatStartRequest request = new(
                StartReason.PlayerFirstHit,
                Side.Enemies,
                10,
                3,
                null,
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 5, 3, 0, 0f, 10f, 1f));
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);

            SkillBook book = new();
            for (int i = 0; i < skills.Length; i++)
                book.Register(new SoSkill(skills[i]));
            CombatSession session = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book)).session;
            return new Fixture(session, session.Allies[0], session.Enemies[0]);
        }

        private SkillDefinitionSO Skill(int id, int mpCost = 1, TargetingRule targeting = TargetingRule.SingleEnemy)
        {
            SkillDefinitionSO skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            skill.skillId = id;
            skill.targeting = targeting;
            SerializedObject serialized = new(skill);
            serialized.FindProperty("finalMpCost").intValue = mpCost;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static void SetDefinition(
            EnemyDefinitionSO definition,
            FinalCombatEnemyBehaviorProfileSO profile)
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("persistentKey").stringValue = "enemy.behavior.test";
            serialized.FindProperty("finalCombatBehaviorProfile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void AttachDefinition(GameObject enemy, FinalCombatEnemyBehaviorProfileSO profile)
        {
            EnemyDefinitionSO definition = Track(ScriptableObject.CreateInstance<EnemyDefinitionSO>());
            SetDefinition(definition, profile);
            AttachDefinition(enemy, definition);
        }

        private static void AttachDefinition(GameObject enemy, EnemyDefinitionSO definition)
        {
            EnemySourceComponent source = enemy.AddComponent<EnemySourceComponent>();
            SerializedObject serializedSource = new(source);
            serializedSource.FindProperty("definition").objectReferenceValue = definition;
            serializedSource.ApplyModifiedPropertiesWithoutUndo();
        }

        private FinalCombatEnemyBehaviorProfileSO Profile(params (int skillId, float maximumHpRatio)[] priorities)
        {
            FinalCombatEnemyBehaviorProfileSO profile = Track(ScriptableObject.CreateInstance<FinalCombatEnemyBehaviorProfileSO>());
            ConfigureProfile(profile, priorities);
            return profile;
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
                SerializedProperty element = values.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("skillId").intValue = priorities[i].skillId;
                element.FindPropertyRelative("maximumHpRatio").floatValue = priorities[i].maximumHpRatio;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject Actor(string name, int hp)
        {
            GameObject actor = Track(new GameObject(name));
            CombatHpComponent health = actor.AddComponent<CombatHpComponent>();
            health.MaxHP = 10;
            health.HP = hp;
            return actor;
        }

        private static void AddLoadout(GameObject actor, IReadOnlyList<SkillDefinitionSO> skills)
        {
            CombatSkillLoadoutComponent loadout = actor.AddComponent<CombatSkillLoadoutComponent>();
            SerializedObject serialized = new(loadout);
            SerializedProperty ids = serialized.FindProperty("skillIds");
            ids.arraySize = skills.Count;
            for (int i = 0; i < skills.Count; i++)
                ids.GetArrayElementAtIndex(i).intValue = skills[i].skillId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }

        private sealed class Fixture
        {
            public Fixture(CombatSession session, ICombatant ally, ICombatant enemy)
            {
                Session = session;
                Ally = ally;
                Enemy = enemy;
            }

            public CombatSession Session { get; }
            public ICombatant Ally { get; }
            public ICombatant Enemy { get; }
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
