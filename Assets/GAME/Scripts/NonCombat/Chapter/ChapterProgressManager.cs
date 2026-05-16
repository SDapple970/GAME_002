using System.Collections.Generic;
using Game.NonCombat.Progress;
using UnityEngine;

namespace Game.NonCombat.Chapter
{
    public sealed class ChapterProgressManager : MonoBehaviour
    {
        public static ChapterProgressManager Instance { get; private set; }

        [SerializeField] private string currentChapterId;
        [SerializeField] private GameProgressState progressState;

        private readonly HashSet<string> _completedObjectives = new();

        public string CurrentChapterId => currentChapterId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void SetChapter(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) return;
            currentChapterId = chapterId;
            (progressState != null ? progressState : GameProgressState.Instance)?.SetChapter(chapterId);
            Debug.Log($"[Chapter] Current chapter = {chapterId}", this);
        }

        public void CompleteObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId)) return;
            _completedObjectives.Add(objectiveId);
            (progressState != null ? progressState : GameProgressState.Instance)?.CompleteObjective(objectiveId);
            Debug.Log($"[Chapter] Objective completed: {objectiveId}", this);
        }

        public bool IsObjectiveCompleted(string objectiveId) => !string.IsNullOrEmpty(objectiveId) && _completedObjectives.Contains(objectiveId);
        public List<string> ExportCompletedObjectives() => new(_completedObjectives);

        public void Import(string chapterId, IEnumerable<string> completedObjectives)
        {
            currentChapterId = chapterId;
            _completedObjectives.Clear();

            if (completedObjectives != null)
            {
                foreach (string objectiveId in completedObjectives)
                {
                    if (!string.IsNullOrEmpty(objectiveId))
                        _completedObjectives.Add(objectiveId);
                }
            }

            GameProgressState state = progressState != null ? progressState : GameProgressState.Instance;
            if (state != null)
            {
                state.SetChapter(currentChapterId);
                state.ImportCompletedObjectives(_completedObjectives);
            }
        }
    }
}
