// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Story.Interaction
{
    public sealed class StoryInteractionController : MonoBehaviour
    {
        public static StoryInteractionController Instance { get; private set; }

        [SerializeField] private StoryInteractionPromptUI promptUI;
        [SerializeField] private StoryInteractionConfirmUI confirmUI;
        [SerializeField] private StoryEventRunner runner;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.E;
        [SerializeField] private bool useFallbackKeyboardInput = true;
        [SerializeField] private bool autoCreateUIIfMissing = true;

        private readonly List<StoryInteractable2D> _nearby = new();
        private StoryInteractable2D _current;
        private bool _confirmOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveRunner();
            ResolveUI();
        }

        private void Update()
        {
            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                if (!_confirmOpen)
                {
                    promptUI?.Hide();
                }
                return;
            }

            if (!_confirmOpen)
            {
                RefreshCurrentTarget();
            }

            if (useFallbackKeyboardInput && !_confirmOpen && Input.GetKeyDown(fallbackInteractKey))
            {
                TryInteractCurrent();
            }
        }

        public void Register(StoryInteractable2D interactable)
        {
            if (interactable == null || _nearby.Contains(interactable)) return;

            _nearby.Add(interactable);
            RefreshCurrentTarget();
        }

        public void Unregister(StoryInteractable2D interactable)
        {
            if (interactable == null) return;

            _nearby.Remove(interactable);
            if (_current == interactable)
            {
                _current = null;
            }

            RefreshCurrentTarget();
        }

        public void RequestInteract(StoryInteractable2D interactable)
        {
            if (interactable == null || !interactable.CanInteract()) return;

            _current = interactable;
            promptUI?.Hide();

            if (interactable.AskConfirmation)
            {
                _confirmOpen = true;
                if (GameStateMachine.Instance != null)
                {
                    GameStateMachine.Instance.SetState(GameState.UIOnly);
                }

                if (confirmUI != null)
                {
                    confirmUI.Show(interactable.ConfirmationMessage, ConfirmCurrent, CancelConfirm);
                }
                else
                {
                    Debug.LogWarning("[StoryInteractionController] Confirm UI is missing. Starting event without confirmation UI.");
                    ConfirmCurrent();
                }
            }
            else
            {
                interactable.StartLinkedEvent();
                promptUI?.Hide();
            }
        }

        public void ConfirmCurrent()
        {
            confirmUI?.Hide();
            _confirmOpen = false;

            StoryInteractable2D target = _current;
            if (target != null)
            {
                target.StartLinkedEvent();
            }

            promptUI?.Hide();
            RefreshCurrentTarget();
        }

        public void CancelConfirm()
        {
            confirmUI?.Hide();
            _confirmOpen = false;

            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Current == GameState.UIOnly)
            {
                GameStateMachine.Instance.SetState(GameState.Exploration);
            }

            RefreshCurrentTarget();
        }

        public void RefreshCurrentTarget()
        {
            StoryInteractable2D best = null;
            float bestDistance = float.MaxValue;

            for (int i = _nearby.Count - 1; i >= 0; i--)
            {
                StoryInteractable2D candidate = _nearby[i];
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsPlayerInside)
                {
                    _nearby.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanInteract()) continue;

                float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
                if (best == null || candidate.Priority > best.Priority || (candidate.Priority == best.Priority && distance < bestDistance))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            _current = best;
            if (_current != null)
            {
                promptUI?.Show(_current.GetPromptText());
            }
            else
            {
                promptUI?.Hide();
            }
        }

        public void TryInteractCurrent()
        {
            if (_current == null) return;
            RequestInteract(_current);
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

        private void ResolveUI()
        {
            if (promptUI == null)
            {
#if UNITY_2023_1_OR_NEWER
                promptUI = FindFirstObjectByType<StoryInteractionPromptUI>();
#else
                promptUI = FindObjectOfType<StoryInteractionPromptUI>();
#endif
            }

            if (confirmUI == null)
            {
#if UNITY_2023_1_OR_NEWER
                confirmUI = FindFirstObjectByType<StoryInteractionConfirmUI>();
#else
                confirmUI = FindObjectOfType<StoryInteractionConfirmUI>();
#endif
            }

            if (!autoCreateUIIfMissing) return;

            if (promptUI == null)
            {
                promptUI = StoryInteractionAutoUIBootstrapper.CreatePromptUI();
            }

            if (confirmUI == null)
            {
                confirmUI = StoryInteractionAutoUIBootstrapper.CreateConfirmUI();
            }
        }
    }
}
