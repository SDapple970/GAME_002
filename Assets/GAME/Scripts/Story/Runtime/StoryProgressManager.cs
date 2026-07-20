// Assets/GAME/Scripts/Story/Runtime/StoryProgressManager.cs
using System.Collections.Generic;
using UnityEngine;
using Game.NonCombat.Save;

namespace Game.Story
{
    public sealed class StoryProgressManager : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static StoryProgressManager Instance { get; private set; }

        [SerializeField] private int currentChapter = 1;
        [SerializeField] private int mainProgress = 0;

        private readonly HashSet<string> completedEventIds = new();

        public int CurrentChapter => currentChapter;
        public int MainProgress => mainProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            currentChapter = Mathf.Max(1, currentChapter);
            mainProgress = Mathf.Max(0, mainProgress);
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        public void SetChapter(int chapter)
        {
            currentChapter = Mathf.Max(1, chapter);
        }

        public void SetMainProgress(int progress)
        {
            mainProgress = Mathf.Max(0, progress);
        }

        public void AdvanceMainProgress(int amount = 1)
        {
            mainProgress = Mathf.Max(0, mainProgress + amount);
        }

        public bool IsEventCompleted(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return false;
            return completedEventIds.Contains(eventId);
        }

        public void MarkEventCompleted(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            completedEventIds.Add(eventId);
        }

        public void ClearEventCompleted(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            completedEventIds.Remove(eventId);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.story ??= new StorySaveData();
            saveData.story.currentChapter = currentChapter;
            saveData.story.mainProgress = mainProgress;
            saveData.story.completedEventIds.Clear();
            saveData.story.completedEventIds.AddRange(completedEventIds);
            saveData.story.completedEventIds.RemoveAll(string.IsNullOrWhiteSpace);
            saveData.story.completedEventIds.Sort(System.StringComparer.Ordinal);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            if (saveData?.story == null) return;
            currentChapter = Mathf.Max(1, saveData.story.currentChapter);
            mainProgress = Mathf.Max(0, saveData.story.mainProgress);
            completedEventIds.Clear();
            if (saveData.story.completedEventIds == null) return;
            foreach (string id in saveData.story.completedEventIds)
                if (!string.IsNullOrWhiteSpace(id)) completedEventIds.Add(id);
        }
    }
}
