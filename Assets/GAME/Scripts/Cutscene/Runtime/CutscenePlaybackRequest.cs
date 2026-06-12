using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace Game.Cutscene
{
    [Serializable]
    public sealed class CutscenePlaybackRequest
    {
        [SerializeField] private VideoClip clip;
        [SerializeField] private string url;
        [SerializeField] private bool useUrl;
        [SerializeField] private bool showRewardPanelOnFinished = true;
        [SerializeField] private UnityEvent onFinished = new UnityEvent();

        public VideoClip Clip => clip;
        public string Url => url;
        public bool UseUrl => useUrl;
        public bool ShowRewardPanelOnFinished => showRewardPanelOnFinished;
        public UnityEvent OnFinished => onFinished;
        public Action FinishedCallback { get; set; }

        public bool HasPlayableSource => useUrl ? !string.IsNullOrWhiteSpace(url) : clip != null;

        public void InvokeFinished()
        {
            onFinished?.Invoke();
            FinishedCallback?.Invoke();
        }
    }
}
