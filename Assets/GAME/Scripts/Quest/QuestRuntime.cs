using System;
using System.Collections.Generic;
using Game.Mission;
using Game.Mission.Data;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestRuntime : MonoBehaviour
    {
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private QuestDefinitionSO[] questDefinitions;

        private readonly Dictionary<string, RuntimeQuestState> _runtimeByQuestId = new();

        public event Action<string> OnQuestCompleted;
        public event Action<string, string, int, int> OnObjectiveProgressChanged;

        private void Awake()
        {
            ResolveMissionManager();
            RegisterSerializedDefinitions();
        }

        public void StartQuest(MissionDefinitionSO definition)
        {
            ResolveMissionManager();
            missionManager?.StartMission(definition);
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            ResolveMissionManager();
            missionManager?.CompleteObjective(questId, objectiveId);
        }

        public void CompleteQuest(string questId)
        {
            ResolveMissionManager();
            missionManager?.CompleteMission(questId);
        }

        public void StartQuest(QuestDefinitionSO definition)
        {
            if (definition == null)
            {
                Debug.LogWarning("[QuestRuntime] StartQuest ignored. QuestDefinitionSO is null.", this);
                return;
            }

            RuntimeQuestState state = GetOrCreateState(GetQuestId(definition), definition);
            state.Definition = definition;
            state.Completed = false;
        }

        public bool ApplyEvent(QuestEvent questEvent)
        {
            if (questEvent.Type == QuestEventType.Unknown)
                return false;

            string questId = questEvent.QuestId;
            if (string.IsNullOrWhiteSpace(questId))
            {
                Debug.LogWarning($"[QuestRuntime] QuestEvent ignored. QuestId is empty. type={questEvent.Type}, objectiveId={questEvent.ObjectiveId}", this);
                return false;
            }

            RuntimeQuestState state = GetOrCreateState(questId, FindDefinition(questId));
            if (state.Completed)
                return false;

            QuestObjectiveDefinition objective = state.Definition != null
                ? state.Definition.FindObjective(questEvent)
                : null;

            if (state.Definition != null && objective == null)
                return false;

            string objectiveId = objective != null ? objective.ObjectiveId : questEvent.ObjectiveId;
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                Debug.LogWarning($"[QuestRuntime] QuestEvent ignored. ObjectiveId is empty. questId={questId}, type={questEvent.Type}", this);
                return false;
            }

            int requiredCount = objective != null ? objective.RequiredCount : 1;
            int amount = Mathf.Max(1, questEvent.Amount);
            int current = state.GetProgress(objectiveId);
            int next = Mathf.Min(current + amount, requiredCount);

            if (next == current)
                return false;

            state.SetProgress(objectiveId, next);
            OnObjectiveProgressChanged?.Invoke(questId, objectiveId, next, requiredCount);

            if (state.Definition != null && state.AreRequiredObjectivesComplete())
            {
                state.Completed = true;
                OnQuestCompleted?.Invoke(questId);
            }

            return true;
        }

        public bool IsQuestComplete(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state) &&
                   state.Completed;
        }

        public int GetObjectiveProgress(string questId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
                return 0;

            return _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.GetProgress(objectiveId)
                : 0;
        }

        private void ResolveMissionManager()
        {
            if (missionManager == null)
                missionManager = MissionManager.Instance != null
                    ? MissionManager.Instance
                    : FindFirstObjectByType<MissionManager>();
        }

        private void RegisterSerializedDefinitions()
        {
            if (questDefinitions == null)
                return;

            for (int i = 0; i < questDefinitions.Length; i++)
            {
                QuestDefinitionSO definition = questDefinitions[i];
                if (definition == null)
                    continue;

                GetOrCreateState(GetQuestId(definition), definition);
            }
        }

        private RuntimeQuestState GetOrCreateState(string questId, QuestDefinitionSO definition)
        {
            if (string.IsNullOrWhiteSpace(questId))
                questId = definition != null ? definition.name : string.Empty;

            if (!_runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state))
            {
                state = new RuntimeQuestState(questId, definition);
                _runtimeByQuestId[questId] = state;
            }
            else if (state.Definition == null && definition != null)
            {
                state.Definition = definition;
            }

            return state;
        }

        private QuestDefinitionSO FindDefinition(string questId)
        {
            if (questDefinitions == null)
                return null;

            for (int i = 0; i < questDefinitions.Length; i++)
            {
                QuestDefinitionSO definition = questDefinitions[i];
                if (definition != null && GetQuestId(definition) == questId)
                    return definition;
            }

            return null;
        }

        private static string GetQuestId(QuestDefinitionSO definition)
        {
            if (definition == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(definition.QuestId)
                ? definition.QuestId
                : definition.name;
        }

        private sealed class RuntimeQuestState
        {
            private readonly Dictionary<string, int> _progressByObjectiveId = new();

            public RuntimeQuestState(string questId, QuestDefinitionSO definition)
            {
                QuestId = questId;
                Definition = definition;
            }

            public string QuestId { get; }
            public QuestDefinitionSO Definition { get; set; }
            public bool Completed { get; set; }

            public int GetProgress(string objectiveId)
            {
                return !string.IsNullOrWhiteSpace(objectiveId) &&
                       _progressByObjectiveId.TryGetValue(objectiveId, out int progress)
                    ? progress
                    : 0;
            }

            public void SetProgress(string objectiveId, int progress)
            {
                if (!string.IsNullOrWhiteSpace(objectiveId))
                    _progressByObjectiveId[objectiveId] = Mathf.Max(0, progress);
            }

            public bool AreRequiredObjectivesComplete()
            {
                if (Definition == null || Definition.Objectives == null)
                    return false;

                bool hasRequiredObjective = false;
                for (int i = 0; i < Definition.Objectives.Length; i++)
                {
                    QuestObjectiveDefinition objective = Definition.Objectives[i];
                    if (objective == null || objective.Optional)
                        continue;

                    hasRequiredObjective = true;
                    if (GetProgress(objective.ObjectiveId) < objective.RequiredCount)
                        return false;
                }

                return hasRequiredObjective;
            }
        }
    }
}
