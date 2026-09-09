using Game.Common.Identity;
using Game.Interaction;
using UnityEngine;

namespace Game.Quest
{
    /// <summary>
    /// Translates a successful production InteractionRunner outcome into the canonical
    /// quest channel. It deliberately owns no quest state and never grants rewards.
    /// </summary>
    public sealed class InteractionQuestObjectivePublisher : MonoBehaviour
    {
        [SerializeField] private InteractionRunner interactionRunner;
        [SerializeField] private string questId;
        [SerializeField] private string objectiveId;
        [SerializeField] private string targetInteractionId;
        [SerializeField] private QuestEventType eventType = QuestEventType.Interact;

        private InteractionRunner _subscribedRunner;

        private void Awake()
        {
            if (interactionRunner == null)
                interactionRunner = InteractionRunner.Instance != null
                    ? InteractionRunner.Instance
                    : FindFirstObjectByType<InteractionRunner>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (interactionRunner == null)
                interactionRunner = InteractionRunner.Instance != null
                    ? InteractionRunner.Instance
                    : FindFirstObjectByType<InteractionRunner>();

            if (_subscribedRunner == interactionRunner)
                return;

            Unsubscribe();
            _subscribedRunner = interactionRunner;
            if (_subscribedRunner != null)
                _subscribedRunner.OnInteractionCompleted += HandleInteractionCompleted;
        }

        private void Unsubscribe()
        {
            if (_subscribedRunner != null)
                _subscribedRunner.OnInteractionCompleted -= HandleInteractionCompleted;
            _subscribedRunner = null;
        }

        private void HandleInteractionCompleted(InteractionResult result)
        {
            string targetId = InteractionIdentity.Normalize(targetInteractionId);
            if (!result.Succeeded || targetId == null || result.SourceId != targetId ||
                string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId) ||
                eventType != QuestEventType.Interact)
            {
                return;
            }

            GameplayOutcomeIdentity identity = new(
                GameplayOutcomeSourceType.Interaction,
                targetId,
                $"quest:{questId}:objective:{objectiveId}");
            QuestEventChannel.Publish(new QuestEvent(
                eventType,
                questId,
                objectiveId,
                identity,
                1,
                gameObject,
                targetId));
        }
    }
}
