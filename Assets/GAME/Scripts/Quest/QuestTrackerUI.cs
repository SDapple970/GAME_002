using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Quest
{
    public sealed class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField] private QuestManager questManager;
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text objectiveText;

        private bool _legacySubscribed;
        private bool _runtimeSubscribed;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            Subscribe();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RefreshCurrent();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void AutoBindReferences()
        {
            if (root == null)
                root = gameObject;

            if (questManager == null)
                questManager = QuestManager.Instance != null ? QuestManager.Instance : FindFirstObjectByType<QuestManager>();

            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }

        private void Subscribe()
        {
            if (!_runtimeSubscribed && questRuntime != null)
            {
                questRuntime.OnQuestStarted += HandleQuestStarted;
                questRuntime.OnObjectiveProgressChanged += HandleObjectiveProgressChanged;
                questRuntime.OnQuestCompleted += HandleQuestCompleted;
                questRuntime.OnQuestFailed += HandleQuestFailed;
                questRuntime.OnQuestRetried += HandleQuestRetried;
                questRuntime.OnObjectiveVisibilityChanged += HandleObjectiveVisibilityChanged;
                questRuntime.OnStateRestored += HandleQuestStateRestored;
                _runtimeSubscribed = true;
            }

            if (!_legacySubscribed && questManager != null)
            {
                questManager.OnQuestChanged += Refresh;
                _legacySubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_runtimeSubscribed && questRuntime != null)
            {
                questRuntime.OnQuestStarted -= HandleQuestStarted;
                questRuntime.OnObjectiveProgressChanged -= HandleObjectiveProgressChanged;
                questRuntime.OnQuestCompleted -= HandleQuestCompleted;
                questRuntime.OnQuestFailed -= HandleQuestFailed;
                questRuntime.OnQuestRetried -= HandleQuestRetried;
                questRuntime.OnObjectiveVisibilityChanged -= HandleObjectiveVisibilityChanged;
                questRuntime.OnStateRestored -= HandleQuestStateRestored;
            }

            if (_legacySubscribed && questManager != null)
                questManager.OnQuestChanged -= Refresh;

            _runtimeSubscribed = false;
            _legacySubscribed = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Unsubscribe();
            AutoBindReferences();
            Subscribe();
            RefreshCurrent();
        }

        private void HandleQuestStarted(string questId) => RefreshCurrent();

        private void HandleObjectiveProgressChanged(string questId, string objectiveId, int current, int required)
        {
            RefreshCurrent();
        }

        private void HandleQuestCompleted(string questId) => RefreshCurrent();

        private void HandleQuestFailed(string questId, string reasonId) => RefreshCurrent();

        private void HandleQuestRetried(string questId, int attempt) => RefreshCurrent();

        private void HandleObjectiveVisibilityChanged(string questId, string objectiveId, bool visible) => RefreshCurrent();

        private void HandleQuestStateRestored() => RefreshCurrent();

        private void RefreshCurrent()
        {
            if (questRuntime != null && questRuntime.TryGetFirstActiveQuestId(out string questId))
            {
                RefreshRuntimeQuest(questId);
                return;
            }

            if (questRuntime != null && questRuntime.TryGetFirstFailedQuestId(out questId))
            {
                RefreshFailedQuest(questId);
                return;
            }

            Refresh(questManager != null ? questManager.GetActiveQuest() : null);
        }

        private void RefreshFailedQuest(string questId)
        {
            string title = questId;
            if (questRuntime.TryGetQuestTitle(questId, out string configuredTitle))
                title = configuredTitle;

            if (titleText != null)
                titleText.text = title;
            if (objectiveText != null)
                objectiveText.text = "Failed";
            SetVisible(true);
        }

        private void RefreshRuntimeQuest(string questId)
        {
            string title = questId;
            if (questRuntime.TryGetQuestTitle(questId, out string configuredTitle))
                title = configuredTitle;

            if (titleText != null)
                titleText.text = title;

            if (objectiveText != null)
                objectiveText.text = BuildRuntimeObjectiveText(questId);

            SetVisible(true);
        }

        private string BuildRuntimeObjectiveText(string questId)
        {
            if (!questRuntime.TryGetDefinition(questId, out _))
            {
                return questId;
            }

            System.Collections.Generic.IReadOnlyList<QuestObjectiveDefinition> objectives =
                questRuntime.GetVisibleObjectives(questId);
            for (int i = 0; i < objectives.Count; i++)
            {
                QuestObjectiveDefinition objective = objectives[i];
                if (objective == null || !questRuntime.IsObjectiveActive(questId, objective.ObjectiveId))
                    continue;

                int current = questRuntime.GetObjectiveProgress(questId, objective.ObjectiveId);
                string description = string.IsNullOrWhiteSpace(objective.Description)
                    ? objective.ObjectiveId
                    : objective.Description;
                return $"{description} {current}/{objective.RequiredCount}";
            }

            return string.Empty;
        }

        private void Refresh(QuestProgress progress)
        {
            if (progress == null || progress.quest == null)
            {
                SetVisible(false);
                return;
            }

            QuestStepData step = progress.CurrentStepData;
            if (step != null && !step.showInTracker)
            {
                SetVisible(false);
                return;
            }

            if (titleText != null)
                titleText.text = progress.quest.QuestTitle;

            if (objectiveText != null)
                objectiveText.text = step != null ? step.objectiveText : string.Empty;

            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }
    }
}
