using System.Collections.Generic;
using Game.Daily;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Quest
{
    [DisallowMultipleComponent]
    public sealed class QuestCalendarIntegration : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer, INewGameRuntimeReset
    {
        private static readonly Dictionary<int, QuestCalendarIntegration> OwnersByRuntimeId = new();

        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private CalendarService calendarService;

        private readonly HashSet<string> _appliedQuestIds = new();
        private readonly HashSet<string> _appliedRunKeys = new();
        private QuestRuntime _subscribedRuntime;
        private bool _ownsRuntime;
        private bool _missingCalendarWarned;
        private bool _duplicateOwnerWarned;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (TryClaimRuntime())
                Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseRuntime();
        }

        public bool TryApplyMissionDayCost(string questId)
        {
            questId = string.IsNullOrWhiteSpace(questId) ? null : questId.Trim();
            if (questRuntime != null && !string.IsNullOrWhiteSpace(questRuntime.GetQuestRunId(questId)))
                return false;
            if (questId == null || _appliedQuestIds.Contains(questId))
                return false;

            ResolveReferences();
            if ((!_ownsRuntime && !TryClaimRuntime()) ||
                questRuntime == null ||
                !questRuntime.TryGetDefinition(questId, out QuestDefinitionSO definition) ||
                definition.MissionDayCost <= 0)
            {
                return false;
            }

            if (calendarService == null)
            {
                WarnMissingCalendar(questId);
                return false;
            }

            if (!calendarService.TryAdvanceDays(definition.MissionDayCost))
                return false;

            _appliedQuestIds.Add(questId);
            return true;
        }

        // CompletionFlow supplies the immutable accepted-run snapshot. The legacy
        // quest-ID ledger remains separate so old saves cannot alias a new run.
        public bool TryApplyRunDayCost(QuestRunCompletion completion)
        {
            if (!completion.IsValid) return false;
            if (completion.MissionDayCost == 0 || _appliedRunKeys.Contains(completion.Key)) return true;
            ResolveReferences();
            bool pending = false;
            if (questRuntime != null)
                foreach (QuestRunCompletion candidate in questRuntime.GetPendingRunCompletions())
                    if (candidate.Key == completion.Key) { pending = true; break; }
            if (!pending) return false;
            if ((!_ownsRuntime && !TryClaimRuntime()) || calendarService == null) return false;
            _appliedRunKeys.Add(completion.Key);
            if (calendarService.TryAdvanceDays(completion.MissionDayCost)) return true;
            _appliedRunKeys.Remove(completion.Key);
            return false;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.futureDaily ??= new FutureDailySaveData();
            saveData.futureDaily.appliedQuestDayCostIds ??= new List<string>();
            saveData.futureDaily.appliedQuestDayCostIds.Clear();
            List<string> questIds = new(_appliedQuestIds);
            questIds.Sort(System.StringComparer.Ordinal);
            saveData.futureDaily.appliedQuestDayCostIds.AddRange(questIds);
            saveData.futureDaily.appliedQuestRunDayCostIds = new List<string>(_appliedRunKeys);
            saveData.futureDaily.appliedQuestRunDayCostIds.Sort(System.StringComparer.Ordinal);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _appliedQuestIds.Clear();
            _appliedRunKeys.Clear();
            if (saveData?.futureDaily?.appliedQuestRunDayCostIds != null)
                foreach (string key in saveData.futureDaily.appliedQuestRunDayCostIds)
                    if (!string.IsNullOrWhiteSpace(key)) _appliedRunKeys.Add(key);
            List<string> questIds = saveData?.futureDaily?.appliedQuestDayCostIds;
            if (questIds == null)
                return;

            for (int i = 0; i < questIds.Count; i++)
            {
                string questId = questIds[i];
                if (!string.IsNullOrWhiteSpace(questId))
                    _appliedQuestIds.Add(questId.Trim());
            }
        }

        public void ResetForNewGame() { _appliedQuestIds.Clear(); _appliedRunKeys.Clear(); }

        private void ResolveReferences()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            if (calendarService == null)
                calendarService = CalendarService.Instance != null
                    ? CalendarService.Instance
                    : FindFirstObjectByType<CalendarService>();
        }

        private bool TryClaimRuntime()
        {
            if (questRuntime == null)
                return false;

            int key = questRuntime.GetInstanceID();
            if (OwnersByRuntimeId.TryGetValue(key, out QuestCalendarIntegration owner) && owner != null && owner != this)
            {
                if (!_duplicateOwnerWarned)
                {
                    _duplicateOwnerWarned = true;
                    Debug.LogWarning(
                        $"[QuestCalendarIntegration] Duplicate calendar integration blocked for QuestRuntime '{questRuntime.name}'.",
                        this);
                }

                _ownsRuntime = false;
                return false;
            }

            OwnersByRuntimeId[key] = this;
            _ownsRuntime = true;
            return true;
        }

        private void ReleaseRuntime()
        {
            if (!_ownsRuntime || questRuntime == null)
            {
                _ownsRuntime = false;
                return;
            }

            int key = questRuntime.GetInstanceID();
            if (OwnersByRuntimeId.TryGetValue(key, out QuestCalendarIntegration owner) && owner == this)
                OwnersByRuntimeId.Remove(key);

            _ownsRuntime = false;
        }

        private void Subscribe()
        {
            if (_subscribedRuntime == questRuntime)
                return;

            if (_subscribedRuntime != null)
                _subscribedRuntime.OnQuestCompleted -= HandleQuestCompleted;

            _subscribedRuntime = questRuntime;
            if (_subscribedRuntime != null)
                _subscribedRuntime.OnQuestCompleted += HandleQuestCompleted;
        }

        private void Unsubscribe()
        {
            if (_subscribedRuntime != null)
                _subscribedRuntime.OnQuestCompleted -= HandleQuestCompleted;

            _subscribedRuntime = null;
        }

        private void HandleQuestCompleted(string questId)
        {
            if (questRuntime != null && !string.IsNullOrWhiteSpace(questRuntime.GetQuestRunId(questId)))
                return; // Repeatable runs are coordinated by QuestCompletionFlow.
            TryApplyMissionDayCost(questId);
        }

        private void WarnMissingCalendar(string questId)
        {
            if (_missingCalendarWarned)
                return;

            _missingCalendarWarned = true;
            Debug.LogWarning(
                $"[QuestCalendarIntegration] CalendarService is missing. Mission day cost was not applied. questId={questId}",
                this);
        }
    }
}
