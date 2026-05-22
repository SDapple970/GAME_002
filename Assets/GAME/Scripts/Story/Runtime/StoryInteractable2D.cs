// Assets/GAME/Scripts/Story/Runtime/StoryInteractable2D.cs
using Game.Core;
using Game.Story.Data;
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

        private bool _playerInside;

        private void Awake()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            ResolveRunner();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                _playerInside = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                _playerInside = false;
            }
        }

        public void Interact()
        {
            if (!_playerInside) return;

            if (eventDefinition == null)
            {
                Debug.LogWarning("[StoryInteractable2D] Event definition is not assigned.");
                return;
            }

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return;
            }

            ResolveRunner();
            if (runner == null)
            {
                Debug.LogWarning($"[StoryInteractable2D] StoryEventRunner not found for event='{eventDefinition.EventId}'.");
                return;
            }

            if (runner.IsRunning) return;

            runner.StartEvent(eventDefinition);
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
