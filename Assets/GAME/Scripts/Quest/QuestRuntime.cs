using System;
using System.Collections.Generic;
using Game.Common.Identity;
using Game.Mission;
using Game.Mission.Data;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestRuntime : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private QuestDefinitionSO[] questDefinitions;

        private readonly Dictionary<string, RuntimeQuestState> _runtimeByQuestId = new();
        private bool _missingPersistentEventIdWarned;

        public event Action<string> OnQuestStarted;
        public event Action<string> OnQuestCompleted;
        public event Action<string, string> OnQuestFailed;
        public event Action<string, int> OnQuestRetried;
        public event Action<string, string, bool> OnObjectiveVisibilityChanged;
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
            state.NormalizeRestoredGroup();
            if (state.Status != QuestStatus.Inactive)
                return;

            state.BeginFirstAttempt();
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

            QuestObjectiveDefinition objective = state.FindActiveObjective(questEvent);
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

            string persistentEventId = ResolvePersistentEventId(questEvent);
            if (persistentEventId == null && !questEvent.AllowUntrackedCompatibility)
            {
                WarnMissingPersistentEventId(questEvent);
                return false;
            }

            if (persistentEventId == null)
                WarnUntrackedCompatibilityEvent(questEvent);
            else if (state.HasConsumedEventId(persistentEventId))
            {
                return false;
            }

            int current = state.GetProgress(objectiveId);
            int next = Mathf.Min(current + questEvent.Amount, requiredCount);
            if (next == current)
                return false;

            if (persistentEventId != null)
                state.RememberEventId(persistentEventId);

            ApplyProgressChange(state, objectiveId, next, requiredCount);
            return true;
        }

        private static string ResolvePersistentEventId(QuestEvent questEvent)
        {
            if (questEvent.Identity.IsValid)
                return questEvent.Identity.CanonicalId;

            return string.IsNullOrWhiteSpace(questEvent.EventId)
                ? null
                : questEvent.EventId.Trim();
        }

        private void WarnMissingPersistentEventId(QuestEvent questEvent)
        {
            if (_missingPersistentEventIdWarned)
                return;

            _missingPersistentEventIdWarned = true;
            Debug.LogWarning(
                $"[QuestRuntime] Persistent QuestEvent rejected because its canonical identity is invalid. questId='{questEvent.QuestId}', objectiveId='{questEvent.ObjectiveId}', type={questEvent.Type}.",
                this);
        }

        private void WarnUntrackedCompatibilityEvent(QuestEvent questEvent)
        {
            if (_missingPersistentEventIdWarned)
                return;

            _missingPersistentEventIdWarned = true;
            Debug.LogWarning(
                $"[QuestRuntime] QuestEvent used the explicit untracked compatibility path. New production events require GameplayOutcomeIdentity. questId='{questEvent.QuestId}', objectiveId='{questEvent.ObjectiveId}', type={questEvent.Type}.",
                this);
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            if (!TryGetActiveState(questId, out RuntimeQuestState state) ||
                string.IsNullOrWhiteSpace(objectiveId))
            {
                return;
            }

            if (!state.IsObjectiveActive(objectiveId))
                return;

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

        public bool FailQuest(
            string questId,
            GameplayOutcomeIdentity identity,
            string reasonId = null)
        {
            if (!TryGetActiveState(questId, out RuntimeQuestState state) || !identity.IsValid)
                return false;

            string canonicalId = identity.CanonicalId;
            if (state.HasConsumedEventId(canonicalId))
                return false;

            state.RememberEventId(canonicalId);
            state.Status = QuestStatus.Failed;
            state.FailureReasonId = NormalizeId(reasonId);
            OnQuestFailed?.Invoke(state.QuestId, state.FailureReasonId);
            return true;
        }

        public bool RetryQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) ||
                !_runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state) ||
                state.Status != QuestStatus.Failed ||
                state.Definition == null ||
                state.Definition.RetryPolicy != QuestRetryPolicy.RestartFromBeginning)
            {
                return false;
            }

            state.RestartFromBeginning();
            OnQuestRetried?.Invoke(state.QuestId, state.Attempt);
            return true;
        }

        public bool RevealObjective(string questId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId) ||
                !_runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state) ||
                !state.RevealObjective(objectiveId))
            {
                return false;
            }

            OnObjectiveVisibilityChanged?.Invoke(questId, objectiveId, true);
            return true;
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
            if (state.Status != QuestStatus.Completed && state.Status != QuestStatus.Failed)
            {
                bool newlyActive = state.Status != QuestStatus.Active;
                if (newlyActive)
                    state.BeginFirstAttempt();
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

            if (state.Status == QuestStatus.Failed)
            {
                RetryQuest(questId);
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

        public bool IsQuestFailed(string questId)
        {
            return GetQuestStatus(questId) == QuestStatus.Failed;
        }

        public int GetActiveGroupIndex(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.ActiveGroupIndex
                : 0;
        }

        public int GetQuestAttempt(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.Attempt
                : 0;
        }

        public string GetFailureReasonId(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state)
                ? state.FailureReasonId
                : null;
        }

        public bool IsObjectiveActive(string questId, string objectiveId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state) &&
                   state.Status == QuestStatus.Active &&
                   state.IsObjectiveActive(objectiveId);
        }

        public bool IsObjectiveVisible(string questId, string objectiveId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state) &&
                   state.IsObjectiveVisible(objectiveId);
        }

        public IReadOnlyList<QuestObjectiveDefinition> GetVisibleObjectives(string questId)
        {
            List<QuestObjectiveDefinition> visible = new();
            if (!string.IsNullOrWhiteSpace(questId) &&
                _runtimeByQuestId.TryGetValue(questId, out RuntimeQuestState state))
            {
                state.AppendVisibleObjectives(visible);
            }

            return visible;
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

        public bool TryGetFirstFailedQuestId(out string questId)
        {
            questId = null;
            foreach (KeyValuePair<string, RuntimeQuestState> pair in _runtimeByQuestId)
            {
                if (pair.Value.Status != QuestStatus.Failed)
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
            List<string> questIds = new(_runtimeByQuestId.Keys);
            questIds.Sort(StringComparer.Ordinal);
            for (int questIndex = 0; questIndex < questIds.Count; questIndex++)
            {
                RuntimeQuestState state = _runtimeByQuestId[questIds[questIndex]];
                if (state == null || string.IsNullOrWhiteSpace(state.QuestId))
                    continue;

                QuestStateSaveData questState = new()
                {
                    questId = state.QuestId,
                    completed = state.Status == QuestStatus.Completed,
                    status = state.Status.ToString(),
                    activeGroupIndex = state.ActiveGroupIndex,
                    failureReasonId = state.FailureReasonId,
                    attempt = state.Attempt
                };
                state.AppendObjectiveSaveData(questState.objectives);
                state.AppendRememberedEventIds(questState.processedEventIds);
                state.AppendRetiredEventIds(questState.retiredEventIds);
                state.AppendRevealedObjectiveIds(questState.revealedObjectiveIds);
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
                state.Status = TryParseStatus(questState.status, out QuestStatus status)
                    ? status
                    : questState.completed
                        ? QuestStatus.Completed
                        : string.IsNullOrWhiteSpace(questState.status)
                            ? QuestStatus.Active
                            : QuestStatus.Inactive;
                state.ActiveGroupIndex = Mathf.Max(0, questState.activeGroupIndex);
                state.FailureReasonId = state.Status == QuestStatus.Failed
                    ? NormalizeId(questState.failureReasonId)
                    : null;
                state.Attempt = Mathf.Max(state.Status == QuestStatus.Inactive ? 0 : 1, questState.attempt);
                state.ApplyObjectiveSaveData(questState.objectives);
                state.ApplyRememberedEventIds(questState.processedEventIds);
                state.ApplyRetiredEventIds(questState.retiredEventIds);
                state.ApplyRevealedObjectiveIds(questState.revealedObjectiveIds);
                state.NormalizeRestoredGroup();
            }
        }

        private void ApplyProgressChange(RuntimeQuestState state, string objectiveId, int next, int requiredCount)
        {
            state.SetProgress(objectiveId, next);
            OnObjectiveProgressChanged?.Invoke(state.QuestId, objectiveId, next, requiredCount);
            bool groupAdvanced = state.AdvanceCompletedGroups();
            if (groupAdvanced)
                RaiseVisibilityChangesForActiveGroup(state);
            if (state.AreAllGroupsComplete())
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

        private void RaiseVisibilityChangesForActiveGroup(RuntimeQuestState state)
        {
            List<string> revealed = state.RevealActiveGroupObjectives();
            for (int i = 0; i < revealed.Count; i++)
                OnObjectiveVisibilityChanged?.Invoke(state.QuestId, revealed[i], true);
        }

        private static bool TryParseStatus(string value, out QuestStatus status)
        {
            return Enum.TryParse(value, out status) && Enum.IsDefined(typeof(QuestStatus), status);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
                state.NormalizeRestoredGroup();
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
            private readonly HashSet<string> _retiredEventIds = new();
            private readonly List<string> _retiredEventIdOrder = new();
            private readonly HashSet<string> _revealedObjectiveIds = new();

            public RuntimeQuestState(string questId, QuestDefinitionSO definition)
            {
                QuestId = questId;
                Definition = definition;
                Status = QuestStatus.Inactive;
            }

            public string QuestId { get; }
            public QuestDefinitionSO Definition { get; set; }
            public QuestStatus Status { get; set; }
            public int ActiveGroupIndex { get; set; }
            public string FailureReasonId { get; set; }
            public int Attempt { get; set; }

            public void BeginFirstAttempt()
            {
                Status = QuestStatus.Active;
                Attempt = Mathf.Max(1, Attempt);
                FailureReasonId = null;
                ActiveGroupIndex = GetFirstGroupIndex();
                ResetVisibility();
            }

            public void RestartFromBeginning()
            {
                RetireCurrentEventIds();
                _progressByObjectiveId.Clear();
                Status = QuestStatus.Active;
                FailureReasonId = null;
                Attempt = Mathf.Max(1, Attempt) + 1;
                ActiveGroupIndex = GetFirstGroupIndex();
                ResetVisibility();
            }

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

            public QuestObjectiveDefinition FindActiveObjective(QuestEvent questEvent)
            {
                if (Definition?.Objectives == null)
                    return null;

                for (int i = 0; i < Definition.Objectives.Length; i++)
                {
                    QuestObjectiveDefinition objective = Definition.Objectives[i];
                    if (objective != null && objective.GroupIndex == ActiveGroupIndex && objective.Matches(questEvent))
                        return objective;
                }

                return null;
            }

            public bool IsObjectiveActive(string objectiveId)
            {
                QuestObjectiveDefinition objective = FindDefinitionObjective(objectiveId);
                return objective != null
                    ? objective.GroupIndex == ActiveGroupIndex
                    : _compatibilityRequirements.ContainsKey(objectiveId) && ActiveGroupIndex == 0;
            }

            public bool IsObjectiveVisible(string objectiveId)
            {
                QuestObjectiveDefinition objective = FindDefinitionObjective(objectiveId);
                if (objective == null)
                    return _compatibilityRequirements.ContainsKey(objectiveId);

                return objective.Visibility == QuestObjectiveVisibility.Visible ||
                       _revealedObjectiveIds.Contains(objectiveId);
            }

            public bool RevealObjective(string objectiveId)
            {
                return FindDefinitionObjective(objectiveId) != null &&
                       _revealedObjectiveIds.Add(objectiveId);
            }

            public void AppendVisibleObjectives(List<QuestObjectiveDefinition> destination)
            {
                if (destination == null || Definition?.Objectives == null)
                    return;

                for (int i = 0; i < Definition.Objectives.Length; i++)
                {
                    QuestObjectiveDefinition objective = Definition.Objectives[i];
                    if (objective != null && IsObjectiveVisible(objective.ObjectiveId))
                        destination.Add(objective);
                }
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

            public bool HasConsumedEventId(string eventId)
            {
                return !string.IsNullOrWhiteSpace(eventId) &&
                       (_rememberedEventIds.Contains(eventId) || _retiredEventIds.Contains(eventId));
            }

            public void RememberEventId(string eventId)
            {
                if (string.IsNullOrWhiteSpace(eventId) || !_rememberedEventIds.Add(eventId))
                    return;

                _eventIdOrder.Enqueue(eventId);
            }

            public void ClearRememberedEventIds()
            {
                _rememberedEventIds.Clear();
                _eventIdOrder.Clear();
            }

            public void AppendRememberedEventIds(List<string> destination)
            {
                if (destination != null) destination.AddRange(_eventIdOrder);
            }

            public void ApplyRememberedEventIds(List<string> source)
            {
                ClearRememberedEventIds();
                if (source == null) return;
                for (int i = 0; i < source.Count; i++)
                    RememberEventId(source[i]);
            }

            public void AppendRetiredEventIds(List<string> destination)
            {
                if (destination != null) destination.AddRange(_retiredEventIdOrder);
            }

            public void ApplyRetiredEventIds(List<string> source)
            {
                _retiredEventIds.Clear();
                _retiredEventIdOrder.Clear();
                if (source == null) return;
                for (int i = 0; i < source.Count; i++)
                {
                    string value = source[i];
                    if (!string.IsNullOrWhiteSpace(value) && _retiredEventIds.Add(value))
                        _retiredEventIdOrder.Add(value);
                }
            }

            public void AppendRevealedObjectiveIds(List<string> destination)
            {
                if (destination == null) return;
                destination.AddRange(_revealedObjectiveIds);
                destination.Sort(StringComparer.Ordinal);
            }

            public void ApplyRevealedObjectiveIds(List<string> source)
            {
                _revealedObjectiveIds.Clear();
                if (source != null)
                {
                    for (int i = 0; i < source.Count; i++)
                        if (!string.IsNullOrWhiteSpace(source[i]))
                            _revealedObjectiveIds.Add(source[i]);
                }

                RevealActiveGroupObjectives();
            }

            public void ResetProgress()
            {
                _progressByObjectiveId.Clear();
                ClearRememberedEventIds();
                _retiredEventIds.Clear();
                _retiredEventIdOrder.Clear();
                FailureReasonId = null;
                Attempt = Mathf.Max(1, Attempt);
                ActiveGroupIndex = GetFirstGroupIndex();
                ResetVisibility();
            }

            public bool AdvanceCompletedGroups()
            {
                bool advanced = false;
                while (IsCurrentGroupComplete() && TryGetNextGroupIndex(ActiveGroupIndex, out int nextGroupIndex))
                {
                    ActiveGroupIndex = nextGroupIndex;
                    advanced = true;
                }

                return advanced;
            }

            public bool AreAllGroupsComplete()
            {
                if (Definition?.Objectives == null)
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

            public List<string> RevealActiveGroupObjectives()
            {
                List<string> revealed = new();
                if (Definition?.Objectives == null)
                    return revealed;

                for (int i = 0; i < Definition.Objectives.Length; i++)
                {
                    QuestObjectiveDefinition objective = Definition.Objectives[i];
                    if (objective == null || objective.GroupIndex != ActiveGroupIndex ||
                        objective.Visibility != QuestObjectiveVisibility.RevealWhenGroupActive ||
                        !_revealedObjectiveIds.Add(objective.ObjectiveId))
                    {
                        continue;
                    }

                    revealed.Add(objective.ObjectiveId);
                }

                return revealed;
            }

            public void NormalizeRestoredGroup()
            {
                if (!HasGroup(ActiveGroupIndex))
                    ActiveGroupIndex = GetFirstGroupIndex();

                if (Status == QuestStatus.Active)
                    RevealActiveGroupObjectives();
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
                objectives.Sort((left, right) => string.CompareOrdinal(left.objectiveId, right.objectiveId));
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

            private bool IsCurrentGroupComplete()
            {
                if (Definition?.Objectives == null)
                    return AreCompatibilityObjectivesComplete();

                bool hasRequiredObjective = false;
                for (int i = 0; i < Definition.Objectives.Length; i++)
                {
                    QuestObjectiveDefinition objective = Definition.Objectives[i];
                    if (objective == null || objective.GroupIndex != ActiveGroupIndex || objective.Optional)
                        continue;

                    hasRequiredObjective = true;
                    if (GetProgress(objective.ObjectiveId) < objective.RequiredCount)
                        return false;
                }

                return hasRequiredObjective;
            }

            private int GetFirstGroupIndex()
            {
                if (Definition?.Objectives == null || Definition.Objectives.Length == 0)
                    return 0;

                int first = int.MaxValue;
                for (int i = 0; i < Definition.Objectives.Length; i++)
                    if (Definition.Objectives[i] != null)
                        first = Mathf.Min(first, Definition.Objectives[i].GroupIndex);
                return first == int.MaxValue ? 0 : first;
            }

            private bool TryGetNextGroupIndex(int current, out int next)
            {
                next = int.MaxValue;
                if (Definition?.Objectives != null)
                {
                    for (int i = 0; i < Definition.Objectives.Length; i++)
                    {
                        QuestObjectiveDefinition objective = Definition.Objectives[i];
                        if (objective != null && objective.GroupIndex > current)
                            next = Mathf.Min(next, objective.GroupIndex);
                    }
                }

                if (next != int.MaxValue)
                    return true;

                next = current;
                return false;
            }

            private bool HasGroup(int groupIndex)
            {
                if (Definition?.Objectives == null)
                    return groupIndex == 0;

                for (int i = 0; i < Definition.Objectives.Length; i++)
                    if (Definition.Objectives[i] != null && Definition.Objectives[i].GroupIndex == groupIndex)
                        return true;
                return false;
            }

            private void RetireCurrentEventIds()
            {
                while (_eventIdOrder.Count > 0)
                {
                    string eventId = _eventIdOrder.Dequeue();
                    if (_retiredEventIds.Add(eventId))
                        _retiredEventIdOrder.Add(eventId);
                }
                _rememberedEventIds.Clear();
            }

            private void ResetVisibility()
            {
                _revealedObjectiveIds.Clear();
                RevealActiveGroupObjectives();
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
