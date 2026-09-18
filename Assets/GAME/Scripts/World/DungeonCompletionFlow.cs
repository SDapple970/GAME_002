using System;
using Game.Core;
using Game.Quest;
using Game.Story;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Converts an authored quest completion into a dungeon-level completion
    /// contract. QuestRuntime remains the persistent source of truth; this
    /// scene integration owns neither quest progress nor rewards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonCompletionFlow : MonoBehaviour
    {
        [Header("Completion Condition")]
        [SerializeField] private string dungeonId = "dungeon1";
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private string completionQuestId;

        [Header("Optional Authored Destination")]
        [SerializeField] private string destinationSceneName;
        [SerializeField] private string destinationSpawnPointId;
        [SerializeField] private bool travelWhenCompletionReady;

        private QuestRuntime _subscribedQuestRuntime;
        private GameStateMachine _subscribedStateMachine;
        private bool _completionRaised;
        private bool _travelRequested;
        private bool _destinationPendingLogged;
        private bool _missingSceneFlowLogged;

        public event Action<DungeonCompletionRequest> OnCompletionReady;

        public string DungeonId => dungeonId?.Trim();
        public string CompletionQuestId => completionQuestId?.Trim();
        public string DestinationSceneName => destinationSceneName?.Trim();
        public string DestinationSpawnPointId => destinationSpawnPointId?.Trim();
        public bool IsCompletionReady => questRuntime != null &&
                                         !string.IsNullOrWhiteSpace(CompletionQuestId) &&
                                         questRuntime.IsQuestComplete(CompletionQuestId);
        public bool HasAuthoredDestination => !string.IsNullOrWhiteSpace(DestinationSceneName);

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            EvaluateCompletion();
        }

        private void Start()
        {
            // RuntimeBootstrapper may create the canonical state machine after this
            // scene component is enabled, so bind once more after bootstrap.
            ResolveReferences();
            Subscribe();
            EvaluateCompletion();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Uses the canonical SceneFlow owner only after a destination has been
        /// authored and the configured completion condition is actually satisfied.
        /// A future exit interaction can call this without becoming a scene-load owner.
        /// </summary>
        public bool TryTravelToAuthoredDestination()
        {
            if (!IsCompletionReady)
            {
                Debug.LogWarning(
                    $"[DungeonCompletionFlow] Travel rejected because completion quest '{CompletionQuestId}' is not complete.",
                    this);
                return false;
            }

            if (!HasAuthoredDestination)
            {
                LogDestinationAuthoringPending();
                return false;
            }

            if (!IsSafeToTravel())
                return false;

            if (_travelRequested)
                return false;

            SceneFlowController sceneFlow = ResolveSceneFlow();
            if (sceneFlow == null)
            {
                if (!_missingSceneFlowLogged)
                {
                    _missingSceneFlowLogged = true;
                    Debug.LogWarning(
                        "[DungeonCompletionFlow] SceneFlowController is missing. Completion destination was not loaded.",
                        this);
                }

                return false;
            }

            _travelRequested = true;
            if (string.IsNullOrWhiteSpace(DestinationSpawnPointId))
                sceneFlow.LoadScene(DestinationSceneName);
            else
                SceneTravelService.TravelTo(DestinationSceneName, DestinationSpawnPointId);

            return true;
        }

        private void HandleQuestCompleted(string questId)
        {
            if (!string.Equals(questId, CompletionQuestId, StringComparison.Ordinal))
                return;

            EvaluateCompletion();
        }

        private void HandleQuestStateRestored()
        {
            // QuestRuntime is persistent authority. Re-evaluate rather than saving a
            // second dungeon-complete flag that could drift from the quest snapshot.
            _completionRaised = false;
            _travelRequested = false;
            EvaluateCompletion();
        }

        private void HandleStateChanged(GameState previous, GameState next)
        {
            if (next == GameState.Exploration && travelWhenCompletionReady && IsCompletionReady)
                TryTravelToAuthoredDestination();
        }

        private void EvaluateCompletion()
        {
            if (!IsCompletionReady || _completionRaised)
                return;

            _completionRaised = true;
            OnCompletionReady?.Invoke(new DungeonCompletionRequest(
                DungeonId,
                CompletionQuestId,
                DestinationSceneName,
                DestinationSpawnPointId));

            if (travelWhenCompletionReady)
                TryTravelToAuthoredDestination();
            else if (!HasAuthoredDestination)
                LogDestinationAuthoringPending();
        }

        private void ResolveReferences()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }

        private void Subscribe()
        {
            if (_subscribedQuestRuntime != questRuntime)
            {
                if (_subscribedQuestRuntime != null)
                {
                    _subscribedQuestRuntime.OnQuestCompleted -= HandleQuestCompleted;
                    _subscribedQuestRuntime.OnStateRestored -= HandleQuestStateRestored;
                }

                _subscribedQuestRuntime = questRuntime;
                if (_subscribedQuestRuntime != null)
                {
                    _subscribedQuestRuntime.OnQuestCompleted += HandleQuestCompleted;
                    _subscribedQuestRuntime.OnStateRestored += HandleQuestStateRestored;
                }
            }

            GameStateMachine stateMachine = GameStateMachine.Instance;
            if (_subscribedStateMachine == stateMachine)
                return;

            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleStateChanged;

            _subscribedStateMachine = stateMachine;
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged += HandleStateChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribedQuestRuntime != null)
            {
                _subscribedQuestRuntime.OnQuestCompleted -= HandleQuestCompleted;
                _subscribedQuestRuntime.OnStateRestored -= HandleQuestStateRestored;
            }

            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleStateChanged;

            _subscribedQuestRuntime = null;
            _subscribedStateMachine = null;
        }

        private static bool IsSafeToTravel()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private static SceneFlowController ResolveSceneFlow()
        {
            return SceneFlowController.Instance != null
                ? SceneFlowController.Instance
                : FindFirstObjectByType<SceneFlowController>();
        }

        private void LogDestinationAuthoringPending()
        {
            if (_destinationPendingLogged)
                return;

            _destinationPendingLogged = true;
            Debug.Log(
                $"[DungeonCompletionFlow] Completion contract is ready for dungeon '{DungeonId}', " +
                "but destination authoring is pending. No scene transition was requested.",
                this);
        }
    }
}
