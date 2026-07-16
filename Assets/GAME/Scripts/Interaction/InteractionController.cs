using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Input;
using UnityEngine;

namespace Game.Interaction
{
    public sealed class InteractionController : MonoBehaviour
    {
        public static InteractionController Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private InteractionPromptUI promptUI;

        private readonly List<InteractableObject> _candidates = new();
        private InteractableObject _current;
        private GameInputInstaller _inputInstaller;
        private InputService _inputService;
        private bool _subscribedToInput;
        private Coroutine _messageRoutine;

        public InteractionPromptUI PromptUI => promptUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InteractionController] Multiple controllers exist. Keeping the first instance.", this);
            }
            else
            {
                Instance = this;
            }

            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeInput();
            RefreshCurrentTarget();
        }

        private void Start()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            HidePrompt();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            EnsureInputSubscription();

            RefreshCurrentTarget();
        }

        public void Register(InteractableObject interactable)
        {
            if (interactable == null || _candidates.Contains(interactable))
                return;

            _candidates.Add(interactable);
            RefreshCurrentTarget();
        }

        public void Unregister(InteractableObject interactable)
        {
            if (interactable == null)
                return;

            _candidates.Remove(interactable);
            if (_current == interactable)
                _current = null;

            RefreshCurrentTarget();
        }

        public void RefreshCurrentTarget()
        {
            if (_messageRoutine != null)
            {
                if (!CanAcceptInteractionInput())
                    HidePrompt();

                return;
            }

            _current = FindNearestInteractable();

            if (_current != null && CanAcceptInteractionInput())
                promptUI?.Show(_current.PromptText);
            else
                HidePrompt();
        }

        public bool TryInteractCurrent()
        {
            if (!CanAcceptInteractionInput())
                return false;

            RefreshCurrentTarget();
            if (_current == null || !_current.CanInteract)
                return false;

            _current.Interact(gameObject);
            RefreshCurrentTarget();
            return true;
        }

        public void ShowMessage(string message)
        {
            if (promptUI != null)
                promptUI.Show(message);
            else if (!string.IsNullOrEmpty(message))
                Debug.Log($"[Interaction] {message}", this);
        }

        public void ShowTemporaryMessage(string message, float seconds)
        {
            if (_messageRoutine != null)
                StopCoroutine(_messageRoutine);

            _messageRoutine = StartCoroutine(Co_ShowTemporaryMessage(message, seconds));
        }

        public void ShowMessageSequence(IReadOnlyList<string> messages, float secondsPerMessage)
        {
            if (_messageRoutine != null)
                StopCoroutine(_messageRoutine);

            _messageRoutine = StartCoroutine(Co_ShowMessageSequence(messages, secondsPerMessage));
        }

        private void AutoBindReferences()
        {
            if (promptUI == null)
                promptUI = FindFirstObjectByType<InteractionPromptUI>();
        }

        private void SubscribeInput()
        {
            EnsureInputSubscription();
        }

        private void EnsureInputSubscription()
        {
            GameInputInstaller installer = GameInputInstaller.Instance;
            InputService service = installer != null ? installer.Service : null;

            if (_subscribedToInput && _inputInstaller == installer && _inputService == service)
                return;

            UnsubscribeInput();

            if (installer == null || service == null)
                return;

            _inputInstaller = installer;
            _inputService = service;
            _inputService.ExplorationInteract += HandleInteractInput;
            _subscribedToInput = true;
        }

        private void UnsubscribeInput()
        {
            if (!_subscribedToInput || _inputService == null)
            {
                _subscribedToInput = false;
                _inputInstaller = null;
                _inputService = null;
                return;
            }

            _inputService.ExplorationInteract -= HandleInteractInput;
            _subscribedToInput = false;
            _inputInstaller = null;
            _inputService = null;
        }

        private void HandleInteractInput()
        {
            TryInteractCurrent();
        }

        private InteractableObject FindNearestInteractable()
        {
            InteractableObject nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                InteractableObject candidate = _candidates[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    _candidates.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanInteract)
                    continue;

                float distance = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static bool CanAcceptInteractionInput()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private void HidePrompt()
        {
            promptUI?.Hide();
        }

        private IEnumerator Co_ShowTemporaryMessage(string message, float seconds)
        {
            ShowMessage(message);
            yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
            _messageRoutine = null;
            RefreshCurrentTarget();
        }

        private IEnumerator Co_ShowMessageSequence(IReadOnlyList<string> messages, float secondsPerMessage)
        {
            if (messages != null)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    string message = messages[i];
                    if (string.IsNullOrEmpty(message))
                        continue;

                    ShowMessage(message);
                    yield return new WaitForSeconds(Mathf.Max(0.1f, secondsPerMessage));
                }
            }

            _messageRoutine = null;
            RefreshCurrentTarget();
        }
    }
}
