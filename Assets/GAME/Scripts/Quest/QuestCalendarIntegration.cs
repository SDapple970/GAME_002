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
            TryClaimRuntime();
        }

        private void OnDisable()
        {
            ReleaseRuntime();
        }

        public bool TryApplyMissionDayCost(string questId)
        {
            questId = string.IsNullOrWhiteSpace(questId) ? null : questId.Trim();
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
