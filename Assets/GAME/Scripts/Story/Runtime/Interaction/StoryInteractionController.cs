// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs
using Game.Core;
using Game.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Story.Interaction
{
    public sealed class StoryInteractionController : MonoBehaviour
    {
        public static StoryInteractionController Instance { get; private set; }

        [SerializeField] private StoryInteractionPromptUI promptUI;
        [SerializeField] private StoryEventRunner runner;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.E;
        [FormerlySerializedAs("useFallbackKeyboardInput")]
        [SerializeField] private bool useLegacyFallbackKey = true;
        [SerializeField] private bool debugLogs = false;

        private StoryInteractable2D _current;
        private GameInputInstaller _inputInstaller;
        private InputService _inputService;
        private bool _inputSubscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveRunner();
            ResolvePromptUI();
            promptUI?.Hide();
        }

        private void OnEnable()
        {
            TrySubscribeInput();
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnsubscribeInput();
            promptUI?.Hide();
        }

        private void Update()
        {
            EnsureInputSubscription();

            ResolveRunner();

            if (runner != null && runner.IsRunning)
            {
                promptUI?.Hide();
                return;
            }

            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                promptUI?.Hide();
                return;
            }

            if (_current != null && _current.CanShowPrompt())
            {
                promptUI?.Show(_current.CurrentPromptText);
            }
            else
            {
                promptUI?.Hide();
            }

        }

        public void Register(StoryInteractable2D interactable)
        {
            if (interactable == null) return;

            _current = interactable;

            if (debugLogs)
            {
                Debug.Log(
                    $"[StoryInteractionController] Registered current={interactable.name}, " +
                    $"canShowPrompt={interactable.CanShowPrompt()}, canInteract={interactable.CanInteract}, " +
                    $"prompt={interactable.CurrentPromptText}");
            }

            if (interactable.CanShowPrompt())
            {
                promptUI?.Show(interactable.CurrentPromptText);
            }
        }

        public void Unregister(StoryInteractable2D interactable)
        {
            if (interactable == null || _current != interactable) return;

            _current = null;
            promptUI?.Hide();
        }

        public void TryInteract()
        {
            if (_current == null)
            {
                if (debugLogs)
                {
                    Debug.Log("[StoryInteractionController] TryInteract failed: current is null");
                }

                return;
            }

            bool canShowPrompt = _current.CanShowPrompt();
            bool canInteract = _current.CanInteract;
            if (debugLogs)
            {
                Debug.Log(
                    $"[StoryInteractionController] TryInteract current={GetCurrentName()}, " +
                    $"canShowPrompt={canShowPrompt}, canInteract={canInteract}, " +
                    $"reason={_current.GetCannotInteractReason()}");
            }

            if (!canInteract)
            {
                if (debugLogs)
                {
                    Debug.Log($"[StoryInteractionController] TryInteract blocked by CanInteract=false. current={GetCurrentName()}");
                }

                return;
            }

            promptUI?.Hide();
            if (debugLogs)
            {
                Debug.Log($"[StoryInteractionController] Calling Interact on {GetCurrentName()}");
            }

            _current.Interact();
        }

        public void RefreshCurrentTarget()
        {
            if (_current != null && !_current.CanShowPrompt())
            {
                promptUI?.Hide();
                return;
            }

            if (_current != null)
            {
                promptUI?.Show(_current.CurrentPromptText);
            }
        }

        public void TryInteractCurrent()
        {
            TryInteract();
        }

        public void RequestInteract(StoryInteractable2D interactable)
        {
            Register(interactable);
            TryInteract();
        }

        private void ResolveRunner()
        {
            if (runner != null) return;

#if UNITY_2023_1_OR_NEWER
            runner = FindFirstObjectByType<StoryEventRunner>();
#else
            runner = FindObjectOfType<StoryEventRunner>();
#endif
        }

        private void ResolvePromptUI()
        {
            if (promptUI != null) return;

#if UNITY_2023_1_OR_NEWER
            promptUI = FindFirstObjectByType<StoryInteractionPromptUI>();
#else
            promptUI = FindObjectOfType<StoryInteractionPromptUI>();
#endif
        }

        private string GetCurrentName()
        {
            return _current != null ? _current.name : "null";
        }

        private void TrySubscribeInput()
        {
            EnsureInputSubscription();
        }

        private void EnsureInputSubscription()
        {
            GameInputInstaller installer = global::GameInputInstaller.Instance;
            InputService service = installer != null ? installer.Service : null;

            if (_inputSubscribed && _inputInstaller == installer && _inputService == service)
                return;

            UnsubscribeInput();

            if (installer == null || service == null)
                return;

            _inputInstaller = installer;
            _inputService = service;
            _inputService.ExplorationInteract += HandleInteractInput;
            _inputSubscribed = true;
        }

        private void UnsubscribeInput()
        {
            if (!_inputSubscribed || _inputService == null)
            {
                _inputSubscribed = false;
                _inputInstaller = null;
                _inputService = null;
                return;
            }

            _inputService.ExplorationInteract -= HandleInteractInput;
            _inputSubscribed = false;
            _inputInstaller = null;
            _inputService = null;
        }

        private void HandleInteractInput()
        {
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
                return;

            TryInteract();
        }
    }
}
