using UnityEngine;
using UnityEngine.UI;

namespace Game.Quest
{
    public sealed class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField] private QuestManager questManager;
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text objectiveText;

        private bool _subscribed;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            Subscribe();
            Refresh(questManager != null ? questManager.GetActiveQuest() : null);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_subscribed)
                return;

            AutoBindReferences();
            Subscribe();
            if (_subscribed)
                Refresh(questManager.GetActiveQuest());
        }

        private void AutoBindReferences()
        {
            if (root == null)
                root = gameObject;

            if (questManager == null)
                questManager = QuestManager.Instance != null ? QuestManager.Instance : FindFirstObjectByType<QuestManager>();
        }

        private void Subscribe()
        {
            if (_subscribed || questManager == null)
                return;

            questManager.OnQuestChanged += Refresh;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || questManager == null)
            {
                _subscribed = false;
                return;
            }

            questManager.OnQuestChanged -= Refresh;
            _subscribed = false;
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
