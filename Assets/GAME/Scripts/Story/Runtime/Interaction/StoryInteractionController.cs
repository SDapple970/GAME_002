// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs
using Game.Core;
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

        private StoryInteractable2D _current;

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

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            promptUI?.Hide();
        }

        private void Update()
        {
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

            if (_current != null && _current.CanInteract)
            {
                promptUI?.Show(_current.PromptText);
            }
            else
            {
                promptUI?.Hide();
            }

            if (useLegacyFallbackKey && Input.GetKeyDown(fallbackInteractKey))
            {
                TryInteract();
            }
        }

        public void Register(StoryInteractable2D interactable)
        {
            if (interactable == null) return;

            _current = interactable;

            if (interactable.CanInteract)
            {
                promptUI?.Show(interactable.PromptText);
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
            if (_current == null) return;
            if (!_current.CanInteract) return;

            promptUI?.Hide();
            _current.Interact();
        }

        public void RefreshCurrentTarget()
        {
            if (_current != null && !_current.CanInteract)
            {
                promptUI?.Hide();
                return;
            }

            if (_current != null)
            {
                promptUI?.Show(_current.PromptText);
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
    }
}
