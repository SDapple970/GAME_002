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

        [Header("Events")]
        [SerializeField] private List<InteractionEventSO> events = new();

        private bool _hasInteracted;

        public string PromptText => promptText;

        public bool CanInteract
        {
            get
            {
                if (_hasInteracted && interactOnce)
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

            _hasInteracted = true;

            InteractionController controller = InteractionController.Instance;
            if (interactor != null)
            {
                InteractionController interactorController = interactor.GetComponentInParent<InteractionController>();
                if (interactorController != null)
                    controller = interactorController;
            }

            InteractionContext context = new InteractionContext(interactor, this, controller);
            for (int i = 0; i < events.Count; i++)
            {
                InteractionEventSO interactionEvent = events[i];
                if (interactionEvent != null)
                    interactionEvent.Execute(context);
            }

            if (disableAfterInteract)
                gameObject.SetActive(false);

            controller?.RefreshCurrentTarget();
        }
    }
}
