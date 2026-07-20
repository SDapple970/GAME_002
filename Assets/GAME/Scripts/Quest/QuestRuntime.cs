using System;
using System.Collections.Generic;
using Game.Mission;
using Game.Mission.Data;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestRuntime : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        private const int MaxRememberedEventIdsPerQuest = 256;

        [SerializeField] private MissionManager missionManager;
        [SerializeField] private QuestDefinitionSO[] questDefinitions;

        private readonly Dictionary<string, RuntimeQuestState> _runtimeByQuestId = new();

        public event Action<string> OnQuestStarted;
        public event Action<string> OnQuestCompleted;
        public event Action<string, string, int, int> OnObjectiveProgressChanged;

        private void Awake()
        {
            ResolveMissionManager();
            RegisterSerializedDefinitions();
        }

        // Legacy MissionDefinitionSO compatibility. New production quests use QuestDefinitionSO.
        public void StartQuest(MissionDefinitionSO definition)
        {
            ResolveMissionManager();
            missionManager?.StartMission(definition);
        }

        public void StartQuest(QuestDefinitionSO definition)
        {
            if (definition == null)
            {
                Debug.LogWarning("[QuestRuntime] StartQuest ignored. QuestDefinitionSO is null.", this);
                return;
            }

            string questId = GetQuestId(definition);
            RuntimeQuestState state = GetOrCreateState(questId, definition);
            state.Definition = definition;
            if (state.Status == QuestStatus.Completed || state.Status == QuestStatus.Active)
                return;

            state.Status = QuestStatus.Active;
            OnQuestStarted?.Invoke(questId);
        }

        public bool ApplyEvent(QuestEvent questEvent)
        {
            if (questEvent.Type == QuestEventType.Unknown)
                return false;

            if (questEvent.Amount <= 0)
            {
                Debug.LogWarning(
                    $"[QuestRuntime] QuestEvent ignored. Amount must be positive. questId={questEvent.QuestId}, objectiveId={questEvent.ObjectiveId}, amount={questEvent.Amount}.",
                    this);
                return false;
            }

            string questId = questEvent.QuestId;
            if (string.IsNullOrWhiteSpace(questId))
            {
                Debug.LogWarning($"[QuestRuntime] QuestEvent ignored. QuestId is empty. type={questEvent.Type}, objectiveId={questEvent.ObjectiveId}", this);
                return false;
            }

            if (!_runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state) ||
                state.Status != QuestStatus.Active)
            {
                return false;
            }

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

            int requiredCount = objective != null
                ? objective.RequiredCount
                : state.GetRequiredCount(objectiveId);
            if (requiredCount <= 0)
                return false;

            int current = state.GetProgress(objectiveId);
            int next = Mathf.Min(current + questEvent.Amount, requiredCount);
            if (next == current)
                return false;

            if (!string.IsNullOrWhiteSpace(questEvent.EventId) &&
                !state.TryRememberEventId(questEvent.EventId, MaxRememberedEventIdsPerQuest))
            {
                return false;
            }

            ApplyProgressChange(state, objectiveId, next, requiredCount);
            return true;
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            if (!TryGetActiveState(questId, out RuntimeQuestState state) ||
                string.IsNullOrWhiteSpace(objectiveId))
            {
                return;
            }

            int requiredCount = state.GetRequiredCount(objectiveId);
            if (requiredCount <= 0 || state.GetProgress(objectiveId) >= requiredCount)
                return;

            ApplyProgressChange(state, objectiveId, requiredCount, requiredCount);
        }

        public void CompleteQuest(string questId)
        {
            if (!TryGetActiveState(questId, out RuntimeQuestState state))
                return;

            CompleteState(state);
        }

        public void ConfigureCompatibilityQuest(
            string questId,
            int requiredEnemyKills,
            bool requireNpcTalk,
            bool requireNpcRescue)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                Debug.LogWarning("[QuestRuntime] Compatibility quest mapping ignored. QuestId is empty.", this);
                return;
            }

            RuntimeQuestState state = GetOrCreateState(questId, FindDefinition(questId));
            state.ConfigureObjective("enemy_defeated", Mathf.Max(0, requiredEnemyKills), requiredEnemyKills <= 0);
            state.ConfigureObjective("npc_talked", requireNpcTalk ? 1 : 0, !requireNpcTalk);
            state.ConfigureObjective("npc_rescued", requireNpcRescue ? 1 : 0, !requireNpcRescue);
            if (state.Status != QuestStatus.Completed)
            {
                bool newlyActive = state.Status != QuestStatus.Active;
                state.Status = QuestStatus.Active;
                if (newlyActive)
                    OnQuestStarted?.Invoke(questId);
            }
        }

        public void ResetQuestProgress(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) ||
                !_runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state))
            {
                return;
            }

            state.ResetProgress();
            state.Status = QuestStatus.Active;
            OnQuestStarted?.Invoke(questId);
        }

        public bool IsQuestActive(string questId)
        {
            return GetQuestStatus(questId) == QuestStatus.Active;
        }

        public QuestStatus GetQuestStatus(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.Status
                : QuestStatus.Inactive;
        }

        public bool IsQuestComplete(string questId)
        {
            return GetQuestStatus(questId) == QuestStatus.Completed;
        }

        public int GetObjectiveProgress(string questId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
                return 0;

            return _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.GetProgress(objectiveId)
                : 0;
        }

        public int GetObjectiveRequiredCount(string questId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
                return 0;

            return _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.GetRequiredCount(objectiveId)
                : 0;
        }

        public bool HasQuest(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) && _runtimeByQuestId.ContainsKey(questId);
        }

        public bool TryGetDefinition(string questId, out QuestDefinitionSO definition)
        {
            definition = FindDefinition(questId);
            if (definition == null &&
                !string.IsNullOrWhiteSpace(questId) &&
                _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state))
            {
                definition = state.Definition;
            }

            return definition != null;
        }

        public bool TryGetFirstActiveQuestId(out string questId)
        {
            questId = null;
            foreach (KeyValuePair<string, RuntimeQuestState> pair in _runtimeByQuestId)
            {
                if (pair.Value.Status != QuestStatus.Active)
                    continue;

                questId = pair.Key;
                return true;
            }

            return false;
        }

        public bool TryGetQuestReward(string questId, out int gold, out int exp)
        {
            gold = 0;
            exp = 0;
            if (!TryGetDefinition(questId, out QuestDefinitionSO definition))
                return false;

            gold = definition.RewardGold;
            exp = definition.RewardExp;
            return gold > 0 || exp > 0;
        }

        public bool TryGetQuestTitle(string questId, out string questTitle)
        {
            questTitle = null;
            if (!TryGetDefinition(questId, out QuestDefinitionSO definition) ||
                string.IsNullOrWhiteSpace(definition.QuestTitle))
            {
                return false;
            }

            questTitle = definition.QuestTitle;
            return true;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.quest ??= new QuestSaveData();
            saveData.quest.quests.Clear();
            foreach (KeyValuePair<string, RuntimeQuestState> pair in _runtimeByQuestId)
            {
                RuntimeQuestState state = pair.Value;
                if (state == null || string.IsNullOrWhiteSpace(state.QuestId))
                    continue;

                QuestStateSaveData questState = new()
                {
                    questId = state.QuestId,
                    completed = state.Status == QuestStatus.Completed
                };
                state.AppendObjectiveSaveData(questState.objectives);
                saveData.quest.quests.Add(questState);
            }
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            if (saveData?.quest?.quests == null)
                return;

            for (int i = 0; i < saveData.quest.quests.Count; i++)
            {
                QuestStateSaveData questState = saveData.quest.quests[i];
                if (questState == null || string.IsNullOrWhiteSpace(questState.questId))
                    continue;

                RuntimeQuestState state = GetOrCreateState(questState.questId, FindDefinition(questState.questId));
                state.Status = questState.completed ? QuestStatus.Completed : QuestStatus.Active;
                state.ApplyObjectiveSaveData(questState.objectives);
                state.ClearRememberedEventIds();
            }
        }

        private void ApplyProgressChange(RuntimeQuestState state, string objectiveId, int next, int requiredCount)
        {
            state.SetProgress(objectiveId, next);
            OnObjectiveProgressChanged?.Invoke(state.QuestId, objectiveId, next, requiredCount);
            if (state.AreRequiredObjectivesComplete())
                CompleteState(state);
        }

        private void CompleteState(RuntimeQuestState state)
        {
            if (state == null || state.Status != QuestStatus.Active)
                return;

            state.Status = QuestStatus.Completed;
            OnQuestCompleted?.Invoke(state.QuestId);
        }

        private bool TryGetActiveState(string questId, out RuntimeQuestState state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out state) &&
                   state.Status == QuestStatus.Active;
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
                if (definition != null)
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
            private readonly Dictionary<string, ObjectiveRequirement> _compatibilityRequirements = new();
            private readonly HashSet<string> _rememberedEventIds = new();
            private readonly Queue<string> _eventIdOrder = new();

            public RuntimeQuestState(string questId, QuestDefinitionSO definition)
            {
                QuestId = questId;
                Definition = definition;
                Status = QuestStatus.Inactive;
            }

            public string QuestId { get; }
            public QuestDefinitionSO Definition { get; set; }
            public QuestStatus Status { get; set; }

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

            public void ConfigureObjective(string objectiveId, int requiredCount, bool optional)
            {
                if (!string.IsNullOrWhiteSpace(objectiveId))
                    _compatibilityRequirements[objectiveId] = new ObjectiveRequirement(Mathf.Max(0, requiredCount), optional);
            }

            public int GetRequiredCount(string objectiveId)
            {
                if (string.IsNullOrWhiteSpace(objectiveId))
                    return 0;

                QuestObjectiveDefinition definitionObjective = FindDefinitionObjective(objectiveId);
                if (definitionObjective != null)
                    return definitionObjective.RequiredCount;

                return _compatibilityRequirements.TryGetValue(objectiveId, out ObjectiveRequirement requirement)
                    ? requirement.RequiredCount
                    : 0;
            }

            public bool TryRememberEventId(string eventId, int limit)
            {
                if (!_rememberedEventIds.Add(eventId))
                    return false;

                _eventIdOrder.Enqueue(eventId);
                while (_eventIdOrder.Count > Mathf.Max(1, limit))
                    _rememberedEventIds.Remove(_eventIdOrder.Dequeue());
                return true;
            }

            public void ClearRememberedEventIds()
            {
                _rememberedEventIds.Clear();
                _eventIdOrder.Clear();
            }

            public void ResetProgress()
            {
                _progressByObjectiveId.Clear();
                ClearRememberedEventIds();
            }

            public bool AreRequiredObjectivesComplete()
            {
                if (Definition == null || Definition.Objectives == null)
                    return AreCompatibilityObjectivesComplete();

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

            public void AppendObjectiveSaveData(List<QuestObjectiveSaveData> objectives)
            {
                if (objectives == null)
                    return;

                foreach (KeyValuePair<string, int> pair in _progressByObjectiveId)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;

                    objectives.Add(new QuestObjectiveSaveData
                    {
                        objectiveId = pair.Key,
                        progress = Mathf.Max(0, pair.Value),
                        requiredCount = GetRequiredCount(pair.Key)
                    });
                }
            }

            public void ApplyObjectiveSaveData(List<QuestObjectiveSaveData> objectives)
            {
                _progressByObjectiveId.Clear();
                if (objectives == null)
                    return;

                for (int i = 0; i < objectives.Count; i++)
                {
                    QuestObjectiveSaveData objective = objectives[i];
                    if (objective == null || string.IsNullOrWhiteSpace(objective.objectiveId))
                        continue;

                    SetProgress(objective.objectiveId, objective.progress);
                    if (objective.requiredCount > 0 && FindDefinitionObjective(objective.objectiveId) == null)
                        ConfigureObjective(objective.objectiveId, objective.requiredCount, false);
                }
            }

            private bool AreCompatibilityObjectivesComplete()
            {
                bool hasRequiredObjective = false;
                foreach (KeyValuePair<string, ObjectiveRequirement> pair in _compatibilityRequirements)
                {
                    ObjectiveRequirement requirement = pair.Value;
                    if (requirement.Optional || requirement.RequiredCount <= 0)
                        continue;

                    hasRequiredObjective = true;
                    if (GetProgress(pair.Key) < requirement.RequiredCount)
                        return false;
                }

                return hasRequiredObjective;
            }

            private QuestObjectiveDefinition FindDefinitionObjective(string objectiveId)
            {
                if (Definition == null || Definition.Objectives == null)
                    return null;

                for (int i = 0; i < Definition.Objectives.Length; i++)
                {
                    QuestObjectiveDefinition objective = Definition.Objectives[i];
                    if (objective != null && objective.ObjectiveId == objectiveId)
                        return objective;
                }

                return null;
            }
        }

        private readonly struct ObjectiveRequirement
        {
            public readonly int RequiredCount;
            public readonly bool Optional;

            public ObjectiveRequirement(int requiredCount, bool optional)
            {
                RequiredCount = requiredCount;
                Optional = optional;
            }
        }
    }
}
