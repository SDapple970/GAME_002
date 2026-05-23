// Assets/GAME/Scripts/Story/Runtime/StoryInteractable2D.cs
using System;
using System.Collections.Generic;
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
        [SerializeField] private List<StoryCondition> interactionConditions = new();
        [SerializeField] private bool hidePromptWhenConditionsNotMet = true;
        [SerializeField] private string lockedPromptText = "아직 진행할 수 없습니다.";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool requireExplorationState = true;
        [SerializeField] private string promptText = "E: 대화";
        [SerializeField] private StoryInteractionController interactionController;
        [SerializeField] private StorySpeakerAnchor speakerAnchor;

        [Header("Optional Legacy Settings")]
        [SerializeField] private StoryInteractionKind interactionKind = StoryInteractionKind.Use;
        [SerializeField] private string displayName;
        [SerializeField] private string customPromptText;
        [SerializeField] private bool askConfirmation = false;
        [SerializeField] private string confirmationMessage = "사용할까요?";
        [SerializeField] private bool disableAfterUse = false;
        [SerializeField] private bool blockIfEventCompleted = false;
        [SerializeField] private bool rememberUsedWithFlag = false;
        [SerializeField] private string usedFlagKey;
        [SerializeField] private int priority = 0;
        [SerializeField] private bool debugLogs = false;

        private Collider2D _triggerCollider;
        private bool _playerInside;

        public event Action<StoryInteractable2D> OnPlayerEntered;
        public event Action<StoryInteractable2D> OnPlayerExited;

        public StoryEventDefinitionSO EventDefinition => eventDefinition;
        public string DisplayName => displayName;
        public bool AskConfirmation => askConfirmation;
        public string ConfirmationMessage => confirmationMessage;
        public int Priority => priority;
        public string PromptText => ResolvePromptText();
        public string CurrentPromptText => AreInteractionConditionsMet() ? PromptText : ResolveLockedPromptText();
        public bool HidePromptWhenConditionsNotMet => hidePromptWhenConditionsNotMet;
        public string LockedPromptText => lockedPromptText;
        public bool IsPlayerInside => _playerInside;
        public StorySpeakerAnchor SpeakerAnchor => speakerAnchor;

        public bool CanInteract
        {
            get { return GetCannotInteractReason() == "OK"; }
        }

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider2D>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }

            ResolveRunner();
            ResolveInteractionController();
            ResolveSpeakerAnchor();
        }

        private void OnDisable()
        {
            if (_playerInside)
            {
                OnPlayerExited?.Invoke(this);
            }

            interactionController?.Unregister(this);
            _playerInside = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInside = true;
            OnPlayerEntered?.Invoke(this);

            ResolveInteractionController();
            interactionController?.Register(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInside = false;
            OnPlayerExited?.Invoke(this);
            interactionController?.Unregister(this);
        }

        public void Interact()
        {
            if (!CanInteract)
            {
                LogDebug($"[StoryInteractable2D] Interact blocked. current={name} reason='{GetCannotInteractReason()}'");
                return;
            }

            StartLinkedEvent();
        }

        public string GetPromptText()
        {
            return PromptText;
        }

        public string GetCannotInteractReason()
        {
            if (!_playerInside) return "Player is not inside trigger";
            if (eventDefinition == null) return "Event definition is missing";

            ResolveRunner();
            if (runner == null) return "Runner is missing";
            if (runner.IsRunning) return "Runner is already running";

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return $"GameState is not Exploration: {GameStateMachine.Instance.Current}";
            }

            if (IsCompletedEventBlocked()) return $"Event is completed and blocked: {eventDefinition.EventId}";
            if (!AreInteractionConditionsMet()) return "Interaction conditions are not met";
            if (IsUsedWithFlagBlocked()) return $"Used flag is already set: {usedFlagKey}";

            return "OK";
        }

        public bool CanShowPrompt()
        {
            if (!_playerInside) return false;
            if (eventDefinition == null) return false;

            ResolveRunner();
            if (runner == null) return false;
            if (runner.IsRunning) return false;

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return false;
            }

            if (IsCompletedEventBlocked()) return false;
            if (IsUsedWithFlagBlocked()) return false;

            return AreInteractionConditionsMet() || !hidePromptWhenConditionsNotMet;
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

            ResolveSpeakerAnchor();
            runner.StartEvent(eventDefinition, speakerAnchor);
            MarkUsedIfNeeded();

            if (!disableAfterUse) return;

            _playerInside = false;
            interactionController?.Unregister(this);
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = false;
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

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return false;
            }

            if (IsCompletedEventBlocked()) return false;
            if (!AreInteractionConditionsMet()) return false;

            return !IsUsedWithFlagBlocked();
        }

        private bool AreInteractionConditionsMet()
        {
            if (interactionConditions == null || interactionConditions.Count == 0) return true;

            foreach (StoryCondition condition in interactionConditions)
            {
                if (condition != null && !condition.IsMet())
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsUsedWithFlagBlocked()
        {
            if (!rememberUsedWithFlag) return false;
            if (StoryFlagManager.Instance == null) return true;

            return !string.IsNullOrEmpty(usedFlagKey) && StoryFlagManager.Instance.GetBool(usedFlagKey);
        }

        private bool IsCompletedEventBlocked()
        {
            if (!blockIfEventCompleted || eventDefinition == null || StoryProgressManager.Instance == null)
            {
                return false;
            }

            return StoryProgressManager.Instance.IsEventCompleted(eventDefinition.EventId);
        }

        private string ResolveLockedPromptText()
        {
            return string.IsNullOrEmpty(lockedPromptText) ? "아직 사용할 수 없다." : lockedPromptText;
        }

        private string ResolvePromptText()
        {
            if (!string.IsNullOrEmpty(promptText)) return promptText;
            if (!string.IsNullOrEmpty(customPromptText)) return customPromptText;

            string verb = interactionKind switch
            {
                StoryInteractionKind.Talk => "대화",
                StoryInteractionKind.Inspect => "조사",
                StoryInteractionKind.Use => "사용",
                StoryInteractionKind.Read => "읽기",
                StoryInteractionKind.PickUp => "줍기",
                StoryInteractionKind.Open => "열기",
                StoryInteractionKind.Custom => "상호작용",
                _ => "상호작용"
            };

            return string.IsNullOrEmpty(displayName) ? $"E: {verb}" : $"E: {displayName} {verb}";
        }

        private void ResolveInteractionController()
        {
            if (interactionController != null) return;

            interactionController = StoryInteractionController.Instance;
            if (interactionController != null) return;

#if UNITY_2023_1_OR_NEWER
            interactionController = FindFirstObjectByType<StoryInteractionController>();
#else
            interactionController = FindObjectOfType<StoryInteractionController>();
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

        private void ResolveSpeakerAnchor()
        {
            if (speakerAnchor != null) return;

            speakerAnchor = GetComponentInChildren<StorySpeakerAnchor>();
        }

        private void LogDebug(string message)
        {
            if (debugLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
