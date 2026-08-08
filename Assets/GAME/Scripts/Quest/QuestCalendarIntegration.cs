using System.Collections.Generic;
using Game.Daily;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Quest
{
    [DisallowMultipleComponent]
    public sealed class QuestCalendarIntegration : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        private static readonly Dictionary<int, QuestCalendarIntegration> OwnersByRuntimeId = new();

        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private CalendarService calendarService;

        private readonly HashSet<string> _appliedQuestIds = new();
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
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _appliedQuestIds.Clear();
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

        private void HandleQuestStarted(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) || _appliedQuestIds.Contains(questId))
                return;

            if (questRuntime == null ||
                !questRuntime.TryGetDefinition(questId, out QuestDefinitionSO definition) ||
                definition.MissionDayCost <= 0)
            {
                return;
            }

            ResolveReferences();
            if (calendarService == null)
            {
                WarnMissingCalendar(questId);
                return;
            }

            if (calendarService.TryAdvanceDays(definition.MissionDayCost))
                _appliedQuestIds.Add(questId);
        }

        private void ResolveReferences()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            if (calendarService == null)
                calendarService = CalendarService.Instance != null
                    ? CalendarService.Instance
                    : FindFirstObjectByType<CalendarService>();
        }

        private void Subscribe()
        {
            if (_subscribedRuntime == questRuntime)
                return;

            Unsubscribe();
            _subscribedRuntime = questRuntime;
            if (_subscribedRuntime != null)
                _subscribedRuntime.OnQuestStarted += HandleQuestStarted;
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

        private void Unsubscribe()
        {
            if (_subscribedRuntime != null)
                _subscribedRuntime.OnQuestStarted -= HandleQuestStarted;

            _subscribedRuntime = null;
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
