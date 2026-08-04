using Game.Core;
using Game.Reward;
using UnityEngine;

namespace Game.Interaction
{
    public sealed class InteractionRunner : MonoBehaviour
    {
        public static InteractionRunner Instance { get; private set; }

        [SerializeField] private InteractionRuntime runtime;

        private bool _resolving;
        private int _lastExecutionFrame = -1;
        private string _lastExecutionId;

        public InteractionRuntime Runtime => runtime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureProductionOwnerAfterSceneLoad()
        {
            ResolveOrCreate();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InteractionRunner] Duplicate Production runner rejected.", this);
                enabled = false;
                return;
            }

            Instance = this;
            if (runtime == null)
                runtime = GetComponent<InteractionRuntime>();
            if (runtime == null)
                runtime = InteractionRuntime.Instance;
            if (runtime == null)
                runtime = gameObject.AddComponent<InteractionRuntime>();
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static InteractionRunner ResolveOrCreate()
        {
            if (Instance != null)
                return Instance;

            InteractionRunner existing = FindFirstObjectByType<InteractionRunner>();
            if (existing != null)
                return existing;

            GameObject owner = new GameObject("InteractionRuntime");
            InteractionRuntime state = owner.AddComponent<InteractionRuntime>();
            InteractionRunner runner = owner.AddComponent<InteractionRunner>();
            runner.runtime = state;
            return runner;
        }

        public InteractionResult Execute(InteractionRequest request)
        {
            if (_resolving)
                return Result(InteractionResultStatus.BlockedState, request, "interaction.busy");

            string requestKey = request.InteractionId ?? request.Source?.GetInstanceID().ToString();
            if (_lastExecutionFrame == Time.frameCount && _lastExecutionId == requestKey)
                return Result(InteractionResultStatus.AlreadyConsumed, request, "interaction.duplicate-command");

            _lastExecutionFrame = Time.frameCount;
            _lastExecutionId = requestKey;
            if (!request.IsValid)
                return Result(InteractionResultStatus.Failed, request, "interaction.invalid-request");

            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
                return Result(InteractionResultStatus.BlockedState, request, "interaction.blocked-state");

            if (request.UsePolicy == InteractionUsePolicy.PersistentOnce && request.InteractionId == null)
            {
                Debug.LogWarning("[InteractionRunner] PersistentOnce interaction rejected because interactionId is empty.", request.Source);
                return Result(InteractionResultStatus.InvalidIdentity, request, "interaction.invalid-id");
            }

            if (runtime == null)
                runtime = InteractionRuntime.Instance;
            if (runtime == null)
                return Result(InteractionResultStatus.Failed, request, "interaction.runtime-missing");

            request.Source.BindRuntime(runtime);
            if (runtime.IsConsumed(request.InteractionId, request.UsePolicy))
                return Result(InteractionResultStatus.AlreadyConsumed, request, "interaction.already-consumed");

            InteractionContext conditionContext = new(request.Interactor, request.Source, request.Controller);
            if (!request.Source.AreConditionsMet(conditionContext, out string blockedReason))
                return Result(
                    InteractionResultStatus.BlockedCondition,
                    request,
                    string.IsNullOrWhiteSpace(blockedReason) ? "interaction.blocked-condition" : blockedReason);

            _resolving = true;
            try
            {
                return ExecuteEvents(request);
            }
            finally
            {
                _resolving = false;
            }
        }

        private InteractionResult ExecuteEvents(InteractionRequest request)
        {
            bool accepted = false;
            bool stateChanged = false;
            bool irreversible = false;
            bool failed = false;
            string message = null;
            RewardGrantResult reward = default;
            bool storyAccepted = false;
            bool questAccepted = false;
            InteractionContext legacy = new(request.Interactor, request.Source, request.Controller);

            for (int i = 0; i < request.Events.Count; i++)
            {
                InteractionEventSO interactionEvent = request.Events[i];
                if (interactionEvent == null)
                    continue;

                string actionId = InteractionIdentity.ResolveActionId(interactionEvent, i);
                InteractionExecutionContext context = new(request, runtime, legacy, actionId, i);
                InteractionEventResult eventResult = interactionEvent.ExecuteProduction(context);
                accepted |= eventResult.Accepted;
                stateChanged |= eventResult.StateChanged;
                irreversible |= eventResult.Accepted && eventResult.Irreversible;
                failed |= eventResult.HasFailure;
                if (!string.IsNullOrWhiteSpace(eventResult.Message))
                    message = eventResult.Message;
                if (eventResult.Reward.SourceType != RewardSourceType.Unknown)
                    reward = eventResult.Reward;
                storyAccepted |= eventResult.StoryAccepted;
                questAccepted |= eventResult.QuestAccepted;
            }

            if (request.UsePolicy == InteractionUsePolicy.LegacyCompatibility)
                request.Source.MarkLegacyInteracted();

            bool consume = request.UsePolicy != InteractionUsePolicy.Repeatable &&
                           request.UsePolicy != InteractionUsePolicy.LegacyCompatibility &&
                           (stateChanged || irreversible);
            if (consume)
                runtime.MarkConsumed(request.InteractionId, request.UsePolicy);

            InteractionResultStatus status;
            if (accepted && failed)
                status = InteractionResultStatus.PartialFailure;
            else if (accepted)
                status = InteractionResultStatus.Success;
            else if (failed)
                status = InteractionResultStatus.Failed;
            else
                status = InteractionResultStatus.NoEffect;

            InteractionResult result = new(
                status,
                request.InteractionId,
                message,
                stateChanged,
                consume,
                true,
                reward,
                storyAccepted,
                questAccepted);
            request.Source.ApplyResult(result);
            request.Controller?.PresentResult(result);
            return result;
        }

        private static InteractionResult Result(
            InteractionResultStatus status,
            InteractionRequest request,
            string message)
        {
            InteractionResult result = new(status, request.InteractionId, message, false, false, true);
            request.Source?.ApplyResult(result);
            request.Controller?.PresentResult(result);
            return result;
        }

        internal static void ResetOwnershipForTests()
        {
            Instance = null;
        }
    }
}
