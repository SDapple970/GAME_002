using System;
using System.Collections.Generic;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Daily
{
    public sealed class DaySettlementFlow : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static DaySettlementFlow Instance { get; private set; }

        [SerializeField] private DailyFlowController dailyFlowController;
        [SerializeField] private CalendarService calendarService;
        [SerializeField] private bool enterSettlementPhaseOnRequest;

        private readonly HashSet<string> _completedSettlementIds = new();
        private DaySettlementRequest _activeRequest;
        private bool _duplicateSettlementWarned;
        private bool _invalidRequestWarned;

        public event Action<DaySettlementRequest> OnSettlementReady;
        public event Action<DaySettlementResult> OnSettlementCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveReferences();
        }

        public bool PrepareSettlement(DaySettlementRequest request)
        {
            if (!IsValidRequest(request))
            {
                WarnInvalidRequest();
                return false;
            }

            string settlementId = ResolveSettlementId(request);
            if (_activeRequest != null || _completedSettlementIds.Contains(settlementId))
            {
                WarnDuplicateSettlementBlocked(settlementId);
                return false;
            }

            ResolveReferences();
            request.settlementId = settlementId;
            _activeRequest = request;

            if (enterSettlementPhaseOnRequest)
                dailyFlowController?.EnterSettlement();

            OnSettlementReady?.Invoke(request);
            return true;
        }

        public bool CompleteSettlement()
        {
            if (_activeRequest == null)
                return false;

            ResolveReferences();

            DaySettlementResult result = new()
            {
                settlementId = _activeRequest.settlementId,
                questId = _activeRequest.questId,
                missionId = _activeRequest.missionId,
                completedDay = calendarService != null ? calendarService.CurrentDay : 0,
                completedPhase = calendarService != null ? calendarService.CurrentPhase : DayPhase.None
            };

            if (!string.IsNullOrWhiteSpace(result.settlementId))
                _completedSettlementIds.Add(result.settlementId);

            _activeRequest = null;
            dailyFlowController?.CompleteSettlement();
            OnSettlementCompleted?.Invoke(result);
            return true;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.futureDaily ??= new FutureDailySaveData();
            saveData.futureDaily.completedSettlementIds.Clear();
            foreach (string settlementId in _completedSettlementIds)
            {
                if (!string.IsNullOrWhiteSpace(settlementId))
                    saveData.futureDaily.completedSettlementIds.Add(settlementId);
            }
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _completedSettlementIds.Clear();
            if (saveData?.futureDaily?.completedSettlementIds == null)
                return;

            for (int i = 0; i < saveData.futureDaily.completedSettlementIds.Count; i++)
            {
                string settlementId = saveData.futureDaily.completedSettlementIds[i];
                if (!string.IsNullOrWhiteSpace(settlementId))
                    _completedSettlementIds.Add(settlementId);
            }
        }

        private void ResolveReferences()
        {
            if (calendarService == null)
                calendarService = CalendarService.Instance != null
                    ? CalendarService.Instance
                    : FindFirstObjectByType<CalendarService>();

            if (dailyFlowController == null)
                dailyFlowController = FindFirstObjectByType<DailyFlowController>();
        }

        private static bool IsValidRequest(DaySettlementRequest request)
        {
            return request != null &&
                   (!string.IsNullOrWhiteSpace(request.settlementId) ||
                    !string.IsNullOrWhiteSpace(request.questId) ||
                    !string.IsNullOrWhiteSpace(request.missionId));
        }

        private static string ResolveSettlementId(DaySettlementRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.settlementId))
                return request.settlementId;

            if (!string.IsNullOrWhiteSpace(request.questId))
                return $"quest:{request.questId}";

            return $"mission:{request.missionId}";
        }

        private void WarnInvalidRequest()
        {
            if (_invalidRequestWarned)
                return;

            _invalidRequestWarned = true;
            Debug.LogWarning("[DaySettlementFlow] Invalid settlement request ignored.", this);
        }

        private void WarnDuplicateSettlementBlocked(string settlementId)
        {
            if (_duplicateSettlementWarned)
                return;

            _duplicateSettlementWarned = true;
            Debug.LogWarning($"[DaySettlementFlow] Duplicate settlement request blocked. settlementId={settlementId}", this);
        }
    }
}
