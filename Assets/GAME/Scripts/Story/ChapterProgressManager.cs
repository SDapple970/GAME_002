using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    public sealed class ChapterProgressManager : MonoBehaviour
    {
        public static ChapterProgressManager Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private ChapterProgress current = new ChapterProgress();

        private readonly Dictionary<ChapterId, ChapterProgress> _progressByChapter = new();

        public ChapterProgress Current => current;
        public ChapterId CurrentChapter => current != null ? current.chapterId : ChapterId.None;
        public int CurrentStep => current != null ? current.step : 0;
        public bool IsCurrentCompleted => current != null && current.completed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        public void StartChapter(ChapterId id)
        {
            if (id == ChapterId.None)
            {
                Debug.LogWarning("[ChapterProgressManager] Cannot start ChapterId.None.", this);
                return;
            }

            if (!_progressByChapter.TryGetValue(id, out ChapterProgress progress))
            {
                progress = new ChapterProgress(id);
                _progressByChapter[id] = progress;
            }

            progress.completed = false;
            current = progress;

            Debug.Log($"[ChapterProgressManager] StartChapter: {id}", this);
        }

        public void SetStep(int step)
        {
            EnsureCurrentProgress();
            current.step = Mathf.Max(0, step);
            Debug.Log($"[ChapterProgressManager] SetStep: chapter={current.chapterId}, step={current.step}", this);
        }

        public void CompleteChapter(ChapterId id)
        {
            if (id == ChapterId.None)
                return;

            if (!_progressByChapter.TryGetValue(id, out ChapterProgress progress))
            {
                progress = new ChapterProgress(id);
                _progressByChapter[id] = progress;
            }

            progress.completed = true;

            if (current == null || current.chapterId == id)
                current = progress;

            Debug.Log($"[ChapterProgressManager] CompleteChapter: {id}", this);
        }

        public ChapterProgress GetProgress(ChapterId id)
        {
            if (id == ChapterId.None)
                return null;

            _progressByChapter.TryGetValue(id, out ChapterProgress progress);
            return progress;
        }

        private void EnsureCurrentProgress()
        {
            if (current == null)
                current = new ChapterProgress();

            if (current.chapterId != ChapterId.None && !_progressByChapter.ContainsKey(current.chapterId))
                _progressByChapter[current.chapterId] = current;
        }
    }
}
