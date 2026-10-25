using System;
using Game.Core;
using Game.Quest;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Reusable acceptance/presentation adapter. Put its widgets under the
    /// existing routed UI root; it never activates a panel or switches GameState.</summary>
    public sealed class GeneralMissionBoard : MonoBehaviour
    {
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private QuestDefinitionSO[] missions = Array.Empty<QuestDefinitionSO>();
        private QuestRuntime _subscribed;
        public event Action OnChanged;
        public event Action<string> OnAcceptanceRejected;
        public System.Collections.Generic.IReadOnlyList<QuestDefinitionSO> Missions => Array.AsReadOnly(missions);

        private void OnEnable() => Bind();
        private void Start() => Bind();
        private void Bind()
        {
            if (questRuntime == null) questRuntime = FindFirstObjectByType<QuestRuntime>();
            if (_subscribed == questRuntime) return;
            OnDisable(); _subscribed = questRuntime;
            if (_subscribed != null)
            {
                _subscribed.OnGeneralMissionChanged += Changed;
                _subscribed.OnQuestStarted += Changed;
                _subscribed.OnQuestFailed += Failed;
                _subscribed.OnQuestRetried += Retried;
                _subscribed.OnStateRestored += Refresh;
            }
            Refresh();
        }
        private void OnDisable()
        {
            if (_subscribed != null)
            {
                _subscribed.OnGeneralMissionChanged -= Changed;
                _subscribed.OnQuestStarted -= Changed;
                _subscribed.OnQuestFailed -= Failed;
                _subscribed.OnQuestRetried -= Retried;
                _subscribed.OnStateRestored -= Refresh;
            }
            _subscribed = null;
        }
        private void Changed(string _) => Refresh();
        private void Failed(string _, string __) => Refresh();
        private void Retried(string _, int __) => Refresh();
        private void Refresh() => OnChanged?.Invoke();
        public bool TryAccept(int index)
        {
            if (questRuntime == null || index < 0 || index >= missions.Length) return false;
            bool accepted = questRuntime.TryAcceptGeneralMission(missions[index], out string reason);
            if (!accepted) OnAcceptanceRejected?.Invoke(reason);
            Refresh(); return accepted;
        }
        public void Accept(int index) => TryAccept(index);
        public string GetProgressText(int index)
        {
            if (index < 0 || index >= missions.Length || missions[index] == null) return "";
            QuestDefinitionSO definition = missions[index];
            GeneralMissionSnapshot state = questRuntime != null ? questRuntime.GetGeneralMissionSnapshot(definition.QuestId) : null;
            if (state == null) return $"{definition.QuestTitle} · 1일";
            string progress = state.Kind == GeneralMissionKind.Exploration ? $"탐사 {state.ExplorationPercent}% / 90%" :
                state.Kind == GeneralMissionKind.Rescue ? $"HP {state.RescueHp}/{state.RescueMaxHp} · {(state.Following ? "동행 중" : "구출 대기")}" :
                $"{state.Progress}/{state.Required}";
            string status = state.Status switch { QuestStatus.Active => "진행 중", QuestStatus.Completed => "완료",
                QuestStatus.Failed => "실패 · 재도전 가능", _ => "수락 가능" };
            return $"{definition.QuestTitle} · {status} · {progress}";
        }
    }
}
