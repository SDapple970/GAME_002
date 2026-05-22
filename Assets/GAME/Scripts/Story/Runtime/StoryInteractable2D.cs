// Assets/GAME/Scripts/Story/Runtime/StoryInteractable2D.cs
using Game.Core;
using Game.Story.Data;
using Game.Story.Interaction;
using UnityEngine;

namespace Game.Story
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class StoryInteractable2D : MonoBehaviour
    {
        [SerializeField] private StoryEventRunner runner;
        [SerializeField] private StoryEventDefinitionSO eventDefinition;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool requireExplorationState = true;
        [SerializeField] private StoryInteractionKind interactionKind = StoryInteractionKind.Use;
        [SerializeField] private string displayName;
        [SerializeField] private string customPromptText;
        [SerializeField] private bool askConfirmation = true;
        [SerializeField] private string confirmationMessage = "사용할까요?";
        [SerializeField] private bool disableAfterUse = false;
        [SerializeField] private bool rememberUsedWithFlag = false;
        [SerializeField] private string usedFlagKey;
        [SerializeField] private int priority = 0;

        private Collider2D _triggerCollider;
        private StoryInteractionController _controller;
        private bool _playerInside;

        public StoryEventDefinitionSO EventDefinition => eventDefinition;
        public string DisplayName => displayName;
        public bool AskConfirmation => askConfirmation;
        public string ConfirmationMessage => confirmationMessage;
        public int Priority => priority;
        public bool IsPlayerInside => _playerInside;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider2D>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }

            ResolveRunner();
            ResolveController();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Unregister(this);
            }

            _playerInside = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                _playerInside = true;
                ResolveController();
                _controller?.Register(this);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                _playerInside = false;
                _controller?.Unregister(this);
            }
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            ResolveController();
            if (askConfirmation)
            {
                if (_controller != null)
                {
                    _controller.RequestInteract(this);
                }
                else
                {
                    Debug.LogWarning($"[StoryInteractable2D] StoryInteractionController not found for event='{eventDefinition.EventId}'.");
                }

                return;
            }

            StartLinkedEvent();
        }

        public string GetPromptText()
        {
            if (!string.IsNullOrEmpty(customPromptText)) return customPromptText;

            string verb = interactionKind switch
            {
                StoryInteractionKind.Talk => "대화하기",
                StoryInteractionKind.Inspect => "조사하기",
                StoryInteractionKind.Use => "사용하기",
                StoryInteractionKind.Read => "읽기",
                StoryInteractionKind.PickUp => "줍기",
                StoryInteractionKind.Open => "열기",
                StoryInteractionKind.Custom => "상호작용",
                _ => "상호작용"
            };

            return string.IsNullOrEmpty(displayName) ? $"[E] {verb}" : $"[E] {displayName} {verb}";
        }

        public bool CanInteract()
        {
            if (!_playerInside) return false;

            if (eventDefinition == null)
            {
                Debug.LogWarning("[StoryInteractable2D] Event definition is not assigned.");
                return false;
            }

            ResolveRunner();
            ResolveController();

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return false;
            }

            if (runner == null)
            {
                Debug.LogWarning($"[StoryInteractable2D] StoryEventRunner not found for event='{eventDefinition.EventId}'.");
                return false;
            }

            if (runner.IsRunning) return false;

            if (askConfirmation && _controller == null)
            {
                Debug.LogWarning($"[StoryInteractable2D] StoryInteractionController not found for event='{eventDefinition.EventId}'.");
                return false;
            }

            if (rememberUsedWithFlag)
            {
                if (StoryFlagManager.Instance == null)
                {
                    Debug.LogWarning($"[StoryInteractable2D] StoryFlagManager missing for used flag key='{usedFlagKey}'.");
                    return false;
                }

                if (!string.IsNullOrEmpty(usedFlagKey) && StoryFlagManager.Instance.GetBool(usedFlagKey))
                {
                    return false;
                }
            }

            return true;
        }

        public void MarkUsedIfNeeded()
        {
            if (!rememberUsedWithFlag) return;

            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning($"[StoryInteractable2D] StoryFlagManager missing. Used flag key='{usedFlagKey}' was not set.");
                return;
            }

            if (string.IsNullOrEmpty(usedFlagKey))
            {
                Debug.LogWarning($"[StoryInteractable2D] Empty used flag key for event='{eventDefinition?.EventId}'.");
                return;
            }

            StoryFlagManager.Instance.SetBool(usedFlagKey, true);
        }

        public void StartLinkedEvent()
        {
            if (!CanStartLinkedEvent()) return;

            runner.StartEvent(eventDefinition);
            MarkUsedIfNeeded();

            if (disableAfterUse)
            {
                _playerInside = false;
                _controller?.Unregister(this);
                if (_triggerCollider != null)
                {
                    _triggerCollider.enabled = false;
                }
            }
        }

        private bool CanStartLinkedEvent()
        {
            if (eventDefinition == null)
            {
                Debug.LogWarning("[StoryInteractable2D] Event definition is not assigned.");
                return false;
            }

            ResolveRunner();
            if (runner == null)
            {
                Debug.LogWarning($"[StoryInteractable2D] StoryEventRunner not found for event='{eventDefinition.EventId}'.");
                return false;
            }

            if (runner.IsRunning) return false;

            if (!rememberUsedWithFlag) return true;

            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning($"[StoryInteractable2D] StoryFlagManager missing for used flag key='{usedFlagKey}'.");
                return false;
            }

            return string.IsNullOrEmpty(usedFlagKey) || !StoryFlagManager.Instance.GetBool(usedFlagKey);
        }

        private void ResolveController()
        {
            if (_controller != null) return;

            _controller = StoryInteractionController.Instance;
            if (_controller != null) return;

#if UNITY_2023_1_OR_NEWER
            _controller = FindFirstObjectByType<StoryInteractionController>();
#else
            _controller = FindObjectOfType<StoryInteractionController>();
#endif
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
    }
}
