using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private QuestProgress activeQuest;

        private readonly Dictionary<QuestId, QuestProgress> _progressByQuest = new();

        public event Action<QuestProgress> OnQuestChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RaiseChanged();
        }

        public void StartQuest(QuestDataSO quest)
        {
            if (quest == null)
            {
                Debug.LogWarning("[QuestManager] StartQuest ignored. Quest is null.", this);
                return;
            }

            if (!_progressByQuest.TryGetValue(quest.QuestId, out QuestProgress progress))
            {
                progress = new QuestProgress(quest);
                _progressByQuest[quest.QuestId] = progress;
            }
            else
            {
                progress.quest = quest;
                if (progress.completed)
                {
                    progress.currentStep = quest.GetFirstStepIndex();
                    progress.completed = false;
                }
            }

            activeQuest = progress;
            Debug.Log($"[QuestManager] StartQuest: {quest.QuestId}", this);
            RaiseChanged();
        }

        public void SetStep(QuestId id, int step)
        {
            QuestProgress progress = GetProgress(id);
            if (progress == null || progress.completed)
            {
                Debug.LogWarning($"[QuestManager] SetStep ignored. Quest is not active. id={id}", this);
                return;
            }

            progress.currentStep = Mathf.Max(0, step);
            activeQuest = progress;
            Debug.Log($"[QuestManager] SetStep: {id} -> {progress.currentStep}", this);
            RaiseChanged();
        }

        public void AdvanceStep(QuestId id)
        {
            QuestProgress progress = GetProgress(id);
            if (progress == null || progress.completed)
            {
                Debug.LogWarning($"[QuestManager] AdvanceStep ignored. Quest is not active. id={id}", this);
                return;
            }

            int nextStep = progress.quest != null
                ? progress.quest.GetNextStepIndex(progress.currentStep)
                : progress.currentStep + 1;

            SetStep(id, nextStep);
        }

        public void CompleteQuest(QuestId id)
        {
            QuestProgress progress = GetProgress(id);
            if (progress == null)
            {
                Debug.LogWarning($"[QuestManager] CompleteQuest ignored. Quest is not active. id={id}", this);
                return;
            }

            progress.completed = true;

            if (activeQuest == progress)
                activeQuest = null;

            int gold = progress.quest != null ? progress.quest.RewardGold : 0;
            int exp = progress.quest != null ? progress.quest.RewardExp : 0;
            Debug.Log($"[QuestManager] CompleteQuest: {id}, rewardGold={gold}, rewardExp={exp}", this);
            RaiseChanged();
        }

        public QuestProgress GetActiveQuest()
        {
            return activeQuest != null && !activeQuest.completed ? activeQuest : null;
        }

        public bool IsActiveQuest(QuestId id)
        {
            QuestProgress active = GetActiveQuest();
            return active != null && active.QuestId == id;
        }

        public QuestProgress GetProgress(QuestId id)
        {
            if (id == QuestId.None)
                return null;

            _progressByQuest.TryGetValue(id, out QuestProgress progress);
            return progress;
        }

        private void RaiseChanged()
        {
            OnQuestChanged?.Invoke(GetActiveQuest());
        }
    }
}
