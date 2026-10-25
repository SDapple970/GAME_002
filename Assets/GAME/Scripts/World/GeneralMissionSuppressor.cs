using System;
using Game.Interaction;
using Game.NonCombat.Interaction;
using Game.Quest;
using UnityEngine;
using UnityEngine.Events;

namespace Game.World
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GeneralMissionSuppressor : GeneralMissionBinding, IInteractable, INonCombatInteractable
    {
        [SerializeField] private string deviceId;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private UnityEvent<bool> onActivationChanged = new();
        public bool IsSelected => HasActiveRun && HasId(Snapshot.SelectedTargetIds, deviceId);
        public bool IsActivated => Snapshot != null && HasId(Snapshot.CompletedTargetIds, deviceId);
        public bool CanInteract => AllowsFieldAction && IsSelected && !IsActivated;
        public string PromptText => "억제 장치 가동";
        public void Interact() => Interact(null);
        public void Interact(GameObject interactor)
        {
            if (CanInteract) questRuntime.TryActivateMissionSuppressor(questId, Snapshot.RunId, Snapshot.Attempt, deviceId);
        }
        protected override void ApplyState()
        {
            // Only a child visual may be hidden. The adapter stays enabled for the next run.
            if (visualRoot != null && visualRoot != gameObject) visualRoot.SetActive(IsSelected || IsActivated);
            onActivationChanged.Invoke(IsActivated);
        }
        internal static bool HasId(System.Collections.Generic.IReadOnlyList<string> ids, string id)
        { for (int i = 0; i < ids.Count; i++) if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true; return false; }
    }
}
