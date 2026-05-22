// Assets/GAME/Scripts/Story/Runtime/StoryEventTrigger2D.cs
using Game.Core;
using Game.Story.Data;
using UnityEngine;

namespace Game.Story
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class StoryEventTrigger2D : MonoBehaviour
    {
        [SerializeField] private StoryEventRunner runner;
        [SerializeField] private StoryEventDefinitionSO eventDefinition;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private bool requireExplorationState = true;

        private bool _triggered;

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
            if (_triggered && triggerOnce) return;
            if (!other.CompareTag(playerTag)) return;
            if (!CanStartEvent()) return;

            runner.StartEvent(eventDefinition);
            _triggered = true;

            if (triggerOnce)
            {
                enabled = false;
            }
        }

        private bool CanStartEvent()
        {
            if (eventDefinition == null)
            {
                Debug.LogWarning("[StoryEventTrigger2D] Event definition is not assigned.");
                return false;
            }

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return false;
            }

            ResolveRunner();
            if (runner == null)
            {
                Debug.LogWarning($"[StoryEventTrigger2D] StoryEventRunner not found for event='{eventDefinition.EventId}'.");
                return false;
            }

            return !runner.IsRunning;
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
