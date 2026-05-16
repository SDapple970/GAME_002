using System.Collections.Generic;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    public sealed class GameProgressState : MonoBehaviour
    {
        public static GameProgressState Instance { get; private set; }

        [SerializeField] private string currentChapterId;
        [SerializeField] private string currentIncidentId;

        private readonly HashSet<string> _completedObjectives = new();

        public string CurrentChapterId => currentChapterId;
        public string CurrentIncidentId => currentIncidentId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void SetChapter(string chapterId) => currentChapterId = chapterId;
        public void SetIncident(string incidentId) => currentIncidentId = incidentId;
        public void CompleteObjective(string objectiveId)
        {
            if (!string.IsNullOrEmpty(objectiveId))
                _completedObjectives.Add(objectiveId);
        }

        public bool IsObjectiveCompleted(string objectiveId) => !string.IsNullOrEmpty(objectiveId) && _completedObjectives.Contains(objectiveId);
        public List<string> ExportCompletedObjectives() => new(_completedObjectives);

        public void ImportCompletedObjectives(IEnumerable<string> objectiveIds)
        {
            _completedObjectives.Clear();
            if (objectiveIds == null) return;

            foreach (string objectiveId in objectiveIds)
            {
                if (!string.IsNullOrEmpty(objectiveId))
                    _completedObjectives.Add(objectiveId);
            }
        }
    }
}
