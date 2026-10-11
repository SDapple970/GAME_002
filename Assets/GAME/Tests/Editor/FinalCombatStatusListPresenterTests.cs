#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    public sealed class FinalCombatStatusListPresenterTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Render_ShowsBuffIdentityStackAndRemainingExchangeDuration()
        {
            Fixture fixture = CreateFixture();
            CombatStatusDefinitionSO status = CreateStatus(
                "focus", "Focus", CombatStatusPolarity.Buff, CombatStatusStackPolicy.Stack, 3, 2);
            fixture.AllyState.ApplyStatus(status);
            fixture.AllyState.ApplyStatus(status);

            fixture.Presenter.Render(fixture.Session);

            StringAssert.Contains("ALLY 1 | BUFF | Focus [focus] x2 | EX 2 | ACTIVE", fixture.Text.text);
            Assert.That(fixture.Root.activeSelf, Is.True);
        }

        [Test]
        public void Render_ShowsDebuffAndSafelyFallsBackToStatusIdForMissingDisplayName()
        {
            Fixture fixture = CreateFixture();
            fixture.EnemyState.ApplyStatus(CreateStatus(
                "exposed", string.Empty, CombatStatusPolarity.Debuff, CombatStatusStackPolicy.Replace, 1, 3));

            fixture.Presenter.Render(fixture.Session);

            StringAssert.Contains("ENEMY 2 | DEBUFF | exposed [exposed] x1 | EX 3 | ACTIVE", fixture.Text.text);
        }

        [Test]
        public void Render_IdentifiesNonExpiringStatuses()
        {
            Fixture fixture = CreateFixture();
            fixture.AllyState.ApplyStatus(CreateStatus(
                "aura", "Aura", CombatStatusPolarity.Buff, CombatStatusStackPolicy.Replace, 1, 0));

            fixture.Presenter.Render(fixture.Session);

            StringAssert.Contains("Aura [aura] x1 | PERSIST | ACTIVE", fixture.Text.text);
        }

        [Test]
        public void Render_RemovesExpiredStatusFromList()
        {
            Fixture fixture = CreateFixture();
            fixture.AllyState.ApplyStatus(CreateStatus(
                "brief", "Brief", CombatStatusPolarity.Buff, CombatStatusStackPolicy.Replace, 1, 1));
            fixture.Presenter.Render(fixture.Session);
            Assert.That(fixture.Root.activeSelf, Is.True);

            AdvanceDurations(fixture.AllyState);
            fixture.Presenter.Render(fixture.Session);

            Assert.That(fixture.Text.text, Is.Empty);
            Assert.That(fixture.Root.activeSelf, Is.False);
        }

        [Test]
        public void BinderExchangeRefresh_UsesSnapshotsAndClearsOnCombatEnd()
        {
            Fixture fixture = CreateFixture();
            fixture.AllyState.ApplyStatus(CreateStatus(
                "guard", "Guard", CombatStatusPolarity.Buff, CombatStatusStackPolicy.Replace, 1, 2));
            GameObject binderObject = Track(new GameObject("Binder"));
            FinalCombatUIBinder binder = binderObject.AddComponent<FinalCombatUIBinder>();
            SetField(binder, "statusListPresenter", fixture.Presenter);
            SetField(binder, "_session", fixture.Session);
            SetField(binder, "_stateMachine", fixture.StateMachine);
            SetField(binder, "_controller", new FinalCombatPlayerCommandController());

            Invoke(binder, "HandleExchangeStateChanged", 1);
            StringAssert.Contains("Guard [guard]", fixture.Text.text);

            binder.Unbind();
            Assert.That(fixture.Text.text, Is.Empty);
            Assert.That(fixture.Root.activeSelf, Is.False);
        }

        [Test]
        public void Render_NewSessionDoesNotExposePreviousSessionStatuses()
        {
            Fixture first = CreateFixture();
            first.AllyState.ApplyStatus(CreateStatus(
                "old", "Old", CombatStatusPolarity.Buff, CombatStatusStackPolicy.Replace, 1, 2));
            first.Presenter.Render(first.Session);
            StringAssert.Contains("Old [old]", first.Text.text);

            CombatSession second = CreateSession(CombatFlowMode.StandoffClashChain, out _, out _);
            first.Presenter.Render(second);

            Assert.That(first.Text.text, Is.Empty);
            Assert.That(first.Root.activeSelf, Is.False);
        }

        [Test]
        public void MissingOptionalReferences_AndLegacySession_DoNotThrowOrLeaveStatusVisible()
        {
            FinalCombatStatusListPresenter unconfigured =
                Track(new GameObject("UnconfiguredPresenter")).AddComponent<FinalCombatStatusListPresenter>();
            CombatSession legacy = CreateSession(CombatFlowMode.LegacyPlanning, out TestCombatant ally, out _);
            legacy.GetCombatState(ally).ApplyStatus(CreateStatus(
                "ignored", "Ignored", CombatStatusPolarity.Buff, CombatStatusStackPolicy.Replace, 1, 2));

            Assert.DoesNotThrow(() => unconfigured.Render(legacy));
            Assert.DoesNotThrow(unconfigured.Clear);
        }

        private Fixture CreateFixture()
        {
            CombatSession session = CreateSession(CombatFlowMode.StandoffClashChain, out TestCombatant ally, out TestCombatant enemy);
            GameObject root = Track(new GameObject("StatusRoot"));
            GameObject textObject = Track(new GameObject("StatusText"));
            textObject.transform.SetParent(root.transform);
            Text text = textObject.AddComponent<Text>();
            FinalCombatStatusListPresenter presenter = root.AddComponent<FinalCombatStatusListPresenter>();
            SetField(presenter, "statusRoot", root);
            SetField(presenter, "statusText", text);
            return new Fixture(session, new Game.Combat.Core.CombatStateMachine(session), ally, enemy, presenter, root, text);
        }

        private static CombatSession CreateSession(
            CombatFlowMode flowMode,
            out TestCombatant ally,
            out TestCombatant enemy)
        {
            ally = new TestCombatant(1, Side.Allies);
            enemy = new TestCombatant(2, Side.Enemies);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                flowMode,
                new CombatRuntimeConfig(5, 3, 3, 0));
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            session.InitializeCombatStates(session.RuntimeConfig);
            return session;
        }

        private CombatStatusDefinitionSO CreateStatus(
            string id,
            string displayName,
            CombatStatusPolarity polarity,
            CombatStatusStackPolicy policy,
            int maximumStacks,
            int duration)
        {
            CombatStatusDefinitionSO status = ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            _created.Add(status);
            SetField(status, "statusId", id);
            SetField(status, "displayName", displayName);
            SetField(status, "polarity", polarity);
            SetField(status, "stackPolicy", policy);
            SetField(status, "maximumStacks", maximumStacks);
            SetField(status, "durationKind", CombatStatusDurationKind.Explicit);
            SetField(status, "explicitDuration", duration);
            return status;
        }

        private static void AdvanceDurations(CombatantCombatState state)
        {
            MethodInfo method = typeof(CombatantCombatState).GetMethod(
                "AdvanceStatusDurations",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(state, new object[] { null });
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, arguments);
        }

        private sealed class Fixture
        {
            public Fixture(
                CombatSession session,
                Game.Combat.Core.CombatStateMachine stateMachine,
                TestCombatant ally,
                TestCombatant enemy,
                FinalCombatStatusListPresenter presenter,
                GameObject root,
                Text text)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                Enemy = enemy;
                Presenter = presenter;
                Root = root;
                Text = text;
            }

            public CombatSession Session { get; }
            public Game.Combat.Core.CombatStateMachine StateMachine { get; }
            public TestCombatant Ally { get; }
            public TestCombatant Enemy { get; }
            public FinalCombatStatusListPresenter Presenter { get; }
            public GameObject Root { get; }
            public Text Text { get; }
            public CombatantCombatState AllyState => Session.GetCombatState(Ally);
            public CombatantCombatState EnemyState => Session.GetCombatState(Enemy);
        }

        private sealed class TestCombatant : ICombatant
        {
            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP { get; private set; } = 10;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => Array.Empty<ISkill>();
            public void ApplyDamage(int amount) => HP = Math.Max(0, HP - Math.Max(0, amount));
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
