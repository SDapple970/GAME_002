#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Input;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Input
{
    public sealed class InputOwnershipTests
    {
        private readonly List<GameObject> _createdObjects = new();

        private GameObject _stateObject;
        private GameObject _flowObject;
        private GameObject _installerObject;
        private GameStateMachine _stateMachine;
        private GameFlowController _flow;
        private GameInputInstaller _installer;

        [SetUp]
        public void SetUp()
        {
            DestroyExistingOwners();

            _stateObject = CreateInactiveObject("InputOwnershipState");
            _stateMachine = _stateObject.AddComponent<GameStateMachine>();
            InvokePrivate(_stateMachine, "Awake");

            _flowObject = CreateInactiveObject("InputOwnershipFlow");
            _flow = _flowObject.AddComponent<GameFlowController>();
            InvokePrivate(_flow, "Awake");

            _installerObject = CreateInactiveObject("InputOwnershipInstaller");
            _installer = _installerObject.AddComponent<GameInputInstaller>();
            InvokePrivate(_installer, "Awake");
            InvokePrivate(_installer, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (_installer != null)
                InvokePrivate(_installer, "OnDisable");

            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
            DestroyExistingOwners();
        }

        [Test]
        public void ExplorationMoveRoutesOnce()
        {
            int count = 0;
            Vector2 received = Vector2.zero;
            _installer.Service.Move += value =>
            {
                count++;
                received = value;
            };

            Process("ProcessMove", new Vector2(1f, 0.25f));

            Assert.That(count, Is.EqualTo(1));
            Assert.That(received, Is.EqualTo(new Vector2(1f, 0.25f)));
            Assert.That(_installer.Service.CurrentMove, Is.EqualTo(received));
        }

        [Test]
        public void ExplorationAttackRoutesOnce()
        {
            int count = 0;
            _installer.Service.Attack += () => count++;

            Process("ProcessAttack");

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void ExplorationInteractRoutesOnlyToExplorationInteract()
        {
            int exploration = 0;
            int dialogue = 0;
            _installer.Service.ExplorationInteract += () => exploration++;
            _installer.Service.DialogueAdvance += () => dialogue++;

            Process("ProcessInteract");

            Assert.That(exploration, Is.EqualTo(1));
            Assert.That(dialogue, Is.Zero);
        }

        [Test]
        public void DialogueInteractRoutesOnlyToDialogueAdvance()
        {
            _flow.BeginDialogue();
            int exploration = 0;
            int dialogue = 0;
            _installer.Service.ExplorationInteract += () => exploration++;
            _installer.Service.DialogueAdvance += () => dialogue++;

            Process("ProcessInteract");

            Assert.That(exploration, Is.Zero);
            Assert.That(dialogue, Is.EqualTo(1));
        }

        [Test]
        public void ChoiceInteractDoesNotAdvanceDialogue()
        {
            _flow.BeginDialogue();
            _flow.BeginChoice();
            int exploration = 0;
            int dialogue = 0;
            _installer.Service.ExplorationInteract += () => exploration++;
            _installer.Service.DialogueAdvance += () => dialogue++;

            Process("ProcessInteract");

            Assert.That(exploration, Is.Zero);
            Assert.That(dialogue, Is.Zero);
        }

        [Test]
        public void CombatPlanningRejectsExplorationInput()
        {
            _flow.EnterCombatPlanning();
            int attack = 0;
            int interact = 0;
            Vector2 received = Vector2.one;
            _installer.Service.Attack += () => attack++;
            _installer.Service.ExplorationInteract += () => interact++;
            _installer.Service.Move += value => received = value;

            Process("ProcessMove", Vector2.one);
            Process("ProcessAttack");
            Process("ProcessInteract");

            Assert.That(received, Is.EqualTo(Vector2.zero));
            Assert.That(_installer.Service.CurrentMove, Is.EqualTo(Vector2.zero));
            Assert.That(attack, Is.Zero);
            Assert.That(interact, Is.Zero);
        }

        [Test]
        public void CombatResolvingRejectsExplorationInput()
        {
            EnterCombatResolving();
            int attack = 0;
            int interact = 0;
            _installer.Service.Attack += () => attack++;
            _installer.Service.ExplorationInteract += () => interact++;

            Process("ProcessAttack");
            Process("ProcessInteract");

            Assert.That(attack, Is.Zero);
            Assert.That(interact, Is.Zero);
        }

        [Test]
        public void CombatResolvingRejectsDialogueAdvance()
        {
            EnterCombatResolving();
            int dialogue = 0;
            _installer.Service.DialogueAdvance += () => dialogue++;

            Process("ProcessInteract");

            Assert.That(dialogue, Is.Zero);
        }

        [Test]
        public void RewardRejectsExplorationInput()
        {
            _flow.EnterReward();
            int attack = 0;
            int interact = 0;
            _installer.Service.Attack += () => attack++;
            _installer.Service.ExplorationInteract += () => interact++;

            Process("ProcessAttack");
            Process("ProcessInteract");

            Assert.That(attack, Is.Zero);
            Assert.That(interact, Is.Zero);
        }

        [Test]
        public void LoadingRejectsGameplayInput()
        {
            _flow.BeginLoading();
            int jump = 0;
            int attack = 0;
            int interact = 0;
            int pause = 0;
            _installer.Service.Jump += () => jump++;
            _installer.Service.Attack += () => attack++;
            _installer.Service.ExplorationInteract += () => interact++;
            _installer.Service.PauseRequested += () => pause++;

            Process("ProcessJump");
            Process("ProcessAttack");
            Process("ProcessInteract");
            Process("ProcessPause");

            Assert.That(jump, Is.Zero);
            Assert.That(attack, Is.Zero);
            Assert.That(interact, Is.Zero);
            Assert.That(pause, Is.Zero);
        }

        [Test]
        public void LeavingExplorationClearsCurrentMove()
        {
            Process("ProcessMove", Vector2.right);

            _flow.BeginDialogue();

            Assert.That(_installer.Service.CurrentMove, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void LeavingExplorationEmitsZeroMovementOnce()
        {
            Process("ProcessMove", Vector2.right);
            int zeroCount = 0;
            _installer.Service.Move += value =>
            {
                if (value == Vector2.zero)
                    zeroCount++;
            };

            _flow.BeginDialogue();
            Process("ProcessMoveCanceled");

            Assert.That(zeroCount, Is.EqualTo(1));
        }

        [Test]
        public void ReenteringExplorationDoesNotRestoreStaleMovement()
        {
            Process("ProcessMove", Vector2.left);
            _flow.BeginDialogue();

            _flow.EnterExploration();

            Assert.That(_installer.Service.CurrentMove, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DuplicateCallbackRegistrationDoesNotDuplicateCommands()
        {
            int attack = 0;
            _installer.Service.Attack += () => attack++;
            InvokePrivate(_installer, "RegisterCallbacks");
            InvokePrivate(_installer, "RegisterCallbacks");
            int registrationCount = GetPrivateField<int>(_installer, "_callbackRegistrationCount");

            Process("ProcessAttack");

            Assert.That(registrationCount, Is.EqualTo(1));
            Assert.That(attack, Is.EqualTo(1));
        }

        [Test]
        public void PauseEntersPausedOnce()
        {
            int pauseRequests = 0;
            int pausedTransitions = 0;
            _installer.Service.PauseRequested += () => pauseRequests++;
            _stateMachine.OnStateChanged += (_, next) =>
            {
                if (next == GameState.Paused)
                    pausedTransitions++;
            };

            Process("ProcessPause");

            Assert.That(pauseRequests, Is.EqualTo(1));
            Assert.That(pausedTransitions, Is.EqualTo(1));
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Paused));
        }

        [Test]
        public void PauseWhilePausedRestoresExactPreviousState()
        {
            _flow.BeginDialogue();
            Process("ProcessPause");
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Paused));

            Process("ProcessPause");

            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Dialogue));
        }

        [Test]
        public void InstallerEnableDisableCyclesDoNotDuplicateSubscriptions()
        {
            int attack = 0;
            _installer.Service.Attack += () => attack++;

            Process("ProcessAttack");
            InvokePrivate(_installer, "OnDisable");
            InvokePrivate(_installer, "OnEnable");
            InvokePrivate(_installer, "OnEnable");
            int registrationCount = GetPrivateField<int>(_installer, "_callbackRegistrationCount");
            Process("ProcessAttack");

            Assert.That(registrationCount, Is.EqualTo(1));
            Assert.That(attack, Is.EqualTo(2));
        }

        [Test]
        public void MissingGameStateMachineFallsBackToExplorationRouting()
        {
            UnityEngine.Object.DestroyImmediate(_stateObject);
            _stateObject = null;
            int exploration = 0;
            int dialogue = 0;
            Vector2 received = Vector2.zero;
            _installer.Service.ExplorationInteract += () => exploration++;
            _installer.Service.DialogueAdvance += () => dialogue++;
            _installer.Service.Move += value => received = value;

            Process("ProcessMove", Vector2.up);
            Process("ProcessInteract");

            Assert.That(received, Is.EqualTo(Vector2.up));
            Assert.That(exploration, Is.EqualTo(1));
            Assert.That(dialogue, Is.Zero);
        }

        private void EnterCombatResolving()
        {
            _flow.EnterCombatPlanning();
            _flow.EnterCombatResolving();
        }

        private void Process(string methodName, params object[] arguments)
        {
            InvokePrivate(_installer, methodName, arguments);
        }

        private GameObject CreateInactiveObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {target.GetType().Name}.{methodName}");
            method.Invoke(target, arguments);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static void DestroyExistingOwners()
        {
            DestroyAll<GameInputInstaller>();
            DestroyAll<GameFlowController>();
            DestroyAll<GameStateMachine>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    UnityEngine.Object.DestroyImmediate(components[i].gameObject);
            }
        }
    }
}
#endif
