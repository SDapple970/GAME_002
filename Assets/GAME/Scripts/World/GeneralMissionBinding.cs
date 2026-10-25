using Game.Core;
using Game.Quest;
using Game.Interaction;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>Scene adapter base. Resolves dependencies at lifecycle boundaries only.</summary>
    public abstract class GeneralMissionBinding : MonoBehaviour
    {
        [SerializeField] protected QuestRuntime questRuntime;
        [SerializeField] protected string questId;
        protected GeneralMissionSnapshot Snapshot { get; private set; }
        private QuestRuntime _subscribed;
        private readonly Dictionary<Collider2D, InteractionController> _controllers = new();
        protected static bool AllowsFieldAction => GameStateMachine.Instance == null || GameStateMachine.Instance.Is(GameState.Exploration);

        protected virtual void OnEnable() => Bind();
        protected virtual void Start() => Bind();
        protected virtual void OnDisable() => Unbind();
        private void Unbind()
        {
            foreach (InteractionController controller in _controllers.Values)
                if (controller != null && this is IInteractable interactable) controller.Unregister(interactable);
            _controllers.Clear();
            if (_subscribed != null)
            {
                _subscribed.OnQuestStarted -= Changed;
                _subscribed.OnQuestRetried -= Retried;
                _subscribed.OnQuestFailed -= Failed;
                _subscribed.OnGeneralMissionChanged -= Changed;
                _subscribed.OnStateRestored -= Restored;
            }
            _subscribed = null;
        }

        private void Bind()
        {
            if (questRuntime == null) questRuntime = FindFirstObjectByType<QuestRuntime>();
            if (_subscribed == questRuntime) { Refresh(); return; }
            Unbind();
            _subscribed = questRuntime;
            if (_subscribed != null)
            {
                _subscribed.OnQuestStarted += Changed;
                _subscribed.OnQuestRetried += Retried;
                _subscribed.OnQuestFailed += Failed;
                _subscribed.OnGeneralMissionChanged += Changed;
                _subscribed.OnStateRestored += Restored;
            }
            Refresh();
        }

        private void Changed(string id) { if (id == questId) Refresh(); }
        private void Retried(string id, int _) => Changed(id);
        private void Failed(string id, string _) => Changed(id);
        private void Restored() { BeforeRestore(); Refresh(); }
        protected virtual void BeforeRestore() { }
        public void Refresh()
        {
            Snapshot = questRuntime != null ? questRuntime.GetGeneralMissionSnapshot(questId) : null;
            ApplyState();
        }
        protected virtual void ApplyState() { }
        protected bool HasActiveRun => Snapshot != null && Snapshot.Status == QuestStatus.Active;
        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (this is not IInteractable interactable || other == null) return;
            InteractionController controller = other.GetComponentInParent<InteractionController>();
            if (controller == null) return;
            _controllers[other] = controller; controller.Register(interactable);
        }
        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (other == null || !_controllers.Remove(other, out InteractionController controller)) return;
            if (controller != null && !_controllers.ContainsValue(controller) && this is IInteractable interactable)
                controller.Unregister(interactable);
        }
    }
}
