using Game.Combat.Integration;
using Game.Common.Identity;
using UnityEngine;

namespace Game.Quest
{
    /// <summary>
    /// Converts a World-confirmed encounter clear into the canonical Quest event.
    /// Save restoration never raises the World clear event, so it cannot replay progress.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterQuestObjectivePublisher : MonoBehaviour
    {
        [SerializeField] private CombatEncounterGroup targetEncounter;
        [SerializeField] private string questId;
        [SerializeField] private string objectiveId;

        private CombatEncounterGroup _subscribedEncounter;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribedEncounter == targetEncounter)
                return;

            Unsubscribe();
            _subscribedEncounter = targetEncounter;
            if (_subscribedEncounter != null)
                _subscribedEncounter.OnEncounterCleared += HandleEncounterCleared;
        }

        private void Unsubscribe()
        {
            if (_subscribedEncounter != null)
                _subscribedEncounter.OnEncounterCleared -= HandleEncounterCleared;

            _subscribedEncounter = null;
        }

        private void HandleEncounterCleared(CombatEncounterGroup clearedEncounter, string completionId)
        {
            if (clearedEncounter != targetEncounter || targetEncounter == null ||
                string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId) ||
                string.IsNullOrWhiteSpace(completionId) ||
                string.IsNullOrWhiteSpace(targetEncounter.EncounterId))
            {
                return;
            }

            GameplayOutcomeIdentity identity = new(
                GameplayOutcomeSourceType.Combat,
                completionId,
                $"quest:{questId}:objective:{objectiveId}");
            QuestEventChannel.Publish(new QuestEvent(
                QuestEventType.ClearEncounter,
                questId,
                objectiveId,
                identity,
                1,
                targetEncounter.gameObject,
                targetEncounter.EncounterId));
        }
    }
}
