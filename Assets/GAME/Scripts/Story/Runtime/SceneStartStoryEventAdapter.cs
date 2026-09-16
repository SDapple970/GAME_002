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
        private const string DiagnosticPrefix = "[D1-05 SceneStartStory]";

        [SerializeField] private StoryEventRunner runner;
        [SerializeField] private StoryEventDefinitionSO eventDefinition;

        private GameStateMachine _stateMachine;
        private SaveLoadService _saveLoadService;
        private RuntimeBootstrapper _runtimeBootstrapper;
        private bool _subscribedToState;
        private bool _subscribedToLoad;
        private bool _subscribedToBootstrap;
        private bool _startRequested;

        private void OnEnable()
        {
            LogDiagnostic("OnEnable");
            SubscribeToBootstrapper();
            ResolveAndSubscribe();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryStartWhenReady();
        }

        private void Start()
        {
            LogDiagnostic("Start");
            ResolveAndSubscribe();
            TryStartWhenReady();
        }

        private void OnDisable()
        {
            LogDiagnostic("OnDisable");
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Unsubscribe();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene)
            {
                LogDiagnostic($"SceneLoaded: {scene.name} ({mode})");
                TryStartWhenReady();
            }
        }

        private void HandleGameStateChanged(GameState previous, GameState next)
        {
            LogDiagnostic($"GameStateChanged: {previous} -> {next}");
            if (next == GameState.Exploration)
                TryStartWhenReady();
        }

        private void HandleLoadCompleted(bool succeeded, string message)
        {
            LogDiagnostic($"LoadCompleted: succeeded={succeeded} message={message}");
            if (succeeded)
                TryStartWhenReady();
        }

        private void HandleInitialStateApplied(Scene scene)
        {
            if (scene != gameObject.scene)
                return;

            LogDiagnostic($"InitialStateApplied: {scene.name}");
            TryStartWhenReady();
        }

        private void TryStartWhenReady()
        {
            if (_startRequested)
            {
                LogDiagnostic("BLOCKED: StartAlreadyRequested");
                return;
            }

            if (!isActiveAndEnabled)
            {
                LogDiagnostic("BLOCKED: AdapterInactive");
                return;
            }

            if (eventDefinition == null)
            {
                LogDiagnostic("BLOCKED: EventDefinitionInvalid");
                return;
            }

            ResolveAndSubscribe();
            StoryProgressManager storyProgress = StoryProgressManager.Instance;
            bool storyCompleted = storyProgress != null &&
                                  storyProgress.IsEventCompleted(eventDefinition.EventId);
            string gameState = _stateMachine != null ? _stateMachine.Current.ToString() : "unavailable";
            string saveState = _saveLoadService != null
                ? _saveLoadService.CurrentOperationState.ToString()
                : "unavailable";
            bool bootstrapReady = _runtimeBootstrapper == null ||
                                  _runtimeBootstrapper.HasAppliedInitialStateFor(gameObject.scene);

            LogDiagnostic(
                $"Evaluate runner={runner != null} runnerActive={runner != null && runner.isActiveAndEnabled} " +
                $"event={eventDefinition.EventId} gameState={gameState} saveState={saveState} " +
                $"storyProgress={storyProgress != null} storyCompleted={storyCompleted} " +
                $"bootstrapReady={bootstrapReady} startRequested={_startRequested} " +
                $"runnerRunning={runner != null && runner.IsRunning}");

            if (string.IsNullOrWhiteSpace(eventDefinition.EventId))
            {
                LogDiagnostic("BLOCKED: EventDefinitionInvalid (empty event id)");
                return;
            }

            if (storyCompleted)
            {
                _startRequested = true;
                LogDiagnostic("BLOCKED: StoryAlreadyCompleted");
                return;
            }

            if (runner == null || !runner.isActiveAndEnabled)
            {
                LogDiagnostic("BLOCKED: RunnerUnavailable");
                return;
            }

            if (!bootstrapReady)
            {
                LogDiagnostic("BLOCKED: BootstrapPending");
                return;
            }

            if (_stateMachine == null || GameFlowController.Instance == null)
            {
                LogDiagnostic("BLOCKED: CoreUnavailable");
                return;
            }

            if (_saveLoadService != null &&
                _saveLoadService.CurrentOperationState != SaveLoadService.OperationState.Idle)
            {
                LogDiagnostic("BLOCKED: SaveLoadBusy");
                return;
            }

            if (_stateMachine.Current != GameState.Exploration)
            {
                LogDiagnostic("BLOCKED: WrongGameState");
                return;
            }

            if (runner.IsRunning)
            {
                LogDiagnostic("BLOCKED: RunnerBusy");
                return;
            }

            LogDiagnostic($"CALL TryStartEvent: {eventDefinition.EventId}");
            _startRequested = runner.TryStartEvent(eventDefinition);
            LogDiagnostic($"RESULT TryStartEvent: {_startRequested}");

            if (!_startRequested)
                LogDiagnostic("BLOCKED: TryStartEventRejected (see StoryEventRunner warning for reason)");
        }

        private void LogDiagnostic(string message)
        {
            Debug.Log($"{DiagnosticPrefix} {message}", this);
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

            _runtimeBootstrapper = FindFirstObjectByType<RuntimeBootstrapper>();
        }

        private void Unsubscribe()
        {
            if (_subscribedToState && _stateMachine != null)
                _stateMachine.OnStateChanged -= HandleGameStateChanged;
            if (_subscribedToLoad && _saveLoadService != null)
                _saveLoadService.OnLoadCompleted -= HandleLoadCompleted;
            if (_subscribedToBootstrap)
                RuntimeBootstrapper.OnInitialStateApplied -= HandleInitialStateApplied;

            _subscribedToState = false;
            _subscribedToLoad = false;
            _subscribedToBootstrap = false;
            _stateMachine = null;
            _saveLoadService = null;
            _runtimeBootstrapper = null;
        }

        private void SubscribeToBootstrapper()
        {
            if (_subscribedToBootstrap)
                return;

            RuntimeBootstrapper.OnInitialStateApplied += HandleInitialStateApplied;
            _subscribedToBootstrap = true;
        }
    }
}
