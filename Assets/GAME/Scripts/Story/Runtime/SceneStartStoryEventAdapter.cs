using System.Collections;
using Game.Core;
using Game.Story.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Story
{
    /// <summary>
    /// Scene-local authored-story entry point. StoryEventRunner retains ownership of
    /// the narrative lifecycle, state transitions, and effect execution.
    /// </summary>
    public sealed class SceneStartStoryEventAdapter : MonoBehaviour
    {
        [SerializeField] private StoryEventRunner runner;
        [SerializeField] private StoryEventDefinitionSO eventDefinition;

        private GameStateMachine _stateMachine;
        private SaveLoadService _saveLoadService;
        private bool _subscribedToState;
        private bool _subscribedToLoad;
        private bool _startRequested;
        private Coroutine _initialReadinessRoutine;

        private void OnEnable()
        {
            ResolveAndSubscribe();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryStartWhenReady();
            if (Application.isPlaying)
                _initialReadinessRoutine = StartCoroutine(WaitForInitialReadiness());
        }

        private void Start()
        {
            ResolveAndSubscribe();
            TryStartWhenReady();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_initialReadinessRoutine != null)
                StopCoroutine(_initialReadinessRoutine);
            _initialReadinessRoutine = null;
            Unsubscribe();
        }

        private IEnumerator WaitForInitialReadiness()
        {
            // RuntimeBootstrapper can be installed after this scene-local component
            // receives OnEnable. A single lifecycle deferral avoids frame polling.
            yield return null;
            _initialReadinessRoutine = null;
            ResolveAndSubscribe();
            TryStartWhenReady();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene)
                TryStartWhenReady();
        }

        private void HandleGameStateChanged(GameState previous, GameState next)
        {
            if (next == GameState.Exploration)
                TryStartWhenReady();
        }

        private void HandleLoadCompleted(bool succeeded, string message)
        {
            if (succeeded)
                TryStartWhenReady();
        }

        private void TryStartWhenReady()
        {
            if (_startRequested || !isActiveAndEnabled || eventDefinition == null)
                return;

            ResolveAndSubscribe();
            if (StoryProgressManager.Instance != null &&
                StoryProgressManager.Instance.IsEventCompleted(eventDefinition.EventId))
            {
                _startRequested = true;
                return;
            }

            if (runner == null ||
                _stateMachine == null ||
                GameFlowController.Instance == null ||
                _stateMachine.Current != GameState.Exploration ||
                (_saveLoadService != null && _saveLoadService.CurrentOperationState != SaveLoadService.OperationState.Idle) ||
                runner.IsRunning)
            {
                return;
            }

            _startRequested = runner.TryStartEvent(eventDefinition);
        }

        private void ResolveAndSubscribe()
        {
            if (runner == null)
                runner = FindFirstObjectByType<StoryEventRunner>();

            GameStateMachine resolvedStateMachine = GameStateMachine.Instance;
            if (_stateMachine != resolvedStateMachine)
            {
                if (_subscribedToState && _stateMachine != null)
                    _stateMachine.OnStateChanged -= HandleGameStateChanged;

                _stateMachine = resolvedStateMachine;
                _subscribedToState = false;
            }

            if (!_subscribedToState && _stateMachine != null)
            {
                _stateMachine.OnStateChanged += HandleGameStateChanged;
                _subscribedToState = true;
            }

            SaveLoadService resolvedSaveLoadService = SaveLoadService.Instance;
            if (_saveLoadService != resolvedSaveLoadService)
            {
                if (_subscribedToLoad && _saveLoadService != null)
                    _saveLoadService.OnLoadCompleted -= HandleLoadCompleted;

                _saveLoadService = resolvedSaveLoadService;
                _subscribedToLoad = false;
            }

            if (!_subscribedToLoad && _saveLoadService != null)
            {
                _saveLoadService.OnLoadCompleted += HandleLoadCompleted;
                _subscribedToLoad = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedToState && _stateMachine != null)
                _stateMachine.OnStateChanged -= HandleGameStateChanged;
            if (_subscribedToLoad && _saveLoadService != null)
                _saveLoadService.OnLoadCompleted -= HandleLoadCompleted;

            _subscribedToState = false;
            _subscribedToLoad = false;
            _stateMachine = null;
            _saveLoadService = null;
        }
    }
}
