using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Common.Identity;
using UnityEngine;

namespace Game.Quest
{
    /// <summary>
    /// Bridges a completed authored field encounter to the canonical Quest event channel.
    /// Combat owns result creation; this integration component only translates a matching result.
    /// </summary>
    public sealed class CombatQuestObjectivePublisher : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint combatEntryPoint;
        [SerializeField] private CombatEncounterGroup targetEncounter;
        [SerializeField] private string questId;
        [SerializeField] private string objectiveId;
        [SerializeField] private QuestEventType eventType = QuestEventType.Kill;

        private CombatEntryPoint _subscribedEntryPoint;

        private void Awake()
        {
            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

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
            if (_subscribedEntryPoint == combatEntryPoint)
                return;

            Unsubscribe();
            _subscribedEntryPoint = combatEntryPoint;
            if (_subscribedEntryPoint != null)
                _subscribedEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void Unsubscribe()
        {
            if (_subscribedEntryPoint != null)
                _subscribedEntryPoint.OnCombatEnded -= HandleCombatEnded;

            _subscribedEntryPoint = null;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (!IsVictory(result) || targetEncounter == null ||
                string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId) ||
                string.IsNullOrWhiteSpace(result.CompletionId) ||
                targetEncounter.ActiveCompletionId != result.CompletionId ||
                result.DefeatedEnemyIds.Count == 0)
            {
                return;
            }

            GameplayOutcomeIdentity identity = new(
                GameplayOutcomeSourceType.Combat,
                result.CompletionId,
                $"quest:{questId}:objective:{objectiveId}");
            QuestEventChannel.Publish(new QuestEvent(
                eventType,
                questId,
                objectiveId,
                identity,
                1,
                targetEncounter.gameObject));
        }

        private static bool IsVictory(CombatResult result)
        {
            return result != null &&
                   (result.EndReason != CombatEndReason.None
                       ? result.EndReason == CombatEndReason.Victory
                       : result.IsWin);
        }
    }
}
