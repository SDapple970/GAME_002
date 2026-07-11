#if UNITY_INCLUDE_TESTS
using Game.Core;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Core
{
    public sealed class GameStateOwnershipTests
    {
        private GameObject _stateObject;
        private GameObject _flowObject;
        private GameStateMachine _stateMachine;
        private GameFlowController _flow;

        [SetUp]
        public void SetUp()
        {
            DestroyExistingSingletons();

            _stateObject = new GameObject("GameStateMachineTest");
            _stateMachine = _stateObject.AddComponent<GameStateMachine>();
            InvokeAwake(_stateMachine);
            _flowObject = new GameObject("GameFlowControllerTest");
            _flow = _flowObject.AddComponent<GameFlowController>();
            InvokeAwake(_flow);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_flowObject);
            Object.DestroyImmediate(_stateObject);
            DestroyExistingSingletons();
        }

        private static void DestroyExistingSingletons()
        {
            GameFlowController[] flows = Object.FindObjectsByType<GameFlowController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < flows.Length; i++)
                Object.DestroyImmediate(flows[i].gameObject);

            GameStateMachine[] stateMachines = Object.FindObjectsByType<GameStateMachine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < stateMachines.Length; i++)
                Object.DestroyImmediate(stateMachines[i].gameObject);
        }

        private static void InvokeAwake(object target)
        {
            target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        [Test]
        public void DuplicateTransitionDoesNotRaiseEvent()
        {
            int eventCount = 0;
            _stateMachine.OnStateChanged += (_, _) => eventCount++;

            Assert.That(_stateMachine.TrySetState(GameState.Exploration, "test duplicate"), Is.False);
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void InvalidTransitionIsRejectedAndLogged()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rejected transition Exploration -> Choice"));

            Assert.That(_stateMachine.TrySetState(GameState.Choice, "test invalid"), Is.False);
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Exploration));
        }

        [Test]
        public void PauseRestoresExactPreviousState()
        {
            _flow.BeginDialogue();
            _flow.Pause();
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Paused));

            _flow.ResumePreviousState();
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Dialogue));
        }

        [Test]
        public void LoadingCanCompleteIntoTitleOrExploration()
        {
            _flow.BeginLoading();
            Assert.That(_stateMachine.TrySetState(GameState.Title, "test title load"), Is.True);
            _flow.BeginLoading();
            _flow.EnterExploration();

            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Exploration));
        }

        [Test]
        public void DialogueChoiceDialogueExplorationIsAllowed()
        {
            _flow.BeginDialogue();
            _flow.BeginChoice();
            _flow.BeginDialogue();
            _flow.EnterExploration();

            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(_stateMachine.Previous, Is.EqualTo(GameState.Dialogue));
        }

        [Test]
        public void ReentrantTransitionIsRejectedWithoutCorruptingOuterTransition()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("state-change callback is already running"));
            _stateMachine.OnStateChanged += (_, _) => _stateMachine.TrySetState(GameState.Choice, "reentrant test");

            Assert.That(_stateMachine.TrySetState(GameState.Dialogue, "outer test"), Is.True);
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Dialogue));
            Assert.That(_stateMachine.Previous, Is.EqualTo(GameState.Exploration));
        }
    }
}
#endif
