using System;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Daily
{
    public sealed class CalendarService : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static CalendarService Instance { get; private set; }

        [SerializeField] private int currentDay = 1;
        [SerializeField] private int currentWeek = 1;
        [SerializeField] private string currentChapterId;
        [SerializeField] private DayPhase currentPhase = DayPhase.None;

        public int CurrentDay => currentDay;
        public int CurrentWeek => currentWeek;
        public string CurrentChapterId => currentChapterId;
        public DayPhase CurrentPhase => currentPhase;

        public event Action<DayPhase, DayPhase> OnDayPhaseChanged;
        public event Action<int> OnDayAdvanced;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void SetPhase(DayPhase nextPhase)
        {
            if (currentPhase == nextPhase)
                return;

            DayPhase previous = currentPhase;
            currentPhase = nextPhase;
            OnDayPhaseChanged?.Invoke(previous, currentPhase);
        }

        public void AdvanceDay()
        {
            currentDay = Mathf.Max(1, currentDay + 1);
            currentWeek = Mathf.Max(1, ((currentDay - 1) / 7) + 1);
            OnDayAdvanced?.Invoke(currentDay);
        }

        public void SetChapter(string chapterId)
        {
            currentChapterId = chapterId;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.futureDaily ??= new FutureDailySaveData();
            saveData.futureDaily.dayIndex = Mathf.Max(1, currentDay);
            saveData.futureDaily.weekIndex = Mathf.Max(1, currentWeek);
            saveData.futureDaily.currentChapterId = currentChapterId;
            saveData.futureDaily.currentDayPhase = currentPhase.ToString();
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            if (saveData?.futureDaily == null)
                return;

            currentDay = Mathf.Max(1, saveData.futureDaily.dayIndex);
            currentWeek = Mathf.Max(1, saveData.futureDaily.weekIndex);
            currentChapterId = saveData.futureDaily.currentChapterId;

            if (Enum.TryParse(saveData.futureDaily.currentDayPhase, out DayPhase restoredPhase))
                SetPhase(restoredPhase);
        }
    }
}
