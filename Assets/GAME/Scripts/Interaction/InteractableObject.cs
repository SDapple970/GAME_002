using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Interaction
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class InteractableObject : MonoBehaviour, IInteractable
    {
        [Header("Prompt")]
        [SerializeField] private string promptText = "E: 조사";

        [Header("Rules")]
        [SerializeField] private bool interactOnce;
        [SerializeField] private bool disableAfterInteract;
        [SerializeField] private string playerTag = "Player";

        [Header("Production Identity")]
        [SerializeField] private string interactionId;
        [SerializeField] private InteractionUsePolicy usePolicy = InteractionUsePolicy.LegacyCompatibility;

        [Header("Events")]
        [SerializeField] private List<InteractionConditionSO> conditions = new();
        [SerializeField] private List<InteractionEventSO> events = new();

        private bool _hasInteracted;
        private InteractionRuntime _boundRuntime;

        public string PromptText => promptText;
        public string InteractionId => InteractionIdentity.Normalize(interactionId);
        public InteractionUsePolicy UsePolicy => usePolicy;
        public IReadOnlyList<InteractionEventSO> Events => events;

        public bool CanInteract
        {
            get
            {
                if (_hasInteracted && interactOnce)
                    return false;

                if ((usePolicy == InteractionUsePolicy.OncePerSession || usePolicy == InteractionUsePolicy.PersistentOnce) &&
                    InteractionRuntime.Instance != null &&
                    InteractionRuntime.Instance.IsConsumed(InteractionId, usePolicy))
                {
                    return false;
                }

                if (usePolicy == InteractionUsePolicy.PersistentOnce && InteractionId == null)
                    return false;

                return GameStateMachine.Instance == null ||
                       GameStateMachine.Instance.Is(GameState.Exploration);
            }
        }

        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
                trigger.isTrigger = true;
        }

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null && !trigger.isTrigger)
                Debug.LogWarning("[InteractableObject] Collider2D should be configured as Trigger.", this);
        }

        private void OnEnable()
        {
            BindRuntime(InteractionRuntime.Instance);
            ApplyPersistentVisualState();
        }

        private void OnDisable()
        {
            UnbindRuntime();
        }

        private void OnDestroy()
        {
            UnbindRuntime();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag))
                return;

            InteractionController controller = other.GetComponentInParent<InteractionController>();
            if (controller == null)
                controller = InteractionController.Instance;

            controller?.Register(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag))
                return;

            InteractionController controller = other.GetComponentInParent<InteractionController>();
            if (controller == null)
                controller = InteractionController.Instance;

            controller?.Unregister(this);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract)
                return;

            InteractionController controller = InteractionController.Instance;
            if (interactor != null)
            {
                InteractionController interactorController = interactor.GetComponentInParent<InteractionController>();
                if (interactorController != null)
                    controller = interactorController;
            }

            InteractionRunner runner = InteractionRunner.ResolveOrCreate();
            runner.Execute(new InteractionRequest(
                this,
                InteractionId,
                interactor,
                controller,
                usePolicy,
                events));
        }

        internal void MarkLegacyInteracted()
        {
            _hasInteracted = true;
        }

        internal bool AreConditionsMet(InteractionContext context, out string blockedReason)
        {
            blockedReason = null;
            if (conditions == null)
                return true;

            for (int i = 0; i < conditions.Count; i++)
            {
                InteractionConditionSO condition = conditions[i];
                if (condition != null && !condition.IsMet(context, out blockedReason))
                    return false;
            }

            return true;
        }

        private void OnValidate()
        {
            if (usePolicy == InteractionUsePolicy.PersistentOnce && InteractionId == null)
                Debug.LogWarning("[InteractableObject] PersistentOnce requires a stable interactionId.", this);
        }

        internal void BindRuntime(InteractionRuntime runtime)
        {
            if (_boundRuntime == runtime)
                return;

            UnbindRuntime();
            _boundRuntime = runtime;
            if (_boundRuntime != null)
                _boundRuntime.StateRestored += ApplyPersistentVisualState;
        }

        internal void ApplyResult(InteractionResult result)
        {
            if (result.Consumed)
                ApplyConsumedVisualState(true);

            bool legacyDisable = usePolicy == InteractionUsePolicy.LegacyCompatibility &&
                                 disableAfterInteract &&
                                 _hasInteracted;
            if (legacyDisable)
                gameObject.SetActive(false);
        }

        private void ApplyPersistentVisualState()
        {
            bool consumed = _boundRuntime != null &&
                            _boundRuntime.IsConsumed(InteractionId, usePolicy);
            ApplyConsumedVisualState(consumed);
        }

        private void ApplyConsumedVisualState(bool consumed)
        {
            MonoBehaviour[] components = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
                if (components[i] is IInteractionVisualState visualState)
                    visualState.ApplyConsumedState(consumed);
        }

        private void UnbindRuntime()
        {
            if (_boundRuntime != null)
                _boundRuntime.StateRestored -= ApplyPersistentVisualState;
            _boundRuntime = null;
        }
    }
}
