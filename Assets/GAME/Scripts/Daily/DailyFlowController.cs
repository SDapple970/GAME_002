using System;
using UnityEngine;

namespace Game.Daily
{
    public sealed class DailyFlowController : MonoBehaviour
    {
        [SerializeField] private CalendarService calendarService;
        [SerializeField] private bool advanceDayWhenBeginningNewDay = true;

        public event Action<DayPhase> OnDayPhaseChanged;

        private void Awake()
        {
            ResolveCalendar();
        }

        private void OnEnable()
        {
            ResolveCalendar();

            if (calendarService != null)
                calendarService.OnDayPhaseChanged += HandleDayPhaseChanged;
        }

        private void OnDisable()
        {
            if (calendarService != null)
                calendarService.OnDayPhaseChanged -= HandleDayPhaseChanged;
        }

        public void BeginNewDay()
        {
            ResolveCalendar();
            if (calendarService == null)
                return;

            if (advanceDayWhenBeginningNewDay)
                calendarService.AdvanceDay();

            calendarService.SetPhase(DayPhase.Morning);
        }

        public void EnterOffice()
        {
            SetPhase(DayPhase.Office);
        }

        public void EnterMissionSelect()
        {
            SetPhase(DayPhase.MissionSelect);
        }

        public void EnterMission()
        {
            SetPhase(DayPhase.FieldExploration);
        }

        public void EnterSettlement()
        {
            SetPhase(DayPhase.Settlement);
        }

        public void CompleteSettlement()
        {
            SetPhase(DayPhase.Rest);
        }

        private void SetPhase(DayPhase phase)
        {
            ResolveCalendar();
            calendarService?.SetPhase(phase);
        }

        private void ResolveCalendar()
        {
            if (calendarService == null)
                calendarService = CalendarService.Instance != null
                    ? CalendarService.Instance
                    : FindFirstObjectByType<CalendarService>();
        }

        private void HandleDayPhaseChanged(DayPhase previous, DayPhase next)
        {
            OnDayPhaseChanged?.Invoke(next);
        }
    }
}
